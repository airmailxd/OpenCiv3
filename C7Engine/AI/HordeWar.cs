using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7Engine.Pathing;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// Trial switch "horde-war": Advanced AI only. Conquest the way strong
	// human players do it in Civ3 (doc/human-benchmarks.md, 124 CivFanatics
	// Conquests Game-of-the-Month saves on the same 100x100 maps): the first
	// city is taken at a median turn 107, while still settling; captures
	// are about half of all the cities a strong human gains; the army is one
	// fast attacker in bulk (40-110 Horsemen, Knights, Cavalry or a mounted
	// unique unit), with little siege; and in the 50 turns after the first
	// capture about 8 more cities are founded, on razed sites, in gaps and
	// on the beaten civ's empty land.
	//
	// prey-war waits for the plan to name a "much weaker" neighbour once
	// land runs out, which with Standard neighbours (whose armies run about
	// 30 units above an Advanced civ's) rarely happens. Here, from StartTurn
	// once few city sites are left (or from EarliestTurn, if our fast
	// attacker outclasses a reachable neighbour's best defender), the civ
	// picks the best victim on its landmass by its near cities, how many it
	// could take and keep, and how they are held (PickVictim), not by the
	// much-weaker test (only an army more than MaxStrengthRatio times ours
	// rules a civ out), and starts a
	// WarCampaign on it: the stack masses HordeSize attackers (enough for
	// the defenders of its first TargetCities cities, several times over,
	// capped by what the empire can carry) and a few defenders to hold what
	// it takes; the best production cities build the horde unit
	// (ProductionBonus) while the others go on settling. It declares once
	// massed, or after GoAfterTurns with two thirds; then it presses city to
	// city, waiting only for PressAttackers in each city it takes. A city it
	// takes is kept and held against a flip back as prey-war's are, or
	// razed if no garrison it can leave would hold it (ShouldRaze), and
	// settlers may then found cities next to the victim's land
	// (MaySettleNextTo). It gives the campaign up when the war goes badly by
	// PeaceAI's own measure (losses and length), and while it is winning it
	// makes peace only for a city.
	//
	// v2 (it won its fights no better than it traded units: 12 lost a game
	// to 2 off): the horde is of a fast unit only, and goes only for a
	// victim it has the edge on (HasEdge); its builders put up a Barracks
	// first (BarracksScore); its attacks, and those of the civ's other
	// units at war, are made only when the unit is unlikely to die, a fast
	// unit counting its withdrawal (DeathChance), so the stack attacks the
	// turn after it arrives, with its full moves; and half its cities
	// build it.
	public static class HordeWar {
		public const string Switch = "horde-war";

		public static bool On(Player player) => player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);

		// HEURISTIC: the turn from which the civ masses a horde whoever its
		// neighbours are. Strong humans took their first city at a median
		// turn 107 (Regent 122); a horde gathers for 10-20 turns.
		public const int StartTurn = 90;
		// HEURISTIC: the turn from which it masses one sooner against a
		// neighbour whose best defender its fast attacker outclasses (the
		// rushers among the humans struck from turn 60).
		public const int EarliestTurn = 50;
		// HEURISTIC: our fast attacker's attack over their best defence
		// (by the techs they have) for it to outclass them.
		public const double OutclassRatio = 2.0;
		// HEURISTIC: the victim's cities, nearest first, the horde is sized
		// and the victim chosen by.
		public const int TargetCities = 3;
		// HEURISTIC: how many times over the horde should beat those
		// cities' defenders.
		public const double HordeMultiple = 2.0;
		// HEURISTIC: the horde's size: at least MinHorde, at most PerCity
		// attackers for each of our cities (what the empire can carry) and
		// MaxHorde. Strong humans fielded 40-110 by the end; this is the
		// first wave.
		public const int MinHorde = 12;
		public const double PerCity = 2.5;
		public const int MaxHorde = 40;
		// HEURISTIC: after the first city, the stack waits in each city it
		// takes for this many attackers before the next.
		public const int PressAttackers = 8;
		// HEURISTIC: gathering at peace, the horde goes with two thirds of
		// its size after GoAfterTurns, and is given up after MaxGatherTurns
		// short of that.
		public const int GoAfterTurns = 20;
		public const int MaxGatherTurns = 35;
		// HEURISTIC: the defenders that march with the horde, to hold what
		// it takes.
		public const int Escorts = 3;
		// The fortified bonus (Civilopedia GCON_Terrain_Combat: "Fortified
		// Units +25%", compendium 01).
		private const double FortifiedFactor = 1.25;
		// HEURISTIC: a civ whose military strength is more than this many
		// times ours isn't a victim; below it, a bigger army than ours
		// lowers their score in proportion.
		public const float MaxStrengthRatio = 1.5f;
		// HEURISTIC: see JustTookACity.
		public const int PressOnTurns = 15;
		// HEURISTIC: see Time.
		public const int LateStartTurn = 120;
		public const int MaxOpenSites = 3;
		// HEURISTIC: the turns a civ the horde gave up on as going badly
		// is left alone by the next one.
		public const int BarTurns = 50;
		// HEURISTIC: the share of its size a horde gathering at peace goes
		// with after GoAfterTurns.
		public const double GoShare = 0.67;
		// HEURISTIC: the turns after a horde is given up before the next.
		public const int CooldownTurns = 20;
		// HEURISTIC: what building the horde unit is worth, in a horde
		// builder, while the horde is short of attackers. At 40 the builders
		// went on building wonders on a replay (seed 7100) and the horde
		// stood at 11 of 20 for 35 turns.
		public const float BuilderBonus = 150;
		public const float EscortBonus = 20;
		// HEURISTIC: the share of our cities, the most productive, that
		// build the horde (at least two); the rest go on settling. v2: half
		// (v1 a third): on the smoke replays (seeds 7100-7115) a horde took
		// 25-40 turns to mass, so that most wars began after turn 150.
		public const int BuilderShareDivisor = 2;
		public const int BuilderTurns = 20;
		// HEURISTIC: what a city we would raze is worth as a target, as a
		// share of one we would keep.
		public const float RazeValue = 0.4f;
		// HEURISTIC: how far (tiles) from one of our cities a site next to
		// the victim's land may be for our settlers, at war with it.
		public const int SettleNearRadius = 4;

		private static readonly ConditionalWeakTable<Player, StrongBox<int>> nextHordeTurn = new();

		// The fast attacker the horde is made of: the land unit with two or
		// more moves and more attack than defence with the best attack the
		// civ can build in any of its cities (mounted unique units among
		// them), the cheapest among equals. Null if it can build none: v1
		// fell back on the best slow attacker, and on replays (seeds
		// 7100-7103) the archer hordes lost every attack they made (6 of
		// 6, against fortified spearmen), with no retreat to save them.
		// Looked for in every city, the biggest first, as one without
		// horses can't build it (on a replay the unit flickered between
		// Horseman and Archer as the biggest city's road to horses came
		// and went). Kept for the turn.
		public static UnitPrototype HordeUnit(Player player) {
			int turn = EngineStorage.gameData?.turn ?? 0;
			if (hordeUnits.TryGetValue(player, out Tuple<int, UnitPrototype> cached) && cached.Item1 == turn) {
				return cached.Item2;
			}
			UnitPrototype best = null;
			GameData gameData = EngineStorage.gameData;
			foreach (City city in player.cities.OrderByDescending(c => c.residents.Count).ThenBy(c => c.id.ToString())) {
				UnitPrototype here = city.ListProductionOptions(gameData).OfType<UnitPrototype>().Where(IsFastAttacker)
					.OrderByDescending(u => u.attack).ThenBy(u => u.shieldCost).ThenBy(u => u.name, StringComparer.Ordinal).FirstOrDefault();
				if (here != null && (best == null || here.attack > best.attack)) {
					best = here;
				}
			}
			hordeUnits.AddOrUpdate(player, Tuple.Create(turn, best));
			return best;
		}

		private static readonly ConditionalWeakTable<Player, Tuple<int, UnitPrototype>> hordeUnits = new();

		private static bool IsAttacker(UnitPrototype u) {
			return u.IsLandUnit() && !u.isArmy && !u.isSettler && !u.isWorker && u.attack > u.defense && u.attack > 0;
		}

		public static bool IsFastAttacker(UnitPrototype u) {
			return IsAttacker(u) && u.movement >= 2;
		}

		// The attack the horde fights with: its unit's, or failing one our
		// best attacker's.
		private static int HordeAttack(Player player) {
			return HordeUnit(player)?.attack ?? EarlyRush.BestAttack(player);
		}

		// Whether our fast attacker outclasses their best defender.
		public static bool Outclasses(Player us, Player them) {
			UnitPrototype u = HordeUnit(us);
			return u != null && IsFastAttacker(u) && u.attack >= OutclassRatio * Math.Max(1, EarlyRush.BestDefense(them));
		}

		// v2: the odds of a round of our horde unit's against their best
		// defender (by the techs they have) fortified in the city: its
		// defence with the fortified bonus and the city's (size, walls,
		// terrain) added up, as Civ3 adds them (compendium 01, 1.6:
		// "Bonuses add up (they are not multiplied)"). 0 without a horde
		// unit.
		public static double EdgeOdds(Player us, City city) {
			UnitPrototype unit = HordeUnit(us);
			if (unit == null || unit.attack <= 0) {
				return 0;
			}
			double defence = Math.Max(1, EarlyRush.BestDefense(city.owner)) * (PreyWar.DefenceFactor(city) + FortifiedBonus);
			return unit.attack / (unit.attack + defence);
		}

		// v2: whether our horde unit has the edge on the city's defenders:
		// whether a regular of ours, attacking with its full moves, would
		// die no more often than MaxDeathChance against a regular of
		// their best defender fortified there (see DeathChance; it loses
		// with 1 - EdgeOdds, as the hit points are equal, and withdraws
		// with an even chance from a slower defender). Horsemen (attack 2)
		// against Spearmen (2) in a town on grassland: 0.29, in a city of 7:
		// 0.33; against Pikemen (3) in a city of 7: 0.37, no edge; Knights
		// (4) there: 0.29. The "tech step ahead" of the strong humans
		// (doc/human-benchmarks.md) counts, but so does the withdrawal:
		// asked for round odds of 0.45 instead, no horde set out in 16
		// smoke games (seeds 7100-7115), as Horsemen never had them.
		public static bool HasEdge(Player us, City city) {
			UnitPrototype unit = HordeUnit(us);
			UnitPrototype defender = EarlyRush.LandUnitsBuildable(city.owner).OrderByDescending(u => u.defense).ThenBy(u => u.name, StringComparer.Ordinal).FirstOrDefault();
			if (unit == null) {
				return false;
			}
			bool withdraws = defender == null || defender.movement <= 1;
			return (1 - EdgeOdds(us, city)) * (withdraws ? RegularRetreatChance : 1) <= MaxDeathChance;
		}

		// A regular's chance to withdraw from a regular: its retreat bonus
		// (50) over the other's plus 50 (compendium 01, 1.5).
		private const double RegularRetreatChance = 0.5;
		// The fortified bonus, added to the city's (Civilopedia
		// GCON_Terrain_Combat: "Fortified Units +25%", compendium 01).
		private const double FortifiedBonus = 0.25;

		// v2: the chance the attacker dies attacking the defender, given
		// the chance it wins: a fast unit that loses withdraws with its
		// retreat chance (MapUnit.RetreatChance: only against a slower
		// unit, by both experience levels), if it has the movement left
		// for it after the attack (a third of a point, as Fight checks,
		// compendium 01, 1.5) and more than one hit point to start with.
		// So a horde attacks on the turn after it arrives, with its full
		// moves: on the v1 replays not one of its horsemen withdrew, as
		// they attacked off the march.
		// From distance tiles away, it gets there with a move spent on
		// each tile but the last (roughly: roads are cheaper, hills dearer).
		public static double DeathChance(MapUnit attacker, MapUnit defender, double odds, int distance = 1) {
			float movesThere = attacker.movementPoints.remaining - Math.Max(0, distance - 1);
			bool canWithdraw = attacker.CompositeHitPoints() >= 2 && movesThere - 1 >= 1f / 3 - 0.0001f;
			double retreat = canWithdraw ? attacker.RetreatChance(defender, true) : 0;
			return (1 - odds) * (1 - retreat);
		}

		// UNVERIFIED (ours): the highest DeathChance an attack of the horde
		// may have, in an assault the stack can expect to win; above it,
		// only attacks at WarCampaign's sure odds. A regular horseman that
		// can withdraw against a regular defender (an even retreat chance)
		// attacks at odds of 0.3 or more; one that can't, at 0.65.
		public const double MaxDeathChance = 0.35;

		// v2: what a Barracks is worth in a horde builder before the war,
		// so that the horde comes out veteran (4 hit points to 3, a retreat
		// bonus of 58 to 50: compendium 01, 1.2 and 1.5), above
		// BuilderBonus so that it comes first. Only in a builder making at
		// least BarracksMinShields, where the 40 shields pay off over the
		// horde's units. UNVERIFIED (ours).
		public const float BarracksBonus = 200;
		public const int BarracksMinShields = 5;

		public static float BarracksScore(City city, Player player) {
			if (!On(player) || !WarCampaign.HasHordeCampaign(player) || WarCampaign.HordeMassed(player) || !WarCampaign.WantsAttackers(player)
					|| !Builders(player).Contains(city) || city.CurrentProductionYield().useful < BarracksMinShields) {
				return 0;
			}
			return BarracksBonus;
		}

		// Whether it is time to mass a horde against them: from
		// EarliestTurn if we outclass them; from StartTurn once few city
		// sites are left open to us (MaxOpenSites), and from LateStartTurn
		// whatever is left. On the replays a civ of 7 cities with 11 open
		// sites that went to war at turn 90 stopped settling (no sites next
		// to an enemy's land at war) and ended with 11 cities instead of 13.
		public static bool Time(GameData gameData, Player us, Player them) {
			if (gameData.turn >= EarliestTurn && Outclasses(us, them)) {
				return true;
			}
			if (gameData.turn < StartTurn) {
				return false;
			}
			return gameData.turn >= LateStartTurn || us.cities.Count == 0
				|| ChooseProducible.NumberOfReachableOpenCitySpots(us.cities[0]) <= MaxOpenSites;
		}

		// A civ the horde gave up on as going badly isn't a victim again
		// for BarTurns.
		private static readonly ConditionalWeakTable<Player, Dictionary<Player, int>> barred = new();

		internal static void Bar(Player player, Player enemy) {
			barred.GetOrCreateValue(player)[enemy] = (EngineStorage.gameData?.turn ?? 0) + BarTurns;
		}

		private static bool Barred(Player player, Player enemy) {
			return barred.TryGetValue(player, out Dictionary<Player, int> until) && until.TryGetValue(enemy, out int turn)
				&& (EngineStorage.gameData?.turn ?? 0) < turn;
		}

		public static bool CoolingDown(Player player) {
			return nextHordeTurn.TryGetValue(player, out StrongBox<int> next) && (EngineStorage.gameData?.turn ?? 0) < next.Value;
		}

		internal static void StartCooldown(Player player) {
			nextHordeTurn.AddOrUpdate(player, new StrongBox<int>((EngineStorage.gameData?.turn ?? 0) + CooldownTurns));
		}

		// The rival the horde goes for, and the city it takes first, or
		// null. Only a civ we are at war with or may declare on, with no
		// protection pact with us, that the plan doesn't call a friend, with
		// a known city on a landmass of ours that our land units can get to.
		// At war with someone there already, only them: no second front.
		// Scored by its TargetCities nearest cities: each by what it is
		// worth (its size, by the chance we keep it, or RazeValue if we
		// would raze it), over the defence we'd meet there (their
		// defenders, with walls, size and hills, by their best defence over
		// our attack), the nearer the better.
		public static Player PickVictim(Player player, GameData gameData, out City first) {
			first = null;
			if (player.cities.Count == 0) {
				return null;
			}
			HashSet<int> ourLand = player.cities.Select(c => c.location.continent).ToHashSet();
			List<Player> rivals = gameData.players.Where(p => p != player && !p.isBarbarians && !p.IsIndependent && !p.defeated && p.cities.Count > 0
				&& WarCampaign.CanFight(player, p) && !Pacts.HasDeal(player, p, DealSubType.MutualProtectionPact)).ToList();
			List<Player> atWarNear = rivals.Where(p => player.playerRelationships.TryGetValue(p.id, out PlayerRelationship r) && r.AtWar()
				&& p.cities.Any(c => ourLand.Contains(c.location.continent))).ToList();
			if (atWarNear.Count > 0) {
				rivals = atWarNear;
			}
			float ours = Math.Max(1f, player.CalculateMilitaryStrength());
			Player best = null;
			double bestScore = 0;
			foreach (Player p in rivals) {
				if (Barred(player, p) || !Time(gameData, player, p)) {
					continue;
				}
				// A war already going badly isn't one to mass a horde for.
				bool atWar = player.playerRelationships.TryGetValue(p.id, out PlayerRelationship rel) && rel.AtWar();
				if (atWar && GoingBadly(gameData, player, p)) {
					continue;
				}
				// Nor a civ whose army is far bigger than ours.
				float theirs = Math.Max(1f, p.CalculateMilitaryStrength());
				if (theirs > MaxStrengthRatio * ours) {
					continue;
				}
				List<City> near = NearestCities(player, p);
				if (near.Count == 0 || !Reachable(player, near[0])) {
					continue;
				}
				// v2: nor one whose near cities our horde unit has no edge on.
				if (!near.Any(c => HasEdge(player, c))) {
					continue;
				}
				double score = 0;
				foreach (City city in near) {
					score += CityScore(gameData, player, city, 6.0);
				}
				// A bigger army than ours counts against them.
				score *= Math.Min(1.0, ours / theirs);
				if (score > bestScore) {
					bestScore = score;
					best = p;
				}
			}
			if (best != null) {
				first = PickTarget(player, best);
			}
			return first == null ? null : best;
		}

		// Their known cities on a landmass of ours, nearest to one of ours
		// first, TargetCities of them.
		internal static List<City> NearestCities(Player player, Player them) {
			return them.cities.Where(c => AIFogOfWar.KnowsTile(player, c.location) && ArcherRush.DistanceTo(player, c) != int.MaxValue)
				.OrderBy(c => ArcherRush.DistanceTo(player, c)).ThenBy(c => c.id.ToString()).Take(TargetCities).ToList();
		}

		// Whether one of our land combat units could walk from our city
		// nearest theirs to a tile next to it.
		private static bool Reachable(Player player, City city) {
			City from = player.cities.Where(c => c.location.continent == city.location.continent)
				.OrderBy(c => c.location.DistanceTo(city.location)).FirstOrDefault();
			MapUnit walker = player.units.FirstOrDefault(u => u.IsLandUnit() && !u.IsArmy() && u.location.continent == city.location.continent
				&& u.unitType.attack + u.unitType.defense > 0 && !u.IsLoaded());
			if (from == null || walker == null) {
				// No unit to try with: a landmass we share will do.
				return from != null;
			}
			List<Tile> around = city.location.neighbors.Values.Where(t => t != Tile.NONE && t.IsLand() && !t.HasCity()).ToList();
			return around.Count > 0 && PathingAlgorithmChooser.GetAlgorithm(walker).FindFirstReachableIndex(from.location, around, walker) >= 0;
		}

		// The victim's city the horde takes next: as PickVictim scores
		// them, among all their known cities on a landmass of ours. Null if
		// none.
		public static City PickTarget(Player player, Player enemy) {
			GameData gameData = EngineStorage.gameData;
			City best = null;
			double bestScore = double.MinValue;
			foreach (City city in enemy.cities) {
				if (!AIFogOfWar.KnowsTile(player, city.location)) {
					continue;
				}
				if (ArcherRush.DistanceTo(player, city) == int.MaxValue) {
					continue;
				}
				double score = CityScore(gameData, player, city, 4.0);
				// v2: a city our horde unit has the edge on first.
				if (!HasEdge(player, city)) {
					score -= NoEdgePenalty;
				}
				if (score > bestScore) {
					bestScore = score;
					best = city;
				}
			}
			return best;
		}

		// Below any city we have the edge on (CityScore is a few units at
		// most).
		private const double NoEdgePenalty = 1000;

		// A city of theirs as a target: what it is worth (its size, by the
		// chance we keep it, or RazeValue if we would raze it), over the
		// attackers its defenders would take, the nearer the better.
		private static double CityScore(GameData gameData, Player player, City city, double distanceScale) {
			int distance = ArcherRush.DistanceTo(player, city);
			double value = (2 + city.residents.Count) * (PreyWar.WorthKeeping(gameData, player, city) ? PreyWar.KeepChance(gameData, player, city) : RazeValue);
			double cost = 1 + WarCampaign.EstimateDefenders(player, city) * AttackersPerDefender(player, city);
			return value / cost / (1 + distance / distanceScale);
		}

		// The attackers each defender of the city would take, by their best
		// defence (by their techs, fortified: +25%, compendium 01) with the
		// city's walls, size and hills over our horde unit's attack; at
		// least one.
		private static double AttackersPerDefender(Player player, City city) {
			int attack = Math.Max(1, HordeAttack(player));
			double defence = Math.Max(1, EarlyRush.BestDefense(city.owner)) * FortifiedFactor * PreyWar.DefenceFactor(city);
			return Math.Max(1, defence / attack);
		}

		// The attackers the horde masses before it sets out: HordeMultiple
		// times what the defenders of the victim's TargetCities nearest
		// cities would take (each defender, with its city's walls, size and
		// hills, by their best defence over our attack), between MinHorde
		// and what the empire can carry (PerCity each, at most MaxHorde).
		public static int HordeSize(Player player, Player enemy) {
			double need = 0;
			foreach (City city in NearestCities(player, enemy)) {
				need += WarCampaign.EstimateDefenders(player, city) * AttackersPerDefender(player, city);
			}
			int cap = Math.Clamp((int)Math.Round(PerCity * player.cities.Count), MinHorde, MaxHorde);
			return Math.Clamp((int)Math.Ceiling(HordeMultiple * need), MinHorde, cap);
		}

		// Whether a war on them is going badly enough to stop pressing it:
		// by PeaceAI's own outlook, which weighs the losses on each side and
		// the war's length, not the armies' sizes alone (a Standard
		// neighbour's army is often bigger than ours all war).
		// Not before the two sides have lost PeaceAI.MinWarLosses between
		// them: until then the outlook is the armies' sizes alone, and on
		// replays a horde that had just declared on a Standard neighbour
		// was given up the next turn, before a battle.
		public static bool GoingBadly(GameData gameData, Player player, Player enemy) {
			return PeaceAI.WarLosses(player, enemy) + PeaceAI.WarLosses(enemy, player) >= PeaceAI.MinWarLosses
				&& PeaceAI.PeaceOutlook(gameData, player, enemy) <= PeaceAI.WarOutlook.Losing;
		}

		// Whether the horde took a city of theirs within PressOnTurns. On
		// the replays, hordes that had just taken two or three cities were
		// given up as Losing by their units lost (each city taken counts
		// for PeaceAI.CityLossWeight units), as strong humans' hordes too
		// lose many.
		public static bool JustTookACity(int lastCaptureTurn, int turn) {
			return lastCaptureTurn >= 0 && turn - lastCaptureTurn <= PressOnTurns;
		}

		// Whether a horde gathering on them before the war goes on: not once
		// the plan calls them a friend or they have a protection pact with us.
		// v2: nor once our horde unit has the edge on none of their near
		// cities (they have a better defender, or we lost our horses).
		public static bool StillVictim(Player player, Player enemy) {
			return !Pacts.HasDeal(player, enemy, DealSubType.MutualProtectionPact)
				&& NearestCities(player, enemy).Any(c => HasEdge(player, c));
		}

		// Whether to raze the city we just took: no garrison of at most
		// PreyWar.MaxFlipGarrison would hold its flip chance down
		// (PreyWar.MaxTargetFlipChance), or we haven't the units there to
		// leave as many as it needs (PreyWar.GarrisonAgainstFlip).
		public static bool ShouldRaze(GameData gameData, Player player, Player enemy, City city, int unitsThere) {
			if (!CityInteractions.MayRaze(player, city, gameData)) {
				return false;
			}
			int want = PreyWar.GarrisonAgainstFlip(gameData, player, enemy, city);
			return want == int.MaxValue || PreyWar.FlipChanceIfTaken(gameData, player, enemy, city, Math.Min(unitsThere, PreyWar.MaxFlipGarrison)) > PreyWar.MaxTargetFlipChance;
		}

		// While a horde is on them and we aren't losing, peace only for a
		// city of theirs.
		public static bool RefusesPeace(Player us, Player them, PeaceAI.WarOutlook outlook, TradeOffer theirOffer) {
			return On(us) && WarCampaign.HasHordeCampaignAgainst(us, them) && outlook >= PeaceAI.WarOutlook.Even && theirOffer.cities.Count == 0;
		}

		// The cities that build the horde: our most productive third (at
		// least two), once they have a defender.
		private static readonly ConditionalWeakTable<Player, Tuple<int, HashSet<City>>> builders = new();

		public static HashSet<City> Builders(Player player) {
			int turn = EngineStorage.gameData?.turn ?? 0;
			// Kept for BuilderTurns, so that a city that starts on the horde
			// goes on with it (ranked afresh each turn, the set changed with
			// every shield, and the cities choosing that turn were seldom
			// the builders).
			if (builders.TryGetValue(player, out Tuple<int, HashSet<City>> cached) && turn >= cached.Item1 && turn < cached.Item1 + BuilderTurns
					&& cached.Item2.All(c => c.owner == player)) {
				return cached.Item2;
			}
			int count = Math.Max(2, player.cities.Count / BuilderShareDivisor);
			HashSet<City> result = player.cities.OrderByDescending(c => c.CurrentProductionYield().useful).ThenBy(c => c.id.ToString())
				.Take(count).ToHashSet();
			builders.AddOrUpdate(player, Tuple.Create(turn, result));
			return result;
		}

		// What a city wants the unit for the horde: in a horde builder, the
		// horde unit (and other fast attackers, by their attack) while the
		// horde is short of attackers, and defenders while it is short of
		// escorts.
		public static float ProductionBonus(City city, Player player, UnitPrototype unit, bool cityGuarded) {
			if (!On(player) || !cityGuarded || !WarCampaign.HasHordeCampaign(player) || !Builders(player).Contains(city)) {
				return 0;
			}
			if (IsAttacker(unit) && WarCampaign.WantsAttackers(player)) {
				// The horde unit most, a slower one half as much.
				UnitPrototype best = HordeUnit(player);
				if (best == null) {
					return 0;
				}
				float slow = IsFastAttacker(best) && !IsFastAttacker(unit) ? 0.5f : 1;
				return BuilderBonus * slow * Math.Min(1f, unit.attack / (float)Math.Max(1, best.attack));
			}
			if (unit.IsLandUnit() && !unit.isArmy && unit.defense > unit.attack && !unit.isWorker && !unit.isSettler && WarCampaign.WantsHordeEscort(player)) {
				return EscortBonus;
			}
			return 0;
		}

		// Whether, at war, our settlers may found a city on a site next to
		// the land of the civ a horde of ours is on (razed sites, gaps among
		// the cities we took): within SettleNearRadius of a city of ours.
		public static bool MaySettleNextTo(Player player, Tile site) {
			if (!On(player) || !WarCampaign.HasHordeCampaign(player)) {
				return false;
			}
			foreach (City c in player.cities) {
				if (c.location.continent == site.continent && c.location.DistanceTo(site) <= SettleNearRadius) {
					return true;
				}
			}
			return false;
		}
	}
}
