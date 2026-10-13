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
