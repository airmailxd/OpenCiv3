using System;
using System.Linq;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Sacrificing a captive in one of its owner's cities for culture (see
// MapUnit_Sacrifice.cs): Civ3's rules as FUN_005d0670, FUN_005d1990 and
// FUN_005d1a90 have them.
public class SacrificeTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly City city;
	private readonly UnitPrototype worker;
	private readonly Tech ritualSacrifice;

	public SacrificeTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		city = us.cities.FirstOrDefault() ?? BuildCity(us);

		// The default rules have no sacrifice; give a unit type the action
		// and a tech the flag, as the Mesoamerica scenario does (its Worker
		// and Ritual Sacrifice).
		worker = gameData.unitPrototypes.Single(p => p.name == "Worker");
		worker.actions.Add(UnitAction.Sacrifice);
		ritualSacrifice = gameData.techs.First(t => !us.knownTechs.Contains(t.id));
		ritualSacrifice.Flags.Add(SaveTech.Flag.PermitsSacrifice);
		us.knownTechs.Add(ritualSacrifice.id);
		us.government = gameData.governments.First(g => !g.xenophobic);
	}

	public void Dispose() {
		EngineStorage.newsForComputerPlayers = false;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.IsAllowCities() && t.OwningPlayer() == null
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	// A worker of theirs that we hold, in our city.
	private MapUnit Captive(Tile tile = null) {
		MapUnit unit = gameData.SpawnUnit(us, worker, tile ?? city.location);
		unit.nationality = them.civilization;
		return unit;
	}

	private Building Altar(string name) {
		Building b = gameData.Buildings.First(b => b.name == name);
		b.doublesSacrifice = true;
		city.AddBuilding(b);
		return b;
	}

	private int BaseValue() => 2 * us.ShieldCost(worker);

	[Fact]
	public void ACaptiveInOurCityIsWorthTwiceItsCost() {
		MapUnit captive = Captive();
		Assert.True(captive.CanSacrifice());
		Assert.Contains(UnitAction.Sacrifice, captive.GetAvailableActions());
		Assert.Equal(BaseValue(), captive.SacrificeValue());
	}

	[Fact]
	public void SacrificingAddsTheCultureAndUsesUpTheUnit() {
		EngineStorage.newsForComputerPlayers = true;
		MapUnit captive = Captive();
		int before = city.GetCulture();
		int value = captive.SacrificeValue();

		Assert.True(captive.Sacrifice());

		Assert.DoesNotContain(captive, us.units);
		Assert.Equal(before + value, city.GetCulture());
		MsgShowTemporaryPopup news = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			news ??= msg as MsgShowTemporaryPopup;
		}
		Assert.NotNull(news);
		Assert.Equal($"The Sacrifice of {captive.name} has pleased the gods!  Culture rises {value} in {city.name}", news.message);
		Assert.Same(city.location, news.location);
	}

	[Fact]
	public void TheUnitTypeNeedsTheSacrificeAction() {
		worker.actions.Remove(UnitAction.Sacrifice);
		MapUnit captive = Captive();
		Assert.False(captive.CanSacrifice());
		Assert.False(captive.Sacrifice());
		Assert.Contains(captive, us.units);
	}

	[Fact]
	public void TheOwnerNeedsATechThatPermitsSacrifice() {
		us.knownTechs.Remove(ritualSacrifice.id);
		Assert.False(Captive().CanSacrifice());
	}

	[Fact]
	public void OurOwnPeopleCantBeSacrificed() {
		MapUnit ours = Captive();
		ours.nationality = us.civilization;
		Assert.False(ours.CanSacrifice());
		ours.nationality = null;
		Assert.False(ours.CanSacrifice());
	}

	[Fact]
	public void ItMustBeInOneOfOurCities() {
		Tile outside = city.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());
		Assert.False(Captive(outside).CanSacrifice());

		City theirs = them.cities.FirstOrDefault() ?? BuildCity(them);
		Assert.False(Captive(theirs.location).CanSacrifice());
	}

	[Fact]
	public void AFlagUnitCantBeSacrificed() {
		worker.flags.Add(SaveUnitPrototype.Flag.FlagUnit);
		Assert.False(Captive().CanSacrifice());
	}

	[Fact]
	public void ItNeedsMovesLeft() {
		MapUnit captive = Captive();
		captive.movementPoints.onConsumeAll();
		Assert.False(captive.CanSacrifice());
	}

	[Fact]
	public void EachDoublingBuildingDoublesTheCulture() {
		Altar("Temple");
		Assert.Equal(2 * BaseValue(), Captive().SacrificeValue());
		Altar("Library");
		Assert.Equal(4 * BaseValue(), Captive().SacrificeValue());
	}

	[Fact]
	public void AnObsoleteBuildingDoesntDouble() {
		Building altar = Altar("Temple");
		Tech obsoleting = gameData.techs.First(t => !us.knownTechs.Contains(t.id));
		altar.renderedObsoleteBy = obsoleting;
		us.knownTechs.Add(obsoleting.id);
		Assert.Equal(BaseValue(), Captive().SacrificeValue());
	}

	// Under a xenophobic government a sacrifice is worth nothing in a city
	// less than 51% of whose citizens are its owner's.
	[Theory]
	[InlineData(1, 1, false)]
	[InlineData(51, 49, true)]
	[InlineData(2, 1, true)]
	public void XenophobesGainOnlyWithANativeMajority(int natives, int foreigners, bool gains) {
		us.government = gameData.governments.First(g => g.xenophobic);
		CitizenType citizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		city.residents.Clear();
		for (int i = 0; i < natives + foreigners; ++i) {
			city.residents.Add(new CityResident {
				city = city,
				citizenType = citizen,
				nationality = i < natives ? us.civilization : them.civilization,
			});
		}
		Assert.Equal(gains ? BaseValue() : 0, Captive().SacrificeValue());
	}

	[Fact]
	public void TheCommandSurvivesTheNetwork() {
		MapUnit captive = Captive();
		MsgUnitCommand msg = Assert.IsType<MsgUnitCommand>(NetSerialization.DeserializeMessageToEngine(
			NetSerialization.Serialize(new MsgUnitCommand(captive.id, MsgUnitCommand.Command.Sacrifice))));
		Assert.Equal(MsgUnitCommand.Command.Sacrifice, msg.command);
		Assert.Equal(captive.id, msg.unitID);
	}
}
