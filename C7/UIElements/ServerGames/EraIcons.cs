using System;
using System.Collections.Generic;
using C7GameData;
using Godot;
using Serilog;

// A small picture of each era, for My Games to show which era a player's
// civilization is in. Civ3 has no era icons as such, so these are its era
// splash pictures (#ERA_SPLASH_<era> in PediaIcons.txt, art\erasplash\*.pcx,
// as EraSplash shows them) made small. Without Civ3's art (standalone mode)
// they're drawn: a disc in the era's color with a pip for each era so far.
public static class EraIcons {
	private static readonly ILogger log = Log.ForContext(typeof(EraIcons));

	public const int Width = 48;
	public const int Height = 36;

	// By era, and whether drawn from Civ3's art, as standalone mode can be
	// switched while the game runs.
	private static readonly Dictionary<(string era, bool civ3), Texture2D> cache = new();

	private static PediaIcons pediaIcons;
	private static string pediaIconsPath;

	// The era's icon; the first era's for an era not known (as for a game
	// whose save the server hasn't read an era from).
	public static Texture2D For(string era) {
		if (EraUtils.GetEraIndex(era) < 0) {
			era = EraUtils.ANCIENT_TIMES_CVLPD;
		}
		bool civ3 = Icons() != null;
		if (cache.TryGetValue((era, civ3), out Texture2D icon)) {
			return icon;
		}
		icon = (civ3 ? FromSplash(era) : null) ?? Drawn(era);
		cache[(era, civ3)] = icon;
		return icon;
	}

	// What the icon shows, for its tooltip.
	public static string Name(string era) {
		return EraUtils.GetEraIndex(era) < 0 ? "Era not known yet" : EraUtils.GetNiceEraName(era);
	}

	private static Texture2D FromSplash(string era) {
		try {
			IReadOnlyList<string> entry = Icons()?.GetBlock("ERA_SPLASH_" + era);
			if (entry == null || entry.Count == 0 || !Util.HasCiv3Media(entry[0])) {
				return null;
			}
			Image image = TextureLoader.LoadByPath(entry[0]).GetImage();
			image.Resize(Width, Height, Image.Interpolation.Lanczos);
			return ImageTexture.CreateFromImage(image);
		} catch (Exception e) {
			log.Warning(e, "Could not load the era picture for {Era}", era);
			return null;
		}
	}

	// A disc in the era's color, with one pip for the first era up to four
	// for the modern one, for when Civ3's pictures aren't there.
	private static Texture2D Drawn(string era) {
		int index = Math.Max(0, EraUtils.GetEraIndex(era));
		Color[] colors = [
			new(0.70f, 0.49f, 0.22f), // bronze
			new(0.55f, 0.16f, 0.12f), // a heraldic red
			new(0.36f, 0.38f, 0.42f), // iron
			new(0.18f, 0.36f, 0.62f), // a modern blue
		];
		Color fill = colors[index];
		Color rim = new(0.13f, 0.09f, 0.05f);
		Color pip = new(0.98f, 0.95f, 0.85f);
		Image image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
		Vector2 center = new(Width / 2f, Height / 2f);
		float radius = Height / 2f - 1;
		for (int y = 0; y < Height; ++y) {
			for (int x = 0; x < Width; ++x) {
				float r = new Vector2(x + 0.5f, y + 0.5f).DistanceTo(center);
				if (r <= radius - 2) {
					image.SetPixel(x, y, fill);
				} else if (r <= radius) {
					image.SetPixel(x, y, rim);
				}
			}
		}
		int pips = index + 1;
		float spacing = 7;
		float left = center.X - (pips - 1) * spacing / 2;
		for (int p = 0; p < pips; ++p) {
			Vector2 at = new(left + p * spacing, center.Y);
			for (int y = 0; y < Height; ++y) {
				for (int x = 0; x < Width; ++x) {
					if (new Vector2(x + 0.5f, y + 0.5f).DistanceTo(at) <= 2.5f) {
						image.SetPixel(x, y, pip);
					}
				}
			}
		}
		return ImageTexture.CreateFromImage(image);
	}

	// PediaIcons.txt, read again only if a switch to or from standalone mode
	// means another one.
	private static PediaIcons Icons() {
		try {
			string path = Util.HasCiv3Media("Text/PediaIcons.txt") ? Util.Civ3MediaPath("Text/PediaIcons.txt") : null;
			if (path != pediaIconsPath) {
				pediaIconsPath = path;
				pediaIcons = path == null ? null : new PediaIcons(path);
			}
		} catch (Exception e) {
			log.Warning(e, "Could not read PediaIcons.txt for the era pictures");
			pediaIcons = null;
		}
		return pediaIcons;
	}
}
