using C7Engine;
using C7GameData;
using Godot;

// The page that introduces a custom scenario (Eternal War, Rising Tide,
// Kings & Nomads) when a new game of it starts: what the world is like,
// how to win, its rules, and a few tips (owner's request, 2026-10-23).
public partial class ScenarioIntroPopup : Popup {
	private const int Width = 720;
	private const int Height = 560;

	private readonly string title;
	private readonly string body;

	private ScenarioIntroPopup(string title, string body) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 60);
		this.title = title;
		this.body = body;
	}

	// The page for the scenario, or null for a normal game.
	public static ScenarioIntroPopup ForScenario(GameScenario scenario) {
		return scenario switch {
			GameScenario.EternalWar => new ScenarioIntroPopup("Eternal War", EternalWarText),
			GameScenario.RisingTide => new ScenarioIntroPopup("Rising Tide", RisingTideText),
			GameScenario.KingsAndNomads => new ScenarioIntroPopup("Kings & Nomads", KingsAndNomadsText),
			_ => null,
		};
	}

	// The page for the game's scenario, or null if it isn't one.
	public static ScenarioIntroPopup For(GameData gameData) {
		Rules rules = gameData?.rules;
		if (rules == null) {
			return null;
		}
		if (rules.RisingTide) {
			return ForScenario(GameScenario.RisingTide);
		}
		if (CityStateTrade.IsOn(gameData)) {
			return ForScenario(GameScenario.KingsAndNomads);
		}
		if (rules.StagingUnits && rules.PermanentWar) {
			return ForScenario(GameScenario.EternalWar);
		}
		return null;
	}

	public override void _Ready() {
		base._Ready();
		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader(title, 10);

		RichTextLabel text = new() {
			BbcodeEnabled = true,
			Text = body,
			ScrollActive = true,
			FitContent = false,
			CustomMinimumSize = new Vector2(Width - 60, Height - 140),
			Size = new Vector2(Width - 60, Height - 140),
		};
		text.AddThemeColorOverride("default_color", Colors.Black);
		text.AddThemeFontSizeOverride("normal_font_size", 15);
		text.AddThemeFontSizeOverride("bold_font_size", 16);
		text.SetPosition(new Vector2(30, 55));
		AddChild(text);

		AddConfirmButton(new Vector2(Width - 66, Height - 66), () => GetParent<PopupOverlay>().OnHidePopup());
	}

	private const string EternalWarText =
		"[b]The world[/b]\n"
		+ "One great continent, and every civilization at war with every other, for good. No one may talk, trade or make peace. "
		+ "You begin in the Middle Ages, knowing every Ancient advance, under Monarchy, with your cities scattered among your enemies'.\n\n"
		+ "[b]How to win[/b]\n"
		+ "Conquer. No one can build Settlers, so cities change hands only by force.\n\n"
		+ "[b]The Mercenary Market[/b]\n"
		+ "Each turn, before anyone moves, a few units go up for sale (more on bigger worlds). Every civilization bids gold in secret, "
		+ "within 15 seconds, and its bids can't add up to more than its treasury. The highest bid wins and pays; civilizations tied "
		+ "for the highest each get one. You then choose which city each unit you won reports to; a ship sent inland puts to sea at the "
		+ "nearest coast.\n\n"
		+ "[b]Other rules[/b]\n"
		+ "- No corruption, and fewer citizens are born unhappy.\n"
		+ "- Cities work tiles three rings out, grow to 12 without an aqueduct, and keep their library when taken.\n"
		+ "- Every tile makes a commerce more; forest on tundra a food more; desert (not flood plains) a shield more.\n"
		+ "- Workers defend but can't attack; one beaten in battle becomes the winner's slave.\n"
		+ "- Coastal buildings can be built within three tiles of the sea.\n"
		+ "- Governments support three times the usual units for free.\n\n"
		+ "[b]Tips[/b]\n"
		+ "Keep gold in hand for the market: a well-timed bid can buy the army your cities can't build. "
		+ "Guard your workers, and strike the enemy cities nearest yours before they strike you.";

	private const string RisingTideText =
		"[b]The world[/b]\n"
		+ "One continent, and the sea is rising. Every so often a [b]Tidal Wave[/b] turns all the land touching the sea into coast, "
		+ "a ring at a time. Cities on that land are destroyed, and units standing there drown. The waves come ever faster: "
		+ "21 turns, then 13, 8, 5, 5, 3, 3, 2, 2, 1, and then every turn, until no land is left. "
		+ "The box at the top right counts down to the next wave.\n\n"
		+ "[b]How to win[/b]\n"
		+ "Be the last civilization standing.\n\n"
		+ "[b]Rules[/b]\n"
		+ "- Everyone starts on the shore, in the Ancient Times, with a city of size 5, three Workers and two Archers "
		+ "(and a Scout, if expansionist). The first wave takes that city.\n"
		+ "- Settlers found cities of size 5, so a city's people can move inland ahead of the tide.\n"
		+ "- A city's own tile makes 6 food, enough for three citizens (agricultural civilizations get one more).\n"
		+ "- The sea is no refuge: ships can't be built, and any boat that leaves port sinks.\n"
		+ "- You survive while you hold a city or a Settler on land.\n\n"
		+ "[b]Tips[/b]\n"
		+ "Land far from the sea lasts longest; the heart of the continent is the last ground left, and everyone wants it. "
		+ "Build Settlers in cities about to drown, and don't spend shields on buildings you'll lose. "
		+ "Get your units off the shore before the wave.";

	private const string KingsAndNomadsText =
		"[b]The world[/b]\n"
		+ "No one starts with a city. You are a [b]King[/b] at the head of a wandering horde: two Archers, two Horsemen, a Swordsman, "
		+ "a Warrior and a Worker. Independent [b]city-states[/b] dot the land; their garrisons grow every 10 turns, and they raise walls "
		+ "on turn 40.\n\n"
		+ "[b]How to win[/b]\n"
		+ "Be the last King standing, or hold half of all the cities in the world.\n\n"
		+ "[b]Rules[/b]\n"
		+ "- If your King dies, your civilization falls, and its cities become independent again.\n"
		+ "- The King leads from the front: your units on his tile or next to it fight at double strength, attacking and defending.\n"
		+ "- A city-state's units fight at +50% inside their own city.\n"
		+ "- Settlers can only be built in cities you have taken from someone else.\n"
		+ "- City-states never build Settlers, but you can trade with them: a road from one of your cities to a city-state "
		+ "brings you the luxuries and strategic resources inside its borders, and 3 gold a turn.\n"
		+ "- Played on continents or a pangaea; every civilization starts with city-states nearby.\n\n"
		+ "[b]Tips[/b]\n"
		+ "Strike the nearest city-state early, before its garrison grows and its walls go up. "
		+ "March your King with your army for double strength in battle, but keep him well guarded: lose him and you lose everything. "
		+ "Choose which city-states to conquer and which to trade with, and escort your Workers outside your borders.";
}
