using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Network;
using Godot;
using static SetupStyle;

// How players join a game this computer hosts, and who may, for the foot of
// the Multiplayer Setup screen's Game Settings: the addresses to type, the
// password, and spectators. Used by the setup screen while the host sets up
// a new game, and by the lobby while hosting a saved one.
public partial class HostJoiningSection : VBoxContainer {
	private LineEdit passwordEdit;
	private Label passwordStatus;
	private bool passwordEdited;
	private Civ3Checkbox spectatorsBox;
	private OptionButton spectatorViewChoice;

	// Godot makes nodes with no arguments; Build fills it in.
	public HostJoiningSection() { }

	public void Build(Control fontSource) {
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 2);
		Label heading = MakeHeading(fontSource, "JOINING", 13);
		heading.CustomMinimumSize = new Vector2(0, 26);
		heading.VerticalAlignment = VerticalAlignment.Bottom;
		AddChild(heading);
		AddJoinAddresses();
		AddAccess();
	}

	private static Label MakeNote(string text) {
		Label note = new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		note.AddThemeFontSizeOverride("font_size", 12);
		return note;
	}

	// The addresses players type to join, if the game isn't listed for them.
	private void AddJoinAddresses() {
		List<(string address, string network)> addresses = LanDiscovery.LocalAddressesByNetwork();
		string port = LanSession.Host.Port == LanProtocol.DefaultPort ? "" : $":{LanSession.Host.Port}";
		const string tip = "Players on your network see this game under \"Join LAN Game\". If it isn't listed for them, "
			+ "they type the address on the same network as theirs: usually the one starting with 192.168. or 10.";
		List<string> local = addresses.Where(a => a.network == null).Select(a => a.address + port).ToList();
		AddChild(MakeSettingLabel("Address:"));
		if (local.Count == 0) {
			AddChild(MakeValue(addresses.Count == 0 ? "No network connection found" : "None on a local network", tip));
		}
		foreach (string address in local) {
			AddChild(MakeValue(address, tip));
		}
		// Players on the internet join through a virtual network app, which
		// doesn't list games, so they type the address it gives this computer.
		foreach (var (address, network) in addresses.Where(a => a.network != null)) {
			AddChild(MakeSettingLabel($"{network} Address:"));
			AddChild(MakeValue(address + port, $"Players on your {network} network type this to join."));
		}
	}

	// Who may come in: the game's password, and whether anyone may watch.
	private void AddAccess() {
		LanHost host = LanSession.Host;
		AddChild(MakeSettingLabel("Password:"));
		// A resumed game keeps its password, which isn't kept as itself, so
		// it can only be changed, not shown.
		passwordEdit = MakeDarkField();
		passwordEdit.Secret = true;
		passwordEdit.MaxLength = GamePassword.MaxLength;
		passwordEdit.PlaceholderText = host.HasPassword ? "(kept from the saved game)" : "none";
		passwordEdit.TooltipText = "Players need this to join, unless they're coming back to their seats. Press Enter to set it; "
			+ "clear it and press Enter for no password. The game's LAN autosave keeps what checks the password, which "
			+ "is as good as the password for joining this game, so keep the autosave to yourself.";
		passwordEdit.TextChanged += _ => passwordEdited = true;
		passwordEdit.TextSubmitted += _ => SetPassword();
		passwordEdit.FocusExited += SetPassword;
		AddChild(passwordEdit);
		passwordStatus = MakeNote("");
		AddChild(passwordStatus);
		ShowPasswordStatus();

		spectatorsBox = MakeRule("Allow Spectators", host.AllowSpectators,
			$"Let up to {LanHost.MaxSpectators} players watch the game without playing. Even watching as a "
			+ "civilization shows what it knows to whoever watches, who may be playing its rival, so they're off unless you "
			+ "allow them while hiding what players can't see. Turning this off stops anyone watching.");
		spectatorsBox.Toggled += on => {
			LanSession.Host.AllowSpectators = on;
			spectatorViewChoice.Disabled = !on;
		};
		AddChild(spectatorsBox);

		spectatorViewChoice = MakeDropdown("How spectators may see the game. As a civilization, they see only what it knows, as its player "
			+ "would; as all of them, what any of them knows, so what nobody has seen stays hidden; or the whole game. "
			+ "Unless you say otherwise they may watch any way, and switch at will during the game.");
		AddItems(spectatorViewChoice, SpectatorViewChoices.Select(c => c.text), 0);
		spectatorViewChoice.ItemSelected += index => LanSession.Host.SpectatorViews = SpectatorViewChoices[(int)index].views;
		AddChild(MakeSettingLabel("Spectators See:"));
		AddChild(spectatorViewChoice);
		ShowSpectators();
	}

	// The ways the host can let spectators see the game.
	private static readonly (string text, SpectatorViews views)[] SpectatorViewChoices = [
		("Any way they choose", SpectatorViews.Any),
		("As the civilizations, one or all", SpectatorViews.AsCivs),
		("As all the civilizations", SpectatorViews.AllCivs),
		("As one civilization", SpectatorViews.OneCiv),
		("The whole game", SpectatorViews.Omniscient),
	];

	// Shows whether spectators may watch and how, which hiding what players
	// can't see changes until the host chooses.
	public void ShowSpectators() {
		LanHost host = LanSession.Host;
		if (host == null) {
			return;
		}
		spectatorsBox.SetPressedNoSignal(host.AllowSpectators);
		spectatorViewChoice.Select(Array.FindIndex(SpectatorViewChoices, c => c.views == host.SpectatorViews));
		spectatorViewChoice.Disabled = !host.AllowSpectators;
	}

	private void SetPassword() {
		if (!passwordEdited || LanSession.Host == null) {
			return;
		}
		passwordEdited = false;
		LanSession.Host.SetPassword(passwordEdit.Text);
		passwordEdit.PlaceholderText = "none";
		ShowPasswordStatus();
	}

	private void ShowPasswordStatus() {
		passwordStatus.Text = LanSession.Host.HasPassword ? "Password set." : "Anyone can join.";
	}
}
