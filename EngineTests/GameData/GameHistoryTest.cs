using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Util = QueryCiv3.Util;
using Xunit;

namespace EngineTests.GameData;

// The game's history of events for Civ3's replay (see GameHistory).
public class GameHistoryTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player first;
	private readonly Player second;

	public GameHistoryTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		first = civs[0];
		second = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City Found(Player player) {
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		return CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

	private static string Key(Player p) => p.id.ToString();

	[Fact]
	public void AFoundedCityIsRecordedAfterItsBorders() {
		City city = Found(first);

		var events = gameData.gameHistory.Events;
		ReplayEvent founded = Assert.Single(events, e => e.Type == ReplayEventType.CityFounded);
		Assert.Equal((Key(first), city.location.XCoordinate, city.location.YCoordinate, city.name, gameData.turn),
			(founded.Civ, founded.X, founded.Y, founded.Text, founded.Turn));
		// Every tile it took is a territory change to its owner, recorded first.
		var territory = events.TakeWhile(e => e != founded).ToList();
		Assert.NotEmpty(territory);
		Assert.All(territory, e => {
			Assert.Equal(ReplayEventType.TerritoryChanged, e.Type);
			Assert.Equal(Key(first), e.Civ);
			Assert.Null(e.PreviousCiv);
		});
		Assert.Equal(gameData.map.tiles.Count(t => t.owningCity == city), territory.Count);
	}

	[Fact]
	public void ACaptureIsRecordedForTheCaptorWithTheTilesItTakes() {
		Found(second);
		City city = Found(first);
		// Big enough to survive capture.
		while (city.residents.Count < 3) {
			city.AddCitizen(new CityResident() { city = city, citizenType = city.residents[0].citizenType, tileWorked = city.residents[0].tileWorked });
		}
		int before = gameData.gameHistory.Events.Count;

		CityInteractions.CaptureCity(city, second);

		var after = gameData.gameHistory.Events.Skip(before).ToList();
		ReplayEvent captured = Assert.Single(after, e => e.Type == ReplayEventType.CityCaptured);
		Assert.Equal((Key(second), city.name), (captured.Civ, captured.Text));
		// The capital's tile went from the old owner to the new.
		Assert.Contains(after, e => e.Type == ReplayEventType.TerritoryChanged && e.X == city.location.XCoordinate
			&& e.Y == city.location.YCoordinate && e.Civ == Key(second) && e.PreviousCiv == Key(first));
	}

	[Fact]
	public void ADestroyedCityIsRecordedForItsOwner() {
		Found(second);
		City city = Found(first);
		Tile tile = city.location;

		CityInteractions.DestroyCity(city);

		ReplayEvent destroyed = gameData.gameHistory.Events.Last(e => ReplayPlayback.IsListed(e.Type));
		Assert.Contains(destroyed.Type, new[] { ReplayEventType.CityDestroyed, ReplayEventType.CityDestroyedOwnerKeepsTile });
		Assert.Equal(tile.owningCity?.owner == first, destroyed.Type == ReplayEventType.CityDestroyedOwnerKeepsTile);
		Assert.Equal((Key(first), city.name), (destroyed.Civ, destroyed.Text));
	}

	[Fact]
	public void TheEventsAreSavedAndLoadingAddsNoneOfItsOwn() {
		Found(first);
		Found(second);
		int count = gameData.gameHistory.Events.Count;

		SaveGame save = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);

		Assert.Equal(count, loaded.gameHistory.Events.Count);
		Assert.Equal(gameData.gameHistory.Events.Select(e => (e.Type, e.Civ, e.X, e.Y, e.Text, e.PreviousCiv)),
			loaded.gameHistory.Events.Select(e => (e.Type, e.Civ, e.X, e.Y, e.Text, e.PreviousCiv)));
	}

	[Fact]
	public void ASaveWithoutEventsRecordsItsCitiesAndBordersAsTheyStand() {
		City a = Found(first);
		City b = Found(second);
		SaveGame save = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		save.Replay = null;

		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);

		var events = loaded.gameHistory.Events;
		Assert.Equal(new[] { a.name, b.name }.OrderBy(n => n), events.Where(e => e.Type == ReplayEventType.CityPlaced).Select(e => e.Text).OrderBy(n => n));
		Assert.Equal(loaded.map.tiles.Count(t => t.owningCity != null), events.Count(e => e.Type == ReplayEventType.TerritoryChanged));
		Assert.DoesNotContain(events, e => e.Type == ReplayEventType.CityFounded);
	}

	[Fact]
	public void PlaybackShowsTheWorldAsItStoodAtATurn() {
		GameHistory history = new();
		history.Record(0, ReplayEventType.TerritoryChanged, "a", 2, 2);
		history.Record(0, ReplayEventType.CityFounded, "a", 2, 2, "Alpha");
		history.Record(3, ReplayEventType.TerritoryChanged, "b", 2, 2, null, "a");
		history.Record(3, ReplayEventType.CityCaptured, "b", 2, 2, "Alpha");
		history.Record(5, ReplayEventType.CityDestroyed, "b", 2, 2, "Alpha");
		history.Record(5, ReplayEventType.TerritoryChanged, null, 2, 2, null, "b");

		ReplayPlayback playback = new(history, 7);
		Assert.Equal((0, 7), (playback.firstTurn, playback.lastTurn));

		ReplayPlayback.State start = playback.StateAt(0);
		Assert.Equal("a", start.owners[(2, 2)]);
		Assert.Equal("Alpha", start.cities[(2, 2)]);
		Assert.Equal([ReplayEventType.CityFounded], start.listed.Select(e => e.Type));

		ReplayPlayback.State taken = playback.StateAt(4);
		Assert.Equal("b", taken.owners[(2, 2)]);
		Assert.Equal(2, taken.listed.Count);

		ReplayPlayback.State gone = playback.StateAt(7);
		Assert.Empty(gone.owners);
		Assert.Empty(gone.cities);
		Assert.Equal(3, gone.listed.Count);
	}

	// The Conquests saves' replays read as Civ3 recorded them: a city's
	// founding names the city, at a tile of the map, for a civ of the game.
	[SkippableFact]
	public void ACiv3SavesReplayIsImported() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), Civ3TestData.SkipReason);
		string saves = Path.Join(Civ3Location.GetCiv3Path(), "Conquests", "Saves");
		byte[] defaultBiq = Util.ReadFile(PathUtils.defaultBicPath);
		string path = null;
		foreach (string candidate in Civ3TestData.EnumerateFiles(saves, "*.sav")) {
			try {
				SavData data = new(Util.ReadFile(candidate), defaultBiq);
				if (data.RpltRple != null && data.RpltRple.Any(t => t.Any(e => e.EventType == 1))) {
					path = candidate;
					break;
				}
			} catch (System.Exception) {
				// Saves from before Conquests can't be read.
			}
		}
		Skip.If(path == null, "No Conquests save with a replay");

		SaveGame save = ImportCiv3.ImportSav(path, PathUtils.defaultBicPath, _ => PathUtils.defaultPediaIconsPath);

		Assert.NotEmpty(save.Replay);
		var playerIds = save.Players.Select(p => p.id.ToString()).ToHashSet();
		foreach (ReplayEvent founded in save.Replay.Where(e => e.Type == ReplayEventType.CityFounded)) {
			Assert.False(string.IsNullOrEmpty(founded.Text));
			Assert.Contains(founded.Civ, playerIds);
			Assert.Contains(save.Map.tiles, t => t.X == founded.X && t.Y == founded.Y);
		}
		Assert.All(save.Replay.Where(e => e.Type == ReplayEventType.TerritoryChanged && e.Civ != null), e => Assert.Contains(e.Civ, playerIds));
	}
}
