using System;
using System.Collections.Generic;
using C7GameData;
using Godot;
using Serilog;

// The Civilopedia's art: the page backgrounds, buttons and colored bars in
// Art/Civilopedia, and the icons of its entries listed in PediaIcons.txt.
// Anything missing (e.g. in standalone mode, which has no Civ3 art) loads as
// null, and the Civilopedia draws without it.
public static class CivilopediaArt {
	private static readonly ILogger log = Log.ForContext(typeof(CivilopediaArt));

	public const string Folder = "Art/Civilopedia/";
	public const string IconFolder = "Art/Civilopedia/Icons/";

	// The paths that failed to load, so each is only tried (and logged) once.
	private static readonly HashSet<string> missing = new(StringComparer.OrdinalIgnoreCase);

	public static Texture2D Load(string path) {
		if (string.IsNullOrEmpty(path) || missing.Contains(path)) {
			return null;
		}
		try {
			return TextureLoader.LoadByPath(path.Replace('\\', '/'));
		} catch (Exception e) {
			missing.Add(path);
			log.Debug("No Civilopedia art at {path}: {message}", path, e.Message);
			return null;
		}
	}

	// A part of a sprite sheet, or null if the sheet is missing.
	public static Texture2D Crop(string path, int x, int y, int width, int height) {
		Texture2D sheet = Load(path);
		if (sheet == null) {
			return null;
		}
		return new AtlasTexture { Atlas = sheet, Region = new Rect2(x, y, width, height) };
	}

	public static void ForgetMissing() {
		missing.Clear();
	}

	// The color of the bar behind an entry's name, which differs by section.
	public enum BarColor { Blue, Green, Grey, Orange, Purple, Red, Yellow }

	public static BarColor BarColorOf(CivilopediaSection section) => section switch {
		CivilopediaSection.Main or CivilopediaSection.Index => BarColor.Red,
		CivilopediaSection.Civilizations => BarColor.Orange,
		CivilopediaSection.Advances => BarColor.Blue,
		CivilopediaSection.GreatWonders or CivilopediaSection.SmallWonders => BarColor.Yellow,
		CivilopediaSection.Improvements => BarColor.Orange,
		CivilopediaSection.Governments => BarColor.Grey,
		CivilopediaSection.Units => BarColor.Purple,
		CivilopediaSection.WorkerActions or CivilopediaSection.Terrain => BarColor.Green,
		CivilopediaSection.Resources => BarColor.Green,
		CivilopediaSection.GameConcepts => BarColor.Grey,
		_ => BarColor.Red,
	};

	public static Texture2D Bar(BarColor color) => Load(Folder + color switch {
		BarColor.Blue => "bar-blue.pcx",
		BarColor.Green => "bar-green.pcx",
		BarColor.Grey => "bar-grey.pcx",
		BarColor.Orange => "bar-orange.pcx",
		BarColor.Purple => "bar-purple.pcx",
		BarColor.Red => "bar-red.pcx",
		_ => "bar-yello.pcx",
	});

	// The color of the name written on a bar: a dark shade of the bar's.
	public static Color BarTextColor(BarColor color) => color switch {
		BarColor.Blue => new Color("1a2a8a"),
		BarColor.Green => new Color("1f5a1f"),
		BarColor.Grey => new Color("303030"),
		BarColor.Orange => new Color("7a3a08"),
		BarColor.Purple => new Color("1c1c8c"),
		BarColor.Red => new Color("8a1a14"),
		_ => new Color("80782a"),
	};

	public static Texture2D SectionIcon(CivilopediaSection section, bool large) {
		string size = large ? "large" : "small";
		string file = section switch {
			CivilopediaSection.Main => $"menu_red {size}.pcx",
			CivilopediaSection.Civilizations => large ? "Races/MenuLarge.pcx" : "Races/MenuSMALL.pcx",
			CivilopediaSection.Advances => $"Tech{size}.pcx",
			CivilopediaSection.GreatWonders => $"Wonders{size}.pcx",
			CivilopediaSection.SmallWonders => $"swonder{size}.pcx",
			CivilopediaSection.Improvements => $"Cityimps{size}.pcx",
			CivilopediaSection.Governments => $"Govt{size}.pcx",
			CivilopediaSection.Units => $"Unit{size}.pcx",
			CivilopediaSection.WorkerActions => $"WorkerActions{size}.pcx",
			CivilopediaSection.Terrain => $"Terrain{size}.pcx",
			CivilopediaSection.Resources => $"Resources{size}.pcx",
			CivilopediaSection.GameConcepts => $"concepts {size}.pcx",
			CivilopediaSection.Index => $"menus {size}.pcx",
			_ => null,
		};
		return file == null ? null : Load(IconFolder + file);
	}

	// An entry's large (128x128) or small (32x32) icon. Buildings are drawn
	// as the player's era and culture would build them.
	public static Texture2D EntryIcon(CivilopediaEntry entry, PediaIcons icons, CivilopediaCatalog catalog, bool large, int era, int cultureGroup) {
		if (entry == null) {
			return null;
		}
		Texture2D icon = Load(icons?.GetCivilopediaIcon(entry.Key, large, era, cultureGroup));
		if (icon != null) {
			return icon;
		}
		if (entry.Unit != null) {
			icon = Load(large ? entry.Unit.art.pediaArt.large : entry.Unit.art.pediaArt.small);
		} else if (entry.Tech != null && !large) {
			icon = Load(entry.Tech.SmallIconPath);
		} else if (entry.Government != null) {
			// Only Anarchy and Despotism have icons of their own; the others
			// use their advance's.
			Tech tech = catalog?.TechById(entry.Government.prerequisiteTech);
			if (tech != null) {
				icon = EntryIcon(catalog.For(tech), icons, catalog, large, era, cultureGroup);
			}
		}
		return icon ?? SectionIcon(entry.Section, large);
	}
}
