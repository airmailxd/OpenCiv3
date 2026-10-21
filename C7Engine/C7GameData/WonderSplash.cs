namespace C7GameData {

	// Civ3's wonder splash: the screen a player sees on completing a wonder
	// (Civ3 FUN_005de750, shown from FUN_004b3f20 as the wonder is added to
	// the city). It is shown for great and small wonders alike (the BIQ's
	// Wonder and Small Wonder flags), only to the civ that built it, and not
	// in a network game, which has a plain popup instead (script.txt,
	// MP_WONDER_COMPLETION). Other civs hear of a great wonder from their
	// domestic advisor (script.txt, WONDERPRODUCE).
	public static class WonderSplash {
		// Whether completing the building brings up the splash.
		public static bool IsShownFor(Building building) {
			return building != null && (building.IsGreatWonder() || building.isSmallWonder);
		}

		// The key of the wonder's picture in PediaIcons.txt, such as
		// WON_SPLASH_BLDG_Pyramids, or null if the building has no
		// Civilopedia entry.
		public static string ArtKey(Building building) {
			string entry = building?.civilopediaEntry?.Trim();
			return string.IsNullOrEmpty(entry) ? null : "WON_SPLASH_" + entry;
		}

		// The splash's message (script.txt, WONDERSPLASH): "$RULER_TITLE0,
		// we have completed $LINK0 in $CITY1." Without a ruler title it is
		// the network game's message.
		public static string Message(string rulerTitle, string wonder, string city) {
			if (string.IsNullOrWhiteSpace(rulerTitle)) {
				return PlainMessage(wonder, city);
			}
			return $"{rulerTitle}, we have completed {wonder} in {city}.";
		}

		// The plain popup's message in a network game (script.txt,
		// MP_WONDER_COMPLETION, captioned "Wonder Completed"): "We have
		// completed $WONDERNAME0 in $CITY1."
		public static string PlainMessage(string wonder, string city) {
			return $"We have completed {wonder} in {city}.";
		}

		// How the player is addressed: one of their government's ruler
		// titles for their leader's gender, picked at random among the pairs
		// used (Civ3 FUN_00544d70, given the leader's gender by
		// FUN_00566280). Null if the government has no titles.
		public static string RulerTitle(Player player, System.Random random) {
			Government government = player?.government;
			int pairs = government?.maleRulerTitles?.Count ?? 0;
			if (pairs == 0) {
				return null;
			}
			Gender gender = player.civilization?.leaderGender ?? Gender.Male;
			return government.RulerTitle(gender, random.Next(pairs));
		}
	}
}
