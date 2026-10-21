using C7Engine;
using C7GameData;
using Serilog;

// Shows Civ3's tutorial tips (see C7GameData.Tutorial) as the domestic
// advisor's popups, with the text of the Civ3 install's text\tutorial.txt.
// The tutorial is only for a game with one human at the computer: Civ3 asks
// whether the game is a multiplayer one before each tip, and its Preferences
// grey out Tutorial Mode in one (FUN_00595aa0).
public static class TutorialGuide {
	private static readonly ILogger log = LogManager.ForContext<TutorialText>();

	public const string TextFile = "Text/tutorial.txt";

	private static TutorialText text;
	private static string textPath;

	// The tutorial text for the current game's media (a scenario may have
	// its own), or null without one, as in standalone mode.
	public static TutorialText Text {
		get {
			string path = Util.HasCiv3Media(TextFile) ? Util.Civ3MediaPath(TextFile) : null;
			if (path != textPath || (text == null && path != null)) {
				textPath = path;
				text = path == null ? null : TutorialText.Load(path);
			}
			return text?.Count > 0 ? text : null;
		}
	}

	// Why Tutorial Mode can't be turned on in this game, or null if it can.
	public static string Unavailable(GameData gameData) {
		if (gameData == null) {
			return "Tutorial Mode is turned on in a game.";
		}
		if (IsMultiplayer(gameData)) {
			return "The tutorial is not available in multiplayer games.";
		}
		if (Text == null) {
			return "The tutorial needs Civ3's tutorial text (Text/tutorial.txt), which wasn't found.";
		}
		return null;
	}

	public static bool IsMultiplayer(GameData gameData) {
		return LanSession.IsActive || TurnHandling.IsHotseat(gameData);
	}

	private static bool IsOn(GameData gameData) {
		return gameData != null && gameData.tutorial.enabled && Unavailable(gameData) == null;
	}

	// The unit the player is now to move: the first-move, worker and
	// military tips.
	public static void OnUnitActive(PopupOverlay overlay, MapUnit unit) {
		EngineStorage.ReadGameData(gameData => {
			if (!IsOn(gameData) || unit == null || unit == MapUnit.NONE || unit.owner?.isHuman != true) {
				return;
			}
			Show(overlay, Tutorial.ForActiveUnit(gameData, unit, Text.Variables));
		});
	}

	// The start of the player's turn: a city of theirs in disorder.
	public static void OnTurnStart(PopupOverlay overlay, Player player) {
		EngineStorage.ReadGameData(gameData => {
			if (!IsOn(gameData) || player?.isHuman != true) {
				return;
			}
			Show(overlay, Tutorial.ForCivilDisorder(gameData, player));
		});
	}

	// The city screen opening on one of the player's cities.
	public static void OnCityScreenOpened(PopupOverlay overlay, City city) {
		EngineStorage.ReadGameData(gameData => {
			if (!IsOn(gameData) || city?.owner?.isHuman != true) {
				return;
			}
			foreach (TutorialTip tip in Tutorial.ForCityScreen(gameData)) {
				Show(overlay, tip);
			}
		});
	}

	private static void Show(PopupOverlay overlay, TutorialTip tip) {
		if (tip == null || overlay == null) {
			return;
		}
		TutorialText tutorial = Text;
		TutorialEntry entry = tutorial?.Get(tip.Key);
		string message = tutorial?.PlainTextOf(tip);
		if (entry == null || message == null) {
			log.Warning("No tutorial text for {key}", tip.Key);
			return;
		}
		if (!System.Enum.TryParse(entry.Advisor, ignoreCase: true, out AdvisorHead.Advisor advisor)) {
			advisor = AdvisorHead.Advisor.Domestic;
		}
		if (!System.Enum.TryParse(entry.Mood, ignoreCase: true, out AdvisorHead.Mood mood)) {
			mood = AdvisorHead.Mood.Happy;
		}
		overlay.ShowPopup(new InformationalPopup(message, advisor, mood), PopupOverlay.PopupCategory.Advisor);
	}
}
