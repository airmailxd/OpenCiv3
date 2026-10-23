using System;
using System.Collections.Generic;
using C7GameData;

namespace C7Engine.AI {
	// Where an AI player's units are in danger from its enemies, as far as
	// it can see them (see AIFogOfWar). Used to keep workers and settlers,
	// which can't defend themselves, away from enemy units and out of the
	// land of civs we're at war with. (AI heuristics, not Civ3 rules.)
	public sealed class AIThreat {
		private readonly Player player;

		// The enemy land units we can see that could attack, and how far
		// (in tiles) each could reach next turn.
		private readonly List<(Tile at, int reach)> enemies = new();

		// Whether the other player's units may attack ours: a civ we're at
		// war with, or the barbarians.
		public static bool IsHostile(Player player, Player other) {
			return other != null && other != player && !player.IsAtPeaceWith(other);
		}

		// Whether the tile is within the borders of a civ we're at war with.
		public static bool IsHostileTerritory(Player player, Tile tile) {
			Player owner = tile.OwningPlayer();
			return owner != null && !owner.isBarbarians && IsHostile(player, owner);
		}

		// What the player can see of its enemies now (as of the last
		// AIFogOfWar.Refresh). Cheap to ask of once made, so make one per
		// decision rather than per tile.
		public static AIThreat Assess(Player player) {
			AIThreat threat = new(player);
			List<Player> players = EngineStorage.gameData?.players;
			if (players == null) {
				return threat;
			}
			foreach (Player other in players) {
				if (!IsHostile(player, other)) {
					continue;
				}
				foreach (MapUnit u in other.units) {
					Tile t = u.location;
					if (t == null || t == Tile.NONE || !u.IsLandUnit() || !u.CanAttack() || u.IsLoaded()
							|| !AIFogOfWar.SeesUnitsOn(player, t) || !u.IsSeenBy(player)) {
						continue;
					}
					// A unit's reach is its moves, counting the step onto
					// the tile; roads can carry it further, but we don't
					// run from every unit that could use one.
					threat.enemies.Add((t, Math.Max(1, u.MaxMovementPoints())));
				}
			}
			return threat;
		}

		private AIThreat(Player player) {
			this.player = player;
		}

		// Whether any enemy unit we can see could attack.
		public bool AnyEnemies => enemies.Count > 0;

		// Where the enemy units we can see stand.
		public IEnumerable<Tile> EnemyTiles {
			get {
				foreach ((Tile at, int _) in enemies) {
					yield return at;
				}
			}
		}

		// Whether an enemy we can see could reach the tile next turn.
		public bool EnemyCanReach(Tile tile) {
			foreach ((Tile at, int reach) in enemies) {
				if (at.DistanceTo(tile) <= reach) {
					return true;
				}
			}
			return false;
		}

		// Whether a unit of ours that can't fight is unsafe on the tile: it's
		// in the land of a civ we're at war with, or an enemy could reach it,
		// with no defender of ours there.
		public bool IsUnsafeForCivilian(Tile tile) {
			if (!IsHostileTerritory(player, tile) && !EnemyCanReach(tile)) {
				return false;
			}
			return !IsDefended(player, tile);
		}

		// Whether a unit of ours that can defend stands on the tile, or it's
		// one of our cities.
		public static bool IsDefended(Player player, Tile tile) {
			if (tile.cityAtTile?.owner == player) {
				return true;
			}
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u.owner == player && u.CanDefendOnLand() && !u.unitType.isWorker && !u.unitType.isSettler) {
					return true;
				}
			}
			return false;
		}
	}
}
