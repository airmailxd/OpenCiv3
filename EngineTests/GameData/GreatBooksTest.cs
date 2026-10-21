using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Civ3's Great Books (see GreatBooks and GreatBooksText).
public class GreatBooksTest {
	private static Player Civ(string id, string noun, bool human = false) =>
		new() { id = ID.FromString(id), civilization = new Civilization(noun) { noun = noun }, isHuman = human };

	private static C7GameData.GameData Game(params Player[] players) {
		C7GameData.GameData gameData = new() { history = new Dictionary<string, List<HistTurnRecord>>(), timeOptions = new TimeOptions() };
		gameData.players.AddRange(players);
		return gameData;
	}

	[Fact]
	public void ABookIsWrittenAThousandYearsAfterTheGameBegan() {
		Player us = Civ("player-1", "Romans", human: true);
		C7GameData.GameData gameData = Game(us, Civ("player-2", "Greeks"));
		System.Random random = new(1);

		// The first turn starts the wait: 4000 BC.
		Assert.Null(GreatBooks.MaybeWrite(gameData, random));
		// 25 turns of 50 years and 5 of 40 make 1450 years; 20 turns, 1000.
		gameData.turn = 19;
		Assert.Null(GreatBooks.MaybeWrite(gameData, random));
		gameData.turn = 20;
		GreatBooks.Book book = GreatBooks.MaybeWrite(gameData, random);
		Assert.NotNull(book);
		Assert.Equal(GreatBooks.Year(gameData, 20), gameData.gameHistory.greatBooks.lastYear);
		Assert.Null(GreatBooks.MaybeWrite(gameData, random));
	}

	[Fact]
	public void LaterErasWaitFiveHundredYears() {
		Assert.Equal(1000, GreatBooks.YearsBetweenBooks(0));
		Assert.Equal(1000, GreatBooks.YearsBetweenBooks(1));
		Assert.Equal(500, GreatBooks.YearsBetweenBooks(2));
		Assert.Equal(500, GreatBooks.YearsBetweenBooks(3));
	}

	[Fact]
	public void NoBookIsWrittenOnceTheGameIsOver() {
		C7GameData.GameData gameData = Game(Civ("player-1", "Romans", human: true));
		gameData.gameHistory.greatBooks.lastYear = -6000;
		gameData.gameOver = true;
		Assert.Null(GreatBooks.MaybeWrite(gameData, new System.Random(1)));
	}

	[Fact]
	public void EachHistorianAndTitleIsUsedOnceBeforeAnyRepeats() {
		C7GameData.GameData gameData = Game(Civ("player-1", "Romans", human: true));
		System.Random random = new(7);
		List<GreatBooks.Book> books = Enumerable.Range(0, GreatBooks.HistorianCount).Select(_ => GreatBooks.Write(gameData, random)).ToList();
		Assert.Equal(Enumerable.Range(0, GreatBooks.HistorianCount), books.Select(b => b.historian).OrderBy(h => h));
		Assert.Equal(Enumerable.Range(0, GreatBooks.TitleCount), books.Take(GreatBooks.TitleCount).Select(b => (int)b.title).OrderBy(t => t));
	}

	[Fact]
	public void TheRankingIsMostFirstWithoutTheBarbariansOrTheDefeatedAndAtMostEight() {
		List<Player> players = Enumerable.Range(1, 10).Select(i => Civ($"player-{i}", $"Civ{i}")).ToList();
		for (int i = 0; i < players.Count; ++i) {
			players[i].gold = i * 10;
		}
		players[9].defeated = true;
		Player barbarians = new() { id = ID.FromString("player-0"), civilization = new Civilization("Barbarians") { isBarbarian = true } };
		barbarians.gold = 1000;
		C7GameData.GameData gameData = Game([barbarians, .. players]);

		List<Player> ranking = GreatBooks.Rank(gameData, GreatBooks.Title.Wealthiest);

		Assert.Equal(players.Take(9).Reverse().Take(8), ranking);
	}

	[Fact]
	public void PowerIsTheHistographsLatestPower() {
		Player a = Civ("player-1", "Romans");
		Player b = Civ("player-2", "Greeks");
		C7GameData.GameData gameData = Game(a, b);
		gameData.history[a.HistoryKey] = [new HistTurnRecord { Power = 50 }, new HistTurnRecord { Power = 10 }];
		gameData.history[b.HistoryKey] = [new HistTurnRecord { Power = 20 }];
		Assert.Equal([b, a], GreatBooks.Rank(gameData, GreatBooks.Title.MostPowerful));
	}

	// Made-up text in the layout of Civ3's Text/Great Books.txt.
	private const string SampleText = """
		#COMMENT
		A note.

		#GREAT_HISTORY_BOOK
		#ADVISOR Cultural Happy
		#CAPTION Book Caption
		#Text
		^{$HISTORIAN0 wrote
		^^["The $BOOKTITLE1 Ones".]}
		^1) The $HISTORY_RANK2
		^2) The $HISTORY_RANK3
		^3) The $HISTORY_RANK4

		#HISTORIAN
		Hist A
		Hist B

		#BOOK_TITLE
		Title A
		Title B

		#HISTORY_RANK
		Rank A
		Rank B
		Rank C
		""";

	[Fact]
	public void TheBookIsWordedFromTheFile() {
		GreatBooksText text = GreatBooksText.Parse(SampleText);
		Assert.Equal("Book Caption", text.Caption);
		Assert.Equal("Cultural Happy", text.Advisor);
		Assert.Equal(["Hist A", "Hist B"], text.Historians);

		Player a = Civ("player-1", "Alphans");
		Player b = Civ("player-2", "Betans");
		GreatBooks.Book book = new(1, GreatBooks.Title.MostPowerful, [b, a]);

		string formatted = text.Format(book, p => p.civilization.noun);

		Assert.Equal("Hist B wrote\n\n\"The Title B Ones\".\n1) The Rank A Betans\n2) The Rank B Alphans", formatted);
	}

	[Fact]
	public void WithoutTheFileThereIsNoBook() {
		Assert.Null(GreatBooksText.Load(null));
		Assert.False(GreatBooksText.Parse("").HasBook);
	}
}
