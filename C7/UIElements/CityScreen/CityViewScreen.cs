using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using ConvertCiv3Media;
using Godot;
using Serilog;

// Civ3's City View: the picture of a city with its buildings, wonders and
// walls (see C7GameData.CityView, which works out the pieces), over the city
// screen, which opens it with its View button. Pointing at a building shows
// its name, as Civ3 does (FUN_00415b50). The picture is Civ3's art at its
// native 1024x768, so it is a child of the city screen's background, which is
// scaled to fit the window.
public partial class CityViewScreen : Control {
	private ILogger log = LogManager.ForContext<CityViewScreen>();

	// How dark the darkest part of a shadow is. Civ3 draws shadows at a level
	// kept in its data (DAT_009e9b44) which isn't known; this is C7's.
	private const float SHADOW_DARKNESS = 0.5f;

	private readonly record struct Piece(CityView.Sprite sprite, ImageTexture texture, Image image);
	private readonly List<Piece> pieces = [];

	// The pieces' textures and images, kept while the game runs, as the same
	// sheets make every city's picture.
	private static readonly Dictionary<(string, int, int, int, int, bool), (ImageTexture, Image)> cache = [];

	private readonly Dictionary<int, string> buildingNames = [];
	private Label hoverLabel;
	private int hoveredArt = -1;

	// Whether there is Civ3 art to show a city with.
	public static bool Available() {
		return !C7Settings.UseStandaloneMode() && Util.HasCiv3Media(CityView.ART + "Backgrounds/G-SML.pcx");
	}

	public override void _Ready() {
		Position = Vector2.Zero;
		Size = new Vector2(1024, 768);
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;

		TextureButton exit = new() {
			Position = new Vector2(972, 15),
			FocusMode = FocusModeEnum.None,
		};
		TextureLoader.SetButtonTextures(exit, "city_view.exit");
		exit.Pressed += Hide;
		AddChild(exit);

		StyleBoxFlat box = new() {
			BgColor = new Color(0, 0, 0, 0.7f),
			ContentMarginLeft = 6,
			ContentMarginRight = 6,
			ContentMarginTop = 2,
			ContentMarginBottom = 2,
		};
		hoverLabel = new Label() {
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		hoverLabel.AddThemeStyleboxOverride("normal", box);
		hoverLabel.AddThemeColorOverride("font_color", Colors.White);
		AddChild(hoverLabel);
	}

	// Shows the picture of a city.
	public void ShowFor(City city) {
		List<CityView.Sprite> sprites = null;
		Dictionary<int, string> names = [];
		EngineStorage.ReadGameData((GameData gameData) => {
			sprites = CityView.Compose(CityView.InputFor(gameData, city));
			foreach (Building building in gameData.Buildings) {
				int art = CityView.ArtOf(building.civilopediaEntry);
				if (art >= 0 && !names.ContainsKey(art)) {
					names[art] = building.name;
					if (art == CityView.PALACE) {
						names[CityView.PALACE_SECOND_PIECE] = building.name;
					}
				}
			}
		});
		ShowPicture(sprites, names);
	}

	// Shows a picture made of the given pieces, naming the buildings by art
	// index.
	public void ShowPicture(List<CityView.Sprite> sprites, Dictionary<int, string> names) {
		buildingNames.Clear();
		foreach ((int art, string name) in names) {
			buildingNames[art] = name;
		}
		pieces.Clear();
		foreach (CityView.Sprite sprite in sprites) {
			(ImageTexture texture, Image image) = Load(sprite);
			if (texture != null) {
				pieces.Add(new Piece(sprite, texture, image));
			}
		}
		SetHovered(-1, Vector2.Zero);
		QueueRedraw();
		Show();
		try {
			GetNodeOrNull<AudioManager>("/root/GlobalAudioManager")?.PlayUIAudio("city_view.open");
		} catch (Exception e) {
			log.Warning(e, "Couldn't play the City View's sound");
		}
	}

	// A piece's texture, and its image for telling where it's drawn, or
	// nulls if its file isn't there.
	private (ImageTexture, Image) Load(CityView.Sprite sprite) {
		var key = (sprite.path, sprite.cropX, sprite.cropY, sprite.cropWidth, sprite.cropHeight, sprite.isShadow);
		if (cache.TryGetValue(key, out (ImageTexture, Image) found)) {
			return found;
		}
		(ImageTexture, Image) result = (null, null);
		try {
			if (Util.HasCiv3Media(sprite.path)) {
				Pcx pcx = TextureLoader.LoadPCX(sprite.path);
				int x = sprite.WholeFile ? 0 : sprite.cropX;
				int y = sprite.WholeFile ? 0 : sprite.cropY;
				int width = Math.Min(sprite.WholeFile ? pcx.Width : sprite.cropWidth, pcx.Width - x);
				int height = Math.Min(sprite.WholeFile ? pcx.Height : sprite.cropHeight, pcx.Height - y);
				if (width > 0 && height > 0) {
					CropRegion crop = new(x, y, width, height);
					Image image = sprite.isShadow
						? ShadowImage(pcx, crop)
						: PCXToGodot.getImageFromPCX(pcx, crop, new PCXToGodot.ColorOptions(false));
					result = (ImageTexture.CreateFromImage(image), image);
				}
			}
		} catch (Exception e) {
			log.Warning(e, "Couldn't load {Path} for the City View", sprite.path);
		}
		cache[key] = result;
		return result;
	}

	// A shadow file's grey levels say how dark to make what's under it; its
	// other colors (the magenta around the pictures, the lines between them)
	// are see-through.
	private static Image ShadowImage(Pcx pcx, CropRegion crop) {
		byte[] data = new byte[crop.CroppedWidth * crop.CroppedHeight * 4];
		int i = 0;
		for (int y = crop.TopStart; y < crop.TopStart + crop.CroppedHeight; y++) {
			for (int x = crop.LeftStart; x < crop.LeftStart + crop.CroppedWidth; x++, i += 4) {
				int index = pcx.ColorIndexAt(x, y);
				byte red = pcx.Palette[index, 0], green = pcx.Palette[index, 1], blue = pcx.Palette[index, 2];
				if (index >= 253 || red != green || green != blue) {
					continue;
				}
				data[i + 3] = (byte)(red * SHADOW_DARKNESS);
			}
		}
		return Image.CreateFromData(crop.CroppedWidth, crop.CroppedHeight, false, Image.Format.Rgba8, data);
	}

	public override void _Draw() {
		foreach (Piece piece in pieces) {
			DrawTexture(piece.texture, new Vector2(piece.sprite.x, piece.sprite.y));
		}
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is InputEventMouseMotion motion) {
			SetHovered(BuildingAt(motion.Position), motion.Position);
		}
		AcceptEvent();
	}

	public override void _UnhandledKeyInput(InputEvent @event) {
		if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape }) {
			GetViewport().SetInputAsHandled();
			Hide();
		}
	}

	// The building drawn at a point, the frontmost, or -1. Only buildings
	// count, so a house or wall in front doesn't hide one (Civ3 marks where
	// each building is drawn, FUN_00608b30, and only buildings).
	private int BuildingAt(Vector2 point) {
		for (int i = pieces.Count - 1; i >= 0; i--) {
			Piece piece = pieces[i];
			if (piece.sprite.art < 0 || piece.sprite.isShadow) {
				continue;
			}
			int x = (int)point.X - piece.sprite.x, y = (int)point.Y - piece.sprite.y;
			if (x >= 0 && y >= 0 && x < piece.image.GetWidth() && y < piece.image.GetHeight()
					&& piece.image.GetPixel(x, y).A > 0) {
				return piece.sprite.art;
			}
		}
		return -1;
	}

	private void SetHovered(int art, Vector2 at) {
		if (art < 0 || !buildingNames.TryGetValue(art, out string name)) {
			hoveredArt = -1;
			hoverLabel.Visible = false;
			return;
		}
		if (art != hoveredArt) {
			hoveredArt = art;
			hoverLabel.Text = name;
			hoverLabel.ResetSize();
		}
		Vector2 position = at + new Vector2(16, 16);
		position.X = Math.Min(position.X, Size.X - hoverLabel.Size.X);
		position.Y = Math.Min(position.Y, Size.Y - hoverLabel.Size.Y);
		hoverLabel.Position = position;
		hoverLabel.Visible = true;
	}
}
