using C7Engine;
using Serilog;
using static C7GameData.Tile.TileOverlays;
using Layer = C7GameData.TerrainImprovement.Layer;

namespace C7GameData;

// Outposts and colonies: holdings a worker builds for its civ, which belong
// to that civ. A tile has at most one of them. An outpost sits on the
// tile's holdings layer, so it doesn't go with a fortress; a colony has a
// layer of its own, as Civ3 lets a colony share its tile with a fortress
// (one of the Civ3 saves looked at has a tile with both).
//
// Outposts, from the Conquests Civilopedia (GCON_Outposts, TFRM_Outpost):
//   "Outposts eliminate fog of war. To construct an Outpost, move a worker
//   to any land tile in neutral or owned territory and select the Build
//   Outpost unit action. Construction of the Outpost consumes the worker.
//   The range of vision for the Outpost is 2 tiles on flat terrain, 3 on
//   hills and 4 on mountains. Outposts can only be used by the player who
//   created them. If an Outpost falls within the territory of any other
//   civilization, it will be destroyed. Outposts can only be built in a tile
//   that is empty, has a mine, irrigation, a road and/or a railroad. You
//   cannot build an Outpost on a tile with an Airfield or a Colony. The
//   "Build Outpost" unit-action becomes available after the Masonry advance
//   is researched."
//
// Colonies, from the same Civilopedia (TFRM_Build_Colony, GCON_Colony):
//   "A worker can establish a colony on any square that contains a tradable
//   resource. If the colony is then connected to a city, that city gains
//   access to the resource. A colony is necessary only if the resource is
//   not inside your borders. Note that when a city's borders expand to
//   include a colony, the colony disappears because it is no longer needed.
//   The worker is consumed by building a colony."
// and "[Colonies] ... enable exploitation of tradable resources that lie
// outside your territory. As with all tradable resources (strategic and
// luxury), you need a road connecting the colony to one of your cities to
// get the benefits of the resource."
public partial class Tile {
	private static readonly ILogger holdingLog = Log.ForContext<Tile>();

	// The civ whose outpost or colony is on the tile, or null if there is
	// none. An outpost or colony placed by a scenario without an owner has
	// none either, and does nothing for anyone.
	public Player holdingOwner;

	public bool HasOutpost() {
		return overlays.HasImprovementWithKey(OUTPOST, Layer.Holdings);
	}

	public bool HasColony() {
		return overlays.HasImprovementWithKey(COLONY, Layer.Colonies);
	}

	// The civ whose outpost is on the tile, or null.
	public Player OutpostOwner() {
		return holdingOwner != null && HasOutpost() ? holdingOwner : null;
	}

	// The civ whose colony is on the tile, or null.
	public Player ColonyOwner() {
		return holdingOwner != null && HasColony() ? holdingOwner : null;
	}

	// How far an outpost here sees: "2 tiles on flat terrain, 3 on hills and
	// 4 on mountains". In the standard rules hills have a height of 2 and
	// mountains (and volcanoes) of 4; anything lower counts as flat.
	public int OutpostSightRange() {
		int height = overlayTerrainType.height;
		if (height >= 4) {
			return 4;
		}
		if (height >= 2) {
			return 3;
		}
		return 2;
	}

	// The tile's outpost or colony, or null.
	public TerrainImprovement OwnedHolding() {
		TerrainImprovement colony = overlays.ImprovementAtLayer(Layer.Colonies);
		if (colony != null && IsOwnedHolding(colony)) {
			return colony;
		}
		TerrainImprovement holding = overlays.ImprovementAtLayer(Layer.Holdings);
		return holding != null && IsOwnedHolding(holding) ? holding : null;
	}

	// Whether the player's worker could build an outpost here: a land tile
	// that is neutral or the player's own, without a city, and without a
	// fortress, colony or other holding (mines, irrigation and roads are
	// fine).
	public bool CanTakeOutpost(Player player) {
		if (!IsLand() || HasCity()) {
			return false;
		}
		Player owner = OwningPlayer();
		if (owner != null && owner != player) {
			return false;
		}
		return overlays.ImprovementAtLayer(Layer.Holdings) == null && overlays.ImprovementAtLayer(Layer.Colonies) == null;
	}

	// Whether the player's worker could found a colony here: a land tile
	// with a strategic or luxury resource the player knows about, outside
	// every civ's borders (inside them it would be taken in by the borders at
	// once), without a city, a colony or an outpost. A fortress is fine.
	// UNVERIFIED: the Civilopedia says "any square that contains a tradable
	// resource"; whether Civ3 lets a colony go up inside a rival's borders,
	// or next to an outpost, isn't stated. Since borders take a colony in,
	// it is only offered outside them, and one civ's holding isn't put on
	// another's.
	public bool CanTakeColony(Player player) {
		if (!IsLand() || HasCity() || Resource == null || Resource == Resource.NONE) {
			return false;
		}
		if (Resource.Category != ResourceCategory.STRATEGIC && Resource.Category != ResourceCategory.LUXURY) {
			return false;
		}
		if (OwningPlayer() != null || OwnedHolding() != null) {
			return false;
		}
		return player.KnowsAboutResource(Resource);
	}

	// Puts the player's outpost or colony on the tile. An outpost shows its
	// owner what it can see at once; a colony may give them a resource.
	internal void PlaceHolding(TerrainImprovement holding, Player owner) {
		TryRemoveRuins(this);
		overlays.Add(holding);
		holdingOwner = owner;
		TileChangeJournal.Record(this);
		if (holding.key == COLONY) {
			EngineStorage.gameData?.InvalidateCachedTradeNetwork();
		} else if (holding.key == OUTPOST) {
			owner?.tileKnowledge.AddTilesToKnown(this);
		}
	}

	// Called when an outpost or colony leaves the tile, however it goes.
	internal void ForgetHolding(TerrainImprovement holding) {
		holdingOwner = null;
		TileChangeJournal.Record(this);
		if (holding.key == COLONY) {
			EngineStorage.gameData?.InvalidateCachedTradeNetwork();
		}
	}

	// Called when the city whose borders hold the tile changes. Borders take
	// in any colony, and destroy another civ's outpost.
	internal void CheckHoldingAgainstBorders() {
		City city = owningCity;
		if (city == null) {
			return;
		}
		TerrainImprovement holding = OwnedHolding();
		if (holding == null) {
			return;
		}
		if (holding.key == COLONY || city.owner != holdingOwner) {
			holdingLog.Information("The borders of {City} take in the {Holding} of {Owner} at {Tile}", city.name, holding.key, holdingOwner, this);
			overlays.Remove(holding);
		}
	}
}
