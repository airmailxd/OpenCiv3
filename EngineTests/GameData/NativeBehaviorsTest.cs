using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using EngineTests.Utils;
using MoonSharp.Interpreter;
using Xunit;

namespace EngineTests.GameData;

// The stock Lua behaviors that run as C# (see NativeBehaviors) must give
// exactly what the Lua does.
public class NativeBehaviorsTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public NativeBehaviorsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public static IEnumerable<object[]> NativeNames() => NativeBehaviors.Names.Select(n => new object[] { n });

	// The stock scripts mark every function that has a C# version, so the
	// game runs the C#.
	[Theory]
	[MemberData(nameof(NativeNames))]
	public void StockFunctionRunsAsCSharp(string name) {
		Assert.Same(NativeBehaviors.Get<Action<Tile.Yield>>(name), fixture.behaviors.ImportFunc<Action<Tile.Yield>>(name));
	}

	// The C# and the Lua give the same yield for every kind of tile,
	// improvement, owner, yield type and yield.
	[Theory]
	[MemberData(nameof(NativeNames))]
	public void CSharpMatchesLua(string name) {
		Action<Tile.Yield> native = NativeBehaviors.Get<Action<Tile.Yield>>(name);
		Action<Tile.Yield> lua = fixture.behaviors.ImportLuaFunc<Action<Tile.Yield>>(name);
		Assert.NotSame(native, lua);

		// A tile of each terrain, away from any city, so its owner can be set.
		List<Tile> tiles = gameData.map.tiles
			.Where(t => t.cityAtTile == null && t.owningCity == null)
			.GroupBy(t => (t.baseTerrainType, t.overlayTerrainType))
			.Select(g => g.First())
			.ToList();
		Assert.Contains(tiles, t => t.IsWater());
		Assert.Contains(tiles, t => t.IsLand());

		List<TerrainImprovement> developments = [null, .. gameData.terrainImprovements.Where(ti => ti.layer == TerrainImprovement.Layer.ResourceDevelopment)];
		Assert.Contains(developments, ti => ti?.key == "mine");
		Assert.Contains(developments, ti => ti?.key == "irrigation");

		Player player = gameData.players.First(p => !p.isBarbarians);
		Government originalGovernment = player.government;
		List<Player> owners = [null, player];

		int compared = 0;
		try {
			foreach (Tile tile in tiles) {
				TerrainImprovement original = tile.overlays.ImprovementAtLayer(TerrainImprovement.Layer.ResourceDevelopment);
				Player originalOwner = tile.territoryOf;
				try {
					foreach (TerrainImprovement development in developments) {
						SetDevelopment(tile, development);
						foreach (Player owner in owners) {
							tile.territoryOf = owner;
							foreach (Government government in owner == null ? [originalGovernment] : gameData.governments) {
								player.government = government;
								foreach (Tile.YieldType type in Enum.GetValues<Tile.YieldType>()) {
									for (int baseYield = 0; baseYield <= 4; ++baseYield) {
										for (int bonus = 0; bonus <= 1; ++bonus) {
											for (int penalty = 0; penalty <= 1; ++penalty) {
												Tile.Yield expected = new(tile, baseYield, type) { bonus = bonus, penalty = penalty };
												Tile.Yield actual = new(tile, baseYield, type) { bonus = bonus, penalty = penalty };
												lua(expected);
												native(actual);
												string what = $"{name} on {tile} ({development?.key ?? "nothing"}, {owner?.civilization?.name ?? "no owner"}, {government.name}), {type} {baseYield}+{bonus}-{penalty}";
												Assert.True(expected.bonus == actual.bonus, $"bonus: {what}");
												Assert.True(expected.penalty == actual.penalty, $"penalty: {what}");
												++compared;
											}
										}
									}
								}
							}
						}
					}
				} finally {
					SetDevelopment(tile, original);
					tile.territoryOf = originalOwner;
				}
			}
		} finally {
			player.government = originalGovernment;
		}
		Assert.True(compared > 1000);
	}

	private static void SetDevelopment(Tile tile, TerrainImprovement development) {
		TerrainImprovement current = tile.overlays.ImprovementAtLayer(TerrainImprovement.Layer.ResourceDevelopment);
		if (current != null) {
			tile.overlays.Remove(current);
		}
		tile.overlays.Add(development);
	}

	// A function an addon puts in place of a stock one isn't marked, so it
	// runs as the addon wrote it.
	[Fact]
	public void ReplacedFunctionRunsAsLua() {
		Script script = ScriptInitializer.Initialize();
		Table rules = script.DoString(@"
			return {
				stock = NATIVE_DEFAULT('buildings.tile_modifiers.increased_trade', function(yield) end),
				replaced = function(yield) yield.bonus = yield.bonus + 5 end,
			}").Table;
		BehaviorEngine engine = new(script, rules);

		Assert.Same(NativeBehaviors.Get<Action<Tile.Yield>>("buildings.tile_modifiers.increased_trade"), engine.ImportFunc<Action<Tile.Yield>>("stock"));

		Action<Tile.Yield> replaced = engine.ImportFunc<Action<Tile.Yield>>("replaced");
		Assert.DoesNotContain(replaced, NativeBehaviors.Names.Select(n => NativeBehaviors.Get<Action<Tile.Yield>>(n)));
		Tile.Yield yield = new(gameData.map.tiles[0], 1, Tile.YieldType.Commerce);
		replaced(yield);
		Assert.Equal(5, yield.bonus);
	}

	[Fact]
	public void MarkingAFunctionWithNoCSharpVersionFails() {
		Script script = ScriptInitializer.Initialize();
		Assert.ThrowsAny<InterpreterException>(() => script.DoString("return NATIVE_DEFAULT('no.such.function', function() end)"));
	}
}
