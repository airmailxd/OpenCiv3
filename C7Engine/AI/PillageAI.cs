using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// The AI pillages its enemies' land in wartime, as Civ3's AI does: it
	// may pillage at all, "There is a condition stopping AI units from
	// pillaging tiles if doing so would leave the unit itself stranded"
	// (Flintlock, reading the Civ3 code, C3X thread post-16398188;
	// compendium 1.13), and cutting an enemy city off by pillaging its roads
	// is a known way to weaken it (ainwood, CFC 61562 #1, compendium 3: "By
	// pillaging and blocking the roads, the city was cut-off from the
	// empire"). Pillaging also costs the victim war weariness.
	//
	// HEURISTIC (when, and what): after its units have moved, each unit
	// left with moves that stands on an improvement in the land of a civ
	// we're at war with pillages it. Using only leftover moves, a raid never
	// delays an attack or a march. A unit doesn't pillage:
	//   - in a city-state's or the barbarians' land, nor its own or no one's;
	//   - with a hostile land unit that can attack next to it, as it may
	//     need its moves (our reading of Civ3's "stranded" condition);
	//   - when badly wounded, as it should make for home;
	//   - around the city our war campaign means to take, a mine or
	//     irrigation we'll want to work once it's ours (its roads are cut,
	//     to keep reinforcements out).
	// A fortified unit fortifies again after pillaging.
	public static class PillageAI {
		private static readonly ILogger log = Log.ForContext(typeof(PillageAI));

		// HEURISTIC: a unit with less than this share of its hit points left
		// makes for home rather than pillage.
		public const double MinHealthShare = 0.5;

		// HEURISTIC: the tiles this near the campaign's target city are kept
		// for us to work once we take it.
		public const int TargetCityRadius = 2;

		// An army pillages for nothing (see MapUnit.Pillage), so it could go
		// on until the tile is bare: a railroad, then its road and the rest.
		private const int MaxPillagesPerUnit = 3;

		// Pillages with each of the player's units that should; returns how
		// many tiles were pillaged.
		public static int PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.IsIndependent || !gameData.players.Any(p => p != player && IsPillageable(player, p))) {
				return 0;
			}
			if (AIFogOfWar.EnabledFor(player)) {
				AIFogOfWar.Refresh(player);
			}
			Tile[] spare = SparedTiles(player);
			int pillaged = 0;
			foreach (MapUnit unit in player.units.ToArray()) {
				for (int i = 0; i < MaxPillagesPerUnit && ShouldPillage(unit, player, spare); ++i) {
					bool wasFortified = unit.isFortified;
					if (!unit.Pillage()) {
						break;
					}
					++pillaged;
					if (wasFortified) {
						unit.isFortified = true;
					}
				}
			}
			if (pillaged > 0) {
				log.Information("{Player} pillaged {Count} tiles", player, pillaged);
			}
			return pillaged;
		}

		// Whether the unit should pillage the tile it stands on.
		internal static bool ShouldPillage(MapUnit unit, Player player, IReadOnlyCollection<Tile> spare = null) {
			if (unit.owner != player || unit.IsLoaded() || unit.IsLockedInArmy() || !unit.CanPillage()) {
				return false;
			}
			Tile tile = unit.location;
			if (!IsPillageable(player, tile.OwningPlayer())) {
				return false;
			}
			if (unit.CompositeHitPoints() < unit.CompositeMaxHitPoints() * MinHealthShare) {
				return false;
			}
			if (spare != null && spare.Contains(tile) && IsWorkedLand(tile)) {
				return false;
			}
			return !HostileAttackerNextTo(tile, player);
		}

		// Whether the tile has a mine or irrigation.
		private static bool IsWorkedLand(Tile tile) {
			return tile.overlays.GetImprovements().Any(i => i.key is Tile.TileOverlays.MINE or Tile.TileOverlays.IRRIGATION);
		}

		// Land we pillage: a civ's, other than ours, that we're at war with.
		private static bool IsPillageable(Player player, Player landOwner) {
			return landOwner != null && landOwner != player && !landOwner.IsIndependent && !landOwner.defeated
				&& AtWar(player, landOwner);
		}

		// The tiles around the city our war campaign is after.
		private static Tile[] SparedTiles(Player player) {
			if (!WarCampaign.TryGetCampaign(player, out _, out City target, out _, out _) || target?.location == null) {
				return [];
			}
			return target.location.GetTilesWithinRankDistance(TargetCityRadius).ToArray();
		}

		// Whether a hostile land unit that can attack, which we can see, is
		// next to the tile.
		private static bool HostileAttackerNextTo(Tile tile, Player player) {
			bool fog = AIFogOfWar.EnabledFor(player);
			foreach (Tile n in tile.neighbors.Values) {
				if (n == null || n == Tile.NONE || n.unitsOnTile.Count == 0 || (fog && !AIFogOfWar.SeesUnitsOn(player, n))) {
					continue;
				}
				foreach (MapUnit u in n.unitsOnTile) {
					if (u.owner != player && u.IsLandUnit() && u.unitType.attack > 0 && AtWar(player, u.owner)) {
						return true;
					}
				}
			}
			return false;
		}
	}
}
