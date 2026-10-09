using System;
using C7Engine.Network;
using C7GameData;
using Godot;

// What a LAN host can do about a guest, from the lobby or in the game:
// remove them, which frees their seats for anyone, or ban them, which also
// keeps them out of this game for good.
public static class HostActions {
	// Removes the guest in the seat, straight away.
	public static void Remove(ID playerID) {
		LanSession.Host?.Kick(playerID);
	}

	// Asks the host to confirm banning the guest in the seat, then bans them.
	public static void ConfirmBan(Node parent, string name, ID playerID) {
		Confirm(parent, name, () => LanSession.Host?.Kick(playerID, ban: true));
	}

	public static void ConfirmBanSpectator(Node parent, string name) {
		Confirm(parent, name, () => LanSession.Host?.KickSpectator(name, ban: true));
	}

	private static void Confirm(Node parent, string name, Action ban) {
		ConfirmationDialog dialog = new() {
			Title = "Ban Player",
			DialogText = $"Ban {name} from this game?\nThey're removed now, and can't join it again, even after you resume it.",
			OkButtonText = "Ban",
		};
		dialog.Confirmed += ban;
		// The dialog is made for each ban, so free it once it closes.
		dialog.VisibilityChanged += () => {
			if (!dialog.Visible) {
				dialog.QueueFree();
			}
		};
		parent.AddChild(dialog);
		dialog.PopupCentered();
	}
}
