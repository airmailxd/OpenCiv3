using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Reading Civ3's diplomacy.txt (see DiplomacyText).
public class DiplomacyTextTest {
	// Two civs, three moods, two lines each, as the reject labels are laid
	// out, with the placeholders' comments above.
	private const string Sample = """
		; $PLAYER0 = player's name
		; $CIVNAME1 = player's civ
		; $AI3 = AI's name
		; $CIVNAME4 = AI's Civ
		; $CIVADJ6 = AI's people (NOUN)
		#AIWEAKREJECT
		#civ 1
		#power 0
		#mood 1
		#random 2
		"barbarian friendly a"
		"barbarian friendly b"
		"barbarian neutral a"
		"barbarian neutral b"
		"barbarian hostile a"
		"barbarian hostile b"
		"Can you throw in a bit more, $PLAYER0?"
		"$CIVNAME1 is known for its generosity."
		"We are very close."
		"Sweeten the deal."
		"Is the friendship of $CIVNAME4 worth NOTHING?"
		"Do you take the $CIVADJ6 for fools? Ask $AI3."

		#AIWILLACCEPT
		#civ 0
		#power 0
		#mood 0
		#random 3
		“This offer seems fair.”
		"We would be willing to accept this deal as is."
		"If you offer it, we will take it."
		""";

	private static readonly DiplomacySpeakers Speakers = new("Elizabeth", "England", "English", "English",
		"Caesar", "Rome", "Roman", "Romans");

	[Fact]
	public void LinesArePickedByCivThenMoodThenAtRandom() {
		DiplomacyEntry entry = DiplomacyText.Parse(Sample).Get("AIWEAKREJECT");
		Assert.True(entry.ByCiv && entry.ByMood && !entry.ByPower);
		Assert.Equal(2, entry.Random);
		Assert.Equal("barbarian neutral b", entry.Line(0, 1, 1));
		Assert.Equal("We are very close.", entry.Line(1, 1, 0));
		Assert.Equal("Sweeten the deal.", entry.Line(1, 1, 7));
		// A civ beyond the file's list gets the first civ's lines.
		Assert.Equal("barbarian hostile a", entry.Line(9, 2, 0));
	}

	[Fact]
	public void PlaceholdersAreFilledByWhatTheLabelsCommentsSayTheyAre() {
		DiplomacyEntry entry = DiplomacyText.Parse(Sample).Get("AIWEAKREJECT");
		Assert.Equal("Can you throw in a bit more, Elizabeth?", DiplomacyText.Fill(entry, entry.Line(1, 0, 0), Speakers));
		Assert.Equal("England is known for its generosity.", DiplomacyText.Fill(entry, entry.Line(1, 0, 1), Speakers));
		Assert.Equal("Is the friendship of Rome worth NOTHING?", DiplomacyText.Fill(entry, entry.Line(1, 2, 0), Speakers));
		Assert.Equal("Do you take the Romans for fools? Ask Caesar.", DiplomacyText.Fill(entry, entry.Line(1, 2, 1), Speakers));
	}

	[Fact]
	public void CurlyQuotesAreTakenOffAndAPlainLabelIsPickedAtRandom() {
		DiplomacyEntry entry = DiplomacyText.Parse(Sample).Get("#AIWILLACCEPT");
		Assert.Equal(3, entry.Lines.Count);
		Assert.Equal("This offer seems fair.", entry.Line(5, 2, 0));
		Assert.Equal("If you offer it, we will take it.", entry.Line(5, 2, 2));
		// The comments of one label don't carry over to the next.
		Assert.Empty(entry.Placeholders);
	}
}
