using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {

	// What Civ3's era splash (Civ3 FUN_0052d140) tells a player entering a
	// new era. The splash itself is the UI's; this is its wording and the
	// choice it offers, kept here so they can be tested.
	//
	// Flavour only, so the picks take a Random of the caller's rather than
	// the game's, which must stay the same on every machine.
	public static class NewEra {
		// script.txt, #TECH_NEW_ERA1 and #TECH_NEW_ERA2. Civ3 picks one of
		// the two at random (rand() & 1, plus 1, in FUN_0052d140).
		public const string NewEra1 = "$TITLE0, our historians agree, we have entered a distinctly new age in our development!";
		public const string NewEra2 = "The discoveries of our $WISEMEN1 have led to a new era of prosperity for our people, $RULER_TITLE0!";

		// The splash's two choices, labels.txt's 177th and 176th labels,
		// listed in that order (FUN_0052d140 adds the labels at 0x2c4 and
		// 0x2c0 of the label table).
		public const string BigPicture = "What's the big picture?";
		public const string LetsPlay = "Great, let's play the game.";

		// The splash's text for the player, who has just entered their
		// current era. Its first token is the ruler's title, its second what
		// the era calls its researchers.
		public static string Announcement(Player player, Rules rules, Random random) {
			string title = RulerTitle(player, random);
			string researchers = ResearcherName(player.eraCivilopediaName, rules, random);
			// A game from before the researchers were imported has none to
			// name, so it gets the line that doesn't need them.
			string text = researchers == null || random.Next(2) == 0 ? NewEra1 : NewEra2;
			return text
				.Replace("$RULER_TITLE0", title)
				.Replace("$TITLE0", title)
				.Replace("$WISEMEN1", researchers ?? "");
		}

		// One of the titles of the player's government for a leader of their
		// gender, picked at random among the pairs in use; the male title
		// when the female one is blank (Civ3 FUN_00544d70). Without titles
		// (a game from before they were imported), the leader's name.
		public static string RulerTitle(Player player, Random random) {
			List<string> male = player.government?.maleRulerTitles;
			List<string> female = player.government?.femaleRulerTitles;
			string leader = player.civilization?.leader ?? "";
			if (male == null || male.Count == 0) {
				return leader;
			}
			int pair = random.Next(male.Count);
			string title = male[pair];
			if (player.civilization?.leaderGender == Gender.Female && female != null && pair < female.Count
					&& !string.IsNullOrWhiteSpace(female[pair])) {
				title = female[pair];
			}
			return string.IsNullOrWhiteSpace(title) ? leader : title;
		}

		// One of the era's names for its researchers, at random among those
		// in use (Civ3 FUN_005a7060), or null if the rules have none.
		public static string ResearcherName(string eraCivilopediaName, Rules rules, Random random) {
			if (eraCivilopediaName == null || rules?.EraResearcherNames == null
					|| !rules.EraResearcherNames.TryGetValue(eraCivilopediaName, out List<string> names)
					|| names == null || names.Count == 0) {
				return null;
			}
			return names[random.Next(names.Count)];
		}

		// What "What's the big picture?" sets the player researching before
		// it opens the Science Advisor: one of the techs of the player's era
		// that they can research now, at random (Civ3's choice handler at
		// 0x52cea0 calls FUN_0056a500(1), which picks among the unknown techs
		// of the player's era whose prerequisites are all known). Null if
		// there is none.
		public static Tech BigPictureResearch(Player player, GameData gameData, Random random) {
			List<Tech> techs = player.GetAvailableTechsToResearch(gameData.techs)
				.Where(t => t.EraCivilopediaName == player.eraCivilopediaName)
				.ToList();
			return techs.Count == 0 ? null : techs[random.Next(techs.Count)];
		}
	}
}
