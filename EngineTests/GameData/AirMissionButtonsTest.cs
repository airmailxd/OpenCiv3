using System;
using System.IO;
using C7Engine;
using C7Engine.Lua;
using EngineTests.Utils;
using MoonSharp.Interpreter;
using Xunit;

namespace EngineTests.GameData;

// The air missions and the airdrop have unit command buttons in both modes:
// Civ3's own (NormButtons.pcx) and, in standalone mode, ours
// (C7/ModernArt/Art/interface/AirMissionButtons.png, made by
// ArtGen/ui_buttons/build.py), since the standalone asset pack's
// button sheet is blank there.
public class AirMissionButtonsTest {
	private static readonly string[] actions = [
		C7Action.UnitBombing, C7Action.UnitPrecisionBombing, C7Action.UnitRecon,
		C7Action.UnitAirSuperiority, C7Action.UnitRebase, C7Action.UnitAirdrop,
	];

	private static readonly Lazy<GameMode> civ3 = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")));
	private static readonly Lazy<GameMode> standalone = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3", ["standalone"])));

	private static Table Button(GameMode mode, string action) {
		Table ui = (Table)mode.textures.Item2["ui"];
		Table unitControl = (Table)ui["unit_control"];
		return unitControl.Get(action).Table;
	}

	[Fact]
	public void EveryAirMissionHasAButtonAndATooltip() {
		foreach (string action in actions) {
			Assert.NotNull(C7Action.ToUnitAction(action));
			Assert.NotNull(C7Action.ToTooltip(action));
			foreach (GameMode mode in new[] { civ3.Value, standalone.Value }) {
				Table button = Button(mode, action);
				Assert.NotNull(button);
				foreach (string state in new[] { "normal", "hover", "pressed" }) {
					Assert.NotNull(button.Get(state).Table);
				}
			}
		}
	}

	[Fact]
	public void StandaloneAirMissionButtonsAreOurOwn() {
		string sheet = PathUtils.getBasePath("../C7/ModernArt/Art/interface/AirMissionButtons.png");
		Assert.True(File.Exists(sheet), sheet);
		foreach (string action in actions) {
			Table normal = Button(standalone.Value, action).Get("normal").Table;
			Assert.Equal("Art/interface/AirMissionButtons.png", normal.Get("path").String);
		}
	}
}
