using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// Civ3's hidden "Soren" advisor, a developer screen named after Soren
	// Johnson, Civ3's AI programmer. Civ3Conquests.exe 1.22 has it as a
	// complete advisor window (constructor FUN_005b1120, vtable 0x68bd08,
	// one global object at 0xcb10b8), but nothing in the exe ever opens it:
	// the object is referenced only by its static constructor and destructor
	// (0x5b0a10, 0x5b0a30), so the shipped game has no key, menu or debug
	// mode path to it.
	//
	// What it does when opened (Civ3 FUN_005b0ef0): it lists every civ in
	// the game but the viewer, met or not, in player order, and shows eight
	// of them at a time (rows drawn by FUN_005b1260), stepping one civ at a
	// time with its two buttons, the Left/Right arrows or numpad 4/6
	// (FUN_005b0ae0) and the mouse wheel (FUN_005b0c60). Its words are
	// labels.txt's #SOREN_ADVISOR: "Unknown", "The", "Government", "We",
	// "They", "are", "at peace", "at war", "with", "have a", "against the",
	// "mutual protection pact", "military alliance", "trade embargo". The
	// shipped exe loads those words but no code draws them, so how Civ3
	// meant to put them together is UNVERIFIED; this puts them together the
	// way the Foreign Advisor's Treaties tab does (ForeignReport.Treaties).
	// Being a developer screen it knows every tie, embassy or not.
	public static class SorenReport {
		// The civs shown at a time (Civ3 FUN_005b0ef0: the Next button shows
		// while the first civ shown + 8 is less than the number of civs).
		public const int RowsShown = 8;

		// The civs the screen lists: every civ in the game but the viewer and
		// the barbarians (Civ3 FUN_005b0ef0 skips the viewer, DAT_00a1fcbc).
		public static List<Player> Civs(GameData gameData, Player viewer) =>
			gameData.GetRivals(viewer).Where(p => p.isIncludedInGame).ToList();

		// The first civ shown, kept within the list.
		public static int ClampFirst(int first, int count) => Math.Max(0, Math.Min(first, count - RowsShown));

		// Whether the Previous and Next buttons show (Civ3 FUN_005b0ef0,
		// FUN_005b0ae0: Previous while the first shown isn't the first civ,
		// Next while first + 8 < count).
		public static bool HasPrevious(int first) => first > 0;

		public static bool HasNext(int first, int count) => first + RowsShown < count;

		// "The Greeks", over the civ's row.
		public static string Heading(Player civ) => $"The {ForeignReport.CivNoun(civ)}";

		// "Government Despotism", or "Government Unknown" for a civ with none.
		public static string GovernmentLine(Player civ) {
			string name = civ.government?.name;
			return $"Government {(string.IsNullOrEmpty(name) ? "Unknown" : name)}";
		}

		// The civ's row below its heading: its government, then its ties to
		// the viewer ("We ... with the Greeks") and to every other civ it has
		// met ("They ... with the Zulus").
		public static List<string> Lines(GameData gameData, Player viewer, Player civ) {
			List<string> lines = [GovernmentLine(civ)];
			lines.AddRange(TieLines(gameData, viewer, civ, "We", ForeignReport.CivNoun(civ)));
			foreach (Player other in Civs(gameData, viewer)) {
				if (other != civ) {
					lines.AddRange(TieLines(gameData, civ, other, "They", ForeignReport.CivNoun(other)));
				}
			}
			return lines;
		}

		private static List<string> TieLines(GameData gameData, Player first, Player second, string subject, string withNoun) {
			ForeignTie tie = ForeignReport.Tie(gameData, first, second);
			if (tie == null) {
				return [];
			}
			string with = $"with the {withNoun}";
			List<string> lines = [$"{subject} are {(tie.atWar ? "at war" : "at peace")} {with}"];
			if (tie.mutualProtection) {
				lines.Add($"{subject} have a mutual protection pact {with}");
			}
			foreach (Player target in tie.allianceAgainst) {
				lines.Add($"{subject} have a military alliance {with} against the {ForeignReport.CivNoun(target)}");
			}
			foreach (Player target in tie.embargoAgainst) {
				lines.Add($"{subject} have a trade embargo {with} against the {ForeignReport.CivNoun(target)}");
			}
			return lines;
		}
	}
}
