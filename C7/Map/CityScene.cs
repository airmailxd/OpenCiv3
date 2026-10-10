using C7GameData;
using ConvertCiv3Media;
using Godot;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7Engine;

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
		private bool positioned = false;

		// The MapView.contentVersion the city was last refreshed for, and the city's stamp then; see CityStamp.
		private int refreshedVersion;
		private long refreshedStamp;
		private bool refreshed = false;
		private bool refreshedShowDetails;

		// Lets the CityLayer find the cities that weren't drawn.
		internal int lastDrawnPass;

		private AnimatedSprite2D disorderSprite;
		// Loaded when first needed, and forgotten with the other textures.
		private static SpriteFrames disorderFrames;
		private static SpriteFrames DisorderFrames => disorderFrames ??= TextureLoader.LoadAnimation("animations.disorder", "disorder");

		// The size of Civ3's barracks, harbor and airport icons. Other art is shrunk to fit.
		private static readonly Vector2 BuildingIconSize = new(25, 17);

		// An icon Civ3 draws by the player's own cities for one of its buildings, made when the city first has one.
		private class BuildingIcon(string textureKey, Vector2 offset) {
			public readonly string textureKey = "city_building_icons." + textureKey;
			// Where the icon's center is from the tile's center.
			public readonly Vector2 offset = offset;
			public TextureRect rect;
			public Building shown;
		}

		// Placed as in Civ3: the sword to the right of the city and the anchor to its lower left. The airport's
		// place is a guess, mirroring the anchor.
		private readonly BuildingIcon[] buildingIcons = [
			new("barracks", new(70, -20)),
			new("harbor", new(-62, 8)),
			new("airport", new(62, 8)),
		];

		// Textures can differ between games, so they're forgotten when a new map view is made.
		public static void ClearTextureCache() {
			cityTextures.Clear();
			disorderFrames = null;
			CityLabelScene.ClearTextureCache();
		}

		// The city shown.
		public City City => city;

		// The owner the city is shown for, to notice when the city changes hands.
		private Player shownOwner;

		public CityScene(City city) {
			cityLabelScene = new CityLabelScene(city);
			this.city = city;
			this.rules = city.owner.rules;
			this.shownOwner = city.owner;

			cachedDetails = GetCityGraphicsDetails(city);
			ConfigureCityGraphics(cachedDetails);

			cityGraphics.TextureFilter = ModernGraphics.SpriteFilter;
			AddChild(cityGraphics);
			AddChild(cityLabelScene);

			disorderSprite = new();
			disorderSprite.SpriteFrames = DisorderFrames;
			disorderSprite.Animation = "disorder";
			disorderSprite.Position = tileCenter + new Vector2(0, -32);
			disorderSprite.Visible = false;
			AddChild(disorderSprite);
		}

		// Points the scene at the same city in new game data, e.g. a LAN snapshot.
		public void Rebind(City city) {
			this.city = city;
			this.rules = city.owner.rules;
			this.shownOwner = city.owner;
			cityLabelScene.Rebind(city);
			refreshed = false;
		}

		// Updates the city's graphics and label, unless the map hasn't changed since the last update.
		// The label only shows growth and production when showDetails is set, i.e. for the player's own cities.
		public void Refresh(int contentVersion, bool showDetails) {
			if (refreshed && contentVersion == refreshedVersion && showDetails == refreshedShowDetails) {
				return;
			}
			// The map changes all the time while the AI plays, but most cities don't, so only update the ones that may have.
			long stamp = CityStamp();
			bool changed = !refreshed || stamp != refreshedStamp || showDetails != refreshedShowDetails;
			refreshed = true;
			refreshedVersion = contentVersion;
			refreshedStamp = stamp;
			refreshedShowDetails = showDetails;
			if (!changed) {
				return;
			}

			// A captured city is shown in its new owner's colors.
			if (city.owner != shownOwner) {
				shownOwner = city.owner;
				rules = city.owner.rules;
				cityLabelScene.UpdateCivColor();
			}

			cityLabelScene.UpdateContent(showDetails);

			// Like growth and production, the buildings are only shown for the player's own cities.
			ShowBuildingIcon(buildingIcons[0], showDetails ? city.Barracks() : null);
			ShowBuildingIcon(buildingIcons[1], showDetails ? city.Harbor() : null);
			ShowBuildingIcon(buildingIcons[2], showDetails ? city.Airport() : null);

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
			if (positioned && this.tileCenter == tileCenter) {
				return;
			}
			positioned = true;
			this.tileCenter = tileCenter;

			disorderSprite.Position = tileCenter + new Vector2(0, -32);
			cityLabelScene.SetTileCenter(tileCenter);
			PositionCityGraphics();
			foreach (BuildingIcon icon in buildingIcons) {
				PositionBuildingIcon(icon);
			}
		}

		// Shows the icon for the building, or hides it when there's no building.
		// Returns a value that changes whenever anything the city's graphics and label show may have: its owner and their era, name, size,
		// capital and disorder, what it's building, its food and shields, the tiles its citizens work and its buildings. Anything else that
		// changes the yields its growth and production are worked out from, like a mine finished nearby, only changes between turns, so the
		// turn is part of it too.
		private long CityStamp() {
			long stamp = Mix(17, EngineStorage.gameData?.turn ?? 0);
			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(city.owner));
			stamp = Mix(stamp, city.name?.GetHashCode() ?? 0);
			stamp = Mix(stamp, city.owner.EraIndex());
			stamp = Mix(stamp, (city.IsCapital() ? 1 : 0) | (city.isInCivilDisorder ? 2 : 0));
			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(city.itemBeingProduced));
			stamp = Mix(stamp, ((long)city.foodStored << 32) | (uint)city.shieldsStored);
			stamp = Mix(stamp, city.constructed_buildings.Count);
			stamp = Mix(stamp, city.residents.Count);
			foreach (CityResident resident in city.residents) {
				stamp = Mix(stamp, RuntimeHelpers.GetHashCode(resident.tileWorked));
			}
			return stamp;
		}

		private static long Mix(long hash, long value) {
			unchecked {
				hash ^= value * -7046029254386353131L; // 0x9E3779B97F4A7C15
				hash = (hash ^ (hash >> 29)) * -4658895280553007687L; // 0xBF58476D1CE4E5B9
				return hash ^ (hash >> 32);
			}
		}

		private void ShowBuildingIcon(BuildingIcon icon, Building building) {
			if (building == null) {
				if (icon.rect != null) {
					icon.rect.Visible = false;
				}
				return;
			}
			if (icon.rect == null) {
				icon.rect = new TextureRect() {
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					MouseFilter = Control.MouseFilterEnum.Ignore,
					Size = BuildingIconSize,
				};
				AddChild(icon.rect);
				PositionBuildingIcon(icon);
			}
			if (icon.shown != building) {
				icon.shown = building;
				icon.rect.Texture = TextureLoader.Load(icon.textureKey, building, useCache: true);
			}
			icon.rect.Visible = true;
		}

		private void PositionBuildingIcon(BuildingIcon icon) {
			if (icon.rect != null) {
				icon.rect.Position = tileCenter + icon.offset - BuildingIconSize / 2;
			}
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
