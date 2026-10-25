using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// Civ3's Great Books: now and then a historian completes "his Great
	// History of the world", ranking the civs on one measure (Text/Great
	// Books.txt, GREAT_HISTORY_BOOK: "$HISTORIAN0 has completed his Great
	// History of the world ["The $BOOKTITLE1 Nations of the World".] 1) The
	// $HISTORY_RANK2 ..."). The file's note says it "should appear once
	// every thousand years in the Ancient and Middle Ages then every 500
	// years thereafter".
	//
	// From Civ3 1.22 (module governor_history):
	// - FUN_004fe4d0/FUN_004ff290, after every civ has moved and before the
	//   turn advances: unless the game is over, a book is written when the
	//   human's era is Ancient or Middle Ages and more than 999 years have
	//   passed since the last one, or the era is later and more than 499
	//   have (FUN_00500a60: this turn's year less the last book's, year 0
	//   counted as 1).
	// - FUN_00548ce0, when a game is begun or loaded: the wait starts from
	//   the current year.
	// - FUN_00548d80 writes a book: a random one of the 11 historians and of
	//   the 5 book titles, each taken at most once (if the one drawn has been
	//   used, the next unused one after it), the title deciding the measure
	//   the civs are ranked on (FUN_005490f0 to FUN_00549830), most first.
	//   Every civ still in the game but the barbarians is ranked, met or not,
	//   at most 8 of them, and the n-th is called by the n-th HISTORY_RANK
	//   ("Glorious", "Great", ...) before its name.
	public static class GreatBooks {
		public const int HistorianCount = 11;
		public const int TitleCount = 5;
		public const int RankCount = 8;

		// The book titles of Great Books.txt's BOOK_TITLE, in order, by the
		// measure each ranks the civs on.
		public enum Title {
			// "Wealthiest": the treasury (LEAD gold, FUN_005490f0).
			Wealthiest = 0,
			// "Most Powerful": the power the Histograph shows (FUN_005492c0
			// sorts on a LEAD field that ranks the civs in the same order as
			// the HIST section's power, checked in a Conquests save).
			MostPowerful = 1,
			// "Most Advanced": the number of techs known (FUN_00549490; the
			// LEAD field equals the SAV's known tech count).
			MostAdvanced = 2,
			// "Happiest": over all the civ's cities, its happy citizens less
			// its unhappy ones and those of a third mood (FUN_00569fe0,
			// counting citizen moods 0, 2 and 3), taken here to be resisters.
			Happiest = 3,
			// "Largest": the number of cities (FUN_00549830).
			Largest = 4,
		}

		// What lasts between books (see GameHistory.greatBooks).
		public class State {
			// The year of the last book, or of when the game was begun or
			// loaded; null until the first turn played.
			public int? lastYear;
			public bool[] historiansUsed = new bool[HistorianCount];
			public bool[] titlesUsed = new bool[TitleCount];
		}

		// A book: the indexes of its historian (HISTORIAN) and title
		// (BOOK_TITLE) in Great Books.txt, and the civs it ranks, first first.
		public record Book(int historian, Title title, List<Player> ranking);

		// The year of the turn as Civ3 counts it here, which has no year 0.
		public static int Year(GameData gameData, int turn) {
			int year = gameData.timeOptions.GetRawNumber(turn);
			return year == 0 ? 1 : year;
		}

		// The years between books for a reader in the era.
		public static int YearsBetweenBooks(int eraIndex) => eraIndex < 2 ? 1000 : 500;

		// Called once a turn, after every civ has moved and before the turn
		// advances. Returns the book written, or null if it isn't time.
		public static Book MaybeWrite(GameData gameData, Random random) {
			if (gameData.gameOver) {
				return null;
			}
			State state = gameData.gameHistory.greatBooks;
			int year = Year(gameData, gameData.turn);
			if (state.lastYear is not int last) {
				state.lastYear = year;
				return null;
			}
			// UNVERIFIED with several humans: Civ3 asks the era of the human
			// whose turn it is; the first human still playing is asked here.
			Player reader = gameData.players.FirstOrDefault(p => p.isHuman && !p.defeated && !p.IsIndependent);
			if (reader == null || year - last < YearsBetweenBooks(reader.EraIndex())) {
				return null;
			}
			return Write(gameData, random);
		}

		public static Book Write(GameData gameData, Random random) {
			State state = gameData.gameHistory.greatBooks;
			int historian = Draw(state.historiansUsed, random);
			Title title = (Title)Draw(state.titlesUsed, random);
			state.lastYear = Year(gameData, gameData.turn);
			return new Book(historian, title, Rank(gameData, title));
		}

		// A random index not yet used: the one drawn, or the next unused one
		// after it. When all have been used, the one drawn.
		private static int Draw(bool[] used, Random random) {
			int n = used.Length;
			int index = random.Next(n);
			for (int i = 0; i < n; ++i) {
				if (!used[index]) {
					used[index] = true;
					return index;
				}
				index = (index + 1) % n;
			}
			return index;
		}

		// The civs still in the game, barbarians aside, most first, at most
		// RankCount of them. Ties keep the players' order.
		public static List<Player> Rank(GameData gameData, Title title) {
			return gameData.players
				.Where(p => !p.IsIndependent && !p.defeated)
				.OrderByDescending(p => ValueOf(gameData, p, title))
				.Take(RankCount)
				.ToList();
		}

		public static int ValueOf(GameData gameData, Player player, Title title) {
			switch (title) {
				case Title.Wealthiest:
					return player.gold;
				case Title.MostPowerful:
					if (gameData.history != null && gameData.history.TryGetValue(player.HistoryKey, out List<HistTurnRecord> records) && records.Count > 0) {
						return records[^1].Power;
					}
					return 0;
				case Title.MostAdvanced:
					return player.knownTechs.Count;
				case Title.Happiest:
					return Happiness(player);
				case Title.Largest:
					return player.cities.Count;
				default:
					return 0;
			}
		}

		private static int Happiness(Player player) {
			int total = 0;
			foreach (City city in player.cities) {
				int resisters = Math.Clamp(city.resisters, 0, city.residents.Count);
				int calm = city.residents.Count - resisters;
				for (int i = 0; i < calm; ++i) {
					CityResident r = city.residents[i];
					if (!r.citizenType.IsDefaultCitizen) {
						continue;
					}
					if (r.mood == CityResident.Mood.Happy) {
						++total;
					} else if (r.mood == CityResident.Mood.Unhappy) {
						--total;
					}
				}
				total -= resisters;
			}
			return total;
		}
	}
}
