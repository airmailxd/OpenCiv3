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
	// attacks itself. From then on it retires to the safest of our cities
	// and stays there fortified with a guard the rest of the AI leaves
	// alone (see MakeKingAI and GuardsTheKing). Captured cities build
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
			return player != null && !player.isHuman && !player.isBarbarians && IsActive(EngineStorage.gameData);
		}

		// Whether the barbarian unit is part of an independent city's
		// garrison, which stays in the city. The barbarians' ordinary AI sends
		// units in a city out to raid and explore like any others, and in AI
		// trials the walled city-states had emptied themselves within two or
		// three turns, so that the first horde to arrive walked in (owner's
		// design: they are "walled, garrisoned").
		public static bool HoldsCityState(Player player, MapUnit unit) {
			return player.isBarbarians && IsActive(EngineStorage.gameData) && unit.location.cityAtTile?.owner == player
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
			return other.isBarbarians || (player.playerRelationships.TryGetValue(other.id, out PlayerRelationship r) && r.AtWar());
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
				// With a city, the King retires (see MakeKingAI), workers go
				// to work, and the badly hurt go back to heal (see RestAI).
				if (!homeless) {
					foreach (MapUnit u in horde.units.Where(u => u.unitType.isKing || u.unitType.isWorker
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
			ChooseTarget(player, gameData, list, horde, homeless);
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
			int want = 1;
			if (threat.EnemyCanReach(city.location)) {
				++want;
			}
			if (city.location.unitsOnTile.Any(u => u.owner == player && u.unitType.isKing)) {
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
			if (city == null || city.owner != player || !city.location.unitsOnTile.Any(u => u.owner == player && u.unitType.isKing)) {
				return false;
			}
			int rank = KingsGuards(player, city.location).IndexOf(unit);
			return rank >= 0 && rank < GarrisonWanted(player, city, AIThreat.Assess(player));
		}

		// The King's city gets back defenders it is short of from the horde's
		// units in it.
		private static void GuardKing(Player player, Horde horde) {
			City city = KingsCity(player);
			if (city == null) {
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
			return fighters < MaxHordeFighters && (!IsDefensive(u) || guards < MaxHordeGuards);
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

		// HEURISTIC: what taking a city is worth against its garrison and the
		// way there: its size (a captured city builds settlers, so even a
		// small one is worth a lot), far more with a rival King inside
		// (killing it brings down that civ), over the defenders' hit points
		// and the distance.
		private static double CityScore(Player player, City city, Tile from) {
			double value = 4 + city.residents.Count;
			if (city.location.unitsOnTile.Any(u => u.unitType.isKing && u.owner != player)) {
				value += KingValue;
			}
			double defenders = AIFogOfWar.SeesUnitsOn(player, city.location)
				? WarCampaign.DefenderHitPoints(player, city.location) / 3.0
				: Math.Max(2, 1 + city.residents.Count / 3);
			int distance = from.DistanceTo(city.location);
			return value / (1 + defenders) / (1 + distance / 4.0);
		}

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

			// Each horde goes after a city of its own.
			HashSet<City> others = list.Where(h => h != horde && h.targetCity != null).Select(h => h.targetCity).ToHashSet();
			bool Valid(City c) => c != null && gameData.cities.Contains(c) && c.owner != player && AtWarWith(player, c.owner)
				&& !(horde.avoid.TryGetValue(c, out int until) && Turn < until) && !others.Contains(c)
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
				if (p == player || p.isBarbarians || p.defeated) {
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
			List<City> candidates = gameData.cities.Where(Valid).OrderByDescending(c => CityScore(player, c, from)).ToList();
			// The best that can be reached by land, scored as the crow flies.
			foreach (City c in candidates.Take(6)) {
				TilePath path = PathingAlgorithmChooser.GetAlgorithm(lead).PathFrom(from, c.location, lead);
				if (from != c.location && !IsNeighbor(from, c.location) && (path == null || path.PathLength() <= 0)) {
					continue;
				}
				best = c;
				bestScore = CityScore(player, c, from);
				break;
			}
			if (horde.targetCity != null && best != null && best != horde.targetCity
					&& bestScore < SwitchMargin * CityScore(player, horde.targetCity, from)) {
				return;
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

		// With no independent city left within reach, a civ picks a fight
		// with the rival whose city is nearest the horde, if it may declare
		// war on them: in this scenario an empire only grows by conquest.
		// Returns the city to march on, or null.
		private static City MaybeDeclareWar(Player player, GameData gameData, Horde horde) {
			Tile from = horde.stackTile;
			City nearest = null;
			foreach (City c in gameData.cities) {
				Player owner = c.owner;
				if (owner == player || owner.isBarbarians || owner.defeated || c.location.continent != from.continent
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
				// garrison.
				foreach (MapUnit u in horde.units.ToList()) {
					if (PlayerAI.IsStillOurs(u, player) && u.location == horde.stackTile && u.movementPoints.canMove) {
						await u.Move(u.location.DirectionTo(tile));
					}
				}
				horde.stackTile = tile;
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
				if (other.owner != player && !other.owner.isBarbarians) {
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
			if (king == null || here == null || player.cities.Count < 2 || Turn % KingRethinkTurns != 0) {
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
			return city.location.unitsOnTile.Any(u => u.owner == player && u.unitType.isKing) ? 40 : 0;
		}
	}
}
