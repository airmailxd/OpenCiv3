using System.Collections.Generic;
using C7GameData;
using Godot;
using Serilog;

namespace C7.Map {
	public class CityLayer : LooseLayer {
		private Dictionary<City, CityScene> citySceneLookup = new();

		// Counts the times the layer was drawn, so scenes for cities that weren't drawn can be hidden.
		private int drawPass = 0;

		public CityLayer() {
			CityScene.ClearTextureCache();
		}

		public void UpdateAfterCityDestruction(City city) {
			EraseCity(city);
		}

		// The city is redrawn in its new owner's colors on the next draw.
		public void UpdateAfterCityCapture(City city) {
			EraseCity(city);
		}

		private void EraseCity(City city) {
			citySceneLookup.Remove(city, out CityScene cityScene);
			cityScene?.QueueFree();
		}

		// Keeps the scenes of cities that are still there with the same owner, pointing them at the new city objects.
		public override void onGameDataReplaced(GameData gameData) {
			Dictionary<ID, City> newCities = new();
			foreach (City city in gameData.cities) {
				newCities[city.id] = city;
			}

			Dictionary<City, CityScene> oldLookup = citySceneLookup;
			citySceneLookup = new();
			foreach ((City oldCity, CityScene scene) in oldLookup) {
				if (newCities.TryGetValue(oldCity.id, out City newCity) && newCity.owner?.id == oldCity.owner?.id) {
					scene.Rebind(newCity);
					citySceneLookup[newCity] = scene;
				} else {
					scene.QueueFree();
				}
			}
		}

		public override void onBeginDraw(LooseView looseView, GameData gameData) {
			++drawPass;
		}

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			if (tile.cityAtTile is null) {
				return;
			}

			City city = tile.cityAtTile;
			citySceneLookup.TryGetValue(city, out CityScene scene);

			// Hide the city while the tile info box covers it.
			if (looseView.IsTileCoveredByTileInfo(tile)) {
				if (scene != null) {
					scene.lastDrawnPass = drawPass;
					scene.SetShown(false);
				}
				return;
			}

			Vector2I tileCenter2I = new((int)tileCenter.X, (int)tileCenter.Y);

			if (scene == null) {
				scene = new CityScene(city);
				looseView.AddChild(scene);
				citySceneLookup[city] = scene;
			}

			scene.lastDrawnPass = drawPass;
			scene.SetTileCenter(tileCenter2I);
			scene.SetShown(true);
			scene.Refresh(looseView.mapView.contentVersion);

			if (looseView.HasCityLabelToHideFromTileInfo(tile))
				scene.HideLabel();
			else
				scene.ShowLabel();
		}

		// Hide the cities that are no longer in view, so they're not processed.
		public override void onEndDraw(LooseView looseView, GameData gameData) {
			foreach (CityScene scene in citySceneLookup.Values) {
				if (scene.lastDrawnPass != drawPass) {
					scene.SetShown(false);
				}
			}
		}
	}
}
