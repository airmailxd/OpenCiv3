using System;
using System.IO;
using C7Engine.Lua;
using EngineTests.Utils;
using MoonSharp.Interpreter;
using Xunit;

namespace EngineTests.GameData;

// The science advisor's tech boxes: Civ3's own (techboxes.pcx) and, in
// standalone mode, ours (C7/ModernArt/Art/Advisors/TechBoxesAllSizes.png,
// made by ArtGen/ui_buttons/tech_boxes.py), since the standalone asset
// pack's techboxes.png only has the small Ancient boxes and an advance with
// a bigger box had none at all (owner's report, 2026-10-31).
public class StandaloneTechBoxesTest {
	private const string Sheet = "Art/Advisors/TechBoxesAllSizes.png";
	private static readonly string[] states = ["known", "in_progress", "possible", "blocked"];
	private static readonly string[] eras = ["ancient", "middle", "industrial", "modern"];
	private static readonly string[] sizes = ["small", "medium", "large", "long"];

	private static readonly Lazy<GameMode> civ3 = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")));
	private static readonly Lazy<GameMode> standalone = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3", ["standalone"])));

	private static Table Box(GameMode mode, string state, string era, string size) {
		Table boxes = (Table)mode.textures.Item2["tech_boxes"];
		return boxes.Get(state).Table.Get(era).Table.Get(size).Table;
	}

	[Fact]
	public void EveryStandaloneBoxIsOurOwnWhereCiv3sIs() {
		foreach (string state in states) {
			foreach (string era in eras) {
				foreach (string size in sizes) {
					Table ours = Box(standalone.Value, state, era, size);
					Table theirs = Box(civ3.Value, state, era, size);
					Assert.Equal(Sheet, ours.Get("path").String);
					Assert.EndsWith("techboxes.pcx", theirs.Get("path").String);
					for (int i = 1; i <= 4; i++) {
						Assert.Equal(theirs.Get("crop_region").Table.Get(i).Number, ours.Get("crop_region").Table.Get(i).Number);
					}
				}
			}
		}
	}

	// Twice the size of Civ3's techboxes.pcx (1000x1483), as all of
	// ModernArt is, so the crop regions find the same boxes.
	[Fact]
	public void TheSheetIsTwiceCiv3s() {
		string file = PathUtils.getBasePath("../C7/ModernArt/" + Sheet);
		Assert.True(File.Exists(file), file);
		byte[] header = new byte[24];
		using (FileStream f = File.OpenRead(file)) {
			f.ReadExactly(header);
		}
		int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
		int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
		Assert.Equal(2000, width);
		Assert.Equal(2966, height);
	}
}
