using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine.Lua;
using C7GameData;
using EngineTests.Utils;
using MoonSharp.Interpreter;
using Xunit;

namespace EngineTests.GameData;

// Standalone mode shows our own leader heads (ArtGen/leaderheads): one per
// civilization of the bundled rules, in C7/ModernArt/Art/Leaderheads, picked
// by the standalone textures (C7/Lua/standalone/textures.lua).
public class StandaloneLeaderHeadsTest {
	private static string ModernArtFolder => PathUtils.getBasePath("../C7/ModernArt/");
	private const string Placeholder = "Art/Advisors/placeholder_leaderhead.png";

	private static readonly Lazy<GameMode> standalone = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3", ["standalone"])));

	// The media path the standalone textures give a civilization's leader head.
	private static string LeaderHeadPath(Civilization civ) {
		// The textures look at the C# type of what they're given.
		UserData.RegisterType<Type>();
		(Script lua, Table textures) = standalone.Value.textures;
		Table leaderHeads = (Table)textures["leader_heads"];
		DynValue result = lua.Call(leaderHeads.Get("map_object_to_sprite"), leaderHeads, UserData.Create(civ));
		return result.Table.Get("path").String;
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

	[Fact]
	public void EveryCivilizationOfTheStandaloneRulesHasItsOwnLeaderHead() {
		List<Civilization> civs = standalone.Value.GetSave().Civilizations.Where(c => !c.isBarbarian).ToList();
		Assert.True(civs.Count >= 31, $"only {civs.Count} civilizations");
		CheckLeaderHeads(civs);
	}

	// MDJ's clans (C7/Lua/mdj) have their banners (ArtGen/leaderheads/mdj_mon.py).
	[Fact]
	public void EveryClanOfMdjHasItsBanner() {
		List<Civilization> civs = SaveGameFixture.LoadGameMode(C7Engine.GameSetup.MdjGameMode(new GameMode.Config("civ3", ["standalone"])))
			.GetSave().Civilizations.Where(c => !c.isBarbarian).ToList();
		Assert.Equal(12, civs.Count);
		CheckLeaderHeads(civs);
	}

	private static void CheckLeaderHeads(List<Civilization> civs) {
		List<string> problems = new();
		HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
		foreach (Civilization civ in civs) {
			string path = LeaderHeadPath(civ);
			if (path == Placeholder) {
				problems.Add($"{civ.name}: shows the placeholder");
				continue;
			}
			if (!paths.Add(path)) {
				problems.Add($"{civ.name}: shares {path} with another civilization");
			}
			string file = Path.Combine(ModernArtFolder, path);
			if (!File.Exists(file)) {
				problems.Add($"{civ.name}: no {path} in ModernArt");
			} else if (PngSize(file) != (230, 230)) {
				// ModernArt has twice the pixels of Civ3's 115x115 leader heads.
				problems.Add($"{civ.name}: {path} is not 230x230");
			}
		}
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}

	[Fact]
	public void ACivilizationWithoutALeaderHeadShowsThePlaceholder() {
		Assert.Equal(Placeholder, LeaderHeadPath(new Civilization("Atlantis")));
	}
}
