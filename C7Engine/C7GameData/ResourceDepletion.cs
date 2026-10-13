using System.Collections.Generic;
using C7GameData;
using Serilog;

namespace C7Engine {
	// Civ3's strategic resource depletion: a source in use may run out, and
	// a new source of the same resource then turns up elsewhere.
	//
	// - Each turn, every source a civ is using has a 1 in DisappearanceRatio
	//   chance of running out (the BIQ's "disappearance probability": Iron and
	//   Saltpeter 800, Coal and Aluminum 400, Oil 200, Uranium 100; Horses and
	//   Rubber 0, never). A ratio of 0 never runs out.
	//   https://forums.civfanatics.com/threads/when-do-resources-deplete-tested.329362/
	// - Only a source "available to a city" can run out: one inside the civ's
	//   borders and joined to one of its cities by the trade network. A roaded
	//   source not joined to a city doesn't, nor does one the civ can only
	//   reach through a colony (C3 has no colonies yet). Same thread; also
	//   "resources which are not connected to a road won't disappear",
	//   https://forums.civfanatics.com/threads/resource-exhaustion.65409/
	// - "The amount of each resource on the map is fixed - if a coal deposit
	//   becomes depleted, it reappears in another spot on the map in the same
	//   turn", and the "we have discovered a new source" message comes "if it
	//   is inside your borders".
	//   https://forums.civfanatics.com/threads/what-causes-resources-to-deplete.73775/
	// - The messages are Civ3's (Conquests/Text/script.txt): GOODDISAPPEARED,
	//   "This source of $RESOURCES0 has been exhausted!" (trade advisor,
	//   surprised), and GOODAPPEARED, "We have discovered a new source of
	//   $RESOURCES0!" (trade advisor, happy).
	//
	// UNVERIFIED: where exactly the new source goes. Players report it lands
	// anywhere on the map ("It is random", resource-exhaustion thread above;
	// one adds it is often near the old one). Here it goes on a random tile,
	// anywhere on the map, whose terrain the BIQ allows the resource on, with
	// no resource and no city, other than the tile it ran out on.
	// UNVERIFIED: one player recalls a new source appearing only a third of
	// the time on higher difficulties
	// (https://forums.civfanatics.com/threads/disappearing-resources.102155/);
	// that conflicts with the fixed-amount report, so a new source always
	// appears when there is room for it.
	// UNVERIFIED: that the civ must know the resource's tech (it isn't
	// "available" to a city before then), and that the finder of a new
	// source is only told if they know the resource too.
	public static class ResourceDepletion {
		private static readonly ILogger log = Log.ForContext(typeof(ResourceDepletion));

		// Rolls for every source in use; called once per round, after every
		// player has played (the "interturn"). Tiles are gone through in map
		// order so the rolls are the same each time a game is replayed.
		public static void ProcessEndOfRound(GameData gameData) {
			List<Tile> tiles = gameData.map?.tiles;
			if (tiles == null) {
				return;
			}
			// The sources in use are found before any runs out, so a new
			// source doesn't get a roll in the turn it appears.
			List<Tile> inUse = new();
			foreach (Tile tile in tiles) {
				if (MayRunOut(gameData, tile)) {
					inUse.Add(tile);
				}
			}
			bool changed = false;
			foreach (Tile tile in inUse) {
				if (GameData.rng.Next(tile.Resource.DisappearanceRatio) != 0) {
					continue;
				}
				RunOut(gameData, tile);
				changed = true;
			}
			if (changed) {
				gameData.InvalidateCachedTradeNetwork();
			}
		}

		// Whether the source on the tile is one a civ is using, and so may
		// run out this turn.
		public static bool MayRunOut(GameData gameData, Tile tile) {
			Resource resource = tile.Resource;
			if (resource == null || resource == Resource.NONE || resource.DisappearanceRatio <= 0) {
				return false;
			}
			Player owner = tile.OwningPlayer();
			if (owner == null || !owner.KnowsAboutResource(resource)) {
				return false;
			}
			return gameData.GetTradeNetwork().IsJoinedToCity(tile, owner);
		}

		// Whether a source of the resource may turn up on the tile.
		public static bool MayAppearOn(Resource resource, Tile tile) {
			return (tile.Resource == null || tile.Resource == Resource.NONE)
				&& tile.cityAtTile == null
				&& tile.overlayTerrainType.allowedResources.Contains(resource.Key);
		}

		// Takes the source away from the tile and puts a new one elsewhere.
		// Returns the tile the new source went on, or null if there was
		// nowhere for it.
		public static Tile RunOut(GameData gameData, Tile tile) {
			Resource resource = tile.Resource;
			Player owner = tile.OwningPlayer();
			tile.Resource = Resource.NONE;
			tile.ResourceKey = Resource.NONE.Key;
			log.Information("{Resource} at {Tile} has run out", resource.Key, tile);
			if (owner != null && owner.IsToldNews) {
				new MsgShowTradeAdvisorPopup(owner, $"This source of {resource.Name} has been exhausted!", happy: false).send();
			}

			List<Tile> candidates = new();
			foreach (Tile t in gameData.map.tiles) {
				if (t != tile && MayAppearOn(resource, t)) {
					candidates.Add(t);
				}
			}
			if (candidates.Count == 0) {
				log.Information("Nowhere for a new source of {Resource}", resource.Key);
				return null;
			}
			Tile found = candidates[GameData.rng.Next(candidates.Count)];
			found.Resource = resource;
			found.ResourceKey = resource.Key;
			log.Information("A new source of {Resource} has appeared at {Tile}", resource.Key, found);

			Player finder = found.OwningPlayer();
			if (finder != null && finder.IsToldNews && finder.KnowsAboutResource(resource)) {
				new MsgShowTradeAdvisorPopup(finder, $"We have discovered a new source of {resource.Name}!", happy: true).send();
			}
			return found;
		}
	}
}
