using System;
using C7Engine;
using C7GameData;
using C7GameData.AIData;
using C7GameData.Save;
using EngineTests.Utils;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EngineTests.AI.UnitAI {
	public sealed class SettlerLocationAITests : MapBase {
		private Tile MakeDesertTileWithDefaultYield() {
			Tile tile = MakeDesertTile();
			tile.overlayTerrainType.baseFoodProduction = 0;
			tile.overlayTerrainType.baseShieldProduction = 1;
			tile.overlayTerrainType.baseCommerceProduction = 0;
			return tile;
		}
		private Tile MakeFloodPlainTileWithDefaultYield() {
			Tile tile = MakeFloodPlainTile();
			tile.overlayTerrainType.baseFoodProduction = 3;
			tile.overlayTerrainType.baseShieldProduction = 0;
			tile.overlayTerrainType.baseCommerceProduction = 0;
			return tile;
		}

		[Fact]
		private void HillsOverPlains() {
			// a single hill tile surrounded by desert
			InitilizeStartTile(MakeHillTile(), new TileLocation(56, 50));
			Tile hills = startTile;
			List<Tile> map = SurroundTile(hills, MakeDesertTile);

			// a single plains tile surrounded by desert
			InitilizeStartTile(MakePlainsTile(), new TileLocation(44, 50));
			Tile plains = startTile;
			map.AddRange(SurroundTile(plains, MakeDesertTile));

			// starting position at the midpoint between the two viable candidates
			InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
			Tile start = startTile;
			startTile.map = gameMap;

			// settler should choose the hill
			Player player = MakeTestPlayer(map);
			Tile chosenTile = SettlerLocationAI.FindSettlerLocation(start, player);
			Assert.Equal(hills, chosenTile);
		}
		[Fact]
		private void CoastalHillsOverInlandHills() {
			List<Tile> map = new();

			// a hill tile with a coast tile neighbor
			InitilizeStartTile(MakeHillTile(), new TileLocation(56, 50));
			Tile coastalHills = startTile;
			map.Add(coastalHills);
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeCoastTile(), TileDirection.NORTH));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.NORTHEAST));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.EAST));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.SOUTHEAST));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.SOUTH));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.SOUTHWEST));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.WEST));
			map.Add(AddNeighborsAndUpdateMap(coastalHills, MakeDesertTile(), TileDirection.NORTHWEST));

			// a hill surrounded by desert
			InitilizeStartTile(MakeHillTile(), new TileLocation(44, 50));
			Tile inlandHills = startTile;
			map.AddRange(SurroundTile(inlandHills, MakeDesertTile));

			// starting position at the midpoint between the two viable candidates
			InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
			Tile start = startTile;
			startTile.map = gameMap;

			// settler should choose the coastal hill even when starting on the inland one a few tiles away
			Player player = MakeTestPlayer(map);
			Tile chosenTile = SettlerLocationAI.FindSettlerLocation(start, player);
			Assert.Equal(coastalHills, chosenTile);
		}
		[Fact]
		private void CloseOverFar() {
			List<Tile> map = new();

			// a single plains tile surrounded by desert
			// not a good settlement spot, but our settler is already on this tile
			InitilizeStartTile(MakePlainsTile(), new TileLocation(25, 25));
			Tile close = startTile;
			map.AddRange(SurroundTile(close, MakeDesertTileWithDefaultYield));

			// a hill tile surrounded by flood plains
			// high settlement score from yield but very far away
			InitilizeStartTile(MakeHillTile(), new TileLocation(200, 25));
			Tile far = startTile;
			map.AddRange(SurroundTile(far, MakeFloodPlainTileWithDefaultYield));

			InitPartialGameMap(250, 50, map);

			Player player = MakeTestPlayer(map);
			Tile chosenTile = SettlerLocationAI.FindSettlerLocation(close, player);
			Assert.Equal(close, chosenTile);
		}

		// Gives the tiles to a rival city that isn't on the map, so that only
		// their ownership changes.
		private void GiveToRival(IEnumerable<Tile> tiles) {
			City rivalCity = new City(Tile.NONE, MakePlayer(), "Rival City", ID.None(""));
			foreach (Tile t in tiles) {
				t.owningCity = rivalCity;
			}
		}

		[Fact]
		private void NotInRivalTerritory() {
			InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
			startTile.map = gameMap;
			List<Tile> map = new() { startTile };
			GiveToRival(map);

			Player player = MakeTestPlayer(map);
			Assert.Equal(Tile.NONE, SettlerLocationAI.FindSettlerLocation(startTile, player));
		}

		[Fact]
		private void RivalOwnedTilesDontCountTowardsASite() {
			// Two identical hills surrounded by flood plains, the same distance
			// from the start, but a rival owns the flood plains around one.
			InitilizeStartTile(MakeHillTile(), new TileLocation(44, 50));
			Tile claimed = startTile;
			List<Tile> claimedSurroundings = SurroundTile(claimed, MakeFloodPlainTileWithDefaultYield);
			claimedSurroundings.Remove(claimed);
			GiveToRival(claimedSurroundings);
			List<Tile> map = new(claimedSurroundings) { claimed };

			InitilizeStartTile(MakeHillTile(), new TileLocation(56, 50));
			Tile free = startTile;
			map.AddRange(SurroundTile(free, MakeFloodPlainTileWithDefaultYield));

			InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
			Tile start = startTile;
			startTile.map = gameMap;

			Player player = MakeTestPlayer(map);
			// The best site is somewhere in the unclaimed cluster, not by the
			// claimed one.
			Tile chosen = SettlerLocationAI.FindSettlerLocation(start, player);
			Assert.True(chosen.DistanceTo(free) <= 1, $"chose {chosen}");
		}

		[Fact]
		private void NotAlreadyBeingSettled() {
			// just one hill tile
			InitilizeStartTile(MakeHillTile(), new TileLocation(50, 50));
			startTile.map = gameMap;

			List<Tile> map = new();
			map.Add(startTile);
			Player player = MakeTestPlayer(map);

			// with no other settlers in play, our test settler chooses the only available tile
			Tile chosenTile = SettlerLocationAI.FindSettlerLocation(startTile, player);
			Assert.Equal(startTile, chosenTile);

			// add a settler whose destination is the one known tile
			MapUnit settler = MakeLandUnit();
			settler.unitType.name = "Settler";
			SettlerAIData data = new();
			data.destination = startTile;
			settler.currentAI = new SettlerAI(data);
			player.units.Add(settler);

			// now the test settler should have nowhere to go
			chosenTile = SettlerLocationAI.FindSettlerLocation(startTile, player);
			Assert.Equal(Tile.NONE, chosenTile);
		}
	}
}
