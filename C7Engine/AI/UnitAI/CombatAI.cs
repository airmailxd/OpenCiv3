using System.Collections.Generic;
using C7GameData;
using C7GameData.Save;
using C7GameData.AIData;
using Serilog;
using System;
using C7Engine.Pathing;
using System.Linq;

namespace C7Engine.AI.UnitAI {
	/// <summary>
	/// A unit whose intended role is combat, other than purely point-defense combat.
	///
	/// This is likely to evolve significantly, and likely have sub-classes; this is the very first iteration.
	/// This first iteration is focused on defeating barbarians.
	/// </summary>
	public class CombatAI : C7GameData.UnitAI {
		internal CombatAIData data;

		private static ILogger log = Log.ForContext<CombatAI>();

		public CombatAI(CombatAIData d) {
			data = d;
		}

		public static CombatAIData? MakeAiData(MapUnit unit, Player player) {
			AIFogOfWar.Refresh(player);
			CombatAIData? result = GetBestTileToAttack(unit, player, GetPlayersAtWarWith(player));
			if (result == null) {
				return result;
			}

			log.Information($"{unit} is planning to attack {result.destination}");
			return result;
		}

		// Whether PlayTurnImpl should look for a better target now. Doing so
		// is a full target search, so it's done at most once per turn for a
		// given position of the unit and state of the tiles around it.
		private bool ShouldReevaluateTarget(MapUnit unit) {
			int turn = EngineStorage.gameData?.turn ?? 0;
			int signature = NeighborhoodSignature(unit.location);
			if (data.lastReevaluationTurn == turn
				&& data.lastReevaluationLocation == unit.location
				&& data.lastReevaluationSignature == signature) {
				return false;
			}
			data.lastReevaluationTurn = turn;
			data.lastReevaluationLocation = unit.location;
			data.lastReevaluationSignature = signature;
			return true;
		}

		// Summarizes the occupants of the tiles around `center`, so that we
		// notice when a fight nearby changes the situation.
		private static int NeighborhoodSignature(Tile center) {
			HashCode hash = new();
			foreach (Tile t in center.neighbors.Values) {
				hash.Add(t.unitsOnTile.Count);
				hash.Add(t.unitsOnTile.Count > 0 ? t.unitsOnTile[0].owner : null);
				hash.Add(t.cityAtTile?.owner);
				hash.Add(t.hasBarbarianCamp);
			}
			return hash.ToHashCode();
		}

		public void UpdateOnDeath() { }

		public C7GameData.UnitAI.MoveResult PlayTurnImpl(Player player, MapUnit unit) {
			if (data == null) {
				return C7GameData.UnitAI.Result.Error;
			}

			if (unit.location == data.destination) {
				return C7GameData.UnitAI.Result.Done;
			}

			// If there is a no longer an enemy on the tile we were heading
			// towards, ensure we recalculate our plan. With the AI fog of war
			// we only know that once we can see the tile again.
			Tile dest = data.destination;
			AIFogOfWar.Refresh(player);
			bool canSeeDestination = AIFogOfWar.SeesUnitsOn(player, dest);
			List<MapUnit> destUnits = dest.UnitsSeenBy(player);
			bool destinationHasEnemyUnits = !canSeeDestination || (destUnits.Count > 0
				&& !destUnits[0].owner.IsAtPeaceWith(unit.owner));
			bool destinationHasEnemyCity = dest.cityAtTile != null
				&& !data.destination.cityAtTile.owner.IsAtPeaceWith(unit.owner);
			bool destinationHasBarbCamp = dest.hasBarbarianCamp;

			if (!destinationHasEnemyCity && !destinationHasEnemyUnits && !destinationHasBarbCamp) {
				return C7GameData.UnitAI.Result.Done;
			}

			// See if we need to re-evaluate our target if we walk next to an
			// enemy unit or city. We don't return an error here to avoid the
			// case of looping forever if our target didn't change.
			if (data.path.PathLength() > 1) {
				foreach (Tile t in unit.location.neighbors.Values) {
					bool nextToEnemyCity = t.cityAtTile != null && !t.cityAtTile.owner.IsAtPeaceWith(unit.owner);
					List<MapUnit> seenThere = t.UnitsSeenBy(player);
					bool nextToEnemyUnit = seenThere.Count > 0 && !seenThere[0].owner.IsAtPeaceWith(unit.owner);
					bool nextToBarbCamp = t.hasBarbarianCamp;

					if (!nextToEnemyCity && !nextToEnemyUnit && !nextToBarbCamp) {
						continue;
					}

					// Once we re-evaluate once, break out of the loop. There's
					// no point in doing it again if nothing changed.
					if (!ShouldReevaluateTarget(unit)) {
						break;
					}
					CombatAIData? maybeData = MakeAiData(unit, player);
					if (maybeData != null && maybeData.destination != data.destination) {
						maybeData.lastReevaluationTurn = data.lastReevaluationTurn;
						maybeData.lastReevaluationLocation = data.lastReevaluationLocation;
						maybeData.lastReevaluationSignature = data.lastReevaluationSignature;
						data = maybeData;
					}
					break;
				}
			}

			// Before a step onto enemies, look at the odds again: the
			// defenders may have changed, or not have been visible when
			// the plan was made.
			Tile next = data.path?.PeekNext() ?? Tile.NONE;
			if (next != Tile.NONE && next.unitsOnTile.Any(u => !u.owner.IsAtPeaceWith(unit.owner))) {
				MapUnit defender = next.FindTopDefender(unit);
				if (MapUnit.IsMapUnitValid(defender)) {
					double odds = EstimateChanceToWin(unit, defender);
					bool many = next.cityAtTile != null || next.unitsOnTile.Count(u => u.CanDefendOnLand() && !u.owner.IsAtPeaceWith(unit.owner)) > 1;
					if (odds < MIN_CHANCE_TO_ATTACK
							|| (many && !player.isBarbarians && odds < SURE_CHANCE && !EnoughForceNearby(unit, player, next, next.cityAtTile != null))) {
						log.Debug("{Unit} calls off its attack on {Tile} at odds of {Odds}", unit, next, odds);
						return C7GameData.UnitAI.Result.Done;
					}
				}
			}

			// Move along the path, initiating combat if we reach our target.
			return this.TryToMoveAlongPath(unit, ref data.path);
		}

		public string SummarizePlan() {
			return "CombateAI: " + data.ToString();
		}

		// The players whose units and cities we may attack.
		private static HashSet<Player> GetPlayersAtWarWith(Player player) {
			HashSet<Player> enemies = new(ReferenceEqualityComparer.Instance);
			List<Player> players = EngineStorage.gameData?.players;
			if (players == null) {
				return enemies;
			}

			// Special case: barbarians are at war with all other players but
			// don't have player relationships with them.
			if (player.isBarbarians) {
				foreach (Player p in players) {
					if (!p.isBarbarians) {
						enemies.Add(p);
					}
				}
				return enemies;
			}

			foreach (Player p in players) {
				// Barbarians have no relationship with us, but are always
				// enemies (only those near our land are targets, see
				// GetCandidateTiles).
				if (p.isBarbarians || (p.id != null && player.playerRelationships.TryGetValue(p.id, out PlayerRelationship relationship) && relationship.AtWar())) {
					enemies.Add(p);
				}
			}
			return enemies;
		}

		// Barbarians don't have unified knowledge across their "empire", only
		// local knowledge, so they only consider tiles this close to the unit.
		private const int BARBARIAN_TARGET_RANGE = 4;

		// Returns the known tiles that may hold an enemy unit or city, in a
		// stable order. Only the tiles enemies are on are worth scoring, so
		// rather than scanning every known tile we look at where the enemies
		// are (or, for barbarians, at the few tiles in range).
		private static List<Tile> GetCandidateTiles(MapUnit unit, Player player, HashSet<Player> enemies) {
			List<Tile> result = new();
			HashSet<Tile> seen = new(ReferenceEqualityComparer.Instance);
			Tile here = unit.location;

			if (player.isBarbarians) {
				if (here == null || here == Tile.NONE || here.map == null) {
					return result;
				}
				// DistanceTo is (|dx| + |dy|) / 2 in tile coordinates, so the
				// tiles in range form a diamond.
				int reach = 2 * BARBARIAN_TARGET_RANGE;
				for (int dy = -reach; dy <= reach; ++dy) {
					int width = reach - Math.Abs(dy);
					for (int dx = -width; dx <= width; ++dx) {
						Tile t = here.map.tileAt(here.XCoordinate + dx, here.YCoordinate + dy);
						if (t == Tile.NONE || t.DistanceTo(here) > BARBARIAN_TARGET_RANGE || !player.tileKnowledge.isTileKnown(t)) {
							continue;
						}
						if (seen.Add(t)) {
							result.Add(t);
						}
					}
				}
				return result;
			}

			foreach (Player enemy in enemies) {
				foreach (City c in enemy.cities) {
					Tile t = c.location;
					if (t != null && t != Tile.NONE && player.tileKnowledge.isTileKnown(t) && seen.Add(t)) {
						result.Add(t);
					}
				}
				// With the AI fog of war, only the units we can see now.
				// Barbarian units only in or near our land: chasing them
				// across the map would draw units away from home.
				foreach (MapUnit u in enemy.units) {
					Tile t = u.location;
					if (enemy.isBarbarians && t != null && t != Tile.NONE && !NearOurLand(player, t)) {
						continue;
					}
					if (t != null && t != Tile.NONE && player.tileKnowledge.isTileKnown(t)
						&& AIFogOfWar.SeesUnitsOn(player, t) && u.IsSeenBy(player) && seen.Add(t)) {
						result.Add(t);
					}
				}
			}
			return result;
		}

		// HEURISTIC: how near one of our cities a barbarian unit outside our
		// borders must be for our units to go after it.
		private const int BARBARIAN_HUNT_RANGE = 3;

		// Whether a barbarian unit we can see stands in or near our land
		// (see BARBARIAN_HUNT_RANGE).
		public static bool BarbariansNearOurLand(Player player) {
			List<Player> players = EngineStorage.gameData?.players;
			if (players == null || player.isBarbarians) {
				return false;
			}
			foreach (Player p in players) {
				if (!p.isBarbarians) {
					continue;
				}
				foreach (MapUnit u in p.units) {
					Tile t = u.location;
					if (t != null && t != Tile.NONE && u.IsLandUnit() && NearOurLand(player, t)
							&& AIFogOfWar.SeesUnitsOn(player, t) && u.IsSeenBy(player)) {
						return true;
					}
				}
			}
			return false;
		}

		private static bool NearOurLand(Player player, Tile t) {
			return t.OwningPlayer() == player || player.cities.Any(c => c.location.DistanceTo(t) <= BARBARIAN_HUNT_RANGE);
		}

		private static CombatAIData? GetBestTileToAttack(MapUnit unit, Player player, HashSet<Player> enemies) {
			// First we want to check all the tiles in our visible knowledge for
			// enemy units. As of 2025-03-09, we don't track known and visible
			// tiles separately, so this should be updated in the future.
			List<(Tile tile, float score)> scoredTiles = new();
			PathingShortcuts.WaterBodies waterBodies = null;
			foreach (Tile t in GetCandidateTiles(unit, player, enemies)) {
				// FindFirstReachable would never pick a tile we can't possibly
				// get to (e.g. on another continent), so don't score it.
				// Leaving it out of the stable sort keeps the others' order.
				if (PathingShortcuts.IsTriviallyUnreachable(unit.location, t, unit, ref waterBodies)) {
					continue;
				}
				float score = ScoreTile(t, unit, player, enemies);
				if (score == int.MinValue) {
					continue;
				}

				scoredTiles.Add((t, score));
			}

			if (scoredTiles.Count == 0) {
				return null;
			}

			// Find the best target we can reach, in order of preference. This
			// is a stable sort, so ties keep the enumeration order.
			List<Tile> sortedTiles = scoredTiles.OrderByDescending(x => x.score).Select(x => x.tile).ToList();
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			int index = algorithm.FindFirstReachable(unit.location, sortedTiles, unit, out TilePath path);
			if (index < 0) {
				return null;
			}

			CombatAIData result = new();
			result.destination = sortedTiles[index];
			result.path = path;
			return result;
		}

		private static float ScoreTile(Tile t, MapUnit unit, Player player, HashSet<Player> enemies) {
			bool hasEnemyCity = t.cityAtTile != null && enemies.Contains(t.cityAtTile.owner);
			// With the AI fog of war, the units in a city we can't see now
			// are unknown: we assume it is defended, by nobody in particular.
			bool seesUnits = AIFogOfWar.SeesUnitsOn(player, t);
			List<MapUnit> seenUnits = seesUnits ? t.UnitsSeenBy(player) : null;
			bool hasEnemyUnits = seesUnits ? seenUnits.Count > 0 && enemies.Contains(seenUnits[0].owner) : hasEnemyCity;
			float score = 0;

			// Ignore tiles without units or cities.
			if (!hasEnemyCity && !hasEnemyUnits) {
				return int.MinValue;
			}

			// Ignore land tiles for sea units, and vice versa.
			if (t.IsLand() != unit.IsLandUnit()) {
				return int.MinValue;
			}

			// Don't throw units away on defenders they are unlikely to beat.
			// FindTopDefender gives MapUnit.NONE, not null, when there's none.
			MapUnit defender = hasEnemyUnits && seesUnits ? t.FindTopDefender(unit) : MapUnit.NONE;
			double winChance = MapUnit.IsMapUnitValid(defender) ? EstimateChanceToWin(unit, defender) : 1.0;
			if (winChance < MIN_CHANCE_TO_ATTACK) {
				return int.MinValue;
			}
			if (MapUnit.IsMapUnitValid(defender) && !player.isBarbarians
					&& winChance < SURE_CHANCE && !EnoughForceNearby(unit, player, t, hasEnemyCity)) {
				return int.MinValue;
			}

			// Handle the case of units running around.
			//
			// Note: Civ3 rates unit strength using this formula:
			//   HP * (1.5* AttackPoints + 1 * DefensePoints ) 0.175 * Bombard Points
			//   (https://forums.civfanatics.com/threads/study-of-inner-workings-of-military-advisor.83599/)
			//
			// TODO: We should figure out how to incorporate this.
			if (defender != null && !hasEnemyCity) {
				// Units in our territory are a threat.
				if (t.owningCity != null && t.owningCity.owner == player) {
					score += 3 * defender.unitType.attack;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					// Units next to our cities are an even bigger threat. Note
					// that it is possible for an enemy unit to be next to a
					// city but not in our borders if two cities are very close
					// together.
					if (neighbor.cityAtTile != null && neighbor.cityAtTile.owner == player) {
						score += 3 * defender.unitType.attack;
					}

					if (neighbor == unit.location) {
						// Units next to us are also a threat.
						score += 1 * defender.unitType.attack;
					}
				}
			}

			if (hasEnemyCity) {
				// Unguarded cities are pretty tempting.
				if (!hasEnemyUnits) {
					score += 20;
				} else {
					// TODO: we really want stack attacks
					score += 10;
				}
			}

			// Prefer fights we are more likely to win.
			if (score > 0) {
				score *= (float)winChance;
			}

			// Prefer to attack nearer targets.
			score -= (float)Math.Pow(t.DistanceTo(unit.location), 2);

			return score;
		}

		// Below this estimated chance of winning, a unit doesn't attack.
		private const double MIN_CHANCE_TO_ATTACK = 0.3;

		// At this estimated chance of winning, a unit attacks a city or a
		// stack even alone: it's very likely to kill a defender.
		private const double SURE_CHANCE = 0.7;

		// How near (in tiles) a unit of ours must be to the tile to count as
		// joining in an attack on it.
		private const int NEARBY_FORCE_RANGE = 2;

		// HEURISTIC: how many more hits than the defenders have the attackers
		// must expect to deal (as for a war campaign's assault).
		private const double FORCE_MARGIN = 1.3;

		// Whether a city, or a stack of more than one defender, is worth
		// attacking: our attackers near it (this one too), together, can
		// expect to wear down every defender, so the attacks take the city or
		// destroy the stack rather than trickle in one at a time and die.
		// A lone defender in the open is fought by the odds alone.
		private static bool EnoughForceNearby(MapUnit unit, Player player, Tile t, bool isCity) {
			int defenders = 0;
			foreach (MapUnit u in t.unitsOnTile) {
				if (!u.IsLoaded() && !u.owner.IsAtPeaceWith(player) && u.CanDefendOnLand()) {
					++defenders;
				}
			}
			if (!isCity && defenders < 2) {
				return true;
			}
			double hits = WarCampaign.ExpectedHits(unit, t.FindTopDefender(unit));
			foreach (Tile near in t.GetTilesWithinRankDistance(NEARBY_FORCE_RANGE)) {
				foreach (MapUnit u in near.unitsOnTile) {
					// Only units free to join in: not garrisons dug in, units
					// healing, or campaign members.
					if (u == unit || u.owner != player || u.IsLoaded() || !u.IsLandUnit() || u.currentAI is WarCampaign.MemberAI
							|| u.isFortified || u.currentAI is RestAI
							|| !(WarCampaign.IsAttacker(u) || (u.IsArmy() && u.PassengerCount() > 0))) {
						continue;
					}
					MapUnit defender = t.FindTopDefender(u);
					if (MapUnit.IsMapUnitValid(defender)) {
						hits += WarCampaign.ExpectedHits(u, defender);
					}
				}
			}
			return hits >= FORCE_MARGIN * WarCampaign.DefenderHitPoints(player, t);
		}

		// The attacker's chance to beat the defender. Civ3 combat is a
		// series of rounds, each won by the attacker with probability
		// attack / (attack + defense), the loser of a round losing a hit
		// point, until one side has none left; so this is the chance the
		// attacker takes all the defender's hit points before losing all
		// its own. (A simple ratio of round odds and hit points, used before,
		// called a fight with round odds of 0.3 a 30% chance when it is
		// nearer 16%.) Bonuses that depend on where the attack comes from
		// (like river crossings) are left out, as are retreats.
		internal static double EstimateChanceToWin(MapUnit attacker, MapUnit defender) {
			double attack = attacker.StrengthVersus(defender, CombatRole.Attack, null);
			double defense = defender.StrengthVersus(attacker, CombatRole.Defense, null);
			if (attack <= 0) {
				return 0;
			}
			if (defense <= 0) {
				return 1;
			}
			double roundOdds = attack / (attack + defense);
			// An army fights with all its members' hit points; for other
			// units this is just their own.
			return ChanceToWinRounds(roundOdds, Math.Max(1, attacker.CompositeHitPoints()), Math.Max(1, defender.hitPointsRemaining));
		}

		// The chance that a side winning each round with probability p, with
		// `ours` hit points, takes all `theirs` before losing all its own.
		internal static double ChanceToWinRounds(double p, int ours, int theirs) {
			// win[d] is the chance to win from the current row's attacker
			// hit points with d defender hit points left; win[0] = 1.
			double[] win = new double[theirs + 1];
			// With 0 attacker hit points every fight is lost, except one
			// already won.
			win[0] = 1;
			for (int a = 1; a <= ours; ++a) {
				// From (a, d): win the round to (a, d - 1), or lose it to
				// (a - 1, d), which is the previous row's value.
				for (int d = 1; d <= theirs; ++d) {
					win[d] = p * win[d - 1] + (1 - p) * win[d];
				}
			}
			return win[theirs];
		}
	}
}
