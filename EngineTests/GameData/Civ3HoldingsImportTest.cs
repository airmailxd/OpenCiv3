using System.IO;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;
using static C7GameData.Tile.TileOverlays;

namespace EngineTests.GameData;

// The outposts and colonies of Civ3 games and scenarios are imported with
// their owners, rather than left out.
public class Civ3HoldingsImportTest {
	private static string Conquests => Path.Join(Civ3Location.GetCiv3Path(), "Conquests");

	private static SaveGame ImportScenario(string name) {
		string scenarios = Path.Join(Conquests, "Conquests");
		return ImportCiv3.ImportBiq(Path.Join(scenarios, name), PathUtils.defaultBicPath, modPath => {
			if (string.IsNullOrEmpty(modPath)) {
				return Path.GetFullPath(Path.Combine(Conquests, "Text", "PediaIcons.txt"));
			}
			if (!System.OperatingSystem.IsWindows()) {
				modPath = modPath.Replace("\\conquests\\", "/Conquests/");
			}
			return Path.GetFullPath(Path.Combine(scenarios, modPath, "Text", "PediaIcons.txt"));
		});
	}

	private static SaveTile TileAt(SaveGame save, int x, int y) {
		return save.Map.tiles.Single(t => t.X == x && t.Y == y);
	}

	// Fall of Rome has one colony, in its CLNY section. Rise of Rome has
	// none, though many of its tiles have a Colony field set.
	[SkippableFact]
	public void AScenariosColoniesComeFromItsColonyList() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), Civ3TestData.SkipReason);

		SaveGame fall = ImportScenario("3 Fall of Rome.biq");
		SaveTile colony = TileAt(fall, 135, 101);
		Assert.Contains(COLONY, colony.overlays);
		Assert.Single(fall.Map.tiles, t => t.overlays.Contains(COLONY));

		SaveGame rise = ImportScenario("2 Rise of Rome.biq");
		Assert.DoesNotContain(rise.Map.tiles, t => t.overlays.Contains(COLONY));
	}

	// The first of the Civ3 install's saves with outposts or colonies has
	// each of them, with its owner, as the save's OUTP and CLNY sections
	// list them.
	[SkippableFact]
	public void ASavesOutpostsAndColoniesAreImportedWithTheirOwners() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), Civ3TestData.SkipReason);
		byte[] defaultBiq = Util.ReadFile(PathUtils.defaultBicPath);
		string path = null;
		SavData sav = null;
		foreach (string candidate in Civ3TestData.EnumerateFiles(Path.Join(Conquests, "Saves"), "*.sav")) {
			try {
				SavData data = new(Util.ReadFile(candidate), defaultBiq);
				if ((data.Outp?.Length ?? 0) > 0 && (data.Clny?.Length ?? 0) > 0) {
					(path, sav) = (candidate, data);
					break;
				}
			} catch (System.Exception) {
				// Saves from before Conquests can't be read.
			}
		}
		Skip.If(path == null, "No Conquests save with both outposts and colonies");

		SaveGame save = ImportCiv3.ImportSav(path, PathUtils.defaultBicPath, _ => PathUtils.defaultPediaIconsPath);

		foreach (QueryCiv3.Sav.OUTP outpost in sav.Outp) {
			SaveTile tile = TileAt(save, outpost.XCoordinate, outpost.YCoordinate);
			Assert.Contains(OUTPOST, tile.overlays);
			Assert.NotNull(tile.holdingOwner);
		}
		foreach (QueryCiv3.Sav.CLNY colony in sav.Clny) {
			SaveTile tile = TileAt(save, colony.XCoordinate, colony.YCoordinate);
			Assert.True(tile.overlays.Contains(COLONY), $"{Path.GetFileName(path)} ({colony.XCoordinate},{colony.YCoordinate}) id{colony.UniqueID} p{colony.PlayerID}: {string.Join(",", tile.overlays)}");
			Assert.NotNull(tile.holdingOwner);
		}
		// Nothing else became an outpost or a colony.
		Assert.Equal(sav.Outp.Length, save.Map.tiles.Count(t => t.overlays.Contains(OUTPOST)));
		Assert.Equal(sav.Clny.Length, save.Map.tiles.Count(t => t.overlays.Contains(COLONY)));
	}
}
