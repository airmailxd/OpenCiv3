using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Network;
using C7Relay;
using Godot;
using static SetupStyle;

// How players join a game this computer hosts, and who may, for the foot of
// the Multiplayer Setup screen's Game Settings: the addresses to type, the
// join code once hosted online, the public listing, the password, and
// spectators. Used by the setup screen while the host sets up a new game,
// and by the lobby while hosting a saved one. Update is called each frame.
public partial class HostJoiningSection : VBoxContainer {
	// Called when the game goes online or stops being, as the game type
	// shows it.
	public event Action<bool> OnlineChanged;

	// The setup screen chooses hosting online with its Game Type, so this
	// has no button of its own for it there.
	private bool hostOnlineButtonShown;

	private Button hostOnlineButton;
	private HBoxContainer onlineCodeRow;
	private Label onlineCode;
	private VBoxContainer relayRow;
	private LineEdit relayEdit;
	private Label onlineStatus;
	private VBoxContainer listingBox;
	private Label listingStatus;
	private LineEdit passwordEdit;
	private Label passwordStatus;
	private bool passwordEdited;
	private Civ3Checkbox spectatorsBox;
	private OptionButton spectatorViewChoice;

	// Godot makes nodes with no arguments; Build fills it in.
	public HostJoiningSection() { }

	public void Build(Control fontSource, bool hostOnlineButtonShown) {
		this.hostOnlineButtonShown = hostOnlineButtonShown;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 2);
		Label heading = MakeHeading(fontSource, "JOINING", 13);
		heading.CustomMinimumSize = new Vector2(0, 26);
		heading.VerticalAlignment = VerticalAlignment.Bottom;
		AddChild(heading);
		AddJoinAddresses();
		AddOnlineHosting();
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

	// Hosting online, for players anywhere to join with a join code through
	// the relay, as well as on the network.
	private void AddOnlineHosting() {
		AddChild(MakeSettingLabel("Join Code:"));
		hostOnlineButton = MakeButton("Host Online", "Host the game online too, for players anywhere to join with a code.");
		hostOnlineButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		hostOnlineButton.Pressed += () => HostOnline();
		hostOnlineButton.Visible = hostOnlineButtonShown;
		AddChild(hostOnlineButton);

		// The host chooses the relay; players are told it with the code.
		relayRow = new VBoxContainer();
		relayRow.AddChild(MakeSettingLabel("Relay Server:"));
		relayEdit = MakeDarkField();
		relayEdit.Text = OnlineRelay.SameRelay(OnlineRelay.Url, OnlineRelay.DefaultUrl) ? "" : OnlineRelay.Url;
		relayEdit.PlaceholderText = "relay.example.org";
		relayEdit.TooltipText = "The relay that carries the game online; leave it empty for the usual one.";
		relayEdit.TextSubmitted += _ => {
			if (hostOnlineButtonShown || LanSession.Host?.Online != null) {
				HostOnline();
			}
		};
		relayRow.AddChild(relayEdit);
		AddChild(relayRow);

		onlineCodeRow = new HBoxContainer { Visible = false };
		onlineCodeRow.AddThemeConstantOverride("separation", 8);
		onlineCode = MakeValue("");
		onlineCode.AddThemeFontSizeOverride("font_size", 18);
		onlineCodeRow.AddChild(onlineCode);
		onlineCodeRow.AddChild(MakeSmallButton("Copy", "Copy the join code to send to the players.", () => {
			if (OnlineInvite() is string invite) {
				DisplayServer.ClipboardSet(invite);
			}
		}));
		AddChild(onlineCodeRow);

		onlineStatus = MakeNote("");
		onlineStatus.Visible = false;
		AddChild(onlineStatus);

		AddPublicListing();
		if (!hostOnlineButtonShown) {
			onlineStatus.Visible = true;
			onlineStatus.Text = "Choose Internet as the Game Type to host online.";
		}
	}

	// Starts taking guests through the relay typed, or else the one in the
	// settings, or the one a resumed game's code is from.
	public void HostOnline(string relayUrl = null, string code = null, string key = null) {
		relayUrl ??= relayEdit.Text;
		relayUrl = string.IsNullOrWhiteSpace(relayUrl) ? OnlineRelay.Url : relayUrl.Trim();
		onlineStatus.Visible = true;
		if (OnlineRelay.Problem(relayUrl) is string problem) {
			onlineStatus.Text = problem;
			return;
		}
		// A relay chosen here is the one used from now on, here and when
		// joining.
		if (code == null) {
			OnlineRelay.SetUrl(relayUrl);
		}
		LanSession.Host.HostOnline(relayUrl, code, key);
		hostOnlineButton.Visible = false;
		relayRow.Visible = false;
		listingBox.Visible = true;
		OnlineChanged?.Invoke(true);
		Update();
	}

	// Stops hosting online: players on the network can still join.
	public void StopOnline() {
		LanSession.Host?.StopHostingOnline();
		hostOnlineButton.Text = "Host Online";
		hostOnlineButton.Visible = hostOnlineButtonShown;
		relayRow.Visible = true;
		listingBox.Visible = false;
		onlineCodeRow.Visible = false;
		onlineCode.Text = "";
		onlineStatus.Visible = !hostOnlineButtonShown;
		onlineStatus.Text = "Choose Internet as the Game Type to host online.";
		OnlineChanged?.Invoke(false);
	}

	// Listing the game in the relay's public list, under its name, for
	// players anywhere to find under "Browse Online Games", once it's hosted
	// online.
	private void AddPublicListing() {
		LanHost host = LanSession.Host;
		listingBox = new VBoxContainer { Visible = false };
		listingBox.AddThemeConstantOverride("separation", 2);
		AddChild(listingBox);

		Civ3Checkbox list = MakeRule("List Publicly", host.ListPublicly,
			"Show this game in the online game browser, under its name, for anyone using this relay to find and join. "
			+ "Set a password to choose who can join.");
		list.Toggled += on => LanSession.Host.ListPublicly = on;
		listingBox.AddChild(list);
		LineEdit description = MakeDarkField();
		description.Text = host.PublicDescription ?? "";
		description.PlaceholderText = "A few words for the list";
		description.MaxLength = RelayProtocol.MaxGameDescriptionLength;
		description.TooltipText = "A few words about the game, for the public list (optional).";
		description.TextChanged += text => LanSession.Host.PublicDescription = text;
		listingBox.AddChild(description);
		listingStatus = MakeNote("");
		listingBox.AddChild(listingStatus);
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

	// What players type to join: the code, with the relay when it isn't the
	// default one.
	private static string OnlineInvite() {
		RelayHostLink link = LanSession.Host?.Online;
		return link == null ? null : OnlineRelay.Invite(link.FormattedCode, link.RelayUrl);
	}

	// Shows how hosting online stands.
	public void Update() {
		RelayHostLink link = LanSession.Host?.Online;
		if (link == null) {
			return;
		}
		string code = OnlineInvite() ?? "";
		if (onlineCode.Text != code) {
			onlineCode.Text = code;
			onlineCode.TooltipText = code;
			// An invite naming the relay is longer than a code alone.
			onlineCode.AddThemeFontSizeOverride("font_size", code.Contains('@') ? 13 : 18);
			onlineCodeRow.Visible = code != "";
		}
		int guests = link.GuestCount;
		string text = link.State switch {
			RelayHostLink.LinkState.Connecting => link.Error == null ? "Connecting to the relay..." : $"Connecting to the relay... ({link.Error})",
			RelayHostLink.LinkState.Online => "Online. Players choose \"Join LAN Game\" and type this code under \"Online game code\"."
				+ (guests == 0 ? "" : $" {guests} connected through the relay."),
			RelayHostLink.LinkState.Reconnecting => $"Lost the relay, reconnecting... Players online are back once it is. ({link.Error})",
			_ => $"Could not host online: {link.Error}",
		};
		if (onlineStatus.Text != text) {
			onlineStatus.Text = text;
		}
		string listing = !LanSession.Host.ListPublicly ? "Not listed: players need the code to join."
			: link.IsListed ? "Listed publicly: players can find it under \"Browse Online Games\"."
			: link.ListingError != null ? $"Not listed: {link.ListingError}"
			: link.State == RelayHostLink.LinkState.Online ? "Listing..."
			: "Listed once the game is back online.";
		if (listingStatus.Text != listing) {
			listingStatus.Text = listing;
		}
		// Hosting online failed for good, as with a code that can't be had
		// again: trying again gets a new one.
		if (link.State == RelayHostLink.LinkState.Failed && hostOnlineButtonShown && !hostOnlineButton.Visible) {
			hostOnlineButton.Text = "Host Online Again";
			hostOnlineButton.Visible = true;
			relayRow.Visible = true;
			OnlineChanged?.Invoke(false);
		}
	}
}
