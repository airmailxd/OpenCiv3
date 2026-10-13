using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The barbarian uprisings when the second civ reaches the Middle Ages and
// the Industrial Age.
public class BarbarianUprisingTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public BarbarianUprisingTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private static List<Player> Civs(C7GameData.GameData gameData) =>
		gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.isIncludedInGame).ToList();

	private static int BarbarianUnits(C7GameData.GameData gameData) =>
		gameData.players.Single(p => p.isBarbarians).units.Count;

	[Fact]
	public void TheSecondCivReachingTheMiddleAgesSetsOffOneUprising() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Restless;
		int camps = gameData.map.barbarianCamps.Count;
		Assert.True(camps > 0);
		List<Player> civs = Civs(gameData);

		// Nothing in the Ancient Times, or with one civ in the Middle Ages.
		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(gameData));
		civs[0].eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(gameData));

		int unitsBefore = BarbarianUnits(gameData);
		civs[1].eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		int spawned = BarbarianInteractions.MaybeStartUprisings(gameData);
		Assert.Equal(camps * BarbarianInteractions.UprisingUnitsPerCamp(BarbarianActivity.Restless), spawned);
		Assert.Equal(unitsBefore + spawned, BarbarianUnits(gameData));
		foreach (Tile camp in gameData.map.barbarianCamps) {
			Assert.Equal(BarbarianInteractions.UprisingUnitsPerCamp(BarbarianActivity.Restless),
				camp.unitsOnTile.Count(u => u.unitType == gameData.barbarianInfo.advancedBarbarian));
		}

		// Only once, even after saving and loading.
		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(gameData));
		C7GameData.GameData loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		Assert.True(loaded.barbarianInfo.middleAgesUprisingHappened);
		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(loaded));
	}

	[Fact]
	public void TheIndustrialAgeSetsOffAnotherUprising() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		BarbarianInteractions.MaybeStartUprisings(gameData);
		List<Player> civs = Civs(gameData);
		civs[0].eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		civs[1].eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		Assert.True(BarbarianInteractions.MaybeStartUprisings(gameData) > 0);

		civs[0].eraCivilopediaName = EraUtils.INDUSTRIAL_AGE_CVLPD;
		civs[1].eraCivilopediaName = EraUtils.INDUSTRIAL_AGE_CVLPD;
		Assert.True(BarbarianInteractions.MaybeStartUprisings(gameData) > 0);
		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(gameData));
	}

	[Fact]
	public void AGameAlreadyPastTheMiddleAgesWhenFirstCheckedHasNoUprising() {
		// As for a game saved before uprisings were added.
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Assert.Null(gameData.barbarianInfo.middleAgesUprisingHappened);
		List<Player> civs = Civs(gameData);
		civs[0].eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		civs[1].eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;

		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(gameData));
		Assert.True(gameData.barbarianInfo.middleAgesUprisingHappened);
		Assert.False(gameData.barbarianInfo.industrialAgeUprisingHappened);
	}

	[Fact]
	public void NoUprisingWithoutBarbarians() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.None;
		BarbarianInteractions.MaybeStartUprisings(gameData);
		foreach (Player civ in Civs(gameData)) {
			civ.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		}
		Assert.Equal(0, BarbarianInteractions.MaybeStartUprisings(gameData));
	}
}
