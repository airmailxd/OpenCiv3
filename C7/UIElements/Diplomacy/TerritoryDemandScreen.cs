using C7Engine;
using C7GameData;
using Godot;

// An AI tells the human to take their units out of its territory, or it
// will declare war.
public partial class TerritoryDemandScreen : TextureRect {
	private ID humanPlayerId;
	private ID opponentPlayerId;
	private int unitCount;
	private bool repeatOffense;

	Theme fontTheme = new();

	public TerritoryDemandScreen(ID humanPlayer, ID opponentPlayer, int unitCount, bool repeatOffense) {
		this.humanPlayerId = humanPlayer;
		this.opponentPlayerId = opponentPlayer;
		this.unitCount = unitCount;
		this.repeatOffense = repeatOffense;
	}

	public override void _Ready() {
		fontTheme.DefaultFont = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 14);
		this.Texture = TextureLoader.Load("diplomacy.offer");

		string adjective = "";
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponent = gD.GetPlayer(opponentPlayerId);
			GetParent<Diplomacy>().AddLeaderHeadAndLabel(this, opponent, fontTheme);
			adjective = opponent.civilization.noun;
		});

		string units = unitCount == 1 ? "a unit" : "units";
		string demand = repeatOffense
			? $"You promised to keep out of our lands, yet you have {units} in our territory again!\nWithdraw at once, or we will drive you out by force!"
			: $"You have {units} in our territory without our permission.\nRemove them immediately, or face the wrath of the {adjective}!";
		Label message = new();
		message.Theme = fontTheme;
		message.SetPosition(new Vector2(0, 400));
		AddChild(message);
		message.SetTextAndCenterLabel(demand);

		int offset = 500;
		int gap = 20;
		AddAnswer("Very well, we will withdraw.", offset, withdraw: true);
		AddAnswer("We will go where we please!", offset + gap, withdraw: false);
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
