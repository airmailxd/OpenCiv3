using System;
using C7GameData;

namespace C7Engine.AI {
	// The markup an AI puts on its prices when it trades with a human, as
	// measured by TheNiceOne in "AI-human trade revisited" (Civ3 1.29, Jan
	// 2003, https://forums.civfanatics.com/threads/ai-human-trade-revisited.42110/;
	// mirror https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/86979-ai-human-trade-revisited).
	// It is hard-coded: "the AI-AI trade bonus that can be found in the
	// editor does not affect AI-human trade in any way", and "On chieftan,
	// the AI-human trade bonus is 0%". It rises with the difficulty and the
	// AI's aggression level, and is a point or two lower when the AI is
	// polite or gracious: "If the AI-human trade bonus is 8% or higher, it
	// may be reduced once, by 1%. If the bonus is 15% or higher, it may be
	// reduced twice, each time by 1%." Attitudes below cautious add
	// nothing ("no AI will demand more if falling below catious").
	//
	// What the human buys costs base x (1 + bonus), and what the human
	// sells fetches base / (1 + bonus): his Deity, aggression 3 example
	// prices a 1000-beaker tech at "1000 / 0.6 x 1.15 = 1917".
	//
	// Between AIs the BIQ's AI-to-AI trade rate applies instead (see
	// PlayerAI.TechDeal), so this is used only against a human.
	public static class TradeMarkup {
		// Percent by difficulty (Chieftain, Warlord, Regent, Monarch,
		// Emperor, Deity), then aggression level 1 to 5 (the editor's -2 to
		// 2), then attitude: cautious or worse, polite, gracious. The table
		// as TheNiceOne posted it:
		//   -----AL1-----AL2-------AL3--------AL4--------AL5
		//   Ch. 0-0-0---0-0-0----0--0--0----0--0--0----0--0--0
		//   Wl. 0-0-0---0-0-0----1--1--1----2--2--2----3--3--3
		//   Rg. 0-0-0---2-2-2----4--4--4----6--6--6----8--7--7
		//   Mn. 0-0-0---3-3-3----6--6--6----9--8--8---12-11-11
		//   Em. 4-4-4---8-8-8---12-11-11---16-15-14---16-15-14
		//   Dt. 5-5-5--10-9-9---15-14-13---20-19-18---20-19-18
		// ("The three columns for each AL is for the three attitude
		// levels.")
		//
		// UNVERIFIED: Demigod and Sid, which the study doesn't cover. They
		// continue its pattern: each level's step is one more than the last
		// (Emperor 4, Deity 5, so Demigod 6 and Sid 7), starting at
		// aggression 1 and capped at aggression 4 as on Emperor and Deity,
		// with the same attitude reductions.
		private static readonly int[][][] Percents = [
			[[0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0]],
			[[0, 0, 0], [0, 0, 0], [1, 1, 1], [2, 2, 2], [3, 3, 3]],
			[[0, 0, 0], [2, 2, 2], [4, 4, 4], [6, 6, 6], [8, 7, 7]],
			[[0, 0, 0], [3, 3, 3], [6, 6, 6], [9, 8, 8], [12, 11, 11]],
			[[4, 4, 4], [8, 8, 8], [12, 11, 11], [16, 15, 14], [16, 15, 14]],
			[[5, 5, 5], [10, 9, 9], [15, 14, 13], [20, 19, 18], [20, 19, 18]],
			Extrapolated(6),
			Extrapolated(7),
		];

		private static int[][] Extrapolated(int step) {
			int[][] result = new int[5][];
			for (int al = 0; al < 5; ++al) {
				int bonus = step * Math.Min(al + 1, 4);
				result[al] = [bonus, bonus >= 8 ? bonus - 1 : bonus, bonus >= 15 ? bonus - 2 : bonus >= 8 ? bonus - 1 : bonus];
			}
			return result;
		}

		// The markup, in percent, that the AI puts on its deals with the
		// human: 0 if either isn't so, or the game's difficulty isn't one of
		// its listed ones.
		public static int Percent(GameData gameData, Player ai, Player human) {
			if (ai == null || human == null || ai.isHuman || !human.isHuman) {
				return 0;
			}
			string name = gameData?.gameDifficulty?.Name;
			int difficulty = name == null ? -1 : gameData.difficulties.FindIndex(d => d.Name == name);
			if (difficulty < 0 || difficulty >= Percents.Length) {
				return 0;
			}
			int aggression = Math.Clamp(ai.civilization?.aggressionLevel ?? 0, -2, 2) + 2;
			int attitude = Attitudes.Level(gameData, ai, human) switch {
				AttitudeLevel.Gracious => 2,
				AttitudeLevel.Polite => 1,
				_ => 0,
			};
			return Percents[difficulty][aggression][attitude];
		}

		// What the AI asks for what it gives: value x (1 + markup).
		public static int Asking(int value, int percent) {
			return (int)Math.Round(value * (100.0 + percent) / 100.0);
		}

		// What the AI pays for what it is given: value / (1 + markup).
		public static int Paying(int value, int percent) {
			return (int)Math.Round(value * 100.0 / (100.0 + percent));
		}
	}
}
