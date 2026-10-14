using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using MoonSharp.Interpreter;
using Xunit;

namespace EngineTests.GameData;

// In standalone mode the art comes from C7's assets (the C7/Assets
// submodule) and the remade art of C7/ModernArt. The standalone texture config
// lists the art it takes from there in place of Civ3's; art missing from both
// throws when it's loaded. The advisor faces and the war and peace icons
// once were only on one machine, so the release's advisor screens, all but
// the Domestic advisor's, failed to open (owner's report, 2026-10-12).
public class StandaloneArtTest {
	// Art the game draws itself when it's missing: the faces of the advisors'
	// sidebar (AdvisorUtils) and the Military advisor's war and peace icons.
	private static readonly string[] drawnWhenMissing = [
		"Art/SmallHeads/advisor_tab.png",
		"Art/Advisors/militaryNAV.png",
	];

	private static readonly Lazy<GameMode> standalone = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3", ["standalone"])));

	[Fact]
	public void TheArtStandaloneModeListsIsThere() {
		string c7 = PathUtils.getBasePath("../C7");
		string assets = Path.Combine(c7, "Assets");
		// The submodule isn't checked out everywhere the tests run.
		if (!Directory.Exists(Path.Combine(assets, "Art"))) {
			return;
		}

		string config = File.ReadAllText(Path.Combine(c7, "Lua", "standalone", "textures.lua"));
		Match list = Regex.Match(config, @"local c7_texture_list = \{(.*?)\}", RegexOptions.Singleline);
		Assert.True(list.Success, "The standalone texture config has no c7_texture_list");
		string[] paths = Regex.Matches(list.Groups[1].Value, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
		Assert.NotEmpty(paths);

		string[] missing = paths
			.Where(p => !drawnWhenMissing.Contains(p))
			.Where(p => !ExistsIgnoringCase(assets, p) && !ExistsIgnoringCase(Path.Combine(c7, "ModernArt"), p))
			.ToArray();
		Assert.Empty(missing);
	}

	// Each advance and building of the standalone rules has its own icon in
	// the tech tree, the production menus and the city screen: a whole file
	// of C7/ModernArt with twice the pixels of Civ3's icon. The standalone
	// asset pack only has placeholders for them, which showed the words
	// "Tech" and "Bldg" (owner's report, 2026-10-12).
	[Fact]
	public void EveryAdvanceAndBuildingOfTheStandaloneRulesHasItsOwnIcons() {
		SaveGame rules = standalone.Value.GetSave();
		Assert.True(rules.Techs.Count >= 80 && rules.Buildings.Count >= 80,
			$"only {rules.Techs.Count} advances and {rules.Buildings.Count} buildings");
		List<(string name, string configKey, object obj, (int, int) size)> icons = [];
		foreach (SaveTech tech in rules.Techs) {
			icons.Add((tech.Name, "tech_icons.small", tech.ToTechWithoutPrereqs(), (64, 64)));
		}
		foreach (SaveBuilding b in rules.Buildings) {
			Building building = NewBuilding(b.name, b.civilopediaEntry, b.iconRowIndex);
			icons.Add((b.name, "building_icons.small", building, (64, 64)));
			icons.Add((b.name, "building_icons.large", building, (100, 80)));
		}
		foreach (SaveInflow i in rules.Inflows) {
			Inflow inflow = new(new SaveInflow() { name = i.name, iconRowIndex = i.iconRowIndex, localYield = [] }, null);
			icons.Add((i.name, "building_icons.small", inflow, (64, 64)));
			icons.Add((i.name, "building_icons.large", inflow, (100, 80)));
		}

		string modernArt = PathUtils.getBasePath("../C7/ModernArt");
		List<string> problems = [];
		Dictionary<(string, string), string> shownFor = [];
		foreach ((string name, string configKey, object obj, (int, int) size) in icons) {
			(string path, Table crop) = Sprite(configKey, obj);
			string file = FindIgnoringCase(modernArt, path);
			if (file == null) {
				problems.Add($"{name} ({configKey}): no {path} in ModernArt");
			} else if (crop != null) {
				problems.Add($"{name} ({configKey}): only a part of {path}");
			} else if (PngSize(file) != size) {
				problems.Add($"{name} ({configKey}): {path} is {PngSize(file)}, not {size}");
			}
			if (shownFor.TryGetValue((configKey, path.ToLowerInvariant()), out string other)) {
				problems.Add($"{name} ({configKey}): shares {path} with {other}");
			}
			shownFor[(configKey, path.ToLowerInvariant())] = name;
		}
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}

	// Every building of the standalone rules shows an icon wherever buildings
	// are drawn: the production menus and the tech tree (checked above), the
	// Civilopedia, which reads our own C7/Civilopedia, and for barracks,
	// harbors and airports the icons by a city on the map. Barracks and
	// harbors were reported missing (owner's report, 2026-10-13): the map
	// showed their pictures shrunk to 25x17, which couldn't be made out.
	[Fact]
	public void EveryBuildingOfTheStandaloneRulesHasACivilopediaIcon() {
		SaveGame rules = standalone.Value.GetSave();
		string civilopedia = PathUtils.getBasePath("../C7/Civilopedia");
		PediaIcons icons = new(Path.Combine(civilopedia, "Text", "PediaIcons.txt"));
		List<string> problems = [];
		foreach (SaveBuilding b in rules.Buildings) {
			foreach (bool large in new[] { true, false }) {
				string path = icons.GetCivilopediaIcon(b.civilopediaEntry, large);
				if (path == null) {
					problems.Add($"{b.name}: no {(large ? "large" : "small")} icon in PediaIcons.txt");
				} else if (!ExistsIgnoringCase(civilopedia, path.Replace('\\', '/'))) {
					problems.Add($"{b.name}: no {path} in C7/Civilopedia");
				}
			}
		}
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}

	[Fact]
	public void BarracksHarborsAndAirportsHaveTheirOwnIconsByTheCity() {
		SaveGame rules = standalone.Value.GetSave();
		string modernArt = PathUtils.getBasePath("../C7/ModernArt");
		HashSet<string> shown = [];
		foreach ((string key, string buildingName) in new[] { ("barracks", "Barracks"), ("harbor", "Harbor"), ("airport", "Airport") }) {
			SaveBuilding save = rules.Buildings.Single(b => b.name == buildingName);
			Building building = NewBuilding(save.name, save.civilopediaEntry, save.iconRowIndex);
			(string path, Table crop) = Sprite("city_building_icons." + key, building);
			string file = FindIgnoringCase(modernArt, path);
			Assert.True(file != null, $"{buildingName}: no {path} in ModernArt");
			Assert.NotNull(crop);
			int x = (int)crop.Get(1).Number, y = (int)crop.Get(2).Number, w = (int)crop.Get(3).Number, h = (int)crop.Get(4).Number;
			// Shown as Civ3's are, 25x17, from art with twice the pixels.
			Assert.Equal((25, 17), (w, h));
			(int width, int height) = PngSize(file);
			Assert.True((x + w) * 2 <= width && (y + h) * 2 <= height, $"{buildingName}: {path} is too small for its icon");
			Assert.True(shown.Add($"{path} {x},{y}"), $"{buildingName}: shares its icon with another building");
		}
	}

	// The cells of the asset pack's Art/interface/NormButtons.png with a
	// picture, by column and row; the rest are blank golden balls.
	private static readonly HashSet<(int, int)> drawnNormButtons = [
		(0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (5, 0),
		(2, 1), (3, 1),
		(5, 2), (6, 2), (7, 2),
		(0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (6, 3), (7, 3),
	];

	// Every unit order's button has a picture: one of the asset pack's that
	// has one, or one of our own in ModernArt. Sentry, Load, Upgrade, Build
	// Barricade and others were blank golden balls in the unit bar and the
	// tech tree.
	[Fact]
	public void EveryUnitOrderButtonHasAPicture() {
		(Script lua, Table textures) = standalone.Value.textures;
		Table buttons = (Table)((Table)textures["ui"])["unit_control"];
		string modernArt = PathUtils.getBasePath("../C7/ModernArt");
		List<string> orders = typeof(C7Engine.C7Action).GetFields()
			.Where(f => f.IsLiteral && f.FieldType == typeof(string))
			.Select(f => (string)f.GetRawConstantValue())
			.Where(v => v.StartsWith("unit_"))
			.ToList();
		Assert.True(orders.Count > 30, $"only {orders.Count} unit orders");
		List<string> problems = [];
		foreach (string order in orders) {
			if (buttons[order] is not Table button) {
				problems.Add($"{order}: no button");
				continue;
			}
			Table normal = (Table)button["normal"];
			string path = normal.Get("path").String;
			Table crop = normal.Get("crop_region").Table;
			if (FindIgnoringCase(modernArt, path) != null) {
				continue;
			}
			if (path.Contains("NormButtons", StringComparison.OrdinalIgnoreCase) && crop != null) {
				(int, int) cell = ((int)crop.Get(1).Number / 32, (int)crop.Get(2).Number / 32);
				if (!drawnNormButtons.Contains(cell)) {
					problems.Add($"{order}: the asset pack's button at {cell} is blank");
				}
				continue;
			}
			problems.Add($"{order}: {path} isn't in ModernArt");
		}
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}

	// An advance or a building the bundled rules don't have, as from an
	// addon, shows the placeholders.
	[Fact]
	public void AnAdvanceOrBuildingWithoutAnIconShowsThePlaceholder() {
		Tech tech = new() { Name = "Alchemy", CivilopediaEntry = "TECH_Alchemy" };
		Assert.Equal("Art/Tech Chooser/Icons/placeholder.png", Sprite("tech_icons.small", tech).path);
		Building building = NewBuilding("Mint", "BLDG_Mint", 3);
		Assert.Equal("Art/city screen/buildings-small.png", Sprite("building_icons.small", building).path);
		Assert.Equal("Art/city screen/buildings-large.png", Sprite("building_icons.large", building).path);
	}

	private static Building NewBuilding(string name, string civilopediaEntry, int iconRowIndex) {
		SaveBuilding save = new() { name = name, civilopediaEntry = civilopediaEntry, iconRowIndex = iconRowIndex };
		return new Building(save, new C7GameData.GameData());
	}

	// The media path and crop region the standalone textures give an object
	// for a config key, like "tech_icons.small".
	private static (string path, Table crop) Sprite(string configKey, object obj) {
		// The textures look at the C# type of what they're given.
		UserData.RegisterType<Type>();
		(Script lua, Table textures) = standalone.Value.textures;
		Table entry = textures;
		foreach (string key in configKey.Split('.')) {
			entry = (Table)entry[key];
		}
		DynValue result = lua.Call(entry.Get("map_object_to_sprite"), entry, UserData.Create(obj));
		return (result.Table.Get("path").String, result.Table.Get("crop_region").Table);
	}

	private static (int, int) PngSize(string path) {
		byte[] header = new byte[24];
		using (FileStream stream = File.OpenRead(path)) {
			Assert.Equal(24, stream.Read(header, 0, 24));
		}
		Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, header[0..4]);
		int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
		int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
		return (width, height);
	}

	// Whether a file is under a folder, ignoring the case of its path, as
	// the game finds media.
	private static bool ExistsIgnoringCase(string root, string relPath) {
		return FindIgnoringCase(root, relPath) != null;
	}

	// The file at a path under a folder, found ignoring case, or null.
	private static string FindIgnoringCase(string root, string relPath) {
		string current = root;
		foreach (string part in relPath.Split('/')) {
			if (!Directory.Exists(current)) {
				return null;
			}
			string match = Directory.EnumerateFileSystemEntries(current)
				.FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				return null;
			}
			current = match;
		}
		return File.Exists(current) ? current : null;
	}
}
