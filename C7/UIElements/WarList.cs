using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Godot;

// The wars going on in a game the user watches the AIs play (observer
// mode), shown at each pause instead of announcing each war as it starts:
// who is at war with whom and since when, and the wars that started or
// ended since the last pause.
public partial class WarList : CanvasLayer {
	private sealed class War {
		public int sinceTurn;
		// Who declared it, if we heard.
		public Player aggressor;
	}

	private readonly Dictionary<(Player, Player), War> wars = new();
	private readonly List<string> sinceLastPause = new();
	private readonly Dictionary<(Player, Player), Player> declaredBy = new();
	private GameData trackedGame;
	// Whether the wars going on when watching began have been noted, so
	// that only later ones are news.
	private bool noted;

	private PanelContainer panel;
	private Label body;

	public override void _Ready() {
		Layer = 90;
		panel = new PanelContainer { Visible = false };
		panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		panel.Position = new Vector2(16, 80);
		panel.AddThemeStyleboxOverride("panel", TemporaryPopup.PopupStyleBox());

		VBoxContainer box = new();
		box.AddThemeConstantOverride("separation", 6);
		panel.AddChild(box);

		HBoxContainer header = new();
		Label title = new() { Text = "Wars", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		title.AddThemeFontSizeOverride("font_size", 18);
		title.AddThemeColorOverride("font_color", Colors.White);
		header.AddChild(title);
		Button hide = new() { Text = "Hide" };
		hide.Pressed += () => panel.Visible = false;
		header.AddChild(hide);
		box.AddChild(header);

		body = new Label();
		body.AddThemeFontSizeOverride("font_size", 15);
		body.AddThemeColorOverride("font_color", Colors.White);
		box.AddChild(body);

		AddChild(panel);
	}

	// The same key for a pair whichever way round they're given.
	private static (Player, Player) Key(Player x, Player y) {
		return string.CompareOrdinal(x.id.ToString(), y.id.ToString()) <= 0 ? (x, y) : (y, x);
	}

	// Notes who declared a war, from the engine's announcement of it.
	public void RecordDeclaration(Player aggressor, Player opponent) {
		declaredBy[Key(aggressor, opponent)] = aggressor;
	}

	// Looks at who is at war at the start of a turn, noting the wars that
	// started or ended since the last look.
	public void Track(GameData gameData) {
		if (trackedGame != gameData) {
			trackedGame = gameData;
			noted = false;
			wars.Clear();
			declaredBy.Clear();
			sinceLastPause.Clear();
		}
		int turn = gameData.turn;
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();
		HashSet<(Player, Player)> atWar = new();
		for (int i = 0; i < civs.Count; ++i) {
			for (int j = i + 1; j < civs.Count; ++j) {
				Player x = civs[i], y = civs[j];
				if (x.playerRelationships.TryGetValue(y.id, out PlayerRelationship r) && r.AtWar()) {
					atWar.Add(Key(x, y));
				}
			}
		}

		foreach ((Player, Player) key in atWar) {
			if (!wars.ContainsKey(key)) {
				declaredBy.Remove(key, out Player aggressor);
				wars[key] = new War { sinceTurn = turn, aggressor = aggressor };
				if (noted) {
					sinceLastPause.Add(aggressor != null
						? $"Turn {turn}: the {aggressor.civilization.noun} declared war on the {Other(key, aggressor).civilization.noun}"
						: $"Turn {turn}: war between the {key.Item1.civilization.noun} and the {key.Item2.civilization.noun}");
				}
			}
		}
		noted = true;
		foreach ((Player, Player) key in wars.Keys.ToList()) {
			if (!atWar.Contains(key)) {
				int length = turn - wars[key].sinceTurn;
				wars.Remove(key);
				string how = key.Item1.defeated || key.Item2.defeated ? "ended" : "ended in peace";
				sinceLastPause.Add($"Turn {turn}: the war between the {key.Item1.civilization.noun} and the {key.Item2.civilization.noun} {how} after {length} turns");
			}
		}
	}

	private static Player Other((Player, Player) key, Player one) => key.Item1 == one ? key.Item2 : key.Item1;

	// Shows the list, as the game pauses.
	public void ShowAtPause(int turn) {
		List<string> lines = new();
		if (wars.Count == 0) {
			lines.Add("No civilizations are at war.");
		} else {
			lines.Add("At war now:");
			foreach (((Player a, Player b), War war) in wars.OrderBy(w => w.Value.sinceTurn)) {
				string who = war.aggressor != null
					? $"the {war.aggressor.civilization.noun} against the {Other((a, b), war.aggressor).civilization.noun}"
					: $"the {a.civilization.noun} and the {b.civilization.noun}";
				lines.Add($"  {who}, since turn {war.sinceTurn} ({turn - war.sinceTurn} turns)");
			}
		}
		if (sinceLastPause.Count > 0) {
			lines.Add("");
			lines.Add("Since the last pause:");
			// The latest few, newest last.
			foreach (string line in sinceLastPause.Skip(System.Math.Max(0, sinceLastPause.Count - 15))) {
				lines.Add("  " + line);
			}
			if (sinceLastPause.Count > 15) {
				lines.Add($"  (and {sinceLastPause.Count - 15} earlier)");
			}
		}
		body.Text = string.Join("\n", lines);
		panel.Visible = true;
	}

	// Hides the list as the game goes on, starting a new "since the last
	// pause".
	public void HideAndContinue() {
		panel.Visible = false;
		sinceLastPause.Clear();
	}
}
