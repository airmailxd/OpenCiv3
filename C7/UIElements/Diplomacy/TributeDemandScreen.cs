using C7Engine;
using C7GameData;
using Godot;

// An AI demands tribute from the human, gold or a tech, or it will declare
// war.
public partial class TributeDemandScreen : TextureRect {
	private ID humanPlayerId;
	private ID opponentPlayerId;
	private int gold;
	private string techName;

	Theme fontTheme = new();

	public TributeDemandScreen(ID humanPlayer, ID opponentPlayer, int gold, string techName) {
		this.humanPlayerId = humanPlayer;
		this.opponentPlayerId = opponentPlayer;
		this.gold = gold;
		this.techName = techName;
	}

	public override void _Ready() {
		fontTheme.DefaultFont = FixedSizeFonts.Get("res://Fonts/NotoSans-Regular.ttf", 14);
		this.Texture = TextureLoader.Load("diplomacy.offer");

		string adjective = "";
		EngineStorage.ReadGameData((GameData gD) => {
			Player opponent = gD.GetPlayer(opponentPlayerId);
			GetParent<Diplomacy>().AddLeaderHeadAndLabel(this, gD, opponent, gD.GetPlayer(humanPlayerId), fontTheme);
			adjective = opponent.civilization.noun;
		});

		string tribute = gold > 0 ? $"{gold} gold" : $"the secret of {techName}";
		string demand = $"Our armies are mighty, and yours are few.\nGive us {tribute} as tribute, or face the wrath of the {adjective}!";
		Label message = new();
		message.Theme = fontTheme;
		message.SetPosition(new Vector2(0, 400));
		AddChild(message);
		message.SetTextAndCenterLabel(demand);

		int offset = 500;
		int gap = 20;
		AddAnswer("Very well, we will pay.", offset, pay: true);
		AddAnswer("We will never pay you tribute!", offset + gap, pay: false);
	}

	private void AddAnswer(string text, int y, bool pay) {
		Button answer = new();
		answer.Text = text;
		answer.SetPosition(new Vector2(512 - 205, y));
		answer.Theme = fontTheme;
		answer.Pressed += () => {
			// Answer for the human this was put to, who in a hotseat game
			// may not be the player the UI is following.
			new MsgRespondToTributeDemand(pay) { playerID = humanPlayerId }.send();
			GetParent<Diplomacy>().Hide();
		};
		AddChild(answer);
	}
}
