using System.Collections.Generic;
using C7GameData;
using Godot;
using Resource = C7GameData.Resource;
using Serilog;

namespace C7.Map {
	public partial class ResourceLayer : LooseLayer {
		private ILogger log = LogManager.ForContext<ResourceLayer>();

		private static readonly Vector2 resourceSize = new Vector2(50, 50);

		// Each resource's texture, so tiles don't look theirs up in the texture loader's cache.
		private readonly Dictionary<Resource, ImageTexture> textures = new(ReferenceEqualityComparer.Instance);

		// Resources belong to a game.
		public override void onGameDataReplaced(GameData gameData) {
			textures.Clear();
		}

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			Resource resource = tile.Resource;
			if (resource == Resource.NONE) {
				return;
			}

			if (!ResourceVisible(gameData, looseView, tile)) {
				return;
			}

			if (!textures.TryGetValue(resource, out ImageTexture texture)) {
				texture = TextureLoader.Load("resources.large", resource, useCache: true);
				textures[resource] = texture;
			}

			looseView.DrawTexture(texture, tileCenter - 0.5f * texture.GetSize());
		}

		// Every resource is shown while the whole map is shown, so map generation can be looked over.
		private static bool ResourceVisible(GameData gameData, LooseView looseView, Tile t) {
			if (looseView.mapView.ShowsWholeMap(gameData)) {
				return true;
			}
			return looseView.uiPlayer.KnowsAboutResource(t.Resource);
		}
	}
}
