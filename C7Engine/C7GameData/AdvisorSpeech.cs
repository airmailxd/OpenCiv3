using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {

	// The six advisors, in Civ3's order, which is also the order of their
	// speech slots (Civ3 FUN_0042ce60) and of AdvisorHead.Advisor.
	public enum AdvisorKind {
		Domestic,
		Trade,
		Military,
		Foreign,
		Culture,
		Science,
	}

	// What an advisor is to say: a script.txt key and the values its
	// placeholders are filled from (see ScriptText.Format), with the face the
	// code chose. An "#advisor" line in the entry overrides the face.
	public class AdvisorLine {
		public string Key;
		public readonly string[] Texts = new string[8];
		public readonly int[] Numbers = new int[4];
		public AdvisorMood Mood = AdvisorMood.Happy;

		public AdvisorLine(string key) {
			Key = key;
		}

		public AdvisorLine Text(int slot, string value) {
			Texts[slot] = value ?? "";
			return this;
		}

		public AdvisorLine Number(int slot, int value) {
			Numbers[slot] = value;
			return this;
		}

		public AdvisorLine WithMood(AdvisorMood mood) {
			Mood = mood;
			return this;
		}

		// The line in the words of script.txt, with its face, or null if
		// the script has no such entry.
		public (string text, AdvisorMood mood)? Say(ScriptText script) {
			ScriptEntry entry = script?.Get(Key);
			if (entry == null || entry.Text.Length == 0) {
				return null;
			}
			return (ScriptText.Format(entry.Text, Texts, Numbers), entry.Mood ?? Mood);
		}
	}

	// Why a city can't grow past its size: Civ3 FUN_004b8d50 names the
	// improvement it needs.
	public enum GrowthBlocker {
		None,
		Aqueduct,
		Hospital,
	}

	public record CityAdviceFacts(string Name, int FoodSurplus, bool FoodBoxFull, GrowthBlocker Blocker,
		int Happy, int Unhappy, bool InDisorder);

	// What the foreign advisor knows of another civ.
	public record ForeignAdviceFacts(string Noun, string Adjective, AttitudeLevel? Attitude,
		string CulturalOpinion, AdvisorMood OpinionMood, string BestUnit, bool CanTrade,
		string TheirSurplus, string OurSurplus);

	// A civ's opinion of our culture, for the cultural advisor.
	public record CultureOpinionFacts(string Adjective, string Opinion, AdvisorMood Mood);

	// The facts the advisors' speech is chosen from. AdvisorFactsGatherer
	// fills in those of one advisor from the game; tests fill them in by
	// hand.
	public class AdvisorFacts {
		// Domestic (Civ3 FUN_0042ad00).
		public List<CityAdviceFacts> Cities = [];
		// The ruler titles the player may be called by, one of which is
		// picked at random (Civ3 FUN_00544d70).
		public List<string> RulerTitles = [];
		public int AnarchyTurns;

		// Trade (Civ3 FUN_0042b020).
		public List<string> SingleSourceResources = [];
		public List<string> CivsWithoutTradeRoute = [];
		public List<(string partner, string enemy)> EmbargoPartners = [];
		public bool HasNoResources;

		// Military (Civ3 FUN_0042b3d0). Strength is null when no rival is
		// picked to compare with.
		public (long ours, long theirs)? Strength;
		public int CityCount;
		public int NetGoldPerTurn;
		public int UnitSupportCost;
		public string BestUnit;
		public (string tribe, string city)? BarbariansNear;
		public bool CanPlantSpy;

		// Foreign (Civ3 FUN_0053f1e0): the civs on the screen, the one
		// picked (an index into ForeignCivs, or null), and whether the
		// screen is showing trade.
		public List<ForeignAdviceFacts> ForeignCivs = [];
		public int? SelectedForeignCiv;
		public bool ForeignTradeView;

		// Culture (Civ3 FUN_0042bc80): a building's name once for each of
		// our cities that can build it.
		public List<string> CultureImprovementChoices = [];
		public List<string> WonderChoices = [];
		public List<CultureOpinionFacts> CultureOpinions = [];

		// Science (Civ3 FUN_0042bfa0).
		public int CivsAheadInScience;
		public int CivsNotAheadInScience;
		public int ScienceRate;
		public bool Researching;
		public string RecommendedTech;

		// The rare remarks (Civ3 FUN_0042c4d0).
		public int LocalHour = 12;
		public string LeaderTitle = "";
		public string OurCivName = "";
		public List<string> OtherCivNames = [];
		public bool NuclearWeaponsAllowed;
	}

	// Chooses what Civ3's advisors say on their screens, as Civ3 does
	// (Civ3 FUN_0042ce80 and the advisor functions it calls, module
	// advisor_speech, 0x429960-0x42d090). Each advisor goes round a fixed
	// list of topics, one step each time it speaks, skipping the topics with
	// nothing to say; one time in 500 it makes a joke instead.
	public class AdvisorSpeech {
		// How far each advisor's step counts before going back to 0 (Civ3
		// FUN_0042ce80). Steps with no topic are skipped.
		private static readonly int[] LastStep = [5, 6, 4, 1, 6, 3];

		// One time in this many an advisor makes a joke (Civ3 FUN_0042c4d0).
		public const int JokeOdds = 500;

		private readonly int[] steps = new int[6];
		private int foreignTopic;

		// Every advisor starts its topics over, as for a new game (Civ3
		// FUN_0042cdd0).
		public void Reset() {
			Array.Clear(steps);
			foreignTopic = 0;
		}

		public AdvisorLine Next(AdvisorKind advisor, AdvisorFacts facts, Random rng) {
			int index = (int)advisor;
			// Every advisor has a topic that always has something to say, so
			// the loop ends within a round; the cap is only a guard.
			for (int attempt = 0; attempt < 32; attempt++) {
				AdvisorLine joke = Joke(advisor, facts, rng);
				if (joke != null) {
					return joke;
				}
				steps[index]++;
				if (steps[index] > LastStep[index]) {
					steps[index] = 0;
				}
				AdvisorLine line = advisor switch {
					AdvisorKind.Domestic => Domestic(steps[index], facts, rng),
					AdvisorKind.Trade => Trade(steps[index], facts, rng),
					AdvisorKind.Military => Military(steps[index], facts),
					AdvisorKind.Foreign => Foreign(steps[index], facts, rng),
					AdvisorKind.Culture => Culture(steps[index], facts, rng),
					AdvisorKind.Science => Science(steps[index], facts),
					_ => null,
				};
				if (line != null) {
					return line;
				}
			}
			return null;
		}

		private static T Pick<T>(List<T> list, Random rng) => list[rng.Next(list.Count)];

		private static string RulerTitle(AdvisorFacts facts, Random rng) =>
			facts.RulerTitles.Count == 0 ? "" : Pick(facts.RulerTitles, rng);

		// Civ3 FUN_0042ad00, with the city's facts from FUN_0052b200 and
		// FUN_0052b450 (each of which picks a city at random).
		internal static AdvisorLine Domestic(int step, AdvisorFacts facts, Random rng) {
			switch (step) {
				case 0:
					return facts.Cities.Count > 0 ? null : new AdvisorLine("DOMESTICADVICERANK");
				case 1: {
						if (facts.Cities.Count == 0) {
							return null;
						}
						CityAdviceFacts city = Pick(facts.Cities, rng);
						// Civ3 sets "thrives" and "!" when the surplus is over
						// 1, but then always overwrites them with "is growing
						// slowly" when it is over 0 (FUN_0052b200 at 0x52b2c1),
						// so a growing city is always "growing slowly", and
						// "is starving" is never said. Labels from labels.txt,
						// #DOMESTIC_ADVISOR.
						string does = city.FoodSurplus > 0 ? "is growing slowly" : "is not growing";
						// The improvement is only asked for once the food box
						// is full (FUN_0052b200 compares the food stored with
						// the box).
						string optional = !city.FoodBoxFull ? "" : city.Blocker switch {
							GrowthBlocker.Aqueduct => "We need an aqueduct to continue growing.",
							GrowthBlocker.Hospital => "We need a hospital to continue our city's growth.",
							_ => "",
						};
						return new AdvisorLine("DOMESTICADVICECITYSIZE")
							.Text(0, city.Name).Text(1, does).Text(2, RulerTitle(facts, rng)).Text(3, ".").Text(4, optional)
							.WithMood(AdvisorMood.Sad);
					}
				case 2: {
						if (facts.Cities.Count == 0) {
							return null;
						}
						string title = RulerTitle(facts, rng);
						CityAdviceFacts city = Pick(facts.Cities, rng);
						(string mood, AdvisorMood face) = city.Unhappy < city.Happy ? ("happy", AdvisorMood.Happy)
							: city.Happy < city.Unhappy ? ("unhappy", AdvisorMood.Sad)
							: ("content", AdvisorMood.Happy);
						return new AdvisorLine("DOMESTICADVICEHAPPINESS").Text(0, title).Text(1, city.Name).Text(2, mood).WithMood(face);
					}
				case 3:
					return facts.AnarchyTurns < 1 ? null : new AdvisorLine("DOMESTICADVICEANARCHY").Number(0, facts.AnarchyTurns);
				default:
					return null;
			}
		}

		// Civ3 FUN_0042b020, with its topics' facts from FUN_005bdf80,
		// FUN_005be0a0, FUN_005be210 and FUN_005be460.
		internal static AdvisorLine Trade(int step, AdvisorFacts facts, Random rng) {
			switch (step - 1) {
				case 0:
					return facts.SingleSourceResources.Count == 0 ? null
						: new AdvisorLine("TRADEADVICEMORE").Text(0, Pick(facts.SingleSourceResources, rng));
				case 1:
					return facts.CivsWithoutTradeRoute.Count == 0 ? null
						: new AdvisorLine("TRADEADVICEROUTE").Text(0, Pick(facts.CivsWithoutTradeRoute, rng));
				case 2: {
						if (facts.EmbargoPartners.Count == 0) {
							return null;
						}
						(string partner, string enemy) = Pick(facts.EmbargoPartners, rng);
						return new AdvisorLine("TRADEADVICEEMBARGO").Text(0, partner).Text(1, enemy);
					}
				case 3:
					return facts.HasNoResources ? new AdvisorLine("TRADEADVICEWEHAVENOTHING") : null;
				case 4:
					return new AdvisorLine("TRADEADVICEACQUIREMORE");
				default:
					return null;
			}
		}

		// Civ3 FUN_0042b3d0.
		internal static AdvisorLine Military(int step, AdvisorFacts facts) {
			switch (step) {
				case 0: {
						if (facts.Strength is not (long ours, long theirs)) {
							return null;
						}
						if (ours < theirs * 4 / 5) {
							return new AdvisorLine("MILITARYADVICERANK_WEAK");
						}
						if (theirs < ours * 4 / 5) {
							return new AdvisorLine("MILITARYADVICERANK_STRONG");
						}
						return new AdvisorLine("MILITARYADVICERANK_AVERAGE");
					}
				case 1:
					// Civ3 compares the number of cities with the gold made
					// each turn (FUN_00569df0), and the gold paid for units
					// (FUN_005693a0).
					if (facts.CityCount < facts.NetGoldPerTurn || facts.UnitSupportCost < 1) {
						return facts.BestUnit == null ? new AdvisorLine("MILITARYADVICESUPPORT_NOTENOUGHUNITS")
							: new AdvisorLine("MILITARYADVICESUPPORT_NOTENOUGHUNITS_BESTUNIT").Text(0, facts.BestUnit);
					}
					return new AdvisorLine("MILITARYADVICESUPPORT_TOO_COSTLY");
				case 2:
					return facts.BarbariansNear is (string tribe, string city)
						? new AdvisorLine("MILITARYADVICEBARBARIANS").Text(0, tribe).Text(1, city) : null;
				case 3:
					return facts.CanPlantSpy ? new AdvisorLine("MILITARYADVICEMOLE") : null;
				default:
					return null;
			}
		}

		// The foreign advisor (Civ3 FUN_0042ce80's case 3): about the leader
		// picked on the screen, going round its three topics (FUN_0053fc70);
		// otherwise about a leader and topic picked at random
		// (FUN_0053f1e0(-1, 0)), or "Make more friends!" with no leader to
		// talk about.
		internal AdvisorLine Foreign(int step, AdvisorFacts facts, Random rng) {
			if (facts.SelectedForeignCiv is int selected && selected >= 0 && selected < facts.ForeignCivs.Count) {
				foreignTopic = (foreignTopic + 1) % 3;
				AdvisorLine about = ForeignTopic(facts.ForeignCivs[selected], foreignTopic, facts.ForeignTradeView);
				if (about != null) {
					return about;
				}
			}
			if (step != 0) {
				return null;
			}
			if (facts.ForeignCivs.Count > 0) {
				ForeignAdviceFacts civ = Pick(facts.ForeignCivs, rng);
				AdvisorLine line = ForeignTopic(civ, rng.Next(3), facts.ForeignTradeView);
				if (line != null) {
					return line;
				}
			}
			return new AdvisorLine("FOREIGNADVICERANK");
		}

		// Civ3 FUN_0053f1e0: topic 0 is the civ's attitude toward us; on the
		// trade view, topics 1 and 2 are what they could sell us and what we
		// could sell them; otherwise the best unit we know they have, and
		// what they think of our culture.
		internal static AdvisorLine ForeignTopic(ForeignAdviceFacts civ, int topic, bool tradeView) {
			switch (topic) {
				case 0:
					// Civ3 names the attitude with the foreign advisor's
					// labels ("gracious" ... "furious"), "toward us" for the
					// friendly three and "with us", with a sad face, for the
					// others.
					return civ.Attitude switch {
						null => null,
						AttitudeLevel.Annoyed or AttitudeLevel.Furious => new AdvisorLine("FA_HAPPINESS_2")
							.Text(0, civ.Noun).Text(1, ForeignReport.AttitudeWord(civ.Attitude.Value)).WithMood(AdvisorMood.Sad),
						_ => new AdvisorLine("FA_HAPPINESS_1").Text(0, civ.Noun).Text(1, ForeignReport.AttitudeWord(civ.Attitude.Value)),
					};
				case 1:
					if (tradeView) {
						if (!civ.CanTrade) {
							return new AdvisorLine("FA_NO_CAN_TRADE").Text(0, civ.Adjective);
						}
						return civ.TheirSurplus != null ? new AdvisorLine("FA_IMPORT_GOOD").Text(0, civ.Noun).Text(1, civ.TheirSurplus)
							: new AdvisorLine("FA_NO_SURPLUS").Text(0, civ.Noun);
					}
					return civ.BestUnit != null ? new AdvisorLine("FA_BEST_UNIT").Text(0, civ.Noun).Text(1, civ.BestUnit)
						: new AdvisorLine("FA_NO_KNOWLEDGE").Text(0, civ.Adjective);
				case 2:
					if (tradeView) {
						if (!civ.CanTrade) {
							return new AdvisorLine("FA_NO_CAN_TRADE").Text(0, civ.Adjective);
						}
						return civ.OurSurplus != null ? new AdvisorLine("FA_EXPORT_GOOD").Text(0, civ.Noun).Text(1, civ.OurSurplus)
							: new AdvisorLine("FA_WE_NO_SURPLUS").Text(0, civ.Noun);
					}
					return new AdvisorLine("FA_CULTURAL").Text(0, civ.Adjective).Text(1, civ.CulturalOpinion).WithMood(civ.OpinionMood);
				default:
					return null;
			}
		}

		// Civ3 FUN_0042bc80, with its topics' facts from FUN_00504fe0,
		// FUN_00504cb0 and FUN_00504e20.
		internal static AdvisorLine Culture(int step, AdvisorFacts facts, Random rng) {
			switch (step - 2) {
				case 0:
					return facts.CultureImprovementChoices.Count == 0 ? null
						: new AdvisorLine("CULTURALADVICEIMPROVEMENT").Text(0, Pick(facts.CultureImprovementChoices, rng));
				case 1: {
						if (facts.CultureOpinions.Count == 0) {
							return null;
						}
						CultureOpinionFacts opinion = Pick(facts.CultureOpinions, rng);
						return new AdvisorLine("CULTURALADVICEOPINION").Text(1, opinion.Opinion).Text(2, opinion.Adjective)
							.WithMood(opinion.Mood);
					}
				case 2:
					return facts.WonderChoices.Count == 0 ? null
						: new AdvisorLine("CULTURALADVICEWONDER").Text(0, Pick(facts.WonderChoices, rng));
				case 3:
					return new AdvisorLine("CULTURALADVICEARTS");
				default:
					return null;
			}
		}

		// Civ3 FUN_0042bfa0.
		internal static AdvisorLine Science(int step, AdvisorFacts facts) {
			switch (step) {
				case 0:
					// Compared by the number of techs, with the civs we
					// have met.
					if (facts.CivsNotAheadInScience < facts.CivsAheadInScience) {
						return new AdvisorLine("SCIENCEADVICERANK_WEAK");
					}
					return facts.CivsAheadInScience == facts.CivsNotAheadInScience
						? new AdvisorLine("SCIENCEADVICERANK_AVERAGE") : new AdvisorLine("SCIENCEADVICERANK_STRONG");
				case 1:
					// The science rate, in tenths.
					return facts.ScienceRate > 6 ? new AdvisorLine("SCIENCEADVICERANK_STRONG_FUNDING")
						: facts.ScienceRate > 2 ? new AdvisorLine("SCIENCEADVICERANK_AVERAGE_FUNDING")
						: new AdvisorLine("SCIENCEADVICERANK_WEAK_FUNDING");
				case 2:
					// Only while nothing is being researched.
					return facts.Researching || facts.RecommendedTech == null ? null
						: new AdvisorLine("SCIENCEADVICETECHRECOMMEND").Text(0, facts.RecommendedTech);
				default:
					return null;
			}
		}

		// Civ3 FUN_0042c4d0: one time in 500, a joke. A topic is picked at
		// random and, if it doesn't suit the advisor, the next is tried,
		// until the catch-all "Hail to the $TITLE0, baby!".
		internal static AdvisorLine Joke(AdvisorKind advisor, AdvisorFacts facts, Random rng) {
			if (rng.Next(JokeOdds) != 0) {
				return null;
			}
			int topic = rng.Next(8);
			for (int i = 0; i < 8; i++, topic = (topic + 1) % 8) {
				AdvisorLine line = JokeTopic(topic, advisor, facts, rng);
				if (line != null) {
					return line;
				}
			}
			return null;
		}

		private static AdvisorLine JokeTopic(int topic, AdvisorKind advisor, AdvisorFacts facts, Random rng) {
			switch (topic) {
				case 1:
					// Late at night, by the computer's clock.
					return facts.LocalHour > 22 || facts.LocalHour < 7 ? new AdvisorLine("TIMESLEEP") : null;
				case 2:
					return advisor == AdvisorKind.Culture ? new AdvisorLine("MYSTERIOUS") : null;
				case 3:
					return advisor == AdvisorKind.Foreign && facts.OtherCivNames.Count > 0
						? new AdvisorLine("GOODEVIL").Text(0, facts.OurCivName).Text(1, Pick(facts.OtherCivNames, rng)) : null;
				case 4:
					return advisor == AdvisorKind.Military && facts.NuclearWeaponsAllowed ? new AdvisorLine("MILITARYNUKE") : null;
				case 5: {
						if (advisor != AdvisorKind.Domestic) {
							return null;
						}
						CityAdviceFacts burning = facts.Cities.FirstOrDefault(c => c.InDisorder);
						return burning == null ? null : new AdvisorLine("DISORDERNEWS").Text(0, burning.Name);
					}
				case 6:
					return advisor == AdvisorKind.Science ? new AdvisorLine("CARROT") : null;
				case 7:
					return advisor == AdvisorKind.Trade ? new AdvisorLine("ADVISORANGRY") : null;
				default:
					return new AdvisorLine("ADVISORRANDDEF").Text(0, facts.LeaderTitle);
			}
		}
	}
}
