using System;
using System.Collections.Generic;
using System.Linq;
using C7.Textures;
using Godot;
using C7GameData;
using Serilog;
using C7Engine;

[GlobalClass]
[Tool]
public partial class TransportInfoBox : Civ3TextureRect {
	private ILogger log = LogManager.ForContext<TransportInfoBox>();

	private readonly Game _game;

	private Vector2I frameOffset = new (-27, -180);
	private Vector2 transportUnitsAnchor = new(70f, 45f);
	private Vector2 miniatureScale = new(0.7f, 0.7f);
	private Vector2 unitButtonSize = new(50, 50);
	// Note: the draw area for the box is a bit more than 200x100

	private TextureRect boxTransportRect = new();

	private readonly List<Button> unitButtons = new();

	// What the box last showed: the transport, where it was, and which units
	// were loaded on it. The buttons are only rebuilt when this changes.
	private MapUnit shownTransport;
	private Tile shownLocation;
	private int shownPassengerCount;
	private int shownPassengerHash;
	private bool hasShown;

	private Vector2 lastViewportSize = new(-1, -1);

	public TransportInfoBox(Game game) {
		_game = game;

		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		ImageTexture boxTransport = TextureLoader.Load("transport_infobox.box");

		boxTransportRect = new TextureRect();
		boxTransportRect.Texture = boxTransport;
		boxTransportRect.SetPosition(new Vector2(0, 0));
		AddChild(boxTransportRect);
	}

	public override void _Process(double delta) {
		if (Engine.IsEditorHint())
			return;

		RepositionFrame();

		var unit = _game.CurrentlySelectedUnit;
		if (!MapUnit.IsMapUnitValid(unit) || !unit.CanTransport()) {
			Visible = false;
			Reset();
		} else {
			Visible = true;
			Refresh(unit);
		}

		base._Process(delta);
	}

	private void Refresh(MapUnit unit) {
		// A cheap summary of the units loaded on the transport, without
		// building any lists.
		Tile location = unit.location;
		int passengerCount = 0, passengerHash = 17;
		if (Tile.IsTileValid(location)) {
			foreach (MapUnit u in location.unitsOnTile) {
				if (u != unit && u.IsLoadedIn(unit)) {
					++passengerCount;
					passengerHash = HashCode.Combine(passengerHash, u);
				}
			}
		}

		if (hasShown && shownTransport == unit && shownLocation == location
			&& shownPassengerCount == passengerCount && shownPassengerHash == passengerHash) {
			return;
		}

		// Wait for game to load unit graphics
		if (AnimationManager.AnimationThumbnails.Count == 0)
			return;

		Reset();
		hasShown = true;
		shownTransport = unit;
		shownLocation = location;
		shownPassengerCount = passengerCount;
		shownPassengerHash = passengerHash;

		// The transport first, then its passengers in game order.
		List<MapUnit> passengers = unit.Passengers();
		if (passengers.Count > 1) {
			// One pass over the game's units picks the passengers out in
			// order, rather than looking each up again for every comparison.
			HashSet<MapUnit> aboard = new(passengers);
			List<MapUnit> ordered = new(passengers.Count);
			foreach (MapUnit u in EngineStorage.gameData.mapUnits) {
				if (aboard.Remove(u)) {
					ordered.Add(u);
					if (aboard.Count == 0) {
						break;
					}
				}
			}
			// Any not in the game's list (shouldn't happen) go first, as
			// sorting by IndexOf's -1 put them.
			ordered.InsertRange(0, passengers.Where(aboard.Contains));
			passengers = ordered;
		}
		AddUnitButton(unit, 0);
		for (int i = 0; i < passengers.Count; ++i) {
			AddUnitButton(passengers[i], i + 1);
		}
	}

	private void RepositionFrame() {
		// Position frame and map relative to viewport
		var vp = GetViewportRect().Size;
		if (vp == lastViewportSize)
			return;
		lastViewportSize = vp;
		var boxSize = boxTransportRect.Texture.GetSize();
		SetPosition(frameOffset + new Vector2(vp.X - boxSize.X, vp.Y - boxSize.Y));
	}

	private void Reset() {
		if (!hasShown)
			return;
		hasShown = false;
		shownTransport = null;
		shownLocation = null;
		foreach (Button button in unitButtons)
			button.QueueFree();
		unitButtons.Clear();
	}

	private void AddUnitButton(MapUnit unit, int idx) {
		// Get sprites
		var (unitSprite, unitTintSprite) = SpriteUtils.GetUnitSprites(_game, unit);

		// Resize sprites (tint is a child of the main sprite and is scaled with parent)
		unitSprite.SetScale(miniatureScale);

		// Create button
		Button unitButton = new();
		unitButton.SetSize(unitButtonSize);
		unitButton.ActionMode = BaseButton.ActionModeEnum.Press;
		unitButton.Pressed += () => HandleUnitClick(unit);
		AddChild(unitButton);
		unitButtons.Add(unitButton);

		// Position button
		var pos = CalculateUnitButtonPosition(idx);
		unitButton.SetPosition(pos);

		// Add sprites
		unitButton.AddChild(unitSprite);
		unitSprite.AddChild(unitTintSprite);

		// Draw sprites centered on the button
		unitSprite.Position += unitButtonSize / 2;

		// Draw a box around the transport unit
		if (idx == 0) {
			var line = new Line2D();

			line.Width = 3f;
			line.DefaultColor = TextureLoader.LoadColor(unit.owner.GetPlayerColor());

			// draw lines at normal scale, let parent scale things down
			line.AddPoint(new Vector2(0, 0));
			line.AddPoint(new Vector2(unitButtonSize.X, 0));
			line.AddPoint(new Vector2(unitButtonSize.X, unitButtonSize.Y));
			line.AddPoint(new Vector2(0, unitButtonSize.Y));
			line.AddPoint(new Vector2(0, 0));

			// parent to button
			unitButton.AddChild(line);
		}
	}

	private Vector2 CalculateUnitButtonPosition(int idx) {
		var drawAreaWidth = boxTransportRect.Texture.GetWidth() - transportUnitsAnchor.X;
		var columns = (int) Math.Floor(drawAreaWidth / unitButtonSize.X);

		var unitSpritePosition = transportUnitsAnchor;

		// offset based on index
		unitSpritePosition.X += (idx % columns) * unitButtonSize.X;
		unitSpritePosition.Y += ((int)Math.Floor(idx / (1f * columns))) * unitButtonSize.Y;

		// from centered to draw corner
		unitSpritePosition -= unitButtonSize / 2;

		return unitSpritePosition;
	}

	private void HandleUnitClick(MapUnit unit) {
		_game.SelectUnit(unit);
	}
}
