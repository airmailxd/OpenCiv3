using C7Engine;
using C7GameData;
using Godot;
using Serilog;
using System;

/// This class mediates between the Engine and the game's animation
/// system. It receives animation messages from the Engine, checks
/// whether they are relevant for the player, and starts and updates
/// animations using the AnimationTracker and the AnimationManager.
///
/// Which moves and battles are shown is up to the player's preferences
/// (Show ... Moves, Animate ... Moves, Animate Battles). Caps Lock switches
/// all animations on or off for now, and holding Shift does until it's let
/// go of.
[GlobalClass]
public partial class AnimationController : Node {
	private static readonly ILogger log = LogManager.ForContext<AnimationController>();

	public AnimationManager civ3AnimData;
	public AnimationTracker animTracker;

	[Export]
	Game game;

	// How long the map stays on a unit whose move it shows without animating
	// it, so the player can see where it went.
	private const double ShownMoveSeconds = 0.25;

	// What's shown: what the preferences say, or, once the player has switched
	// animations with Caps Lock or Shift, everything or nothing.
	private enum Mode { FromPreferences, Everything, Nothing }
	private Mode mode = Mode.FromPreferences;

	// The mode to go back to when Shift is let go of, while it's held.
	private Mode? modeBeforeHold;

	// How a unit's animation is shown: not at all; by moving the map to the
	// unit; by animating it where it is; or both.
	private enum Showing { Nothing, Follow, Animate, AnimateAndFollow }

	public override void _Ready() {
		var animSoundPlayer = new AudioStreamPlayer { Bus = AudioManager.SfxAudioBus };
		AddChild(animSoundPlayer);
		civ3AnimData = new AnimationManager(animSoundPlayer);
		animTracker = new AnimationTracker(civ3AnimData);

		EngineStorage.uiShowsUnitAnimation = (unit, action) => ShowingFor(unit, action) != Showing.Nothing;
		EngineStorage.uiShowsEffectAnimation = _ => ShowsEffects();
		Preferences.Changed += OnPreferenceChanged;
	}

	public override void _ExitTree() {
		Preferences.Changed -= OnPreferenceChanged;
		EngineStorage.uiShowsUnitAnimation = null;
		EngineStorage.uiShowsEffectAnimation = null;
		base._ExitTree();
	}

	private void OnPreferenceChanged(Preferences.Option option) {
		if (option is >= Preferences.Option.ShowOurManualMoves and <= Preferences.Option.AnimateEnemyMoves) {
			ApplyPreferences();
		}
	}

	public void HandleEngineMessage(AnimationMessage msg) {
		// The engine waits for every animation it starts, so one that fails to
		// start must still be completed, or the game would freeze.
		try {
			StartAnimationFor(msg);
		} catch (Exception e) {
			log.Error(e, "Couldn't start the animation of {Message}", msg.GetType().Name);
			msg.markCompleted();
		}
	}

	private void StartAnimationFor(AnimationMessage msg) {
		GameData gameData = EngineStorage.gameData;

		switch (msg) {
			case MsgStartUnitAnimation mSUA:
				MapUnit unit = gameData.GetUnit(mSUA.unitID);
				Showing showing = Showing.Nothing;
				if (unit != null && (game.controller.tileKnowledge.isActiveTile(unit.location) || game.controller.tileKnowledge.isActiveTile(unit.previousLocation))) {
					showing = ShowingFor(unit, mSUA.action);
				}
				if (showing is Showing.Follow or Showing.AnimateAndFollow
						|| (showing == Showing.Animate && mSUA.action == MapUnit.AnimatedAction.ATTACK1)) {
					game.ensureLocationIsInView(unit.location);
				}

				if (showing == Showing.Nothing) {
					mSUA.markCompleted();
				} else if (showing == Showing.Follow) {
					// The unit is already where it went; give the player a
					// moment to see it there.
					GetTree().CreateTimer(ShownMoveSeconds).Timeout += mSUA.markCompleted;
				} else {
					animTracker.startAnimation(unit, mSUA.action, mSUA.markCompleted, mSUA.ending);
				}
				break;
			case MsgStartEffectAnimation mSEA:
				int X, Y;
				gameData.map.tileIndexToCoords(mSEA.tileIndex, out X, out Y);
				Tile tile = gameData.map.tileAt(X, Y);
				if (tile != Tile.NONE && game.controller.tileKnowledge.isActiveTile(tile) && ShowsEffects()) {
					animTracker.startAnimation(tile, mSEA.effect, mSEA.markCompleted, mSEA.ending);
				} else {
					mSEA.markCompleted();
				}
				break;
			default:
				msg.markCompleted();
				break;
		}
	}

	private static bool IsBattle(MapUnit.AnimatedAction action) {
		return action is MapUnit.AnimatedAction.ATTACK1 or MapUnit.AnimatedAction.ATTACK2 or MapUnit.AnimatedAction.ATTACK3
			or MapUnit.AnimatedAction.DEFEND or MapUnit.AnimatedAction.DEATH or MapUnit.AnimatedAction.DEAD
			or MapUnit.AnimatedAction.VICTORY;
	}

	// Whose unit it is, as the preferences tell moves apart: the player's
	// own, moved by hand or by their orders (go to, automation, exploring),
	// or another civilization's, at war with the player or not.
	private static Preferences.Mover MoverOf(MapUnit unit) {
		Player us = EngineStorage.gameData?.GetUIControllerPlayer();
		Player owner = unit.owner;
		if (owner == null || us == null || owner.id == us.id) {
			bool automatic = unit.isAutomated || (unit.path != null && unit.path.PathLength() > 0);
			return automatic ? Preferences.Mover.OurAutomatic : Preferences.Mover.OurManual;
		}
		bool atWar = owner.isBarbarians
			|| (us.playerRelationships.TryGetValue(owner.id, out PlayerRelationship relationship) && relationship.AtWar());
		return atWar ? Preferences.Mover.Enemy : Preferences.Mover.Friend;
	}

	private Showing ShowingFor(MapUnit unit, MapUnit.AnimatedAction action) {
		switch (mode) {
			case Mode.Everything:
				return Showing.Animate;
			case Mode.Nothing:
				return Showing.Nothing;
		}
		if (IsBattle(action)) {
			return Preferences.AnimateBattles ? Showing.Animate : Showing.Nothing;
		}
		Preferences.Mover mover = MoverOf(unit);
		bool show = Preferences.ShowMoves(mover);
		bool animate = Preferences.AnimateMoves(mover);
		return (show, animate) switch {
			(true, true) => Showing.AnimateAndFollow,
			(true, false) => Showing.Follow,
			(false, true) => Showing.Animate,
			_ => Showing.Nothing,
		};
	}

	// Bombardment's hits and misses are part of the battle.
	private bool ShowsEffects() {
		return mode switch {
			Mode.Everything => true,
			Mode.Nothing => false,
			_ => Preferences.AnimateBattles,
		};
	}

	// Brings the animations up to date. The MapView calls this once per frame from _Process, before the units are drawn, whether or not the
	// units are visible, since the engine waits for the animations it starts to finish. This method also processes all waiting messages b/c
	// some of them might pertain to animations.
	public void updateAnimations() {
		while (EngineStorage.TryDequeueNextAnimationMessage(out AnimationMessage msg))
			HandleEngineMessage(msg);

		animTracker.update();
	}

	// Whether anything of units moving or fighting is shown now.
	public bool AnimationsEnabled => mode == Mode.Everything || (mode == Mode.FromPreferences && Preferences.AnyMovesShown());

	// Caps Lock: stops showing anything if something is shown, otherwise
	// shows what the preferences say, or, if they show nothing, everything.
	public void ToggleAnimationsEnabled() {
		mode = AnimationsEnabled ? Mode.Nothing : Preferences.AnyMovesShown() ? Mode.FromPreferences : Mode.Everything;
		Apply();
	}

	public void SetAnimationsEnabled(bool enabled) {
		if (enabled != AnimationsEnabled) {
			ToggleAnimationsEnabled();
		}
	}

	// Holding Shift switches animations until it's let go of.
	public void BeginTemporaryToggle() {
		if (modeBeforeHold == null) {
			modeBeforeHold = mode;
			ToggleAnimationsEnabled();
		}
	}

	public void EndTemporaryToggle() {
		if (modeBeforeHold is Mode before) {
			modeBeforeHold = null;
			mode = before;
			Apply();
		}
	}

	// Shows what the preferences say, as at the start of a game or once the
	// player changes them.
	public void ApplyPreferences() {
		mode = Mode.FromPreferences;
		modeBeforeHold = null;
		Apply();
	}

	private void Apply() {
		bool enabled = AnimationsEnabled;
		new MsgSetAnimationsEnabled(enabled).send();
		animTracker.endAllImmediately = !enabled;
	}
}
