using Godot;
using System;
using System.Diagnostics;
using C7GameData;
using C7Engine;
using C7GameData.Save;
using System.Collections.Generic;
using Serilog;

// The popup for picking a new government once anarchy is over.
public partial class GovernmentSelection : Popup {
	private Player player;
	private List<Government> governments;

	// Called when the popup goes away, with whether a government was picked.
	private readonly Action<bool> onClosed;
	private bool chosen = false;

	public GovernmentSelection(Player player, List<Government> governments, Action<bool> onClosed = null) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 200);
		this.player = player;
		this.governments = governments;
		this.onClosed = onClosed;
	}

	// A government must be picked.
	public override bool OnEscape() {
		return false;
	}

	public override void _Ready() {
		base._Ready();

		int width = 530;
		int height = 115 + 25 * governments.Count;
		AddTexture(width, height);
		AddBackground(width, height);
		AddHeader("Select a new government type", 10);

		int vOffset = 65;
		foreach (Government g in governments) {
			AddButton($"{g.name}", vOffset, () => {
				Node parent = GetParent();
				chosen = true;
				new SelectGovernmentMsg(g).send();
				parent.EmitSignal(PopupOverlay.SignalName.HidePopup);
			});
			vOffset += 25;
		}
	}

	public override void _ExitTree() {
		onClosed?.Invoke(chosen);
		base._ExitTree();
	}

}
