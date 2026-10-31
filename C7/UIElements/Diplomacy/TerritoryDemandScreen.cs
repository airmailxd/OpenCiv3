using C7Engine;
using C7GameData;
using Godot;

// An AI tells the human to take their units out of its territory: first a
// warning, then, if they're still there, leave or declare war (see
// TerritoryDemands). The words are Civ3's (Conquests Text/diplomacy.txt).
public partial class TerritoryDemandScreen : TextureRect {
	private ID humanPlayerId;
	private ID opponentPlayerId;
	private int unitCount;
	private bool ultimatum;
	private string cityName;

	Theme fontTheme = new();

	// #AIBORDERWARNING. Civ3 picks a line by the AI's civ and mood; these
	// three are lines of its that name no civ in particular, by mood
	// (friendly, cautious, hostile).
	private static readonly string[] Warnings = [
		"Greetings, $PLAYER0. It appears that some of your troops have - inadvertently, I'm sure - wandered into our territory, near $CITY3. Kindly remove them.",
		"You have troops near $CITY3. I request that you remove them. Immediately.",
		"Your troops around $CITY3 are in $CIVADJ6 sovereign territory. Remove them immediately.",
	];

	// The same, for an AI with no city to name.
	private static readonly string[] WarningsWithoutCity = [
		"$PLAYER0, $CIVADJ2 troops have been spotted within our borders. While I am not upset by their presence, I am startled by their sheer ugliness. Please recall them at once.",
		"Perhaps your generals in the field are too generous with the spirits. Remove your forces from $CIVADJ6 territory immediately.",
		"Finally you provide us with the needed carelessness to eliminate you from this world. Or do you. Remove your brigands from our lands or die on our hostile plains.",
	];

	// #AIBORDERWAR, its three lines, by mood. Here $CIVNAME1 and $CIVADJ2
	// are the AI's.
	private static readonly string[] Ultimatums = [
		"$PLAYER0, are you aware that your movement of troops into $CIVADJ2 territory constitutes a declaration of war against $CIVNAME1? Send them home IMMEDIATELY and we'll forget the whole unfortunate incident.",
		"Your recent movement of troops into our territory constitutes a declaration of war against $CIVNAME1. Send them home immediately or know that YOU will be held responsible for the conflict!",
		"Warmongering fool! Know that your invasion of our territory constitutes a declaration of war against $CIVNAME1! Send your soldiers home immediately or they will be destroyed - and YOU will be held responsible for the bloodshed!",
	];

	// The human's answers: #USERWILLRETREAT to the warning,
	// #USERWILLRETURNHOME to leave or declare war, and #USERWAR to either.
	private const string WillRetreat = "We apologize for the mistake. We will move our troops out of your territory.";
	private const string WillReturnHome = "Our mistake. <Your troops will move automatically.>";
	private const string War = "That's it! Prepare for WAR!";

	public TerritoryDemandScreen(ID humanPlayer, ID opponentPlayer, int unitCount, bool ultimatum, string cityName) {
		this.humanPlayerId = humanPlayer;
		this.opponentPlayerId = opponentPlayer;
		this.unitCount = unitCount;
		this.ultimatum = ultimatum;
		this.cityName = cityName;
	}

	public override void _Ready() {
		fontTheme.DefaultFont = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 14);
		this.Texture = TextureLoader.Load("diplomacy.offer");

		string demand = "";
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponent = gD.GetPlayer(opponentPlayerId);
			Player human = gD.GetPlayer(humanPlayerId);
			GetParent<Diplomacy>().AddLeaderHeadAndLabel(this, gD, opponent, human, fontTheme);

			int mood = Attitudes.Level(gD, opponent, human) switch {
				AttitudeLevel.Gracious or AttitudeLevel.Polite => 0,
				AttitudeLevel.Cautious => 1,
				_ => 2,
			};
			if (ultimatum) {
				demand = Ultimatums[mood]
					.Replace("$PLAYER0", human.civilization.leader)
					.Replace("$CIVNAME1", opponent.civilization.name)
					.Replace("$CIVADJ2", opponent.civilization.adjective);
			} else {
				demand = (cityName != null ? Warnings[mood] : WarningsWithoutCity[mood])
					.Replace("$PLAYER0", human.civilization.leader)
					.Replace("$CIVADJ2", human.civilization.adjective)
					.Replace("$CITY3", cityName ?? "")
					.Replace("$CIVADJ6", opponent.civilization.adjective);
			}
		});

		Label message = new();
		message.Theme = fontTheme;
		message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		message.CustomMinimumSize = new Vector2(700, 0);
		message.Size = new Vector2(700, 0);
		message.SetPosition(new Vector2(0, 400));
		AddChild(message);
		message.SetTextAndCenterLabel($"\"{demand}\"");

		int offset = 500;
		int gap = 20;
		AddAnswer(ultimatum ? WillReturnHome : WillRetreat, offset, withdraw: true);
		AddAnswer(War, offset + gap, withdraw: false);
	}

	private void AddAnswer(string text, int y, bool withdraw) {
		Button answer = new();
		answer.Text = text;
		answer.SetPosition(new Vector2(512 - 205, y));
		answer.Theme = fontTheme;
		answer.Pressed += () => {
			// Answer for the human this was put to, who in a hotseat game
			// may not be the player the UI is following.
			new MsgRespondToTerritoryDemand(withdraw) { playerID = humanPlayerId }.send();
			GetParent<Diplomacy>().Hide();
		};
		AddChild(answer);
	}
}
