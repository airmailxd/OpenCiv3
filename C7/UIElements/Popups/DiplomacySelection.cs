using Godot;
using C7GameData;
using System.Collections.Generic;
using System.Linq;

// The popup for selecting which other civilization to contact.
public partial class DiplomacySelection : Popup {
	private Player player;
	private List<Player> allPlayers;

	public DiplomacySelection(Player player, List<Player> allPlayers) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 200);
		this.player = player;
		this.allPlayers = allPlayers;
	}

	public override void _Ready() {
		base._Ready();

		// Only civs that are still alive can be contacted.
		List<Player> contacts = allPlayers
			.Where(p => !p.defeated && PlayerRelationship.TryGetRelationship(player, p, out _))
			.ToList();

		int width = 530;
		int height = 115 + 25 * contacts.Count;
		AddTexture(width, height);
		AddBackground(width, height);
		AddHeader("Pick the civilization...", 10);

		int vOffset = 65;
		foreach (Player other in contacts) {
			string status = PlayerRelationship.AtWar(player, other) ? "War" : "Peace";
			AddButton($"{other.civilization.noun} (at {status})", vOffset, () => {
				Node parent = GetParent();
				parent.EmitSignal(PopupOverlay.SignalName.HidePopup);
				parent.EmitSignal(PopupOverlay.SignalName.DiplomacySelection, new ParameterWrapper<ID>(other.id));
			});
			vOffset += 25;
		}

		// TODO: Do something when the confirm button is pressed.
		AddConfirmButton(new Vector2(width - 55, height - 47), () => { });
		AddCancelButton(new Vector2(width - 30, height - 47));
	}
}
