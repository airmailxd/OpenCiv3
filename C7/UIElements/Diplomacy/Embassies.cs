using System.Collections.Generic;
using System.Linq;
using System.Text;
using C7Engine;
using C7GameData;

// The embassy pages opened from the "E" button: a list of the civs we've met,
// and for each what our embassy there reported of their capital when it was
// established, with the missions we could send them.
public static class Embassies {
	// The longest line the popups show before wrapping.
	private const int LineLength = 70;

	public static void ShowSelection(PopupOverlay popupOverlay, Player player) {
		List<ChoicePopup.Choice> choices = new();
		EngineStorage.ReadGameData((GameData gameData) => {
			foreach (Player other in gameData.players) {
				if (other.defeated || !PlayerRelationship.TryGetRelationship(player, other, out PlayerRelationship pr)) {
					continue;
				}
				string status = pr.hasEmbassy ? "embassy" : "no embassy";
				choices.Add(new ChoicePopup.Choice($"{other.civilization.noun} ({status})",
					() => Show(popupOverlay, player, other)));
			}
		});
		popupOverlay.ShowPopup(new ChoicePopup("Embassies", "Pick the civilization...", choices),
			PopupOverlay.PopupCategory.Info);
	}

	public static void Show(PopupOverlay popupOverlay, Player player, Player target) {
		string message = "";
		List<ChoicePopup.Choice> choices = new();
		EngineStorage.ReadGameData((GameData gameData) => {
			message = Describe(gameData, player, target);
			choices = MissionChoices(gameData, player, target, null);
		});
		popupOverlay.ShowPopup(new ChoicePopup($"The {target.civilization.noun}", message, choices),
			PopupOverlay.PopupCategory.Info);
	}

	private static string Describe(GameData gameData, Player player, Player target) {
		if (!PlayerRelationship.TryGetRelationship(player, target, out PlayerRelationship pr) || !pr.hasEmbassy) {
			return $"We have no embassy with the {target.civilization.noun}.\nTreasury: {player.gold} gold";
		}
		Espionage.CityReport report = pr.embassyReport;
		if (report == null) {
			return $"We have an embassy with the {target.civilization.noun}, but it has sent no report.";
		}
		string when = gameData.timeOptions.GetDisplayTime(report.turn);
		return Wrap($"Our embassy in {report.cityName} reported in {when}:\n" + Espionage.DescribeReport(report));
	}

	// The diplomatic and espionage missions the player could send against a
	// civ, or one of its cities, with their cost and chance of success. The
	// ones that can't be sent are listed greyed out, with the reason.
	public static List<ChoicePopup.Choice> MissionChoices(GameData gameData, Player player, Player target, City city) {
		List<ChoicePopup.Choice> choices = new();
		foreach (Espionage.MissionOption option in Espionage.GetOptions(gameData, player, target, city)) {
			EspionageMission mission = option.mission;
			string chance = option.successPercent >= 100 ? "" : $", {option.successPercent}% chance";
			string label = $"{Espionage.Describe(mission)} ({option.cost} gold{chance})";
			if (option.available) {
				choices.Add(new ChoicePopup.Choice(label,
					() => new MsgPerformEspionage(mission, target, Espionage.TargetsCity(mission) ? city : null).send()));
			} else {
				choices.Add(new ChoicePopup.Choice($"{Espionage.Describe(mission)}: {option.reason}", null));
			}
		}
		return choices;
	}

	// Breaks the lines of the text that are too long for a popup at their
	// spaces, since the popups' labels don't wrap.
	public static string Wrap(string text) {
		StringBuilder result = new();
		foreach (string line in text.Split('\n')) {
			if (result.Length > 0) {
				result.Append('\n');
			}
			int lineStart = result.Length;
			foreach (string word in line.Split(' ')) {
				if (result.Length > lineStart && result.Length - lineStart + 1 + word.Length > LineLength) {
					result.Append("\n  ");
					lineStart = result.Length - 2;
				} else if (result.Length > lineStart) {
					result.Append(' ');
				}
				result.Append(word);
			}
		}
		return result.ToString();
	}
}
