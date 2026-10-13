using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Citizens of a civ we're at war with protest "Stop the aggression against
// our mother country!" (see City.MotherlandUnhappiness).
public class MotherlandUnhappinessTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly City city;

	public MotherlandUnhappinessTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		us = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		them = gameData.players.First(p => !p.isBarbarians && p != us && p.civilization != us.civilization);
		Tile tile = us.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, us, us.GetNextCityName());
		for (int i = 0; i < 3; ++i) {
			CityResident r = new() {
				city = city,
				nationality = them.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
	}

	private void SetWar(bool war) {
		us.playerRelationships[them.id] = new PlayerRelationship();
		them.playerRelationships[us.id] = new PlayerRelationship();
		if (!war) {
			us.playerRelationships[them.id].multiTurnDeals.Add(MultiTurnDeal.DEFAULT_PEACE);
			them.playerRelationships[us.id].multiTurnDeals.Add(MultiTurnDeal.DEFAULT_PEACE);
		}
	}

	private int Unhappy() {
		city.RecalculateCitizenMoods(gameData);
		return city.residents.Count(r => r.mood == CityResident.Mood.Unhappy);
	}

	[Fact]
	public void ForeignersAreHappyEnoughAtPeace() {
		SetWar(false);
		Assert.Equal(0, city.MotherlandUnhappiness(gameData));
	}

	[Fact]
	public void HalfOfTheEnemysCitizensRoundedUpProtestAtWar() {
		SetWar(false);
		int atPeace = Unhappy();
		SetWar(true);
		Assert.Equal(2, city.MotherlandUnhappiness(gameData));
		Assert.Equal(atPeace + 2, Unhappy());
	}

	[Fact]
	public void OurOwnCitizensDontProtest() {
		foreach (CityResident r in city.residents) {
			r.nationality = us.civilization;
		}
		SetWar(true);
		Assert.Equal(0, city.MotherlandUnhappiness(gameData));
	}

	[Fact]
	public void TheProtestersStartARiot() {
		SetWar(false);
		us.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);
		Assert.False(city.isInCivilDisorder);

		SetWar(true);
		us.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);
		Assert.True(city.isInCivilDisorder);
	}
}
