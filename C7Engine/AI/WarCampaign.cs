using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7Engine.Pathing;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// An AI heuristic for taking cities: rather than sending every attacker
	// at the enemy on its own (see CombatAI), which spends them one at a
	// time against fortified defenders, the AI picks one enemy city, gathers
	// enough attackers (and siege engines, and a defender to cover them) in
	// its own city nearest to it, marches them there together, bombards, and
	// attacks with the best odds until the city falls. Then it garrisons the
	// city and goes on to the next.
	//
	// The stack attacks a city only when, all together, its attackers can
	// expect to wear down every defender (with a margin); short of that it
	// waits a turn or two for those catching up and then falls back together
	// to one of our cities, where the hurt heal before it sets out again
	// with more. An army, if we have one, leads the attack. In AI trials
	// this, with hurt units healing (see RestAI) and the same rule for lone
	// attackers (see CombatAI), made civs about as strong on Continents and
	// stronger on Pangaea (about 4 more cities each in 200 turns).
	//
	// It runs campaigns in the wars the AI is in, and also starts them in
	// peacetime against a weaker neighbour, declaring war only once the
	// attackers are gathered (WarPriority no longer declares wars itself),
	// though not while it still has wide-open land to settle.
	// In AI trials (AiTrialTest), civs playing this way took about 3 more
	// cities each in 200 turns.
	public static class WarCampaign {
		private static readonly ILogger log = Log.ForContext(typeof(WarCampaign));

		public static bool Enabled(Player player) {
			return !player.isHuman && !player.isBarbarians;
		}

		private enum Phase {
			Gather,
			March,
			Siege,
			// Too weak to take the target: the stack goes back to a city of
			// ours together, to heal and wait for more attackers.
			Retreat,
		}

		private sealed class Campaign {
			public Player enemy;
			public City target;
			public Phase phase;
			public int phaseStartTurn;
			// Where the gathered units stand together, once they set out.
			public Tile stackTile;
			public readonly List<MapUnit> units = new();
			// How many times an assault on the target failed.
			public int failures;
			// What we last saw of the target's defenders: their hit points,
			// and the hits one of our attackers can expect to deal them, or
			// -1 if we haven't seen them.
			public double knownDefenderHitPoints = -1;
			public double knownHitsEach;
			// Turns in a row the stack has sat outside the target without the
			// force to take it.
			public int shortTurns;
			// The city the stack is falling back to.
			public Tile retreatTo;
		}

		// HEURISTIC: how many failed attempts on a target (assaults that
		// ran out of attackers, marches that stalled, falling back for want
		// of force) end the campaign. In AI trials, allowing a third did no
		// better.
		private const int MaxFailures = 2;

		private static readonly ConditionalWeakTable<Player, Campaign> campaigns = new();

		// The units ordered to attack during this player's turn. A move the
		// engine turns down changes nothing, so each unit attacks at most
		// once a turn, or a refused order would be repeated forever.
		private static readonly HashSet<MapUnit> triedToAttack = new();
		private static readonly ConditionalWeakTable<Player, StrongBox<int>> nextCampaignTurn = new();

		// HEURISTIC: how many turns to wait after giving up a campaign.
		private const int CooldownTurns = 20;
		// HEURISTIC: how long to wait for the attackers to gather.
		private const int MaxGatherTurns = 25;
		// HEURISTIC: how long to sit outside a city without taking it.
		private const int MaxSiegeTurns = 12;
		// HEURISTIC: in an all-out assault, the attacks not worth making.
		private const double MinAssaultOdds = 0.05;
		// HEURISTIC: in an all-out attack on enemies in the stack's way on
		// the march, likewise.
		private const double MinMarchOdds = 0.2;
		// HEURISTIC: how long the march may take.
		private const int MaxMarchTurns = 15;
		// HEURISTIC: how much stronger than the enemy (by the military
		// advisor's measure) we must be to start a war.
		private const float DeclareStrengthRatio = 1.3f;
		// HEURISTIC: how far from our nearest city a city may be to be worth
		// marching on.
		private const int MaxTargetDistance = 14;

		// A unit taking part in a campaign. The campaign moves it, so the
		// ordinary unit AI leaves it alone (see PlayerAI.DoUnitActions).
		public sealed class MemberAI : C7GameData.UnitAI {
			C7GameData.UnitAI.MoveResult C7GameData.UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
				return C7GameData.UnitAI.Result.Done;
			}

			public string SummarizePlan() {
				return "WarCampaign member";
			}

			public void UpdateOnDeath() { }
		}

		private static int Turn => EngineStorage.gameData?.turn ?? 0;

		internal static bool IsAttacker(MapUnit u) {
			return u.IsLandUnit() && !u.IsArmy() && !u.IsLeader() && !u.IsNuclearWeapon() && u.CanAttack()
				&& u.unitType.attack > u.unitType.defense && !u.unitType.isSettler && !u.unitType.isWorker;
		}

		// An army with members to fight with.
		private static bool IsFightingArmy(MapUnit u) {
			return u.IsArmy() && u.IsLandUnit() && u.PassengerCount() > 0;
		}

		// A unit of the stack that attacks: an attacker or an army.
		private static bool IsFighter(MapUnit u) {
			return IsAttacker(u) || IsFightingArmy(u);
		}

		// The unit's hit points as a share of what it has when healthy (for
		// an army, its members').
		private static double Health(MapUnit u) {
			int max = u.CompositeMaxHitPoints();
			return max > 0 ? (double)u.CompositeHitPoints() / max : 0;
		}

		internal static bool IsBombarder(MapUnit u) {
			return u.IsLandUnit() && !u.IsArmy() && !u.IsNuclearWeapon() && u.unitType.bombard > 0 && u.unitType.attack == 0;
		}

		private static bool IsEscort(MapUnit u) {
			return u.IsLandUnit() && !u.IsArmy() && !u.IsLeader() && u.CanDefendOnLand()
				&& u.unitType.defense > u.unitType.attack && !u.unitType.isSettler && !u.unitType.isWorker;
		}

		// Whether a city should build more attackers for a campaign.
		public static bool WantsAttackers(Player player) {
			return campaigns.TryGetValue(player, out Campaign c) && c.units.Count(IsAttacker) < NeededAttackers(player, c) + 2;
		}

		public static bool WantsBombarders(Player player) {
			if (!campaigns.TryGetValue(player, out Campaign c)) {
				return false;
			}
			int building = player.cities.Count(city => city.itemBeingProduced is UnitPrototype u && u.bombard > 0 && u.attack == 0);
			return c.units.Count(IsBombarder) + building < 2;
		}

		// The city a campaign gathers its units in, or null without a
		// campaign: units bought near it join the campaign soonest (see
		// DevelopmentAI.HurryProduction).
		public static City CampaignStagingCity(Player player) {
			return campaigns.TryGetValue(player, out Campaign c) && c.target != null ? StagingCity(player, c) : null;
		}

		// The enemy and city a campaign is after, and the city it gathers
		// in, for working out the way there (see RightOfPassageAI). False
		// without a campaign.
		internal static bool TryGetCampaign(Player player, out Player enemy, out City target, out City staging, out List<MapUnit> units) {
			enemy = null;
			target = null;
			staging = null;
			units = null;
			if (!campaigns.TryGetValue(player, out Campaign c) || c.target == null) {
				return false;
			}
			enemy = c.enemy;
			target = c.target;
			staging = StagingCity(player, c);
			units = c.units;
			return staging != null;
		}

		public static bool HasCampaignAgainst(Player player, Player enemy) {
			return campaigns.TryGetValue(player, out Campaign c) && c.enemy == enemy && c.phase != Phase.Gather;
		}

		public static async Task PlayTurn(Player player, GameData gameData) {
			if (!Enabled(player) || player.cities.Count == 0) {
				return;
			}
			AIFogOfWar.Refresh(player);
			if (!campaigns.TryGetValue(player, out Campaign campaign)) {
				campaign = MaybeStartCampaign(player, gameData);
				if (campaign == null) {
					return;
				}
				campaigns.AddOrUpdate(player, campaign);
			}

			triedToAttack.Clear();
			if (!await PlayCampaign(player, gameData, campaign)) {
				EndCampaign(player, campaign, cooldown: true);
			}
		}

		private static void EndCampaign(Player player, Campaign campaign, bool cooldown) {
			log.Information("{Player} ends its campaign against {Enemy}", player, campaign.enemy);
			foreach (MapUnit u in campaign.units) {
				Release(u);
			}
			campaign.units.Clear();
			campaigns.Remove(player);
			if (cooldown) {
				nextCampaignTurn.AddOrUpdate(player, new StrongBox<int>(Turn + CooldownTurns));
			}
		}

		private static void Release(MapUnit u) {
			if (u.currentAI is MemberAI) {
				u.currentAI = null;
			}
			u.Wake();
		}

		private static bool AtWarWith(Player player, Player other) {
			return player.playerRelationships.TryGetValue(other.id, out PlayerRelationship r) && r.AtWar();
		}

		private static bool CanDeclareWarOn(Player player, Player other) {
			return player != other && !other.isBarbarians && !other.defeated
				&& player.playerRelationships.TryGetValue(other.id, out PlayerRelationship relationship) && !relationship.AtWar()
				&& !EngineStorage.gameData.AreInLockedPeace(player, other);
		}

		// Returns false if the campaign should be given up.
		private static async Task<bool> PlayCampaign(Player player, GameData gameData, Campaign c) {
			c.units.RemoveAll(u => !PlayerAI.IsStillOurs(u, player) || u.currentAI is not MemberAI);

			// A stack falling back gets home before the campaign is given up.
			if (c.enemy.defeated || (c.failures >= MaxFailures && c.phase != Phase.Retreat)) {
				return false;
			}
			bool atWar = AtWarWith(player, c.enemy);
			// Peace was made since the march began.
			if (!atWar && c.phase != Phase.Gather) {
				return false;
			}
			// Before war, only a campaign we may still declare.
			if (!atWar && !CanDeclareWarOn(player, c.enemy)) {
				return false;
			}

			// The target fell, to us or someone else: garrison it if ours,
			// and go on to the next.
			if (c.target.owner != c.enemy || !gameData.cities.Contains(c.target)) {
				if (c.target.owner == player) {
					Garrison(player, c, c.target);
				}
				City next = PickTarget(player, c.enemy);
				if (next == null) {
					return false;
				}
				c.target = next;
				c.failures = 0;
				c.knownDefenderHitPoints = -1;
				SetPhase(c, Phase.Gather);
			}

			Recruit(player, c);

			if (c.phase == Phase.Gather) {
				return await Gather(player, gameData, c, atWar);
			}

			// Units not with the stack catch up with it, or, when it falls
			// back, go straight to where it's going.
			Tile meet = c.phase == Phase.Retreat ? c.retreatTo : c.stackTile;
			foreach (MapUnit u in c.units.ToList()) {
				if (u.location != c.stackTile && u.location != meet) {
					await MoveTowards(player, u, meet);
				}
			}

			if (c.phase == Phase.March) {
				await March(player, c);
			}
			if (c.phase == Phase.Siege) {
				await Siege(player, c);
			}
			if (c.phase == Phase.Retreat) {
				await Retreat(player, c);
			}

			// Nothing left to attack with: what's left (the stack's defender,
			// its siege engines) goes home together.
			if (c.phase != Phase.Gather && c.phase != Phase.Retreat
					&& !c.units.Any(u => IsFighter(u) && PlayerAI.IsStillOurs(u, player))) {
				StartRetreat(player, c);
			}
			if (c.failures >= MaxFailures && c.phase != Phase.Retreat) {
				return false;
			}

			// Units with the stack fortify, unless they still can move.
			foreach (MapUnit u in c.units) {
				if (PlayerAI.IsStillOurs(u, player) && u.location == c.stackTile && !u.isFortified) {
					u.Fortify();
				}
			}
			return true;
		}

		private static void SetPhase(Campaign c, Phase phase) {
			c.phase = phase;
			c.phaseStartTurn = Turn;
		}

		// Leaves a defender in a city we took: our escort if there is one,
		// or else an attacker.
		private static void Garrison(Player player, Campaign c, City city) {
			if (city.location.unitsOnTile.Any(u => u.owner == player && u.CanDefendOnLand() && u.currentAI is not MemberAI)) {
				return;
			}
			MapUnit keeper = c.units.Where(u => u.location == city.location && u.CanDefendOnLand()).OrderByDescending(u => IsEscort(u) ? 1 : 0)
				.ThenByDescending(u => u.unitType.defense).FirstOrDefault();
			if (keeper == null) {
				return;
			}
			c.units.Remove(keeper);
			keeper.currentAI = new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(keeper, player));
			keeper.Fortify();
			log.Information("{Player} took {City}; {Unit} garrisons it", player, city.name, keeper);
		}

		// Starts a campaign against the enemy we are at war with, or, with
		// war-declare, against a weaker neighbour.
		private static Campaign MaybeStartCampaign(Player player, GameData gameData) {
			if (nextCampaignTurn.TryGetValue(player, out StrongBox<int> next) && Turn < next.Value) {
				return null;
			}

			Player enemy = null;
			City target = null;
			float best = float.MinValue;
			foreach (Player p in gameData.players) {
				if (p == player || p.isBarbarians || p.defeated || !AtWarWith(player, p)) {
					continue;
				}
				City t = PickTarget(player, p, out float score);
				if (t != null && score > best) {
					best = score;
					enemy = p;
					target = t;
				}
			}

			if (enemy == null && !PlayerRelationship.IsInAnyWar(player, gameData.players)
					&& player.cities.Count >= 3) {
				float us = player.CalculateMilitaryStrength();
				foreach (Player p in gameData.players) {
					if (!CanDeclareWarOn(player, p)) {
						continue;
					}
					float them = p.CalculateMilitaryStrength();
					if (us < DeclareStrengthRatio * them) {
						continue;
					}
					City t = PickTarget(player, p, out float score);
					// The weaker they are the better.
					score *= us / Math.Max(1f, them);
					if (t != null && score > best) {
						best = score;
						enemy = p;
						target = t;
					}
				}

				// While there's wide-open land to settle, we settle it rather
				// than start a war, and demand tribute of the weak instead
				// (see TributeDemands). Checked last, as finding the open land
				// scores every known tile.
				if (enemy != null && HasWideOpenLand(player)) {
					log.Debug("{Player} is still expanding, so doesn't attack {Enemy}", player, enemy);
					return null;
				}
			}

			if (enemy == null) {
				return null;
			}
			log.Information("{Player} plans a campaign against {Enemy}, starting with {City}", player, enemy, target.name);
			Campaign c = new() { enemy = enemy, target = target };
			SetPhase(c, Phase.Gather);
			return c;
		}

		// HEURISTIC: a civ with at least this many open city sites it can
		// reach for each city it has is still settling wide-open land. In AI
		// games on standard pangaeas, civs have 35 to 60 such sites until
		// about turn 70, and fewer than their cities only after about turn
		// 105. In AI trials (8 paired standard pangaeas, 150 turns), holding
		// off until then cut the wars civs started from 1.1 each to 0.3,
		// with no measurable change in their score or cities.
		private const int OpenSitesPerCityWhileExpanding = 1;

		// Whether the player still has wide-open land to settle: enough open
		// city sites to double its cities. Judged from its capital, or its
		// first city without one.
		private static bool HasWideOpenLand(Player player) {
			City home = player.cities.Find(c => c.IsCapital()) ?? player.cities[0];
			return ChooseProducible.NumberOfReachableOpenCitySpots(home) >= OpenSitesPerCityWhileExpanding * player.cities.Count;
		}

		private static City PickTarget(Player player, Player enemy) {
			return PickTarget(player, enemy, out _);
		}

		// The enemy city most worth taking: big, weakly held and near.
		private static City PickTarget(Player player, Player enemy, out float bestScore) {
			bestScore = float.MinValue;
			City best = null;
			foreach (City city in enemy.cities) {
				if (!AIFogOfWar.KnowsTile(player, city.location)) {
					continue;
				}
				int distance = int.MaxValue;
				foreach (City ours in player.cities) {
					if (ours.location.continent == city.location.continent) {
						distance = Math.Min(distance, ours.location.DistanceTo(city.location));
					}
				}
				if (distance > MaxTargetDistance) {
					continue;
				}
				float value = 2 + city.residents.Count;
				float cost = 1 + EstimateDefenders(player, city);
				float score = value / cost / (1 + distance / 4f);
				if (score > bestScore) {
					bestScore = score;
					best = city;
				}
			}
			return best;
		}

		private static int EstimateDefenders(Player player, City city) {
			if (AIFogOfWar.SeesUnitsOn(player, city.location)) {
				return city.location.unitsOnTile.Count(u => u.CanDefendOnLand());
			}
			return Math.Max(2, 1 + city.residents.Count / 3);
		}

		// The hits an attacker can expect to deal the defender before it
		// dies: it wins each round with some chance, and dies after losing as
		// many rounds as it has hit points. Even a lost fight wears the
		// defender down for the next attacker.
		// An army fights on with each of its members in turn, so it lasts
		// for all their hit points.
		internal static double ExpectedHits(MapUnit attacker, MapUnit defender) {
			double attack = attacker.StrengthVersus(defender, CombatRole.Attack, null);
			double defense = defender.StrengthVersus(attacker, CombatRole.Defense, null);
			if (attack <= 0) {
				return 0;
			}
			if (defense <= 0) {
				return 100;
			}
			double roundOdds = attack / (attack + defense);
			return Math.Max(1, attacker.CompositeHitPoints()) * roundOdds / (1 - roundOdds);
		}

		// The hit points of the enemy units on the tile that can defend it.
		// Units in an army are counted through the army.
		internal static int DefenderHitPoints(Player player, Tile tile) {
			int result = 0;
			foreach (MapUnit u in tile.unitsOnTile) {
				if (!u.IsLoaded() && !u.owner.IsAtPeaceWith(player) && u.CanDefendOnLand()) {
					result += Math.Max(1, u.CompositeHitPoints());
				}
			}
			return result;
		}

		// The hits the units can expect to deal the tile's defenders, each
		// against the defender that would face it.
		private static double ForceAgainst(IEnumerable<MapUnit> units, Tile tile) {
			double hits = 0;
			foreach (MapUnit u in units) {
				MapUnit defender = tile.FindTopDefender(u);
				hits += MapUnit.IsMapUnitValid(defender) ? ExpectedHits(u, defender) : 100;
			}
			return hits;
		}

		// HEURISTIC: how many more hits than the defenders have left the
		// attackers must expect to deal before they storm a city.
		private const double AssaultMargin = 1.3;

		// Whether the attackers, all together, can expect to wear down every
		// defender of the city.
		private static bool AssaultWorthwhile(Player player, List<MapUnit> stack, Tile city) {
			List<MapUnit> able = stack.Where(u => PlayerAI.IsStillOurs(u, player) && IsFighter(u) && u.movementPoints.canMove
				&& u.CanAttackAgainThisTurn() && !triedToAttack.Contains(u)).ToList();
			return ForceAgainst(able, city) >= AssaultMargin * DefenderHitPoints(player, city);
		}

		// Whether the campaign's fighters, all of them, healthy or not, here
		// or on their way, could expect to take the tile.
		private static bool CampaignStrongEnough(Player player, Campaign c, Tile tile, double margin) {
			List<MapUnit> fighters = c.units.Where(u => PlayerAI.IsStillOurs(u, player) && IsFighter(u)).ToList();
			return ForceAgainst(fighters, tile) >= margin * DefenderHitPoints(player, tile);
		}

		// Notes what the target's defenders are like while we can see them,
		// for planning when we no longer can.
		private static void RememberDefenders(Player player, Campaign c) {
			Tile city = c.target.location;
			if (!AIFogOfWar.SeesUnitsOn(player, city)) {
				return;
			}
			c.knownDefenderHitPoints = DefenderHitPoints(player, city);
			MapUnit sample = c.units.Where(IsAttacker).OrderByDescending(u => u.unitType.attack).FirstOrDefault();
			MapUnit defender = sample == null ? null : city.FindTopDefender(sample);
			c.knownHitsEach = MapUnit.IsMapUnitValid(defender) ? ExpectedHits(sample, defender) * sample.maxHitPoints / Math.Max(1, sample.hitPointsRemaining) : 0;
		}

		// How many attackers to gather: enough to expect to wear down every
		// defender, by our best attacker's odds against their best defender.
		private static int NeededAttackers(Player player, Campaign c) {
			Tile city = c.target.location;
			double hitsEach = 3;
			double defenderHitPoints = 3 * Math.Max(1, EstimateDefenders(player, c.target));
			MapUnit sample = c.units.Where(IsAttacker).OrderByDescending(u => u.unitType.attack).FirstOrDefault();
			if (sample != null && AIFogOfWar.SeesUnitsOn(player, city)) {
				MapUnit defender = city.FindTopDefender(sample);
				if (MapUnit.IsMapUnitValid(defender)) {
					hitsEach = ExpectedHits(sample, defender);
					defenderHitPoints = DefenderHitPoints(player, city);
				}
			} else if (c.knownDefenderHitPoints >= 0) {
				// The defenders we saw last time: a stack that fell back
				// for want of attackers comes back with enough.
				defenderHitPoints = Math.Max(defenderHitPoints, c.knownDefenderHitPoints);
				if (c.knownHitsEach > 0) {
					hitsEach = c.knownHitsEach;
				}
			}
			// Bombardment softens the defenders.
			if (c.units.Any(IsBombarder)) {
				defenderHitPoints *= 0.8;
			}
			int needed = (int)Math.Ceiling(AssaultMargin * defenderHitPoints / Math.Max(0.5, hitsEach)) + 1;
			// An army counts for (at least) as many attackers as it has
			// members.
			int armyMembers = c.units.Where(IsFightingArmy).Sum(a => a.PassengerCount());
			return Math.Clamp(needed - armyMembers, armyMembers > 0 ? 1 : 3, MaxAttackers);
		}

		// HEURISTIC: the most attackers a campaign gathers.
		private const int MaxAttackers = 16;

		private static City StagingCity(Player player, Campaign c) {
			City best = null;
			int bestDistance = int.MaxValue;
			foreach (City city in player.cities) {
				if (city.location.continent != c.target.location.continent) {
					continue;
				}
				int d = city.location.DistanceTo(c.target.location);
				if (d < bestDistance) {
					bestDistance = d;
					best = city;
				}
			}
			return best;
		}

		// Number of units in the city that can defend it and aren't in a
		// campaign.
		private static int HomeDefenders(Player player, Tile tile, MapUnit except) {
			int n = 0;
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u != except && u.owner == player && u.CanDefendOnLand() && u.currentAI is not MemberAI) {
					++n;
				}
			}
			return n;
		}

		// Takes attackers, siege engines and a defender to cover them from
		// among our units on the target's continent, leaving every city
		// at least one defender.
		private static void Recruit(Player player, Campaign c) {
			int continent = c.target.location.continent;
			int needed = NeededAttackers(player, c);
			int attackers = c.units.Count(IsAttacker);
			int bombarders = c.units.Count(IsBombarder);
			int escorts = c.units.Count(IsEscort);
			int armies = c.units.Count(IsFightingArmy);
			foreach (MapUnit u in player.units.ToList()) {
				if (u.currentAI is MemberAI || u.location.continent != continent || u.IsLoaded()
					|| u.currentAI is EscortAI || u.currentAI is FerryAI || IsJoiningArmy(u, player)) {
					continue;
				}
				bool take;
				if (IsFightingArmy(u)) {
					// An army leads the attack: it can take on a city's
					// best defenders and live. Only one that's filled up and
					// healthy.
					if (armies >= MaxArmies || Health(u) < ReadyHealth
						|| u.PassengerCount() < Math.Min(2, u.Capacity())) {
						continue;
					}
					c.units.Add(u);
					u.currentAI = new MemberAI();
					u.Wake();
					++armies;
					log.Information("{Player} has {Army} lead its campaign against {City}", player, u, c.target.name);
					continue;
				}
				if (IsAttacker(u)) {
					take = attackers < needed + 2;
				} else if (IsBombarder(u)) {
					take = bombarders < 3;
				} else if (IsEscort(u)) {
					// The escort needs a real defender to stay behind.
					take = escorts < 1 && u.location.cityAtTile?.owner == player
						&& u.location.unitsOnTile.Count(x => x != u && IsEscort(x) && x.owner == player && x.currentAI is not MemberAI) >= 1;
				} else {
					continue;
				}
				if (!take) {
					continue;
				}
				if (u.location.cityAtTile?.owner == player && HomeDefenders(player, u.location, u) < 1) {
					continue;
				}
				c.units.Add(u);
				u.currentAI = new MemberAI();
				u.Wake();
				if (IsAttacker(u)) {
					++attackers;
				} else if (IsBombarder(u)) {
					++bombarders;
				} else {
					++escorts;
				}
			}
		}

		// HEURISTIC: the most armies one campaign takes.
		private const int MaxArmies = 2;
		// HEURISTIC: the share of its hit points a unit needs to set out.
		private const double ReadyHealth = 0.7;

		// Whether the unit is on its way to join an army (see ArmyAI.Recruit).
		private static bool IsJoiningArmy(MapUnit u, Player player) {
			return u.currentAI is DefenderAI d && d.data.destination is Tile t && t != Tile.NONE
				&& t.unitsOnTile.Any(x => x.IsArmy() && x.owner == player);
		}

		private static async Task<bool> Gather(Player player, GameData gameData, Campaign c, bool atWar) {
			City staging = StagingCity(player, c);
			if (staging == null) {
				return false;
			}
			foreach (MapUnit u in c.units.ToList()) {
				if (u.location != staging.location) {
					await MoveTowards(player, u, staging.location);
				}
			}
			c.units.RemoveAll(u => !PlayerAI.IsStillOurs(u, player));

			List<MapUnit> ready = c.units.Where(u => u.location == staging.location && Health(u) >= ReadyHealth).ToList();
			int readyAttackers = ready.Count(IsAttacker);
			int needed = NeededAttackers(player, c);
			int waited = Turn - c.phaseStartTurn;
			// At war, after a while we go with most of what we need: the
			// stack won't attack without the force to win anyway, but more
			// may join it on the way, and seeing the defenders may show
			// fewer are needed.
			int enough = Math.Max(3, needed * 2 / 3);
			bool go = readyAttackers >= needed || (atWar && waited >= MaxGatherTurns / 2 && readyAttackers >= enough);
			if (!go) {
				foreach (MapUnit u in c.units) {
					if (u.location == staging.location && !u.isFortified) {
						u.Fortify();
					}
				}
				return waited < MaxGatherTurns || atWar;
			}

			if (!atWar) {
				// Still worth it?
				float us = player.CalculateMilitaryStrength();
				if (us < DeclareStrengthRatio * c.enemy.CalculateMilitaryStrength()) {
					return false;
				}
				player.DeclareWarOn(c.enemy, gameData.turn);
				log.Information("{Player} declared war on {Enemy} to take {City}", player, c.enemy, c.target.name);
				MsgWarDeclaration.Announce(player, c.enemy);
			}
			log.Information("{Player} marches {Count} units ({Attackers} attackers) from {From} on {City}", player, ready.Count, readyAttackers, staging.name, c.target.name);
			c.stackTile = staging.location;
			SetPhase(c, Phase.March);
			// The units not ready stay behind until they are, and then
			// catch up.
			await March(player, c);
			if (c.phase == Phase.Siege) {
				await Siege(player, c);
			}
			return true;
		}

		private static bool HasArmedEnemy(Player player, Tile tile) {
			foreach (MapUnit u in tile.unitsOnTile) {
				if (!u.owner.IsAtPeaceWith(player) && u.CanDefendOnLand()) {
					return true;
				}
			}
			return false;
		}

		private static bool IsNeighbor(Tile a, Tile b) {
			foreach (Tile n in a.neighbors.Values) {
				if (n == b) {
					return true;
				}
			}
			return false;
		}

		// The tile next to the target the stack attacks from: one it can
		// get to, preferring good defensive terrain. A tile enemy units
		// stand on will do if need be: the stack fights its way there.
		private static Tile RallyTile(Player player, Campaign c, MapUnit lead, out TilePath path) {
			List<Tile> candidates = c.target.location.neighbors.Values
				.Where(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.continent == c.target.location.continent
					&& !t.unitsOnTile.Any(u => u.owner != player && u.owner.IsAtPeaceWith(player)))
				.OrderByDescending(t => t.overlayTerrainType.defenseBonus.amount * 4 - t.DistanceTo(c.stackTile)
					- (t.unitsOnTile.Any(u => u.owner != player) ? 10 : 0))
				.ToList();
			int index = PathingAlgorithmChooser.GetAlgorithm(lead).FindFirstReachable(c.stackTile, candidates, lead, out path);
			return index < 0 ? null : candidates[index];
		}

		// Moves the units on the stack tile together, at the pace of the
		// slowest, to a tile next to the target.
		private static async Task March(Player player, Campaign c) {
			if (IsNeighbor(c.stackTile, c.target.location)) {
				SetPhase(c, Phase.Siege);
				return;
			}
			RememberDefenders(player, c);
			// Once we can see the defenders, a stack that couldn't take the
			// city even with everyone who's on the way doesn't go and stand
			// outside it.
			if (AIFogOfWar.SeesUnitsOn(player, c.target.location) && HasArmedEnemy(player, c.target.location)
					&& !CampaignStrongEnough(player, c, c.target.location, HopelessMargin(c))) {
				log.Information("{Player} sees {City} is too strongly held, and falls back", player, c.target.name);
				StartRetreat(player, c);
				return;
			}
			List<MapUnit> stack = c.units.Where(u => u.location == c.stackTile).OrderBy(u => u.MaxMovementPoints()).ToList();
			if (stack.Count == 0) {
				return;
			}
			MapUnit lead = stack[0];
			Tile rally = RallyTile(player, c, lead, out TilePath path);
			if (rally == null || path == null) {
				// No way there: give the target up.
				++c.failures;
				SetPhase(c, Phase.Gather);
				return;
			}

			await MoveStack(player, c, stack, path);

			if (IsNeighbor(c.stackTile, c.target.location)) {
				SetPhase(c, Phase.Siege);
			} else if (Turn - c.phaseStartTurn > MaxMarchTurns) {
				++c.failures;
				SetPhase(c, Phase.Gather);
			}
		}

		// Moves the stack (the campaign's units on its tile, slowest first)
		// along the path together as far as the slowest can go, fighting
		// its way past enemies in the way when it's strong enough to.
		private static async Task MoveStack(Player player, Campaign c, List<MapUnit> stack, TilePath path) {
			MapUnit lead = stack[0];
			while (path.PathLength() > 0 && lead.movementPoints.canMove && PlayerAI.IsStillOurs(lead, player)) {
				Tile next = path.PeekNext();
				if (next.unitsOnTile.Any(u => !u.owner.IsAtPeaceWith(player))) {
					// Someone is in the way: attack if the odds are good.
					if (!await ClearTheWay(player, stack, next) || HasArmedEnemy(player, next)) {
						break;
					}
					continue;
				}
				path.Next();
				bool leadArrived = false;
				foreach (MapUnit u in stack) {
					if (!PlayerAI.IsStillOurs(u, player) || u.location != c.stackTile || !u.movementPoints.canMove) {
						continue;
					}
					await u.Move(u.location.DirectionTo(next));
					if (u == lead && u.location == next) {
						leadArrived = true;
					}
				}
				if (!leadArrived) {
					break;
				}
				c.stackTile = next;
				stack = stack.Where(u => PlayerAI.IsStillOurs(u, player) && u.location == next).ToList();
				if (stack.Count == 0) {
					break;
				}
			}
		}

		// Below this many expected hits per defender hit point, not even
		// everyone in the campaign could take the city: they don't expect to
		// wear down every defender (bombardment helping).
		private static double HopelessMargin(Campaign c) {
			return c.units.Any(IsBombarder) ? 0.8 : 1.0;
		}

		// HEURISTIC: an attack the stack makes even without the force to win
		// the whole fight: it's very likely to kill a defender.
		private const double SureOdds = 0.7;

		// Attacks enemies in the stack's way: all out if the stack can expect
		// to beat every unit there, and otherwise only at very good odds, so
		// it doesn't spend itself on a stack it can't destroy. Returns
		// whether it attacked.
		private static async Task<bool> ClearTheWay(Player player, List<MapUnit> stack, Tile tile) {
			bool enough = AssaultWorthwhile(player, stack, tile);
			return await AttackIfGoodOdds(player, stack, tile, enough ? MinMarchOdds : SureOdds);
		}

		// The least an army's odds must be for it to attack: losing it loses
		// all its members.
		private const double MinArmyOdds = 0.6;

		// Attacks the tile with the unit of the stack that has the best odds
		// against its defender, if they are at least minOdds. An army goes
		// first when its odds are good: its hit points last through more
		// than one defender. Returns whether it attacked.
		private static async Task<bool> AttackIfGoodOdds(Player player, List<MapUnit> stack, Tile tile, double minOdds) {
			MapUnit best = null;
			double bestOdds = minOdds;
			bool bestIsArmy = false;
			foreach (MapUnit u in stack) {
				bool army = IsFightingArmy(u);
				if (!PlayerAI.IsStillOurs(u, player) || !(IsAttacker(u) || army) || !u.movementPoints.canMove || !u.CanAttackAgainThisTurn()
					|| !IsNeighbor(u.location, tile) || triedToAttack.Contains(u)) {
					continue;
				}
				MapUnit defender = tile.FindTopDefender(u);
				double odds = MapUnit.IsMapUnitValid(defender) ? CombatAI.EstimateChanceToWin(u, defender) : 1;
				if (army) {
					if (odds >= Math.Max(minOdds, MinArmyOdds) && (!bestIsArmy || odds >= bestOdds)) {
						bestOdds = odds;
						best = u;
						bestIsArmy = true;
					}
				} else if (!bestIsArmy && odds >= bestOdds) {
					bestOdds = odds;
					best = u;
				}
			}
			if (best == null) {
				return false;
			}
			triedToAttack.Add(best);
			await best.Move(best.location.DirectionTo(tile));
			return true;
		}

		// HEURISTIC: how many turns a stack too weak to take the city waits
		// outside it for more attackers, and how many when more are on their
		// way.
		private const int ShortWaitTurns = 1;
		private const int ReinforcedWaitTurns = 4;

		private static async Task Siege(Player player, Campaign c) {
			Tile city = c.target.location;
			if (!IsNeighbor(c.stackTile, city)) {
				SetPhase(c, Phase.March);
				return;
			}
			List<MapUnit> stack = c.units.Where(u => u.location == c.stackTile).ToList();

			// Bombard first, to wear the defenders down.
			foreach (MapUnit u in stack) {
				if (IsBombarder(u) && u.movementPoints.canMove && HasArmedEnemy(player, city) && u.CanBombardTile(city)) {
					await u.Bombard(city);
				}
			}
			RememberDefenders(player, c);

			// Then attack, all out if together we can expect to take the
			// city, best odds first. Short of that, only attacks very likely
			// to win: trickling attackers in one at a time against a city we
			// can't take only loses them.
			while (HasArmedEnemy(player, city)
				&& await AttackIfGoodOdds(player, stack, city, AssaultWorthwhile(player, stack, city) ? MinAssaultOdds : SureOdds)) {
				stack.RemoveAll(u => !PlayerAI.IsStillOurs(u, player));
			}

			// An emptied city is taken by walking in.
			if (c.target.owner == c.enemy && !HasArmedEnemy(player, city)) {
				foreach (MapUnit u in stack) {
					if (PlayerAI.IsStillOurs(u, player) && u.location == c.stackTile && u.movementPoints.canMove && u.CanAttack()) {
						await u.Move(u.location.DirectionTo(city));
						break;
					}
				}
			}

			if (c.target.owner == player) {
				// Everyone moves in; PlayCampaign garrisons it next turn.
				foreach (MapUnit u in c.units) {
					if (PlayerAI.IsStillOurs(u, player) && u.location == c.stackTile && u.movementPoints.canMove) {
						await u.Move(u.location.DirectionTo(city));
					}
				}
				c.stackTile = city;
				Garrison(player, c, c.target);
				return;
			}

			// Not strong enough to take the city next turn, even with the
			// units about to catch up: wait a little for them, then fall back
			// together rather than stand exposed. (Units don't heal in enemy
			// territory, so waiting there doesn't help the hurt.)
			List<MapUnit> nextTurn = c.units.Where(u => PlayerAI.IsStillOurs(u, player) && IsFighter(u)
				&& u.location.DistanceTo(c.stackTile) <= 1).ToList();
			if (!HasArmedEnemy(player, city) || ForceAgainst(nextTurn, city) >= AssaultMargin * DefenderHitPoints(player, city)) {
				c.shortTurns = 0;
			} else {
				++c.shortTurns;
				bool reinforcements = c.units.Any(u => PlayerAI.IsStillOurs(u, player) && IsFighter(u) && u.location.DistanceTo(c.stackTile) > 1)
					&& CampaignStrongEnough(player, c, city, HopelessMargin(c));
				if (c.shortTurns > (reinforcements ? ReinforcedWaitTurns : ShortWaitTurns)) {
					log.Information("{Player} is too weak to take {City}, and falls back", player, c.target.name);
					StartRetreat(player, c);
					return;
				}
			}

			if (Turn - c.phaseStartTurn > MaxSiegeTurns) {
				StartRetreat(player, c);
			}
		}

		// Our city nearest the stack, preferring the staging city, that the
		// stack falls back to.
		private static Tile RetreatCity(Player player, Campaign c) {
			City staging = StagingCity(player, c);
			Tile from = c.stackTile ?? staging?.location;
			if (from == null) {
				return null;
			}
			City nearest = player.cities.Where(city => city.location.continent == from.continent)
				.OrderBy(city => city.location.DistanceTo(from)).FirstOrDefault();
			if (staging != null && nearest != null && staging.location.DistanceTo(from) <= nearest.location.DistanceTo(from) + 2) {
				return staging.location;
			}
			return nearest?.location ?? staging?.location;
		}

		// Gives up the attack on the target for now: the stack goes back to
		// one of our cities together, heals, and gathers more attackers.
		private static void StartRetreat(Player player, Campaign c) {
			++c.failures;
			c.shortTurns = 0;
			c.retreatTo = RetreatCity(player, c);
			if (c.retreatTo == null || c.stackTile == null) {
				SetPhase(c, Phase.Gather);
				return;
			}
			SetPhase(c, Phase.Retreat);
		}

		// Moves the stack back to the city it's falling back to, all
		// together, at the pace of the slowest.
		private static async Task Retreat(Player player, Campaign c) {
			if (c.retreatTo.cityAtTile?.owner != player) {
				c.retreatTo = RetreatCity(player, c);
			}
			if (c.retreatTo == null || c.stackTile == c.retreatTo || Turn - c.phaseStartTurn > MaxMarchTurns) {
				SetPhase(c, Phase.Gather);
				return;
			}
			List<MapUnit> stack = c.units.Where(u => u.location == c.stackTile).OrderBy(u => u.MaxMovementPoints()).ToList();
			if (stack.Count == 0) {
				SetPhase(c, Phase.Gather);
				return;
			}
			TilePath path = PathingAlgorithmChooser.GetAlgorithm(stack[0]).PathFrom(c.stackTile, c.retreatTo, stack[0]);
			if (path == null) {
				SetPhase(c, Phase.Gather);
				return;
			}
			await MoveStack(player, c, stack, path);
			if (c.stackTile == c.retreatTo) {
				SetPhase(c, Phase.Gather);
			}
		}

		// Moves a unit along a path towards the tile, as far as it can this
		// turn, attacking only what it is very likely to beat on the way.
		private static async Task MoveTowards(Player player, MapUnit u, Tile destination) {
			if (!u.movementPoints.canMove || u.location == destination) {
				return;
			}
			TilePath path = PathingAlgorithmChooser.GetAlgorithm(u).PathFrom(u.location, destination, u);
			if (path == null) {
				return;
			}
			while (path.PathLength() > 0 && u.movementPoints.canMove && PlayerAI.IsStillOurs(u, player)) {
				Tile next = path.PeekNext();
				if (next.unitsOnTile.Any(x => !x.owner.IsAtPeaceWith(player))) {
					if (!await AttackIfGoodOdds(player, [u], next, 0.65) || HasArmedEnemy(player, next)) {
						return;
					}
					continue;
				}
				if (!u.CanEnter(next)) {
					return;
				}
				path.Next();
				Tile before = u.location;
				if (!await u.Move(u.location.DirectionTo(next)) || u.location == before) {
					return;
				}
			}
		}
	}
}
