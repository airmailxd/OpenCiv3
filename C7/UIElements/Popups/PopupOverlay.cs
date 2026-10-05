using System.Collections.Generic;
using Godot;
using C7GameData;
using Serilog;

[GlobalClass]
[Tool]
public partial class PopupOverlay : HBoxContainer {

	private ILogger log = LogManager.ForContext<PopupOverlay>();

	// Meta
	[Signal] public delegate void HidePopupEventHandler();
	[Signal] public delegate void ClickEventHandler();

	// Game events
	[Signal] public delegate void BuildCityEventHandler(string name);
	[Signal] public delegate void DiplomacySelectionEventHandler(ParameterWrapper<ID> opponentPlayer);

	// Menu events
	[Signal] public delegate void SaveGameEventHandler();
	[Signal] public delegate void LoadGameEventHandler();
	[Signal] public delegate void RetireEventHandler();
	[Signal] public delegate void QuitEventHandler();


	Control currentChild = null;

	// A popup asked for while another one is up waits here until the one in
	// front of it closes, rather than replacing it: the one in front could be
	// something the player has to answer, like a deal proposal.
	private readonly LinkedList<(Control child, PopupCategory? category)> waiting = new();

	[Export]
	private Control control;

	public enum PopupCategory {
		Advisor,
		Console,
		Info,    //Sounds similar to the above, but lower-pitched in the second half
		TileInfo
	}

	public void OnHidePopup() {
		Reconnect();
		if (currentChild != null) {
			// Popups are made fresh each time they are shown, so free the old
			// one. Freeing is deferred, so a popup that hid itself from one of
			// its own handlers can safely finish running it.
			RemoveChild(currentChild);
			currentChild.QueueFree();
			currentChild = null;
		}

		// Bring up the next popup that was waiting, if any.
		while (waiting.Count > 0) {
			var (child, category) = waiting.First.Value;
			waiting.RemoveFirst();
			if (IsInstanceValid(child)) {
				Present(child, category);
				return;
			}
		}
		Hide();
	}

	public bool ShowingPopup => currentChild is not null;

	/// <summary>The popup currently in front, or null.</summary>
	public Control CurrentPopup => currentChild;

	public void PlaySound(AudioStream stream) {
		AudioStreamPlayer player = GetNode<AudioStreamPlayer>("PopupSound");
		player.Stream = stream;
		player.Play();
	}

	public void ShowPopup(Popup child, PopupCategory category) {
		if (child is null) {
			// not necessary if we don't pass null?
			log.Error("Received request to show null popup");
			return;
		}

		if (child is TileInfoPopup tileInfo && currentChild is not null and not TileInfoPopup) {
			// The overlay keeps clicks from reaching the map while a popup is
			// up, so this shouldn't happen; a tile's info is not worth
			// waiting for.
			log.Warning("Ignoring tile info requested while another popup is up");
			tileInfo.Discard();
			tileInfo.QueueFree();
			return;
		}

		CloseTileInfo(replacement: child);

		if (currentChild is not null) {
			waiting.AddLast((child, category));
			return;
		}

		Present(child, category);
	}

	/// <summary>
	/// Takes a popup down whether it is in front or still waiting, e.g.
	/// because what it was asking about has gone away.
	/// </summary>
	public void Dismiss(Control popup) {
		if (popup is null) {
			return;
		}
		if (popup == currentChild) {
			OnHidePopup();
			return;
		}
		for (var node = waiting.First; node != null; node = node.Next) {
			if (node.Value.child == popup) {
				waiting.Remove(node);
				if (IsInstanceValid(popup)) {
					popup.QueueFree();
				}
				return;
			}
		}
	}

	public void ShowBlank() {
		// The blank stands in for a dialog window (e.g. the file dialog) that
		// is up right now, so it goes in front; whatever was showing waits
		// behind it and comes back once the blank is hidden.
		CloseTileInfo(replacement: null);
		if (currentChild is not null) {
			Control preempted = currentChild;
			Reconnect();
			RemoveChild(preempted);
			currentChild = null;
			waiting.AddFirst((preempted, null));
		}
		Present(new Control(), null);
	}

	// Tile info is a passing look at a tile, so anything else replaces it
	// rather than waiting behind it. A popup other than tile info closes it
	// the normal way, so the game forgets the tile info too; a new tile info
	// just takes the old one's place (the game already points at the new one).
	private void CloseTileInfo(Control replacement) {
		if (currentChild is not TileInfoPopup tileInfo) {
			return;
		}
		if (replacement is TileInfoPopup) {
			RemoveChild(tileInfo);
			tileInfo.QueueFree();
			currentChild = null;
			Reconnect();
			Hide();
		} else {
			tileInfo.CloseTileInfo();
		}
		// Closing should have taken it down; make sure it is gone either way.
		if (currentChild == tileInfo) {
			OnHidePopup();
		}
	}

	private void Present(Control child, PopupCategory? category) {
		if (child is Popup popup) {
			Alignment = popup.alignment;
			OffsetTop = popup.margins.top;
			OffsetBottom = popup.margins.bottom;
			OffsetLeft = popup.margins.left;
			OffsetRight = popup.margins.right;
		}

		var soundFile = category switch {
			PopupCategory.Advisor => "popups.advisor",
			PopupCategory.Console => "popups.console",
			PopupCategory.Info => "popups.info",
			_ => null
		};

		var wav = soundFile == null ? null : AudioLoader.Load(soundFile);

		ShowChild(child);

		if (wav != null) {
			PlaySound(wav);
		}
	}

	private void ShowChild(Control child) {
		AddChild(child);
		currentChild = child;
		Isolate();
		Show();
	}

	public override void _ExitTree() {
		// Popups still waiting aren't in the tree, so nothing else frees them.
		foreach (var (child, _) in waiting) {
			if (IsInstanceValid(child)) {
				child.QueueFree();
			}
		}
		waiting.Clear();
		base._ExitTree();
	}

	// The mouse filters the UI elements had before a popup isolated them,
	// so they can be put back exactly as they were.
	private readonly Dictionary<Control, MouseFilterEnum> savedMouseFilters = new();
	private bool isolated = false;

	/// <summary>
	/// Creates a modal context by preventing UI events outside the popup context.
	/// Inverse of `Reconnect(..)`.
	/// </summary>
	private void Isolate() {
		// 1. Overlay catches mouse events, prevents event propagation
		MouseFilter = MouseFilterEnum.Stop;

		// 2. Stop the world: switch off UI elements
		control.ProcessMode = ProcessModeEnum.Disabled;

		// 3. Ignore all mouse input on UI elements, remembering what they had
		if (!isolated) {
			isolated = true;
			savedMouseFilters.Clear();
			IgnoreMouseRecursive(control);
		}
	}

	private void IgnoreMouseRecursive(Node node) {
		int count = node.GetChildCount();
		for (int i = 0; i < count; ++i) {
			IgnoreMouseRecursive(node.GetChild(i));
		}
		if (node is Control c) {
			savedMouseFilters[c] = c.MouseFilter;
			c.MouseFilter = MouseFilterEnum.Ignore;
		}
	}

	/// <summary>
	/// Unwind a modal context by allowing UI events outside the popup context.
	/// Inverse of `Isolate(..)`.
	/// </summary>
	private void Reconnect() {
		// 1. Let UI elements catch mouse inputs again, as they did before
		if (isolated) {
			isolated = false;
			foreach (var (c, filter) in savedMouseFilters) {
				if (IsInstanceValid(c)) {
					c.MouseFilter = filter;
				}
			}
			savedMouseFilters.Clear();
		}

		// 2. Restart the world: let UI elements run normal
		control.ProcessMode = ProcessModeEnum.Inherit;
	}

	public override void _GuiInput(InputEvent @event) {
		// Raise an event when popup overlay catches a click that the popup itself misses.
		// This usually means a click outside the popup modal.
		if (Visible && @event is InputEventMouseButton ev && ev.Pressed) {
			EmitSignal(SignalName.Click);
		}
	}

	/**
	 * N.B. Some popups should react to certain keys, e.g. the Build City popup should close without building if you
	 * press escape.  Those popups will have to implement this functionality.
	 *
	 * If we find that the majority of popups should close on Escape, we may want to make that the default,
	 * but so far, 2 out of 3 popups do not close on escape.
	 **/
	public override void _UnhandledInput(InputEvent @event) {
		if (Visible && @event is InputEventKey eventKey && eventKey.Pressed) {
			// As I've added more shortcuts, I've realized checking all of them here could be irksome.
			// For now, I'm thinking it would make more sense to process or allow through the ones that should go through,
			// as most of the global ones should *not* go through here.
			GetViewport().SetInputAsHandled();
		}

		if (@event is InputEventMouseButton ev) {
			// Catch right clicks over UI elements to stop awkward TileInfo renders
			if (ev.ButtonIndex == MouseButton.Right) {
				if (IsOverUI()) {
					AcceptEvent();
					GetViewport().SetInputAsHandled();
				}
			}
		}
	}

	private bool IsOverUI() {
		Control ctrl = GetViewport().GuiGetHoveredControl();
		return ctrl is TextureButton or TextureRect;
	}
}
