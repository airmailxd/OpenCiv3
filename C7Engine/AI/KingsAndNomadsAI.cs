using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using Serilog;

namespace C7Engine.AI {
	// How the AI plays Kings & Nomads (see GameSetup.KingsAndNomads.cs; owner's
	// design, 2026-10-23). No civ starts with a city: each is a King and a
	// horde, and an empire is built by taking walled city-states (held by
	// the barbarians) and then rival civs' cities. Losing the King loses the
	// game. The ordinary AI is built around cities (defend them, explore
	// from them, march from them on the enemy, see WarCampaign), so with
	// none it left the horde fortified where it stood: in AI trials before
	// this (AiTrialTest, 8 games of 6 civs and 120 turns on a 70x70 map,
	// every civ AI), no civ took a city on Continents or Pangaea.
	//
	// So here the civ's fighters move as one stack, the horde, which picks
	// the most attractive city it can reach (near, small garrison, and a
	// King inside is worth most of all), marches on it together at the pace
	// of the slowest, waits a little for stragglers, and then storms it with
	// every attack it has in one turn, best odds first, so that the
	// garrison is worn down before it can heal. An emptied city is walked
	// into. Then the city is garrisoned and the horde goes on to the next.
	// Once the civ has cities, a horde that has grown big enough sends out
	// another. Wars with rival civs are fought the same way, and declared
	// when no independent city is left within reach (WarCampaign stands
	// down in this scenario, so the two don't fight over the same units).
	//
	// The King travels with the horde until the civ has a city, guarded by
	// a unit that doesn't attack while the King shares its tile, and never
	// attacks itself. From then on it waits in the safest of our cities,
	// fortified with a guard the rest of the AI leaves alone (see
	// MakeKingAI and GuardsTheKing), for the strongest horde to come and
	// take it along to lead the attack, and back again when it's in
	// danger (see MarchForTheKing). Captured cities build
	// defenders, then settlers, which only they can build (see UnitBonus).
	//
	// In the same trials with this, civs had on average 7.8 cities after
	// 120 turns on Continents and 8.8 on Pangaea, having taken 6.1 and 9.0,
	// the first by turn 24 and 7 (a quarter of the civs on Continents
	// start on islets with nothing to take, which counts as never); about 90%
	// and 80% of the Kings were still alive.
	//
	// Everything here is gated on the scenario's rules (IsActive), so other
	// games play as before.
	public static class KingsAndNomadsAI {
		private static readonly ILogger log = Log.ForContext(typeof(KingsAndNomadsAI));

		// Whether the game is Kings & Nomads, by its rules: settlers only from
		// conquered cities, and fallen civs' cities going independent.
		public static bool IsActive(GameData gameData) {
			return gameData?.rules != null && gameData.rules.SettlersOnlyInConqueredCities && gameData.rules.FallenCitiesGoIndependent;
		}

		// Whether the player's AI plays by this file.
		public static bool PlaysFor(Player player) {
			return player != null && !player.isHuman && !player.IsIndependent && IsActive(EngineStorage.gameData);
		}

		// Whether the barbarian unit is part of an independent city's
		// garrison, which stays in the city. The barbarians' ordinary AI sends
		// units in a city out to raid and explore like any others, and in AI
		// trials the walled city-states had emptied themselves within two or
		// three turns, so that the first horde to arrive walked in (owner's
		// design: they are "walled, garrisoned").
		public static bool HoldsCityState(Player player, MapUnit unit) {
			return player.isCityStates && IsActive(EngineStorage.gameData) && unit.location.cityAtTile?.owner == player
				&& unit.IsLandUnit() && unit.CanDefendOnLand();
		}

		private sealed class Horde {
			// Where the horde's units stand together.
			public Tile stackTile;
			// What the horde is after: a city, or a King in the open.
			public City targetCity;
			public MapUnit targetKing;
			// Turns the horde has waited for stragglers or to heal.
			public int waited;
			// With no target known, the tile it's exploring towards.
			public Tile exploreTo;
			// Cities the horde was too weak to take, and the turn until which
			// it leaves each alone.
			public readonly Dictionary<City, int> avoid = new();
			public readonly List<MapUnit> units = new();
			// Once we have a city, the King's city the horde is going to
			// pick him up from, and the city it's taking him back to (see
			// MarchForTheKing).
			public Tile rendezvous;
			public City retreatTo;
			// Turns in a row the horde with the King stopped short of a tile
			// too dangerous for him.
			public int kingHeld;
			// Turns the horde has waited in the King's city for his guard.
			public int kingWaited;

			public MapUnit King => units.FirstOrDefault(u => u.unitType.isKing);

			public Tile TargetTile => targetCity?.location ?? targetKing?.location;
		}

		private static readonly ConditionalWeakTable<Player, List<Horde>> hordes = new();

		// The units ordered to attack this turn: an attack the engine turns
		// down changes nothing, so each unit is ordered once at most.
		private static readonly HashSet<MapUnit> triedToAttack = new();

		private static int Turn => EngineStorage.gameData?.turn ?? 0;

		// A unit that fights for the horde: a land unit that can attack, but
		// not the King, a leader, a settler, a worker or a siege engine.
		internal static bool IsFighter(MapUnit u) {
			if (!u.IsLandUnit() || u.IsLoaded() || u.unitType.isKing || u.IsLeader() || u.IsScientificLeader()
					|| u.unitType.isSettler || u.unitType.isWorker || u.IsNuclearWeapon()) {
				return false;
			}
			if (u.IsArmy()) {
				return u.PassengerCount() > 0;
			}
			return u.CanAttack() && u.unitType.attack > 0 && !PlayerAI.IsArtillery(u);
		}

		// A fighter better at defending than attacking, like a Spearman: a
		// city's garrison, or the horde's guard.
		private static bool IsDefensive(MapUnit u) {
			return u.CanDefendOnLand() && u.unitType.defense > u.unitType.attack;
		}

		private static bool IsMember(MapUnit u) {
			return u.currentAI is WarCampaign.MemberAI;
		}

		private static double Health(MapUnit u) {
			int max = u.CompositeMaxHitPoints();
			return max > 0 ? (double)u.CompositeHitPoints() / max : 0;
		}

		private static bool IsNeighbor(Tile a, Tile b) {
			foreach (Tile n in a.neighbors.Values) {
				if (n == b) {
					return true;
				}
			}
			return false;
		}

		private static bool HasHostileUnits(Player player, Tile tile) {
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u.owner != player && !player.IsAtPeaceWith(u.owner)) {
					return true;
				}
			}
			return false;
		}

		private static bool HasArmedEnemy(Player player, Tile tile) {
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u.owner != player && !player.IsAtPeaceWith(u.owner) && u.CanDefendOnLand()) {
					return true;
				}
			}
			return false;
		}

		private static bool AtWarWith(Player player, Player other) {
			return other.IsIndependent || (player.playerRelationships.TryGetValue(other.id, out PlayerRelationship r) && r.AtWar());
		}

		// The player's King, or null.
		private static MapUnit KingOf(Player player) {
			foreach (MapUnit u in player.units) {
				if (u.unitType.isKing) {
					return u;
				}
			}
			return null;
		}

		// The city the player's King is in, or null.
		internal static City KingsCity(Player player) {
			MapUnit king = KingOf(player);
			return king != null && king.location.cityAtTile?.owner == player ? king.location.cityAtTile : null;
		}

		public static async Task PlayTurn(Player player, GameData gameData) {
			if (!PlaysFor(player)) {
				return;
			}
			AIFogOfWar.Refresh(player);
			if (!hordes.TryGetValue(player, out List<Horde> list)) {
				list = new List<Horde>();
				hordes.AddOrUpdate(player, list);
			}
			triedToAttack.Clear();
			bool homeless = player.cities.Count == 0;
			foreach (Horde horde in list) {
				horde.units.RemoveAll(u => !PlayerAI.IsStillOurs(u, player) || !IsMember(u));
				// With a city, workers go to work, and the badly hurt go back
				// to heal (see RestAI); the King goes home only when the
				// horde takes him (see MarchForTheKing).
				if (!homeless) {
					foreach (MapUnit u in horde.units.Where(u => u.unitType.isWorker
							|| (IsFighter(u) && !u.IsArmy() && Health(u) < HurtHealth)).ToList()) {
						Release(horde, u);
					}
				}
			}
			MaybeMoveKing(player);
			foreach (Horde horde in list) {
				GuardKing(player, horde);
			}
			Recruit(player, list, homeless);
			if (!homeless) {
				PlanKingsMarch(player, list);
			}
			foreach (Horde horde in list.ToList()) {
				if (!horde.units.Any(IsFighter)) {
					foreach (MapUnit u in horde.units.ToList()) {
						Release(horde, u);
					}
					list.Remove(horde);
					continue;
				}
				await PlayHorde(player, gameData, list, horde, homeless);
			}
		}

		private static async Task PlayHorde(Player player, GameData gameData, List<Horde> list, Horde horde, bool homeless) {
			PickStackTile(player, horde);
			if (!homeless) {
				if (await MarchForTheKing(player, horde)) {
					FortifyStack(player, horde);
					return;
				}
			}
			ChooseTarget(player, gameData, list, horde, homeless);
			if (!homeless && horde.King != null && horde.TargetTile == null) {
				// With nothing to attack, the King goes home.
				StartRetreat(player, horde, "has nothing to attack");
				if (horde.retreatTo != null) {
					await MarchForTheKing(player, horde);
					FortifyStack(player, horde);
					return;
				}
			}
			if (horde.TargetTile == null) {
				if (!await Explore(player, horde) && !homeless) {
					// Nothing to take and nowhere to look: the fighters go back
					// to the ordinary AI (defending our cities) for now.
					foreach (MapUnit u in horde.units.ToList()) {
						Release(horde, u);
					}
				}
				FortifyStack(player, horde);
				return;
			}
			await StrikeExposedKings(player, horde);
			await Advance(player, horde, homeless);
			FortifyStack(player, horde);
		}

		private static void Release(Horde horde, MapUnit u) {
			horde.units.Remove(u);
			if (IsMember(u)) {
				u.currentAI = null;
			}
			u.Wake();
		}

		private static void Join(Horde horde, MapUnit u) {
			horde.units.Add(u);
			u.currentAI = new WarCampaign.MemberAI();
			u.Wake();
		}

		// HEURISTIC: how many defenders each of our cities keeps out of the
		// horde: one, and one more where an enemy could strike next turn.
		// Where the King is, two more, and another for every five cities we
		// hold (up to three): a bigger empire can spare them, and draws
		// bigger hordes. In AI trials Kings in cities of two or three
		// defenders were lost to rival hordes of a dozen.
		internal static int GarrisonWanted(Player player, City city, AIThreat threat) {
			// A civ with one city that no enemy can reach keeps the King two
			// guards there, and the rest of the horde moves on (see
			// MinKingHordeFightersFor).
			if (player.cities.Count <= 1 && !threat.EnemyCanReach(city.location)) {
				return city.location.unitsOnTile.Any(u => u.owner == player && IsResidentKing(u)) ? 2 : 1;
			}
			int want = 1;
			if (threat.EnemyCanReach(city.location)) {
				++want;
			}
			if (city.location.unitsOnTile.Any(u => u.owner == player && IsResidentKing(u))) {
				want += 2 + Math.Min(3, player.cities.Count / 5);
			}
			return want;
		}

		// A defender of the city the King is in, in the order they guard it:
		// the best defenders first.
		private static List<MapUnit> KingsGuards(Player player, Tile tile) {
			return tile.unitsOnTile.Where(u => u.owner == player && u.CanDefendOnLand() && !IsMember(u) && !u.unitType.isKing
					&& !u.unitType.isWorker && !u.unitType.isSettler && !u.IsLeader())
				.OrderByDescending(u => u.unitType.defense).ThenByDescending(u => u.hitPointsRemaining).ThenBy(u => u.id.ToString()).ToList();
		}

		// Whether the unit is one of the defenders the King's city keeps (see
		// GarrisonWanted), which stay there whatever else the AI would have
		// them do. In AI trials Kings were lost in cities whose defenders
		// had gone exploring or escorting, or to defend other cities.
		public static bool GuardsTheKing(MapUnit unit, Player player) {
			if (!PlaysFor(player) || !unit.CanDefendOnLand() || unit.unitType.isKing) {
				return false;
			}
			City city = unit.location.cityAtTile;
			if (city == null || city.owner != player || !city.location.unitsOnTile.Any(u => u.owner == player && IsResidentKing(u))) {
				return false;
			}
			int rank = KingsGuards(player, city.location).IndexOf(unit);
			return rank >= 0 && rank < GarrisonWanted(player, city, AIThreat.Assess(player));
		}

		// The King's city gets back defenders it is short of from the horde's
		// units in it.
		private static void GuardKing(Player player, Horde horde) {
			City city = KingsCity(player);
			if (city == null || IsMember(KingOf(player))) {
				return;
			}
			int want = GarrisonWanted(player, city, AIThreat.Assess(player));
			int have = KingsGuards(player, city.location).Count;
			foreach (MapUnit u in horde.units.Where(u => u.location == city.location && u.CanDefendOnLand() && !u.unitType.isKing
					&& !u.unitType.isWorker && !u.IsArmy()).OrderByDescending(u => u.unitType.defense).ToList()) {
				if (have >= want) {
					break;
				}
				horde.units.Remove(u);
				u.currentAI = new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(u, player));
				u.Fortify();
				++have;
			}
		}

		// HEURISTIC: once we have a city, the most fighters one horde takes,
		// and the most of them that defend better than they attack (a
		// guard for the stack; the rest of those hold our cities). Beyond
		// that, a new horde sets out once there are enough spare attackers.
		// In AI trials a single horde grew to a hundred units, which took
		// one city at a time while our cities were held by one defender each.
		private const int MaxHordeFighters = 16;
		private const int MaxHordeGuards = 2;
		private const int NewHordeFighters = 6;

		private static int ContinentOf(Horde horde) {
			return horde.stackTile?.continent ?? horde.units.FirstOrDefault()?.location.continent ?? -1;
		}

		private static bool HasRoom(Horde horde, MapUnit u, bool homeless) {
			if (homeless) {
				return true;
			}
			int fighters = 0, guards = 0;
			foreach (MapUnit m in horde.units) {
				if (IsFighter(m)) {
					++fighters;
					if (IsDefensive(m)) {
						++guards;
					}
				}
			}
			int maxGuards = horde.King != null ? MaxHordeGuards + KingsHordeGuards : MaxHordeGuards;
			return fighters < MaxHordeFighters && (!IsDefensive(u) || guards < maxGuards);
		}

		// Takes into the hordes the fighters our cities can spare and those in
		// the field, each into the nearest horde on its landmass with room;
		// while we have no city, the King and workers too, which are safest
		// with the horde.
		private static void Recruit(Player player, List<Horde> list, bool homeless) {
			if (list.Count == 0) {
				list.Add(new Horde());
			}
			AIThreat threat = AIThreat.Assess(player);
			int kingContinent = KingOf(player)?.location.continent ?? -1;
			Dictionary<City, int> garrisons = new();
			foreach (City city in player.cities) {
				garrisons[city] = city.location.unitsOnTile.Count(u => u.owner == player && u.CanDefendOnLand() && !IsMember(u)
					&& !u.unitType.isKing && !u.unitType.isWorker && !u.unitType.isSettler);
			}
			List<MapUnit> spare = new();
			// Attackers leave a city first, so its defenders stay.
			foreach (MapUnit u in player.units.OrderBy(u => IsDefensive(u) ? 1 : 0).ToList()) {
				if (IsMember(u) || !u.IsLandUnit() || u.IsLoaded() || u.IsLockedInArmy()
						|| u.currentAI is EscortAI || u.currentAI is FerryAI || u.currentAI is SettlerAI) {
					continue;
				}
				Horde horde;
				if (homeless) {
					// One horde, the King's.
					horde = list[0];
					int continent = ContinentOf(horde);
					if (continent < 0) {
						continent = kingContinent;
					}
					if (continent >= 0 && u.location.continent != continent) {
						continue;
					}
					if (u.unitType.isKing || u.unitType.isWorker) {
						Join(horde, u);
						continue;
					}
				} else {
					horde = null;
				}
				if (!IsFighter(u)) {
					continue;
				}
				City city = u.location.cityAtTile;
				// Once we have a city, the hurt heal before they join.
				if (!homeless && Health(u) < ReadyHealth) {
					continue;
				}
				if (city != null && city.owner == player) {
					if (u.CanDefendOnLand() && garrisons[city] <= GarrisonWanted(player, city, threat)) {
						continue;
					}
				} else if (IsDefensive(u) && u.currentAI is DefenderAI d && d.data.destination?.cityAtTile?.owner == player) {
					// A defender on its way to one of our cities goes there.
					continue;
				}
				horde ??= list.Where(h => ContinentOf(h) == u.location.continent && HasRoom(h, u, homeless))
					.OrderBy(h => h.stackTile?.DistanceTo(u.location) ?? 0).FirstOrDefault();
				if (horde == null) {
					spare.Add(u);
					continue;
				}
				if (city != null && city.owner == player && u.CanDefendOnLand()) {
					--garrisons[city];
				}
				Join(horde, u);
			}
			// Enough spare attackers on a landmass set out as a new horde, with
			// a guard or two.
			foreach (IGrouping<int, MapUnit> group in spare.GroupBy(u => u.location.continent)) {
				List<MapUnit> attackers = group.Where(u => !IsDefensive(u)).ToList();
				if (attackers.Count < NewHordeFighters) {
					continue;
				}
				Horde horde = new();
				list.Add(horde);
				foreach (MapUnit u in attackers.Take(MaxHordeFighters - MaxHordeGuards).Concat(group.Where(IsDefensive).Take(MaxHordeGuards))) {
					City city = u.location.cityAtTile;
					if (city != null && city.owner == player && u.CanDefendOnLand()) {
						if (garrisons[city] <= GarrisonWanted(player, city, threat)) {
							continue;
						}
						--garrisons[city];
					}
					Join(horde, u);
				}
				log.Information("{Player} sends out another horde, of {Count}", player, horde.units.Count);
			}
		}

		// HEURISTIC: the share of its hit points a unit in one of our cities
		// needs before it leaves to join the horde.
		private const double ReadyHealth = 0.7;
		// HEURISTIC: once we have a city, a member of the horde this hurt
		// leaves it to heal.
		private const double HurtHealth = 0.5;

		// The horde's tile: where it stood, if any of it is still there, and
		// otherwise where most of its fighters are (the King's tile while we
		// have no city, so the King is never left alone).
		private static void PickStackTile(Player player, Horde horde) {
			if (horde.stackTile != null && horde.units.Any(u => u.location == horde.stackTile && IsFighter(u))) {
				return;
			}
			MapUnit king = horde.units.FirstOrDefault(u => u.unitType.isKing);
			if (king != null) {
				horde.stackTile = king.location;
				return;
			}
			horde.stackTile = horde.units.Where(IsFighter).GroupBy(u => u.location)
				.OrderByDescending(g => g.Count()).ThenBy(g => g.Key.DistanceTo(horde.TargetTile ?? g.Key)).First().Key;
		}

		// The sum of the attack or defence, whichever is greater, of the
		// player's land fighters.
		private static double ArmyStrength(Player player) {
			return player.units.Where(u => u.IsLandUnit() && IsFighter(u)).Sum(u => Math.Max(u.unitType.attack, u.unitType.defense));
		}

		// What a war with the rival would win us, in points: knocking
		// down the civ with the most cities, ahead of us; cutting its trade
		// with the city-states, the more so with those in our reach;
		// taking resources we lack; and the chance of its King away from
		// its cities, or an army weaker than ours. Less if its army is the
		// stronger, for each of our own trade routes with a city-state near
		// its cities, which the war would put at risk, and for each war
		// we're already in; and the gains count half again for a civ at
		// peace whose land borders the rival's. In AI trials (40 games of 6
		// civs and 120 turns, half the civs weighing their own wars and
		// bordering rivals so) these civs fought 0.7 fewer wars at once on
		// Pangaea and 0.3 more on Continents, with no significant change in
		// cities; counting a rival's other wars in its favour too cost them
		// 17 beakers a turn on Pangaea for no more cities.
		//
		// Owner's design, 2026-10-25: wars where there's good reason for
		// them, for goals like these. Before, a horde only declared war with
		// no city-state left in its reach, which hardly ever happened: in AI
		// trials (20 games of 6 civs and 120 turns, every civ AI) there were
		// no wars at all on Pangaea. In AI trials (40 games, half the civs
		// choosing wars this way) these civs fought 2.1 wars each on Pangaea
		// and took 1.1 more cities; on Continents, where a war is most often
		// with the one neighbour, 0.3 more wars, and no significant change
		// in cities. Choosing any rival city a horde could take, without
		// these goals, won 1.8 cities on Pangaea but lost 0.6 on Continents.
		internal static double WarIncentive(Player player, GameData gameData, Player rival, int continent) {
			double gain = 0, cost = 0;
			List<Player> civs = gameData.players.Where(p => !p.IsIndependent && !p.defeated).ToList();
			if (rival.cities.Count >= civs.Max(p => p.cities.Count) && rival.cities.Count > player.cities.Count) {
				gain += LeaderIncentive;
			}
			foreach (CityStateTrade.Route route in CityStateTrade.Routes(gameData, rival)) {
				gain += TradeIncentive;
				if (route.cityState.location.continent == continent
						&& player.cities.Any(c => c.location.DistanceTo(route.cityState.location) <= NearCityStateDistance)) {
					gain += TradeIncentive;
				}
			}
			HashSet<Resource> have = player.cities.SelectMany(c => c.location.GetTilesWithinRankDistance(1)).Where(t => t != Tile.NONE && t.owningCity?.owner == player)
				.Select(t => t.Resource).ToHashSet();
			int lacking = gameData.map.tiles.Where(t => t.owningCity?.owner == rival && t.Resource != Resource.NONE
					&& t.Resource.Category is ResourceCategory.LUXURY or ResourceCategory.STRATEGIC && player.KnowsAboutResource(t.Resource) && !have.Contains(t.Resource))
				.Select(t => t.Resource).Distinct().Count();
			gain += Math.Min(MaxResourceIncentive, lacking * ResourceIncentive);
			if (KingOf(rival) is MapUnit king && Tile.IsTileValid(king.location) && king.location.cityAtTile?.owner != rival) {
				gain += OpportunityIncentive;
			}
			double ratio = ArmyStrength(player) / Math.Max(1, ArmyStrength(rival));
			if (ratio >= 1.5) {
				gain += OpportunityIncentive;
			} else if (ratio < 0.75) {
				cost += WeakerArmyDisincentive;
			}
			// Our own trade with city-states near them is at risk.
			foreach (CityStateTrade.Route route in CityStateTrade.Routes(gameData, player)) {
				if (rival.cities.Any(c => c.location.DistanceTo(route.cityState.location) <= SpareRivalDistance)) {
					cost += OwnTradeAtRisk;
				}
			}
			int ourWars = civs.Count(p => p != rival && p != player && PlayerRelationship.AtWar(player, p));
			// Our King leads one war at a time, so each war we're already in
			// counts against another (owner's design, 2026-10-25).
			cost += ourWars * OwnWarDisincentive;
			// A civ at peace is an opportunist, looking for a war on its
			// borders (owner's design, 2026-10-25).
			if (ourWars == 0 && Borders(gameData, player, rival)) {
				gain *= OpportunistMultiplier;
			}
			return gain - cost;
		}

		// Whether the two players' lands touch.
		private static bool Borders(GameData gameData, Player a, Player b) {
			foreach (Tile t in gameData.map.tiles) {
				if (t.owningCity?.owner == a && t.neighbors.Values.Any(n => n != Tile.NONE && n.owningCity?.owner == b)) {
					return true;
				}
			}
			return false;
		}

		private const double OpportunistMultiplier = 1.5;

		// HEURISTIC: WarIncentive's points.
		// The points a war must be worth for a civ to declare it. The
		// owner wants warring kingdoms (2026-10-25), measured by wars
		// started rather than by how the warring civs fare against the rest
		// (wars cost both sides). In AI trials (20 games of 6 civs and 120
		// turns, every civ AI), at 4 civs started 1.6 wars each on Pangaea
		// and 1.0 on Continents; at 2, 2.2 and 1.5, taking a little more
		// from each other, with as many civs left; at 1, 2.6 and 1.8, at
		// some cost in research on Pangaea.
		internal const double WarIncentiveNeeded = 2;
		private const double OwnTradeAtRisk = 1.5;
		private const double OwnWarDisincentive = 2;
		private const double LeaderIncentive = 2;
		private const double TradeIncentive = 1;
		private const int NearCityStateDistance = 10;
		private const double ResourceIncentive = 0.5;
		private const double MaxResourceIncentive = 2;
		private const double OpportunityIncentive = 1;
		private const double WeakerArmyDisincentive = 3;

		// A war for a goal (see WarIncentive) has met it once we've taken a
		// city from them, or after WarGoalTurns: we then make peace if they
		// will (see PeaceAI.BuildPeaceOffer).
		public static bool WarGoalMet(GameData gameData, Player us, Player them) {
			if (!PlaysFor(us)) {
				return false;
			}
			int turns = PeaceAI.WarTurns(gameData, us, them);
			int start = gameData.turn - turns;
			return turns >= WarGoalTurns || us.cities.Any(c => c.capturedFrom == them.civilization && c.ownerChangedTurn >= start);
		}

		// HEURISTIC: how long a war for a goal goes on, at most.
		private const int WarGoalTurns = 20;

		// HEURISTIC: the fewest fighters a horde declares war with.
		private const int MinWarFighters = 4;

		// HEURISTIC: what taking a city is worth against its garrison and the
		// way there: its size (a captured city builds settlers, so even a
		// small one is worth a lot), far more with a rival King inside
		// (killing it brings down that civ), over the defenders' hit points
		// as our units would find them and the distance; and what taking a
		// rival civ's city does to that civ. A
		// city-state's defenders count for more the more city-states we've
		// taken (see CityStates.StrengthAgainstCityStates); a rival city's
		// for twice as much with its King in or next to it (his +100%). A
		// rival city is worth more for each city-state it trades with (its
		// owner loses that trade), if its owner holds the most cities of any
		// civ, and if it's cut off from its owner's capital.
		private static double TargetScore(Player player, GameData gameData, City city, Tile from) {
			double value = 4 + city.residents.Count;
			if (city.location.unitsOnTile.Any(u => u.unitType.isKing && u.owner != player)) {
				value += KingValue;
			}
			double defenders = AIFogOfWar.SeesUnitsOn(player, city.location)
				? WarCampaign.DefenderHitPoints(player, city.location) / 3.0
				: Math.Max(2, 1 + city.residents.Count / 3);
			Player owner = city.owner;
			if (owner.isCityStates) {
				defenders /= CityStates.StrengthAgainstCityStates(player.cityStatesCaptured);
			} else if (!owner.IsIndependent) {
				TradeNetwork network = gameData.GetTradeNetwork();
				value += RivalTradeValue * network.CityStatesTradingWith(owner, city).Count;
				int most = gameData.players.Where(p => !p.IsIndependent && !p.defeated).Max(p => p.cities.Count);
				if (owner.cities.Count >= most) {
					value += LeaderValue;
				}
				if (!network.ConnectedToCapital(owner, city)) {
					value += CutOffValue;
				}
				if (KingOf(owner) is MapUnit king && Tile.IsTileValid(king.location) && king.location.DistanceTo(city.location) <= 1) {
					defenders *= 2;
				}
			}
			int distance = from.DistanceTo(city.location);
			return value / (1 + defenders) / (1 + distance / 4.0);
		}

		// HEURISTIC: TargetScore's worth, in citizens, of a rival city's
		// trade with each city-state, of hurting the civ with the most
		// cities, and of a city cut off from its capital.
		private const double RivalTradeValue = 2;
		private const double LeaderValue = 3;
		private const double CutOffValue = 2;

		// HEURISTIC: what a rival King is worth as a target, as so many
		// citizens: killing it eliminates that civ, and its cities go
		// independent, for anyone to take.
		private const double KingValue = 16;

		// HEURISTIC: how far (in tiles) the horde goes after a rival King
		// in the open.
		private const int KingHuntRange = 6;

		// HEURISTIC: how much better another target must score for the horde
		// to turn from the one it's after.
		private const double SwitchMargin = 1.5;

		private static void ChooseTarget(Player player, GameData gameData, List<Horde> list, Horde horde, bool homeless) {
			Tile from = horde.stackTile;
			int continent = from.continent;
			MapUnit lead = horde.units.Where(IsFighter).OrderBy(u => u.MaxMovementPoints()).First();

			// Each horde goes after a city of its own. A city-state we trade
			// with is spared while no rival city is near enough to take it
			// in our stead (see SparedPartners).
			HashSet<City> others = list.Where(h => h != horde && h.targetCity != null).Select(h => h.targetCity).ToHashSet();
			HashSet<City> spared = SparedPartners(player, gameData);
			bool Valid(City c) => c != null && gameData.cities.Contains(c) && c.owner != player && AtWarWith(player, c.owner)
				&& !(horde.avoid.TryGetValue(c, out int until) && Turn < until) && !others.Contains(c) && !spared.Contains(c)
				&& c.location.continent == continent && AIFogOfWar.KnowsTile(player, c.location);
			bool ValidKing(MapUnit k) => k != null && k.owner != player && !k.owner.defeated && k.owner.units.Contains(k)
				&& AtWarWith(player, k.owner) && k.location.continent == continent && k.location.cityAtTile == null
				&& AIFogOfWar.SeesUnitsOn(player, k.location) && k.IsSeenBy(player) && k.location.DistanceTo(from) <= KingHuntRange;

			if (!Valid(horde.targetCity)) {
				horde.targetCity = null;
			}
			if (!ValidKing(horde.targetKing)) {
				horde.targetKing = null;
			}

			// A rival King in the open, near enough to catch, comes first.
			MapUnit king = null;
			double kingScore = 0;
			foreach (Player p in gameData.players) {
				if (p == player || p.IsIndependent || p.defeated) {
					continue;
				}
				MapUnit k = KingOf(p);
				if (!ValidKing(k)) {
					continue;
				}
				double score = KingValue / (1 + WarCampaign.DefenderHitPoints(player, k.location) / 3.0) / (1 + from.DistanceTo(k.location) / 4.0);
				if (score > kingScore) {
					king = k;
					kingScore = score;
				}
			}
			if (king != null) {
				if (horde.targetKing != king) {
					log.Information("{Player}'s horde goes after the {Enemy} King at {Tile}", player, king.owner, king.location);
					horde.waited = 0;
				}
				horde.targetKing = king;
				horde.targetCity = null;
				return;
			}
			horde.targetKing = null;

			City best = null;
			double bestScore = 0;
			// A rival civ's city is weighed against the city-states, as one
			// we'd declare war to take (see TargetScore), if a war with that
			// civ would be worth it (see WarIncentive).
			Dictionary<Player, double> incentive = new();
			bool Worth(Player rival) => (incentive.TryGetValue(rival, out double w) ? w : incentive[rival] = WarIncentive(player, gameData, rival, continent)) >= WarIncentiveNeeded;
			bool MayDeclareFor(City c) => c != null && gameData.cities.Contains(c) && !c.owner.IsIndependent && c.owner != player
				&& !c.owner.defeated && !AtWarWith(player, c.owner) && player.playerRelationships.ContainsKey(c.owner.id)
				&& !gameData.AreInLockedPeace(player, c.owner) && horde.units.Count(IsFighter) >= MinWarFighters
				&& !(horde.avoid.TryGetValue(c, out int until) && Turn < until) && !others.Contains(c)
				&& c.location.continent == continent && AIFogOfWar.KnowsTile(player, c.location) && Worth(c.owner);
			double Score(City c) => TargetScore(player, gameData, c, from);
			List<City> candidates = gameData.cities.Where(c => Valid(c) || MayDeclareFor(c)).OrderByDescending(Score).ToList();
			// The best that can be reached by land, scored as the crow flies.
			foreach (City c in candidates.Take(6)) {
				TilePath path = PathingAlgorithmChooser.GetAlgorithm(lead).PathFrom(from, c.location, lead);
				if (from != c.location && !IsNeighbor(from, c.location) && (path == null || path.PathLength() <= 0)) {
					continue;
				}
				best = c;
				bestScore = Score(c);
				break;
			}
			if (horde.targetCity != null && best != null && best != horde.targetCity
					&& bestScore < SwitchMargin * Score(horde.targetCity)) {
				return;
			}
			if (best != null && !AtWarWith(player, best.owner)) {
				player.DeclareWarOn(best.owner, gameData.turn);
				MsgWarDeclaration.Announce(player, best.owner);
				log.Information("{Player} declares war on {Enemy} to take {City}", player, best.owner, best.name);
				if (!AtWarWith(player, best.owner)) {
					best = null;
				}
			}
			if (best == null && horde.targetCity == null) {
				best = MaybeDeclareWar(player, gameData, horde);
			}
			if (best != null && best != horde.targetCity) {
				log.Information("{Player}'s horde of {Count} marches on {City} ({Owner})", player, horde.units.Count, best.name, best.owner);
				horde.targetCity = best;
				horde.waited = 0;
			}
		}

		// HEURISTIC: a rival city this near a city-state we trade with could
		// take it in our stead, so we don't spare it.
		internal const int SpareRivalDistance = 8;

		// The city-states we trade with that our hordes leave be: those no
		// rival city is within SpareRivalDistance of. A partner pays well,
		// the more so the bigger it grows (see CityStateTrade), and a
		// city-state taken is cut down and weakens us against the rest (see
		// CityStates). In AI trials (40 games of 6 civs and 120 turns, half
		// the civs sparing them) the sparing civs got 3.4 more beakers a
		// turn on Continents (+95 gold of trade over the game) at no cost in
		// cities, and were no worse on Pangaea. Sparing every partner did
		// better on Continents (+13 beakers, +0.6 cities) but cost 1.1
		// cities on Pangaea, where rivals took the spared city-states.
		internal static HashSet<City> SparedPartners(Player player, GameData gameData) {
			return gameData.GetTradeNetwork().ConnectedCityStates(player)
				.Where(c => !gameData.players.Any(p => p != player && !p.IsIndependent && !p.defeated
					&& p.cities.Any(o => o.location.DistanceTo(c.location) <= SpareRivalDistance)))
				.ToHashSet();
		}

		// With no independent city left within reach, a civ picks a fight
		// with the rival whose city is nearest the horde, if it may declare
		// war on them: in this scenario an empire only grows by conquest.
		// Returns the city to march on, or null.
		private static City MaybeDeclareWar(Player player, GameData gameData, Horde horde) {
			Tile from = horde.stackTile;
			City nearest = null;
			foreach (City c in gameData.cities) {
				Player owner = c.owner;
				if (owner == player || owner.IsIndependent || owner.defeated || c.location.continent != from.continent
						|| !AIFogOfWar.KnowsTile(player, c.location) || AtWarWith(player, owner)
						|| !player.playerRelationships.ContainsKey(owner.id) || gameData.AreInLockedPeace(player, owner)) {
					continue;
				}
				if (nearest == null || from.DistanceTo(c.location) < from.DistanceTo(nearest.location)) {
					nearest = c;
				}
			}
			// HEURISTIC: only with a horde of at least 4 fighters, enough to
			// take a small city.
			if (nearest == null || horde.units.Count(IsFighter) < 4) {
				return null;
			}
			player.DeclareWarOn(nearest.owner, gameData.turn);
			MsgWarDeclaration.Announce(player, nearest.owner);
			log.Information("{Player} declares war on {Enemy} to take {City}", player, nearest.owner, nearest.name);
			return AtWarWith(player, nearest.owner) ? nearest : null;
		}

		// With no target known, the horde makes for the nearest land it
		// hasn't explored. Returns false if there is none.
		private static async Task<bool> Explore(Player player, Horde horde) {
			Tile from = horde.stackTile;
			TileKnowledge knowledge = player.tileKnowledge;
			if (horde.exploreTo == null || horde.exploreTo == from
					|| (knowledge.isTileKnown(horde.exploreTo) && !knowledge.borderTiles.Contains(horde.exploreTo))) {
				List<Tile> frontier = knowledge.borderTiles.Where(t => t.IsLand() && t.continent == from.continent)
					.OrderBy(t => t.DistanceTo(from)).Take(20).ToList();
				MapUnit lead = horde.units.Where(IsFighter).OrderBy(u => u.MaxMovementPoints()).First();
				int i = frontier.Count == 0 ? -1 : PathingAlgorithmChooser.GetAlgorithm(lead).FindFirstReachable(from, frontier, lead, out _);
				horde.exploreTo = i < 0 ? null : frontier[i];
			}
			if (horde.exploreTo == null) {
				return false;
			}
			await GatherStragglers(player, horde);
			List<MapUnit> stack = StackUnits(player, horde);
			if (stack.Count > 0) {
				TilePath path = PathingAlgorithmChooser.GetAlgorithm(stack[0]).PathFrom(from, horde.exploreTo, stack[0]);
				if (path != null && path.PathLength() > 0) {
					await MoveStack(player, horde, stack, path, stopNextTo: null);
				} else {
					horde.exploreTo = null;
				}
			}
			return true;
		}

		// The horde's units on its tile, slowest first.
		private static List<MapUnit> StackUnits(Player player, Horde horde) {
			return horde.units.Where(u => PlayerAI.IsStillOurs(u, player) && u.location == horde.stackTile)
				.OrderBy(u => u.MaxMovementPoints()).ToList();
		}

		// Members away from the horde's tile go to it.
		private static async Task GatherStragglers(Player player, Horde horde) {
			foreach (MapUnit u in horde.units.ToList()) {
				if (PlayerAI.IsStillOurs(u, player) && u.location != horde.stackTile) {
					await MoveTowards(player, u, horde.stackTile);
				}
			}
		}

		// The unit that guards the King on its tile: the best defender there
		// other than the King. It doesn't attack while it guards (see
		// MayAttack), so the King is never the one left to defend the tile.
		private static MapUnit Guard(Player player, Tile tile) {
			MapUnit king = tile.unitsOnTile.FirstOrDefault(u => u.owner == player && u.unitType.isKing);
			if (king == null) {
				return null;
			}
			return tile.unitsOnTile.Where(u => u.owner == player && u != king && u.CanDefendOnLand() && !u.unitType.isWorker && !u.unitType.isSettler)
				.OrderByDescending(u => u.unitType.defense * Math.Max(1, u.hitPointsRemaining)).ThenBy(u => u.unitType.attack).FirstOrDefault();
		}

		// HEURISTIC: in an all-out assault, the attacks not worth making.
		private const double MinAssaultOdds = 0.1;
		// HEURISTIC: short of the force to take the city, an attack still
		// made: it's very likely to win.
		private const double SureOdds = 0.7;
		// HEURISTIC: how many more hits than the garrison has left the horde
		// must expect to deal before it storms a city.
		private const double AssaultMargin = 1.2;
		// HEURISTIC: how long the horde waits outside a city for stragglers
		// or for the hurt to heal before it attacks with what it has.
		private const int MaxWaitTurns = 3;
		// HEURISTIC: a King in the open is attacked by any fighter within
		// reach with at least these odds: winning brings down its civ.
		private const double KingStrikeOdds = 0.35;

		// Attacks the tile with the able unit among those given with the best
		// odds against its defender, if they're at least minOdds. Returns
		// whether it attacked.
		private static async Task<bool> AttackIfGoodOdds(Player player, IEnumerable<MapUnit> units, Tile tile, double minOdds) {
			MapUnit best = null;
			double bestOdds = minOdds;
			foreach (MapUnit u in units) {
				if (!MayAttack(player, u, tile)) {
					continue;
				}
				MapUnit defender = tile.FindTopDefender(u);
				double odds = MapUnit.IsMapUnitValid(defender) ? CombatAI.EstimateChanceToWin(u, defender) : 1;
				if (odds >= bestOdds) {
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

		// Whether the unit may attack the tile next to it now. The King never
		// attacks, and nor does its guard.
		private static bool MayAttack(Player player, MapUnit u, Tile tile) {
			return PlayerAI.IsStillOurs(u, player) && IsFighter(u) && u.movementPoints.canMove && u.CanAttackAgainThisTurn()
				&& !triedToAttack.Contains(u) && IsNeighbor(u.location, tile) && u != Guard(player, u.location);
		}

		// The hits the units able to attack the tile now can expect to deal
		// its defenders.
		private static double ForceAgainst(Player player, IEnumerable<MapUnit> units, Tile tile, bool ableNow) {
			double hits = 0;
			foreach (MapUnit u in units) {
				if (!PlayerAI.IsStillOurs(u, player) || !IsFighter(u) || (ableNow && !MayAttack(player, u, tile))) {
					continue;
				}
				MapUnit defender = tile.FindTopDefender(u);
				hits += MapUnit.IsMapUnitValid(defender) ? WarCampaign.ExpectedHits(u, defender) : 100;
			}
			return hits;
		}

		// A rival King in the open that any of our fighters can reach is
		// attacked, by the horde or not: killing it brings down its civ.
		private static async Task StrikeExposedKings(Player player, Horde horde) {
			if (horde.targetKing == null) {
				return;
			}
			Tile tile = horde.targetKing.location;
			while (horde.targetKing.owner.units.Contains(horde.targetKing) && horde.targetKing.location == tile
					&& await AttackIfGoodOdds(player, horde.units, tile, KingStrikeOdds)) {
			}
		}

		private static async Task Advance(Player player, Horde horde, bool homeless) {
			Tile target = horde.TargetTile;
			// Stragglers catch up first, so that the horde can wait for them.
			await GatherStragglers(player, horde);
			List<MapUnit> stack = StackUnits(player, horde);
			if (stack.Count == 0) {
				return;
			}

			if (!IsNeighbor(horde.stackTile, target)) {
				// The horde sets out once it's together, or has waited long
				// enough for the rest.
				bool together = horde.units.Where(IsFighter).All(u => u.location == horde.stackTile);
				bool near = horde.units.Where(IsFighter).All(u => u.location.DistanceTo(horde.stackTile) <= 2);
				if (!together && near && horde.waited < MaxWaitTurns) {
					++horde.waited;
					return;
				}
				horde.waited = 0;
				TilePath path = PathingAlgorithmChooser.GetAlgorithm(stack[0]).PathFrom(horde.stackTile, target, stack[0]);
				if (path == null || path.PathLength() <= 0) {
					horde.targetCity = null;
					horde.targetKing = null;
					return;
				}
				await MoveStack(player, horde, stack, path, stopNextTo: target);
				stack = StackUnits(player, horde);
				if (!IsNeighbor(horde.stackTile, target)) {
					return;
				}
			}

			if (horde.targetCity != null) {
				await Storm(player, horde, stack, homeless);
			} else {
				while (horde.targetKing != null && horde.targetKing.owner.units.Contains(horde.targetKing)
						&& await AttackIfGoodOdds(player, stack, target, MinAssaultOdds)) {
				}
			}
		}

		// Attacks the city next to the horde: all out if together it can
		// expect to wear down every defender, and otherwise only at very
		// good odds while stragglers and the hurt catch up, for a few turns
		// at most. A horde with no city of its own has nowhere to heal or
		// wait (the garrison heals faster in its city than the horde does
		// outside it), so then it attacks with what it has.
		private static async Task Storm(Player player, Horde horde, List<MapUnit> stack, bool homeless) {
			City city = horde.targetCity;
			Tile tile = city.location;
			Player enemy = city.owner;
			int defenderHitPoints = WarCampaign.DefenderHitPoints(player, tile);
			double force = ForceAgainst(player, stack, tile, ableNow: true);
			bool coming = horde.units.Any(u => IsFighter(u) && PlayerAI.IsStillOurs(u, player) && u.location != horde.stackTile
				&& u.location.DistanceTo(tile) <= 3);
			bool hurt = stack.Any(u => IsFighter(u) && Health(u) < ReadyHealth);
			bool enough = force >= AssaultMargin * defenderHitPoints;
			// Short of the force, a horde with a home of its own only makes
			// attacks very likely to win, while it waits for the rest; one
			// without goes all out once there's no one to wait for.
			bool allOut = enough || (homeless && (horde.waited >= MaxWaitTurns || (!coming && !hurt)));
			double minOdds = allOut ? MinAssaultOdds : SureOdds;
			while (HasArmedEnemy(player, tile) && await AttackIfGoodOdds(player, stack, tile, minOdds)) {
				stack.RemoveAll(u => !PlayerAI.IsStillOurs(u, player));
			}

			// An emptied city is taken by walking in.
			if (city.owner == enemy && !HasArmedEnemy(player, tile)) {
				foreach (MapUnit u in stack.Where(u => PlayerAI.IsStillOurs(u, player) && u.location == horde.stackTile && u.movementPoints.canMove
						&& IsFighter(u)).OrderBy(u => u == Guard(player, u.location) ? 1 : 0).ToList()) {
					await u.Move(u.location.DirectionTo(tile));
					if (city.owner == player) {
						break;
					}
				}
			}

			if (city.owner == player) {
				log.Information("{Player}'s horde took {City}", player, city.name);
				// The King and the rest come in, and the city keeps a
				// garrison. The King stays out, and keeps the rest with him,
				// if he has no moves left, or more of his defenders would
				// be left outside (those that have used their moves) than
				// would come in.
				MapUnit king = horde.King;
				bool kingStays = false;
				if (king != null && king.location == horde.stackTile) {
					bool Defends(MapUnit u) => u != king && u.owner == player && u.CanDefendOnLand() && !u.unitType.isWorker && !u.unitType.isSettler;
					int comeIn = horde.stackTile.unitsOnTile.Count(u => Defends(u) && u.movementPoints.canMove) + tile.unitsOnTile.Count(Defends);
					int left = horde.stackTile.unitsOnTile.Count(u => Defends(u) && !u.movementPoints.canMove);
					kingStays = !king.movementPoints.canMove || comeIn < left || comeIn < KingSafeDefenders;
				}
				foreach (MapUnit u in horde.units.ToList()) {
					if (!kingStays && PlayerAI.IsStillOurs(u, player) && u.location == horde.stackTile && u.movementPoints.canMove) {
						await u.Move(u.location.DirectionTo(tile));
					}
				}
				if (!kingStays) {
					horde.stackTile = tile;
				}
				Garrison(player, horde, city);
				horde.targetCity = null;
				horde.waited = 0;
				return;
			}
			if (enough) {
				return;
			}
			++horde.waited;
			// A horde with cities of its own that has waited long enough, or
			// that couldn't take the city even with everyone it has, leaves it
			// alone for a while and tries somewhere else; its hurt go home to
			// heal (see PlayTurn).
			if (!homeless && HasArmedEnemy(player, tile) && (horde.waited > MaxWaitTurns
					|| ForceAgainst(player, horde.units, tile, ableNow: false) < AssaultMargin * WarCampaign.DefenderHitPoints(player, tile))) {
				log.Information("{Player}'s horde is too weak to take {City}, and leaves it for now", player, city.name);
				horde.avoid[city] = Turn + AvoidTurns;
				horde.targetCity = null;
				horde.waited = 0;
			}
		}

		// HEURISTIC: how long a horde leaves alone a city it was too weak to
		// take.
		private const int AvoidTurns = 10;

		// Leaves the city a garrison: the best defenders among the horde's
		// units in it, as many as the city wants (see GarrisonWanted).
		private static void Garrison(Player player, Horde horde, City city) {
			AIThreat threat = AIThreat.Assess(player);
			int want = GarrisonWanted(player, city, threat);
			int have = city.location.unitsOnTile.Count(u => u.owner == player && u.CanDefendOnLand() && !IsMember(u) && !u.unitType.isKing);
			List<MapUnit> keepers = horde.units.Where(u => u.location == city.location && u.CanDefendOnLand() && !u.unitType.isKing
					&& !u.unitType.isWorker && !u.IsArmy())
				.OrderByDescending(u => IsDefensive(u) ? 1 : 0).ThenByDescending(u => u.unitType.defense).ThenBy(u => Health(u)).ToList();
			// HEURISTIC: the horde keeps at least two fighters to go on with.
			int spare = horde.units.Count(IsFighter) - 2;
			foreach (MapUnit keeper in keepers) {
				if (have >= want || (have >= 1 && spare <= 0)) {
					break;
				}
				horde.units.Remove(keeper);
				keeper.currentAI = new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(keeper, player));
				keeper.Fortify();
				++have;
				--spare;
			}
		}

		// Moves the horde's stack along the path together, at the pace of the
		// slowest, stopping next to stopNextTo if given. Enemies in the way
		// are attacked when the odds are good; otherwise the stack waits.
		private static async Task MoveStack(Player player, Horde horde, List<MapUnit> stack, TilePath path, Tile stopNextTo) {
			MapUnit lead = stack[0];
			while (path.PathLength() > 0 && PlayerAI.IsStillOurs(lead, player) && lead.movementPoints.canMove) {
				if (stopNextTo != null && IsNeighbor(horde.stackTile, stopNextTo)) {
					break;
				}
				Tile next = path.PeekNext();
				if (next == stopNextTo) {
					break;
				}
				MapUnit king = stack.FirstOrDefault(u => u.unitType.isKing && u.location == horde.stackTile);
				if (king != null) {
					// The stack doesn't go on without the King, nor, once we
					// have a city to take him back to, to where he'd be in
					// danger.
					if (!king.movementPoints.canMove) {
						break;
					}
					if (player.cities.Count > 0 && !HasHostileUnits(player, next) && !KingSafeOn(player, next, stack)) {
						++horde.kingHeld;
						break;
					}
				}
				if (HasHostileUnits(player, next)) {
					double enough = ForceAgainst(player, stack, next, ableNow: true) >= AssaultMargin * WarCampaign.DefenderHitPoints(player, next) ? 0.3 : SureOdds;
					if (!await AttackIfGoodOdds(player, stack, next, enough) || HasHostileUnits(player, next)) {
						break;
					}
					if (stack.Any(u => PlayerAI.IsStillOurs(u, player) && u.location == horde.stackTile && !u.movementPoints.canMove)) {
						break;
					}
					continue;
				}
				path.Next();
				bool leadArrived = false;
				// The King moves last, so it never steps onto a tile alone.
				foreach (MapUnit u in stack.OrderBy(u => u.unitType.isKing ? 1 : 0)) {
					if (!PlayerAI.IsStillOurs(u, player) || u.location != horde.stackTile || !u.movementPoints.canMove) {
						continue;
					}
					if (u.unitType.isKing && !next.unitsOnTile.Any(x => x.owner == player && x.CanDefendOnLand() && !x.unitType.isKing)) {
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
				horde.stackTile = next;
				if (king != null) {
					horde.kingHeld = 0;
				}
				stack = stack.Where(u => PlayerAI.IsStillOurs(u, player) && u.location == next).ToList();
				if (stack.Count == 0 || stack[0] != lead) {
					break;
				}
			}
		}

		// Moves a member towards the tile, as far as it can this turn. A
		// fighter attacks only what it's very likely to beat on the way;
		// anyone else (the King, a worker) stops short of enemies.
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
				if (HasHostileUnits(player, next)) {
					if (!IsFighter(u) || !await AttackIfGoodOdds(player, [u], next, SureOdds) || HasHostileUnits(player, next)) {
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

		// The horde's units that didn't move fortify where they stand.
		private static void FortifyStack(Player player, Horde horde) {
			foreach (MapUnit u in horde.units) {
				if (PlayerAI.IsStillOurs(u, player) && u.movementPoints.canMove && !u.isFortified && !u.unitType.isWorker) {
					u.Fortify();
				}
			}
		}

		// Kings & Nomads: the King leads the attack (owner's request,
		// 2026-10-23). His civ's units on his tile or next to it fight at
		// +100% (see MapUnit.KingsPresenceBonus), so once we have a city he
		// marches with the strongest horde, on its tile, which is where its
		// attacks are made from, guarded and never attacking; the defenders
		// he stands with get the bonus too. He goes back to a safe city when
		// enemies near enough to strike could wear his stack down, or the
		// horde is weakened or has nothing to attack.
		//
		// In AI trials (AiTrialTest, 20 games of 6 civs and 120 turns on a
		// 70x70 map, half the civs playing this way against the same civs
		// without it), civs had 2.4 more cities at the end on Pangaea and
		// 1.6 more on Continents, and lost no more Kings; on 30 more games
		// on Pangaea, 3.2 more cities. With every civ playing this way, and
		// so every King in danger from King-led hordes, about 90% of the
		// Kings were still alive after 120 turns, against nearly all
		// without.

		// The King, unless he's marching with a horde: he isn't the city's
		// to guard then.
		private static bool IsResidentKing(MapUnit u) {
			return u.unitType.isKing && !IsMember(u);
		}

		// HEURISTIC: the horde the King joins needs at least this many
		// fighters, and he leaves one with fewer than KingLeaveFighters. A
		// horde goes up to KingRendezvousRange tiles out of its way to pick
		// him up from his city, and takes up to KingsHordeGuards more
		// defensive units than other hordes, for him. After going home he
		// stays at least KingRestTurns.
		private const int MinKingHordeFighters = 6;
		private const int KingLeaveFighters = 5;

		// With a city or two, the King leads smaller hordes (owner: "the
		// King provides morale, so he should be with the attacking force
		// when safe"), as long as he's safe with them (see KingSafeOn), and
		// his first city keeps him only two guards (see GarrisonWanted). In
		// AI trials (40 games of 6 civs and 120 turns, half the civs playing
		// this way) these civs took their second city-state 11 turns sooner
		// on Pangaea and 5 on Continents, with half a city more, 36 more
		// beakers a turn on Pangaea, and no more Kings or cities lost.
		private const int FewCitiesKingHordeFighters = 3;
		private const int FewCitiesKingLeaveFighters = 2;
		private static int MinKingHordeFightersFor(Player player) =>
			player.cities.Count <= 2 ? FewCitiesKingHordeFighters : MinKingHordeFighters;
		private static int KingLeaveFightersFor(Player player) =>
			player.cities.Count <= 2 ? FewCitiesKingLeaveFighters : KingLeaveFighters;
		private const int KingRendezvousRange = 12;
		private const int KingsHordeGuards = 2;
		private const int KingRestTurns = 2;
		private const int MinKingsGuards = 2;
		// HEURISTIC: how many turns in a row the horde may stop short of
		// danger to the King before he goes home.
		private const int MaxKingHeld = 2;

		private static readonly ConditionalWeakTable<Player, StrongBox<int>> kingRestsUntil = new();

		// Picks the horde that comes for the King: the strongest within
		// reach of his city, with fighters enough, while he's at home and
		// rested.
		private static void PlanKingsMarch(Player player, List<Horde> list) {
			MapUnit king = KingOf(player);
			City home = KingsCity(player);
			if (king == null || home == null || IsMember(king) || list.Any(h => h.rendezvous != null || h.King != null)
					|| (kingRestsUntil.TryGetValue(player, out StrongBox<int> until) && Turn < until.Value)) {
				return;
			}
			AIThreat threat = AIThreat.Assess(player);
			if (threat.EnemyCanReach(home.location)) {
				return;
			}
			int spareGuards = KingsGuards(player, home.location).Count(IsDefensive) - 1;
			Horde best = null;
			double bestStrength = 0;
			foreach (Horde h in list) {
				List<MapUnit> fighters = h.units.Where(u => IsFighter(u) && PlayerAI.IsStillOurs(u, player)).ToList();
				if (fighters.Count > 0) {
					PickStackTile(player, h);
				}
				if (h.stackTile == null || h.retreatTo != null || fighters.Count < MinKingHordeFightersFor(player)
						|| h.stackTile.continent != home.location.continent || h.stackTile.DistanceTo(home.location) > KingRendezvousRange) {
					continue;
				}
				// He needs guards that defend better than they attack: the
				// horde's, or those his city can spare once he's gone.
				if (fighters.Count(IsDefensive) + spareGuards < MinKingsGuards) {
					continue;
				}
				double strength = fighters.Sum(u => Math.Max(1, u.unitType.attack) * Health(u));
				if (strength > bestStrength) {
					MapUnit lead = fighters.OrderBy(u => u.MaxMovementPoints()).First();
					TilePath path = h.stackTile == home.location ? null : PathingAlgorithmChooser.GetAlgorithm(lead).PathFrom(h.stackTile, home.location, lead);
					if (h.stackTile != home.location && (path == null || path.PathLength() <= 0 || path.PathLength() > KingRendezvousRange)) {
						continue;
					}
					best = h;
					bestStrength = strength;
				}
			}
			if (best != null) {
				log.Information("{Player}'s horde of {Count} goes to {City} for the King", player, best.units.Count, home.name);
				best.rendezvous = home.location;
			}
		}

		// The horde's moves for the King: going to pick him up, taking him
		// home, or checking that he's safe where he is. Returns whether that
		// was the horde's turn.
		private static async Task<bool> MarchForTheKing(Player player, Horde horde) {
			MapUnit king = horde.King;
			if (king != null && horde.retreatTo == null) {
				int fighters = horde.units.Count(u => IsFighter(u) && PlayerAI.IsStillOurs(u, player));
				List<MapUnit> withHim = king.location.unitsOnTile.Where(u => u.owner == player && IsMember(u)).ToList();
				if (fighters < KingLeaveFightersFor(player)) {
					StartRetreat(player, horde, "has too few fighters left");
				} else if (!KingSafeOn(player, king.location, withHim)) {
					StartRetreat(player, horde, "is in danger", inDanger: true);
				} else if (horde.kingHeld >= MaxKingHeld) {
					StartRetreat(player, horde, "can't get near the enemy safely");
				}
			}
			if (horde.retreatTo != null) {
				if (king == null || !player.cities.Contains(horde.retreatTo)) {
					horde.retreatTo = null;
					return false;
				}
				if (king.location != horde.retreatTo.location) {
					await GatherStragglers(player, horde);
					List<MapUnit> stack = StackUnits(player, horde);
					if (stack.Count > 0 && horde.stackTile != horde.retreatTo.location) {
						TilePath path = PathingAlgorithmChooser.GetAlgorithm(stack[0]).PathFrom(horde.stackTile, horde.retreatTo.location, stack[0]);
						if (path == null || path.PathLength() <= 0) {
							horde.retreatTo = null;
							return false;
						}
						await MoveStackHome(player, horde, stack, path);
					}
				}
				if (king.location == horde.retreatTo.location && !WaitForKingsGuard(player, horde, king)) {
					// Home: he stays, guarded (see GuardKing), and the horde
					// goes on without him.
					log.Information("{Player}'s King is back in {City}", player, horde.retreatTo.name);
					Release(horde, king);
					king.currentAI = new KingAI { destination = king.location };
					king.Fortify();
					kingRestsUntil.AddOrUpdate(player, new StrongBox<int>(Turn + KingRestTurns));
					horde.retreatTo = null;
					horde.kingHeld = 0;
					GuardKing(player, horde);
				}
				return true;
			}
			if (horde.rendezvous != null) {
				MapUnit ours = KingOf(player);
				if (ours == null || ours.location != horde.rendezvous || horde.rendezvous.cityAtTile?.owner != player || IsMember(ours)) {
					horde.rendezvous = null;
					return false;
				}
				await GatherStragglers(player, horde);
				List<MapUnit> stack = StackUnits(player, horde);
				if (stack.Count > 0 && horde.stackTile != horde.rendezvous) {
					TilePath path = PathingAlgorithmChooser.GetAlgorithm(stack[0]).PathFrom(horde.stackTile, horde.rendezvous, stack[0]);
					if (path == null || path.PathLength() <= 0) {
						horde.rendezvous = null;
						return false;
					}
					await MoveStack(player, horde, stack, path, stopNextTo: null);
				}
				if (horde.stackTile == horde.rendezvous) {
					JoinKing(player, horde, ours);
				}
				return true;
			}
			return false;
		}

		// HEURISTIC: home, the horde stays with the King until the city has
		// the guard it wants for him (see GarrisonWanted), for up to
		// MaxWaitTurns: the horde's units that had no moves left come on
		// next turn. Returns whether it waits.
		private static bool WaitForKingsGuard(Player player, Horde horde, MapUnit king) {
			Tile tile = king.location;
			int have = tile.unitsOnTile.Count(u => u.owner == player && u != king && u.CanDefendOnLand() && !u.unitType.isWorker
				&& !u.unitType.isSettler && !u.IsArmy());
			int want = 3 + Math.Min(3, player.cities.Count / 5) + (AIThreat.Assess(player).EnemyCanReach(tile) ? 1 : 0);
			bool coming = horde.units.Any(u => PlayerAI.IsStillOurs(u, player) && u.location != tile && u.CanDefendOnLand() && !u.unitType.isWorker);
			if (have >= want || !coming || horde.kingWaited >= MaxWaitTurns) {
				horde.kingWaited = 0;
				return false;
			}
			++horde.kingWaited;
			horde.stackTile = tile;
			return true;
		}

		// The King joins the horde in his city, with defenders the city can
		// spare once he's gone.
		private static void JoinKing(Player player, Horde horde, MapUnit king) {
			Join(horde, king);
			horde.rendezvous = null;
			horde.kingHeld = 0;
			City city = king.location.cityAtTile;
			int guards = horde.units.Count(u => IsFighter(u) && IsDefensive(u));
			int keep = GarrisonWanted(player, city, AIThreat.Assess(player));
			List<MapUnit> garrison = KingsGuards(player, city.location);
			foreach (MapUnit u in garrison.Skip(keep).Where(u => IsFighter(u) && IsDefensive(u) && !u.IsArmy()).ToList()) {
				if (guards >= MaxHordeGuards + KingsHordeGuards) {
					break;
				}
				Join(horde, u);
				++guards;
			}
			log.Information("{Player}'s King marches with the horde of {Count} from {City}", player, horde.units.Count, city.name);
		}

		// The horde makes for the safest of our cities it can reach (see
		// SafetyScore), less a point for each tile of the way, to take the
		// King home; in danger in one of our cities, he stays there.
		private static void StartRetreat(Player player, Horde horde, string why, bool inDanger = false) {
			MapUnit king = horde.King;
			if (king == null) {
				return;
			}
			horde.targetCity = null;
			horde.targetKing = null;
			horde.waited = 0;
			City here = king.location.cityAtTile?.owner == player ? king.location.cityAtTile : null;
			if (here != null && inDanger) {
				horde.retreatTo = here;
				return;
			}
			City best = here;
			AIThreat threat = AIThreat.Assess(player);
			double bestScore = here != null ? SafetyScore(player, here, threat) : double.MinValue;
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(king);
			foreach (City city in player.cities.Where(c => c != here && c.location.continent == king.location.continent)
					.OrderBy(c => c.location.DistanceTo(king.location)).Take(10)) {
				TilePath path = algorithm.PathFrom(king.location, city.location, king);
				if (path == null || path.PathLength() <= 0) {
					continue;
				}
				double score = SafetyScore(player, city, threat) - path.PathLength();
				if (score > bestScore) {
					bestScore = score;
					best = city;
				}
			}
			if (best != null) {
				log.Information("{Player}'s horde {Why}, and takes the King back to {City}", player, why, best.name);
			}
			horde.retreatTo = best;
			horde.targetCity = null;
			horde.targetKing = null;
			horde.waited = 0;
		}

		// Moves the horde's stack, King and all, along the path home. It
		// attacks only what's in the way at very good odds.
		private static async Task MoveStackHome(Player player, Horde horde, List<MapUnit> stack, TilePath path) {
			MapUnit lead = stack[0];
			MapUnit king = horde.King;
			while (path.PathLength() > 0 && PlayerAI.IsStillOurs(lead, player) && lead.movementPoints.canMove
					&& king != null && king.location == horde.stackTile && king.movementPoints.canMove) {
				Tile next = path.PeekNext();
				if (HasHostileUnits(player, next)) {
					if (!await AttackIfGoodOdds(player, stack, next, SureOdds) || HasHostileUnits(player, next)) {
						break;
					}
					continue;
				}
				path.Next();
				bool leadArrived = false;
				foreach (MapUnit u in stack.OrderBy(u => u.unitType.isKing ? 1 : 0)) {
					if (!PlayerAI.IsStillOurs(u, player) || u.location != horde.stackTile || !u.movementPoints.canMove) {
						continue;
					}
					if (u.unitType.isKing && !next.unitsOnTile.Any(x => x.owner == player && x.CanDefendOnLand() && !x.unitType.isKing)) {
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
				horde.stackTile = next;
				stack = stack.Where(u => PlayerAI.IsStillOurs(u, player) && u.location == next).ToList();
				if (stack.Count == 0 || stack[0] != lead) {
					break;
				}
			}
		}

		// The enemy land units we can see that could attack the tile next
		// turn (city-states' garrisons never come out, see HoldsCityState).
		private static List<MapUnit> EnemiesInReach(Player player, Tile tile) {
			List<MapUnit> result = new();
			foreach (Player other in EngineStorage.gameData.players) {
				if (!AIThreat.IsHostile(player, other)) {
					continue;
				}
				foreach (MapUnit u in other.units) {
					Tile t = u.location;
					if (t == null || t == Tile.NONE || !u.IsLandUnit() || !u.CanAttack() || u.unitType.attack <= 0 || u.IsLoaded()
							|| t.DistanceTo(tile) > Math.Max(1, u.MaxMovementPoints()) + KingWarningTiles || HoldsCityState(other, u)
							|| !AIFogOfWar.SeesUnitsOn(player, t) || !u.IsSeenBy(player)) {
						continue;
					}
					result.Add(u);
				}
			}
			return result;
		}

		// HEURISTIC: the King is safe on a tile if, of the defenders he'd
		// have there, he can expect at least KingSafeDefenders to be left
		// after every enemy we can see that could reach it next turn
		// attacked once, and they'd lose no more than half. The odds are
		// each attacker's against the best of them as they stand now (with
		// the King's bonus, see MapUnit.KingsPresenceBonus); a win kills one.
		private const int KingSafeDefenders = 3;
		private const double KingMaxLossShare = 3;
		// HEURISTIC: how much further than an enemy could reach in a turn
		// it counts as a danger to the King: roads carry it further, and a
		// slow King can't outrun one that comes nearer.
		private const int KingWarningTiles = 1;

		private static bool KingSafeOn(Player player, Tile tile, List<MapUnit> with) {
			List<MapUnit> defenders = with.Where(u => PlayerAI.IsStillOurs(u, player) && u.CanDefendOnLand() && !u.unitType.isKing
				&& !u.unitType.isWorker && !u.unitType.isSettler).ToList();
			if (tile.cityAtTile?.owner == player) {
				defenders.AddRange(tile.unitsOnTile.Where(u => u.owner == player && !defenders.Contains(u) && u.CanDefendOnLand()
					&& !u.unitType.isKing && !u.unitType.isWorker && !u.unitType.isSettler));
			}
			if (defenders.Count < KingSafeDefenders) {
				return false;
			}
			double losses = 0;
			foreach (MapUnit enemy in EnemiesInReach(player, tile)) {
				MapUnit defender = tile.FindTopDefender(enemy, defenders);
				if (MapUnit.IsMapUnitValid(defender)) {
					losses += CombatAI.EstimateChanceToWin(enemy, defender);
				}
			}
			return defenders.Count - losses >= KingSafeDefenders && losses <= defenders.Count / KingMaxLossShare;
		}

		// The King, once we have a city: in one of our cities it stays there
		// fortified; elsewhere it goes to the safest one it can reach. Losing
		// it loses the game.
		internal static C7GameData.UnitAI MakeKingAI(MapUnit king, Player player) {
			City here = king.location.cityAtTile;
			if (here != null && here.owner == player) {
				return new KingAI { destination = king.location };
			}
			City best = null;
			TilePath bestPath = null;
			double bestScore = double.MinValue;
			AIThreat threat = AIThreat.Assess(player);
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(king);
			foreach (City city in player.cities.OrderBy(c => c.location.DistanceTo(king.location)).Take(6)) {
				TilePath path = algorithm.PathFrom(king.location, city.location, king);
				if (path == null || path.PathLength() <= 0) {
					continue;
				}
				double score = SafetyScore(player, city, threat) - path.PathLength();
				if (score > bestScore) {
					bestScore = score;
					best = city;
					bestPath = path;
				}
			}
			return new KingAI { destination = best?.location ?? king.location, path = bestPath };
		}

		// HEURISTIC: how safe a city is for the King: how far it is from
		// rival civs' cities and the enemies we can see (up to 12 tiles),
		// its defenders (more for those that defend better than they
		// attack), its walls, and much less if an enemy we can see could
		// strike it next turn. Defenders count for less than distance: the
		// King's city builds and keeps more of them (see GarrisonWanted and
		// UnitBonus), but a city on the frontier stays on it.
		internal static double SafetyScore(Player player, City city, AIThreat threat) {
			double score = 0;
			foreach (MapUnit u in city.location.unitsOnTile) {
				if (u.owner == player && u.CanDefendOnLand() && !u.unitType.isKing && !IsMember(u) && !u.unitType.isWorker && !u.unitType.isSettler) {
					score += IsDefensive(u) ? 2 : 1;
				}
			}
			if (city.constructed_buildings.Any(b => b.building.providesWalls)) {
				score += 4;
			}
			if (threat.EnemyCanReach(city.location)) {
				score -= 8;
			}
			int nearest = SafeDistance;
			foreach (City other in EngineStorage.gameData.cities) {
				if (other.owner != player && !other.owner.IsIndependent) {
					nearest = Math.Min(nearest, other.location.DistanceTo(city.location));
				}
			}
			foreach (Tile t in threat.EnemyTiles) {
				nearest = Math.Min(nearest, t.DistanceTo(city.location));
			}
			return score + nearest;
		}

		private const int SafeDistance = 12;

		// HEURISTIC: every few turns, a King in a city where no enemy can
		// strike next turn moves to a city that is safer by this much (see
		// SafetyScore), if it can get there within MaxKingTrip tiles by way of
		// tiles no enemy we can see could reach next turn.
		private const double SaferBy = 5;
		private const int MaxKingTrip = 8;
		private const int KingRethinkTurns = 5;

		private static void MaybeMoveKing(Player player) {
			MapUnit king = KingOf(player);
			City here = KingsCity(player);
			if (king == null || here == null || player.cities.Count < 2 || Turn % KingRethinkTurns != 0 || IsMember(king)) {
				return;
			}
			AIThreat threat = AIThreat.Assess(player);
			if (threat.EnemyCanReach(here.location)) {
				return;
			}
			double hereScore = SafetyScore(player, here, threat);
			City best = null;
			TilePath bestPath = null;
			double bestScore = hereScore + SaferBy;
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(king);
			foreach (City city in player.cities) {
				if (city == here || city.location.DistanceTo(here.location) > MaxKingTrip || city.location.continent != here.location.continent) {
					continue;
				}
				double score = SafetyScore(player, city, threat);
				if (score < bestScore) {
					continue;
				}
				TilePath path = algorithm.PathFrom(king.location, city.location, king);
				if (path == null || path.PathLength() <= 0 || path.PathLength() > MaxKingTrip
						|| path.path.Any(t => threat.EnemyCanReach(t) && t.cityAtTile?.owner != player)) {
					continue;
				}
				best = city;
				bestPath = path;
				bestScore = score;
			}
			if (best == null) {
				return;
			}
			log.Information("{Player}'s King moves from {From} to the safer {To}", player, here.name, best.name);
			king.Wake();
			king.currentAI = new KingAI { destination = best.location, path = bestPath };
		}

		// A King going to (or staying in) a city of ours. It never attacks:
		// with an enemy in the way it waits, and thinks again next turn.
		public sealed class KingAI : C7GameData.UnitAI {
			public Tile destination;
			public TilePath path;

			C7GameData.UnitAI.MoveResult C7GameData.UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
				if (unit.location == destination) {
					if (!unit.isFortified) {
						unit.Fortify();
					}
					return C7GameData.UnitAI.Result.Done;
				}
				if (destination?.cityAtTile?.owner != player || path == null) {
					return C7GameData.UnitAI.Result.Error;
				}
				Tile next = path.PeekNext();
				if (next == null || next == Tile.NONE || HasHostileUnits(player, next)
						|| (next.cityAtTile?.owner != player && AIThreat.Assess(player).EnemyCanReach(next))) {
					// Stays put this turn rather than attack.
					unit.movementPoints.onConsumeAll();
					return C7GameData.UnitAI.Result.Done;
				}
				return this.TryToMoveAlongPath(unit, ref path);
			}

			public string SummarizePlan() {
				return $"King retiring to {destination}";
			}

			public void UpdateOnDeath() { }
		}

		// HEURISTIC: in this scenario a city's production wants, on top of
		// the ordinary scores (see ChooseProducible.ScoreUnit): a defensive
		// unit (one that defends better than it attacks) first, three where
		// the King is; then settlers, which only captured cities can build,
		// so they make the most of a city we took, and the first of which
		// founds our capital (captured cities have no palace, and the AI
		// never builds one); and attackers for the horde. In AI trials,
		// Kings were lost in cities held by two horsemen or archers.
		internal static float UnitBonus(City city, Player player, UnitPrototype unit) {
			if (!PlaysFor(player) || !unit.IsLandUnit()) {
				return 0;
			}
			int defenders = 0, defensive = 0;
			bool kingHere = false;
			foreach (MapUnit u in city.location.unitsOnTile) {
				if (u.owner != player || IsMember(u) || u.unitType.isWorker || u.unitType.isSettler) {
					continue;
				}
				if (u.unitType.isKing) {
					kingHere = true;
				} else if (u.CanDefendOnLand()) {
					++defenders;
					if (IsDefensive(u)) {
						++defensive;
					}
				}
			}
			int wantDefensive = kingHere ? 3 : 1;
			if (unit.defense > unit.attack && !unit.isSettler && !unit.isWorker) {
				return defensive < wantDefensive ? 25 : 0;
			}
			if (unit.isSettler) {
				if (defenders < 1) {
					return 0;
				}
				return player.cities.Any(c => c.IsCapital()) ? 15 : 30;
			}
			if (unit.attack > unit.defense && defenders >= 1) {
				return 8;
			}
			return 0;
		}

		// HEURISTIC: walls are worth much more where the King is.
		internal static float BuildingBonus(City city, Player player, Building building) {
			if (!PlaysFor(player) || !building.providesWalls) {
				return 0;
			}
			return city.location.unitsOnTile.Any(u => u.owner == player && IsResidentKing(u)) ? 40 : 0;
		}
	}
}
