using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The BIQ's tech ("advance") flags, and the Airport's veteran air units.
public class TechFlagsTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player human;

	public TechFlagsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		human = gameData.players.First(p => p.isHuman);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void TheRulesetHasConquestsTechFlags() {
		Tech writing = gameData.techs.Single(t => t.Name == "Writing");
		Tech satellites = gameData.techs.Single(t => t.Name == "Satellites");
		Tech navigation = gameData.techs.Single(t => t.Name == "Navigation");
		Assert.True(writing.HasFlag(SaveTech.Flag.EnablesDiplomats));
		Assert.True(satellites.RevealsWorldMap);
		Assert.True(navigation.HasFlag(SaveTech.Flag.EnablesMapTrading));
		Assert.Equal(writing, Tech.FindEmbassyTech(gameData.techs));
		Assert.DoesNotContain(gameData.techs, t => t.CannotBeTraded);
	}

	[Fact]
	public void HasTechEnablingFollowsKnownTechs() {
		Tech navigation = gameData.techs.Single(t => t.HasFlag(SaveTech.Flag.EnablesMapTrading));
		human.knownTechs.Remove(navigation.id);
		Assert.False(human.HasTechEnabling(SaveTech.Flag.EnablesMapTrading));
		human.knownTechs.Add(navigation.id);
		Assert.True(human.HasTechEnabling(SaveTech.Flag.EnablesMapTrading));
	}

	[Fact]
	public void TheRevealMapTechRevealsEveryTile() {
		Tech satellites = gameData.techs.Single(t => t.RevealsWorldMap);
		Assert.NotEqual(gameData.map.tiles.Count, human.tileKnowledge.knownTiles.Count);

		human.AcquireTech(gameData, satellites);

		Assert.All(gameData.map.tiles, t => Assert.Contains(t, human.tileKnowledge.knownTiles));
		// Only the player who learned it.
		Player other = gameData.players.First(p => p != human && !p.isBarbarians);
		Assert.NotEqual(gameData.map.tiles.Count, other.tileKnowledge.knownTiles.Count);
	}

	[Fact]
	public void TheEmbassyTechIsFoundByItsFlagNotItsName() {
		SaveTech scribing = new() { id = ID.FromString("tech-1"), Name = "Scribing" };
		scribing.flags.Add(SaveTech.Flag.EnablesDiplomats);
		Tech embassyTech = scribing.ToTechWithoutPrereqs();
		Tech writing = new() { id = ID.FromString("tech-2"), Name = "Writing" };
		C7GameData.GameData data = new() { techs = [writing, embassyTech] };
		Player player = new() { id = ID.FromString("player-1") };

		Assert.Equal(embassyTech, Tech.FindEmbassyTech(data.techs));
		player.knownTechs.Add(writing.id);
		Assert.False(RightOfPassage.KnowsWriting(data, player));
		player.knownTechs.Add(embassyTech.id);
		Assert.True(RightOfPassage.KnowsWriting(data, player));
	}

	[Fact]
	public void AnUntradeableTechIsNotOffered() {
		Tech alphabet = new() { id = ID.FromString("tech-1"), Name = "Alphabet" };
		Tech secret = new() { id = ID.FromString("tech-2"), Name = "Secret", Flags = { SaveTech.Flag.CannotBeTraded } };
		List<Tech> techs = [alphabet, secret];
		Player giver = new() { id = ID.FromString("player-1") };
		giver.knownTechs.UnionWith([alphabet.id, secret.id]);
		Player recipient = new() { id = ID.FromString("player-2") };

		Assert.Equal([alphabet], giver.GetTechsTradableTo(recipient, techs));
	}

	[Fact]
	public void AnAirportMakesVeteranAirUnits() {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity()
			&& t.unitsOnTile.Count == 0 && t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		City city = CityInteractions.BuildCity(tile, human, human.GetNextCityName());
		UnitPrototype fighter = gameData.unitPrototypes.First(p => p.IsAirUnit());
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.IsLandUnit() && p.attack > 0);

		city.AddUnit(fighter, gameData);
		Assert.Equal(gameData.defaultExperienceLevel, tile.unitsOnTile[^1].experienceLevel);

		city.AddBuilding(gameData.Buildings.Single(b => b.name == "Airport"));
		city.AddUnit(fighter, gameData);
		Assert.NotEqual(gameData.defaultExperienceLevel, tile.unitsOnTile[^1].experienceLevel);
		// Only air units.
		city.AddUnit(warrior, gameData);
		Assert.Equal(gameData.defaultExperienceLevel, tile.unitsOnTile[^1].experienceLevel);
	}
}
