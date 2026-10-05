using C7GameData;
using ConvertCiv3Media;
using Godot;
using Serilog;
using System;
using System.Collections.Generic;

namespace C7.Map {
	public record struct CityGraphicsDetails(
		// A size rank of 0 is a town, 1 a city, etc.
		int sizeRank,
		int eraIndex,
		bool hasWalls
	);

	public partial class CityScene : Node2D {
		private ILogger log = LogManager.ForContext<CityScene>();

		// City textures depend only on the graphics details, so all cities share them.
		private static readonly Dictionary<CityGraphicsDetails, ImageTexture> cityTextures = new();

		private ImageTexture cityTexture;
		private TextureRect cityGraphics = new TextureRect();
		private CityLabelScene cityLabelScene;
		private City city;
		private Rules rules;
		private CityGraphicsDetails cachedDetails;
		private Vector2I tileCenter;

		// The MapView.contentVersion the city was last refreshed for.
		private int refreshedVersion;
		private bool refreshed = false;

		// Lets the CityLayer find the cities that weren't drawn.
		internal int lastDrawnPass;

		private AnimatedSprite2D disorderSprite;
		private static SpriteFrames disorderFrames = TextureLoader.LoadAnimation("animations.disorder", "disorder");

		// Textures can differ between games, so they're forgotten when a new map view is made.
		public static void ClearTextureCache() {
			cityTextures.Clear();
		}

		public CityScene(City city) {
			cityLabelScene = new CityLabelScene(city);
			this.city = city;
			this.rules = city.owner.rules;

			cachedDetails = GetCityGraphicsDetails(city);
			ConfigureCityGraphics(cachedDetails);

			AddChild(cityGraphics);
			AddChild(cityLabelScene);

			disorderSprite = new();
			disorderSprite.SpriteFrames = disorderFrames;
			disorderSprite.Animation = "disorder";
			disorderSprite.Position = tileCenter + new Vector2(0, -32);
			disorderSprite.Visible = false;
			AddChild(disorderSprite);
		}

		// Points the scene at the same city in new game data, e.g. a LAN snapshot.
		public void Rebind(City city) {
			this.city = city;
			this.rules = city.owner.rules;
			cityLabelScene.Rebind(city);
			refreshed = false;
		}

		// Updates the city's graphics and label, unless the map hasn't changed since the last update.
		public void Refresh(int contentVersion) {
			if (refreshed && contentVersion == refreshedVersion) {
				return;
			}
			refreshed = true;
			refreshedVersion = contentVersion;

			cityLabelScene.UpdateContent();

			CityGraphicsDetails details = GetCityGraphicsDetails(city);
			if (cachedDetails != details) {
				cachedDetails = details;
				ConfigureCityGraphics(cachedDetails);
				PositionCityGraphics();
			}

			UpdateDisorder();
		}

		// Shows or hides the whole scene, e.g. when the city is off screen.
		public void SetShown(bool shown) {
			if (Visible == shown) {
				return;
			}
			Visible = shown;
			UpdateDisorder();
		}

		// The disorder animation only plays while it can be seen.
		private void UpdateDisorder() {
			bool inDisorder = city.isInCivilDisorder;
			disorderSprite.Visible = inDisorder;
			if (inDisorder && Visible) {
				if (!disorderSprite.IsPlaying()) {
					disorderSprite.Play("disorder");
				}
			} else if (disorderSprite.IsPlaying()) {
				disorderSprite.Pause();
			}
		}

		public void SetTileCenter(Vector2I tileCenter) {
			this.tileCenter = tileCenter;

			disorderSprite.Position = tileCenter + new Vector2(0, -32);
			cityLabelScene.SetTileCenter(tileCenter);
			PositionCityGraphics();
		}

		private void PositionCityGraphics() {
			cityGraphics.Position = new(
				tileCenter.X - (float)0.5 * cityTexture.GetWidth(),
				tileCenter.Y - (float)0.5 * cityTexture.GetHeight()
			);
		}

		private CityGraphicsDetails GetCityGraphicsDetails(City c) {
			CityGraphicsDetails result = new() {
				sizeRank = 0,
				hasWalls = false,
			};
			if (c.residents.Count > rules.MaximumLevel1CitySize) {
				++result.sizeRank;

				// Walls are only displayed for towns, not cities or metropolises
			} else {
				result.hasWalls = c.HasWalls();
			}
			if (c.residents.Count > rules.MaximumLevel2CitySize) {
				++result.sizeRank;
			}
			result.eraIndex = city.owner.EraIndex();
			return result;
		}

		//TODO: Support multiple city flavors and walls.
		private void ConfigureCityGraphics(CityGraphicsDetails details) {
			if (!cityTextures.TryGetValue(details, out cityTexture)) {
				cityTexture = TextureLoader.Load("cities", details);
				cityTextures[details] = cityTexture;
			}

			cityGraphics.MouseFilter = Control.MouseFilterEnum.Ignore;
			cityGraphics.Texture = cityTexture;
		}

		public void HideLabel() => cityLabelScene?.Hide();
		public void ShowLabel() => cityLabelScene?.Show();
	}
}
