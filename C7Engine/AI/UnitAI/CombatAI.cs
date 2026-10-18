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
				if (p.id != null && player.playerRelationships.TryGetValue(p.id, out PlayerRelationship relationship) && relationship.AtWar()) {
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
				foreach (MapUnit u in enemy.units) {
					Tile t = u.location;
					if (t != null && t != Tile.NONE && player.tileKnowledge.isTileKnown(t)
						&& AIFogOfWar.SeesUnitsOn(player, t) && u.IsSeenBy(player) && seen.Add(t)) {
						result.Add(t);
					}
				}
			}
			return result;
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
					if (u == unit || u.owner != player || u.IsLoaded() || !u.IsLandUnit() || u.currentAI is WarCampaign.MemberAI
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

		// A rough estimate of the attacker's chance to beat the defender: the
		// chance of winning each round of combat, weighed by the hit points
		// each side can lose. Bonuses that depend on where the attack comes
		// from (like river crossings) are left out.
		//
		// This is not the true chance of a race of rounds to lose all hit
		// points (it calls a fight with round odds of 0.3 a 30% chance when
		// it is nearer 16%, and 0.7 a 70% chance when it is nearer 84%), but
		// the thresholds below were tuned against it: in AI trials the exact
		// chance, with the same thresholds, did worse.
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
			double ours = roundOdds * Math.Max(1, attacker.CompositeHitPoints());
			double theirs = (1 - roundOdds) * Math.Max(1, defender.hitPointsRemaining);
			return ours / (ours + theirs);
		}
	}
}
