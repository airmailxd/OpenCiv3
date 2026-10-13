using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using C7Engine.Pathing;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.UnitAI {
	// HEURISTIC: how the AI fights with its warships. Each time a warship
	// can move it does the first of these that applies:
	//
	//   - hurt, it sails back to one of our ports to heal (ships only heal
	//     in port);
	//   - it attacks an enemy ship it can reach this turn with good odds;
	//   - it bombards an enemy city or unit in range, the city a war
	//     campaign (see WarCampaign) is after first;
	//   - it sails within range of the campaign's target, if that's a coastal
	//     city it can reach this turn;
	//   - it joins one of our transports carrying units, to guard it;
	//   - it sails for an enemy ship near our coast;
	//   - and otherwise it waits in the nearest port.
	//
	// Its moves keep to water it can't sink in (paths steer clear of unsafe
	// water, and it attacks and stops only on safe tiles), so it always
	// ends its turn in safe waters, as every AI ship must.
	//
	// A privateer at peace may also raid the ships of a weaker civ, with
	// its nationality hidden (see MapUnit_HiddenNationality.cs), but only
	// with very good odds.
	//
	// The AI builds warships when it is at war with a civ whose ships or
	// coastal cities its ports can reach by sea (see WantsWarships): one
	// for each enemy ship and one for their coast, up to one for every
	// four of its cities.
	//
	// Warships come with Magnetism, which AIs seldom reach before turn 250,
	// so AI trials (AiTrialTest, paired) ran longer: civs playing this way
	// sank about one more ship each and did no worse, with score +24 +- 31
	// over 30 games of 400 turns on Archipelago and +78 +- 71 over 19 games
	// of 320 turns on Continents (where they lost 0.3 +- 0.2 fewer cities).
	// Wanting more warships (up to one for every two cities) cost about 0.4
	// techs and 4 citizens a civ on Archipelago.
	public class NavalAI : C7GameData.UnitAI {
		private static readonly ILogger log = Log.ForContext<NavalAI>();

		// HEURISTIC: below this share of its hit points a warship goes home
		// to heal.
		private const double HurtHealth = 0.6;
		// HEURISTIC: the odds a warship wants before attacking a ship, and
		// a privateer before raiding one in peacetime.
		private const double MinAttackOdds = 0.6;
		private const double MinRaidOdds = 0.65;
		// HEURISTIC: how far from our cities (in tiles) enemy ships count as
		// near our coast, and how far a warship goes to guard a transport.
		private const int CoastDistance = 8;
		private const int EscortDistance = 8;
		// HEURISTIC: how far a warship goes to raid in peacetime.
		private const int RaidDistance = 6;

		// What a warship adds to its score in ChooseProducible while we want
		// more of them.
		internal const float WarshipBonus = 10;

		private TilePath path;
		private string plan = "idle";

		public static bool Enabled(Player player) {
			return !player.isHuman && !player.isBarbarians;
		}

		// A ship built to fight: one that attacks or bombards and carries
		// no units (transports, carriers and missile submarines have their
		// own uses).
		public static bool IsWarship(UnitPrototype type) {
			return type.IsSeaUnit() && !type.isArmy && type.capacity == 0 && !type.isNuclearWeapon
				&& (type.attack >= 2 || type.bombard > 0);
		}

		public static bool IsWarship(MapUnit unit) {
			return unit.IsWaterUnit() && IsWarship(unit.unitType);
		}

		// Takes on a warship with naval work to do (see the class comment),
		// or returns null to leave it to the other unit AIs.
		public static NavalAI MaybeMake(MapUnit unit, Player player) {
			if (!Enabled(player) || !IsWarship(unit) || unit.IsLoaded() || !HasNavalWork(unit, player)) {
				return null;
			}
			return new NavalAI();
		}

		private static bool HasNavalWork(MapUnit ship, Player player) {
			return IsInAnyWar(player, EngineStorage.gameData.players) || HostileShipNearCoast(player)
				|| (ship.unitType.hasHiddenNationality && RaidTarget(ship, player, out _) != null);
		}

		public string SummarizePlan() {
			return "NavalAI: " + plan;
		}

		public void UpdateOnDeath() { }

		C7GameData.UnitAI.MoveResult C7GameData.UnitAI.PlayTurnImpl(Player player, MapUnit ship) {
			if (!HasNavalWork(ship, player)) {
				return C7GameData.UnitAI.Result.Done;
			}
			AIFogOfWar.Refresh(player);

			// Hurt: home to heal.
			if (ship.hitPointsRemaining < HurtHealth * ship.maxHitPoints) {
				plan = "heal";
				if (ship.location.cityAtTile?.owner == player) {
					return Hold(ship);
				}
				return SailTo(ship, NearestPort(ship, player), player);
			}

			// Attack a ship within reach.
			if (ship.CanAttackAgainThisTurn()) {
				(Tile target, TilePath attackPath) = ShipToAttack(ship, player);
				if (target != null) {
					plan = $"attack {target}";
					path = attackPath;
					return this.TryToMoveAlongPath(ship, ref path);
				}
			}

			// Bombard what's in range.
			if (ship.unitType.bombard > 0 && ship.CanAttackAgainThisTurn()) {
				Tile bombard = BombardTarget(ship, player);
				if (bombard != null) {
					plan = $"bombard {bombard}";
					return C7GameData.UnitAI.MoveResult.MoveRequested(BombardAndSurvive(ship, bombard, player));
				}

				// Close on the campaign's target.
				if (CampaignBombardSpot(ship, player, out TilePath toSpot) is Tile spot) {
					plan = $"close on {spot}";
					path = toSpot;
					return this.TryToMoveAlongPath(ship, ref path);
				}
			}

			// Guard a transport at sea.
			if (TransportToGuard(ship, player) is MapUnit transport) {
				plan = $"guard {transport}";
				if (ship.location == transport.location) {
					return Hold(ship);
				}
				return SailTo(ship, transport.location, player);
			}

			// Go after an enemy ship near our coast.
			if (EnemyShipNearCoast(ship, player, out TilePath toEnemy) is Tile enemyShip) {
				plan = $"hunt {enemyShip}";
				path = toEnemy;
				// Stop next to it rather than end the turn short of an attack.
				if (path.PathLength() <= 1) {
					return Hold(ship);
				}
				return this.TryToMoveAlongPath(ship, ref path);
			}

			// Nothing to do: wait in port.
			plan = "wait in port";
			if (ship.location.cityAtTile?.owner == player) {
				return Hold(ship);
			}
			return SailTo(ship, NearestPort(ship, player), player);
		}

		private static C7GameData.UnitAI.MoveResult Hold(MapUnit ship) {
			ship.SkipTurn();
			return C7GameData.UnitAI.Result.InProgress;
		}

		private C7GameData.UnitAI.MoveResult SailTo(MapUnit ship, Tile destination, Player player) {
			if (destination == null || destination == ship.location) {
				return Hold(ship);
			}
			if (path == null || path.destination != destination || path.PathLength() <= 0) {
				path = PathingAlgorithmChooser.GetAlgorithm(ship).PathFrom(ship.location, destination, ship);
			}
			C7GameData.UnitAI.MoveResult moved = this.TryToMoveAlongPath(ship, ref path);
			if (moved.Result == C7GameData.UnitAI.Result.Error) {
				path = null;
				return Hold(ship);
			}
			return moved;
		}

		private static async Task<bool> BombardAndSurvive(MapUnit ship, Tile tile, Player player) {
			await ship.Bombard(tile);
			return PlayerAI.IsStillOurs(ship, player);
		}

		// Whether a ship of ours may stop on the tile: water it can't sink
		// in, or one of our cities.
		private static bool SafeFor(MapUnit ship, Tile tile) {
			if (tile.HasCity()) {
				return tile.cityAtTile.owner == ship.owner;
			}
			return tile.IsWater() && !ship.IsUnsafeWater(tile);
		}

		private static int MovesLeft(MapUnit ship) {
			return Math.Max(1, (int)Math.Ceiling(ship.movementPoints.remaining));
		}

		// The best enemy ship the warship can reach and attack this turn
		// with good odds, and the way there.
		private static (Tile, TilePath) ShipToAttack(MapUnit ship, Player player) {
			int reach = MovesLeft(ship);
			List<(Tile tile, double odds)> candidates = new();
			foreach (Tile t in ship.location.GetTilesWithinTileSquare(reach)) {
				if (t == Tile.NONE || t == ship.location || !t.IsWater() || t.HasCity() || t.unitsOnTile.Count == 0
						|| !SafeFor(ship, t) || !AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				MapUnit defender = t.FindTopDefender(ship);
				if (!MapUnit.IsMapUnitValid(defender) || !defender.IsWaterUnit() || !ship.MayAttack(defender)) {
					continue;
				}
				bool raid = ship.MakesHiddenAttackOn(defender);
				if (raid && !IsRaidable(player, defender.owner)) {
					continue;
				}
				double odds = CombatAI.EstimateChanceToWin(ship, defender);
				if (odds < (raid ? MinRaidOdds : MinAttackOdds)) {
					continue;
				}
				candidates.Add((t, odds));
			}
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(ship);
			foreach ((Tile t, double _) in candidates.OrderByDescending(c => c.odds).ThenBy(c => c.tile.DistanceTo(ship.location))) {
				TilePath p = algorithm.PathFrom(ship.location, t, ship);
				if (p != null && p.PathLength() > 0 && p.PathLength() <= reach) {
					return (t, p);
				}
			}
			return (null, null);
		}

		// A privateer's peacetime raiding target near it, if any.
		private static Tile RaidTarget(MapUnit ship, Player player, out MapUnit victim) {
			victim = null;
			foreach (Tile t in ship.location.GetTilesWithinTileSquare(RaidDistance)) {
				if (t == Tile.NONE || !t.IsWater() || t.HasCity() || t.unitsOnTile.Count == 0 || !AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				MapUnit defender = t.FindTopDefender(ship);
				if (MapUnit.IsMapUnitValid(defender) && defender.IsWaterUnit() && ship.MakesHiddenAttackOn(defender)
						&& IsRaidable(player, defender.owner) && CombatAI.EstimateChanceToWin(ship, defender) >= MinRaidOdds) {
					victim = defender;
					return t;
				}
			}
			return null;
		}

		// Whether a privateer of ours raids the civ's ships in peacetime: a
		// civ that isn't our ally and is weaker than us.
		private static bool IsRaidable(Player player, Player other) {
			if (other == null || other.isBarbarians || other == player || EngineStorage.gameData.AreInLockedPeace(player, other)) {
				return false;
			}
			return other.cities.Count * 3 < player.cities.Count * 2;
		}

		// A tile in bombard range with an enemy city or unit of a civ we're
		// at war with, the city a campaign is after first.
		private static Tile BombardTarget(MapUnit ship, Player player) {
			City campaignTarget = WarCampaign.TryGetCampaign(player, out _, out City target, out _, out _) ? target : null;
			Tile best = null;
			int bestRank = int.MaxValue;
			foreach (Tile t in ship.location.GetTilesWithinRankDistance(Math.Max(1, ship.unitType.bombardRange))) {
				if (t == Tile.NONE || t == ship.location || !ship.CanBombardTile(t, out MapUnit.BombardTarget kind)) {
					continue;
				}
				Player owner = kind switch {
					MapUnit.BombardTarget.City => t.cityAtTile.owner,
					MapUnit.BombardTarget.Unit => t.FindTopDefenderForBombard(ship).owner,
					_ => null,
				};
				if (owner == null || owner.isBarbarians && kind == MapUnit.BombardTarget.City || !AtWar(player, owner)) {
					continue;
				}
				int rank = t.cityAtTile != null && t.cityAtTile == campaignTarget ? 0 : kind == MapUnit.BombardTarget.City ? 1 : 2;
				if (rank < bestRank) {
					best = t;
					bestRank = rank;
				}
			}
			return best;
		}

		// A safe water tile in bombard range of the campaign's target city,
		// if the warship can get there this turn.
		private static Tile CampaignBombardSpot(MapUnit ship, Player player, out TilePath spotPath) {
			spotPath = null;
			if (!WarCampaign.TryGetCampaign(player, out _, out City target, out _, out _) || target == null) {
				return null;
			}
			Tile city = target.location;
			int range = Math.Max(1, ship.unitType.bombardRange);
			int reach = MovesLeft(ship);
			if (ship.location.DistanceTo(city) > reach + range) {
				return null;
			}
			List<Tile> spots = city.GetTilesWithinRankDistance(range)
				.Where(t => t != Tile.NONE && t.IsWater() && !t.HasCity() && SafeFor(ship, t) && t.unitsOnTile.Count == 0)
				.OrderBy(t => t.DistanceTo(ship.location)).ToList();
			if (spots.Count == 0) {
				return null;
			}
			int i = PathingAlgorithmChooser.GetAlgorithm(ship).FindFirstReachable(ship.location, spots, ship, out TilePath p);
			if (i < 0 || p == null || p.PathLength() <= 0 || p.PathLength() > reach) {
				return null;
			}
			spotPath = p;
			return spots[i];
		}

		// One of our transports at sea with units aboard and no warship
		// guarding it, near the warship.
		private static MapUnit TransportToGuard(MapUnit ship, Player player) {
			if (!IsInAnyWar(player, EngineStorage.gameData.players)) {
				return null;
			}
			foreach (MapUnit u in player.units) {
				if (u == ship || !SeaRoutes.IsTransport(u) || u.PassengerCount() == 0 || !u.location.IsWater()
						|| u.location.DistanceTo(ship.location) > EscortDistance) {
					continue;
				}
				bool guarded = u.location.unitsOnTile.Any(o => o != ship && o.owner == player && IsWarship(o));
				if (!guarded || ship.location == u.location) {
					return u;
				}
			}
			return null;
		}

		// The nearest enemy ship near our coast the warship can reach, and
		// the way to it.
		private static Tile EnemyShipNearCoast(MapUnit ship, Player player, out TilePath shipPath) {
			shipPath = null;
			List<Tile> targets = new();
			foreach (Player other in EngineStorage.gameData.players) {
				if (other == player || other.defeated || !AtWar(player, other)) {
					continue;
				}
				foreach (MapUnit u in other.units) {
					// Only ships it would attack once there.
					if (u.IsWaterUnit() && Tile.IsTileValid(u.location) && u.location.IsWater() && !u.location.HasCity() && SafeFor(ship, u.location)
							&& NearOurCoast(player, u.location) && AIFogOfWar.SeesUnitsOn(player, u.location)
							&& u.location.FindTopDefender(ship) is MapUnit defender && MapUnit.IsMapUnitValid(defender)
							&& CombatAI.EstimateChanceToWin(ship, defender) >= MinAttackOdds) {
						targets.Add(u.location);
					}
				}
			}
			if (targets.Count == 0) {
				return null;
			}
			targets = targets.Distinct().OrderBy(t => t.DistanceTo(ship.location)).Take(4).ToList();
			int i = PathingAlgorithmChooser.GetAlgorithm(ship).FindFirstReachable(ship.location, targets, ship, out TilePath p);
			if (i < 0 || p == null || p.PathLength() <= 0) {
				return null;
			}
			shipPath = p;
			return targets[i];
		}

		private static bool NearOurCoast(Player player, Tile tile) {
			foreach (City c in player.cities) {
				if (c.location.map == tile.map && c.location.DistanceTo(tile) <= CoastDistance) {
					return true;
				}
			}
			return false;
		}

		private static bool HostileShipNearCoast(Player player) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (other == player || other.defeated || !AtWar(player, other)) {
					continue;
				}
				foreach (MapUnit u in other.units) {
					if (u.IsWaterUnit() && Tile.IsTileValid(u.location) && u.location.IsWater() && (u.unitType.attack > 0 || u.unitType.bombard > 0)
							&& NearOurCoast(player, u.location) && AIFogOfWar.SeesUnitsOn(player, u.location)) {
						return true;
					}
				}
			}
			return false;
		}

		private static Tile NearestPort(MapUnit ship, Player player) {
			List<Tile> ports = player.cities.Select(c => c.location).Where(t => t.NeighborsWater())
				.OrderBy(t => t.DistanceTo(ship.location)).Take(6).ToList();
			if (ports.Count == 0) {
				return null;
			}
			int i = PathingAlgorithmChooser.GetAlgorithm(ship).FindFirstReachableIndex(ship.location, ports, ship);
			return i < 0 ? null : ports[i];
		}

		////////////////////////////////////////////////////////////////////
		// What to build

		private sealed class SeaReach {
			public int turn = -1;
			// Each water tile our ports reach, by the body of water it's in.
			public readonly Dictionary<Tile, int> body = new();
			// The bodies of water with enemy ships or coastal cities.
			public readonly HashSet<int> withTargets = new();
			// How many warships we'd like.
			public int wanted;
		}

		private static readonly ConditionalWeakTable<Player, SeaReach> seaReach = new();

		// HEURISTIC: how far (in tiles of water) from our ports the enemy's
		// ships and coastal cities count as within reach.
		private const int SeaReachDistance = 20;

		// Whether the city should build warships: we're at war with a civ
		// whose ships or coastal cities are within reach of this city's
		// waters, and have fewer warships than we'd like.
		public static bool WantsWarships(Player player, City city) {
			if (!Enabled(player) || !IsInAnyWar(player, EngineStorage.gameData.players)) {
				return false;
			}
			SeaReach reach = GetSeaReach(player);
			if (reach.wanted <= 0) {
				return false;
			}
			bool reaches = false;
			foreach (Tile n in city.location.neighbors.Values) {
				if (n != Tile.NONE && reach.body.TryGetValue(n, out int b) && reach.withTargets.Contains(b)) {
					reaches = true;
					break;
				}
			}
			if (!reaches) {
				return false;
			}
			int have = player.units.Count(IsWarship)
				+ player.cities.Count(c => c != city && c.itemBeingProduced is UnitPrototype p && IsWarship(p));
			return have < reach.wanted;
		}

		private static SeaReach GetSeaReach(Player player) {
			SeaReach reach = seaReach.GetOrCreateValue(player);
			int turn = EngineStorage.gameData?.turn ?? 0;
			if (reach.turn == turn) {
				return reach;
			}
			reach.turn = turn;
			reach.body.Clear();
			reach.withTargets.Clear();
			reach.wanted = 0;

			UnitPrototype warship = BestWarship(player);
			if (warship == null) {
				return reach;
			}
			// Each body of water our ports touch, out to SeaReachDistance.
			int bodies = 0;
			Dictionary<int, (int ships, int cities)> found = new();
			foreach (City port in player.cities) {
				foreach (Tile start in port.location.neighbors.Values) {
					if (start == Tile.NONE || !start.IsWater() || reach.body.ContainsKey(start) || !Sailable(player, warship, start)) {
						continue;
					}
					int b = bodies++;
					Queue<(Tile, int)> open = new();
					reach.body[start] = b;
					open.Enqueue((start, 0));
					HashSet<City> enemyCities = new();
					int ships = 0;
					while (open.TryDequeue(out (Tile t, int d) next)) {
						foreach (MapUnit u in next.t.unitsOnTile) {
							if (u.IsWaterUnit() && !u.owner.isBarbarians && u.owner != player && AtWar(player, u.owner)
									&& AIFogOfWar.SeesUnitsOn(player, next.t)) {
								++ships;
							}
						}
						foreach (Tile n in next.t.neighbors.Values) {
							if (n == Tile.NONE) {
								continue;
							}
							if (n.cityAtTile is City c && c.owner != player && !c.owner.isBarbarians && AtWar(player, c.owner)
									&& AIFogOfWar.KnowsTile(player, n)) {
								enemyCities.Add(c);
							}
							if (next.d < SeaReachDistance && n.IsWater() && !reach.body.ContainsKey(n) && Sailable(player, warship, n)) {
								reach.body[n] = b;
								open.Enqueue((n, next.d + 1));
							}
						}
					}
					if (ships > 0 || enemyCities.Count > 0) {
						reach.withTargets.Add(b);
						found[b] = (ships, enemyCities.Count);
					}
				}
			}
			if (found.Count > 0) {
				int ships = found.Values.Sum(f => f.ships);
				int cities = found.Values.Sum(f => f.cities);
				// A warship for each enemy ship, and one for their coast, but
				// no more than one for every four cities of ours.
				int cap = Math.Max(1, player.cities.Count / 4);
				reach.wanted = Math.Min(cap, ships + (cities > 0 ? 1 : 0));
			}
			return reach;
		}

		private static bool Sailable(Player player, UnitPrototype type, Tile t) {
			TerrainType terrain = t.baseTerrainType;
			bool atRisk = (terrain.IsOcean && type.sinksInOcean) || (terrain.IsSea && type.sinksInSea);
			return !atRisk || player.CanSailSafelyOn(terrain);
		}

		// The strongest warship the player can build, to judge the waters
		// it may sail.
		private static UnitPrototype BestWarship(Player player) {
			UnitPrototype best = null;
			foreach (UnitPrototype type in EngineStorage.gameData.unitPrototypes) {
				if (!IsWarship(type) || type.unproducible || !type.IsProducibleBy(player.civilization) || !player.HasRequiredTechnology(type)) {
					continue;
				}
				if (best == null || type.attack + type.bombard > best.attack + best.bombard) {
					best = type;
				}
			}
			return best;
		}
	}
}
