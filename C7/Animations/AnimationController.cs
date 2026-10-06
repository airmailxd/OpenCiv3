using C7Engine;
using C7GameData;
using Godot;

/// This class mediates between the Engine and the game's animation
/// system. It receives animation messages from the Engine, checks
/// whether they are relevant for the player, and starts and updates
/// animations using the AnimationTracker and the AnimationManager.
[GlobalClass]
public partial class AnimationController : Node {
	public AnimationManager civ3AnimData;
	public AnimationTracker animTracker;

	[Export]
	Game game;

	public override void _Ready() {
		var animSoundPlayer = new AudioStreamPlayer();
		AddChild(animSoundPlayer);
		civ3AnimData = new AnimationManager(animSoundPlayer);
		animTracker = new AnimationTracker(civ3AnimData);
	}

	public void HandleEngineMessage(AnimationMessage msg) {
		GameData gameData = EngineStorage.gameData;

		switch (msg) {
			case MsgStartUnitAnimation mSUA:
				MapUnit unit = gameData.GetUnit(mSUA.unitID);
				if (unit != null && (game.controller.tileKnowledge.isActiveTile(unit.location) || game.controller.tileKnowledge.isActiveTile(unit.previousLocation))) {
					// TODO: This needs to be extended so that the player is shown when AIs found cities, when they move units
					// (optionally, depending on preferences) and generalized so that modders can specify whether custom
					// animations should be shown to the player.
					if (mSUA.action == MapUnit.AnimatedAction.ATTACK1)
						game.ensureLocationIsInView(unit.location);

					animTracker.startAnimation(unit, mSUA.action, mSUA.markCompleted, mSUA.ending);
				} else {
					mSUA.markCompleted();
				}
				break;
			case MsgStartEffectAnimation mSEA:
				int X, Y;
				gameData.map.tileIndexToCoords(mSEA.tileIndex, out X, out Y);
				Tile tile = gameData.map.tileAt(X, Y);
				if (tile != Tile.NONE && game.controller.tileKnowledge.isActiveTile(tile)) {
					animTracker.startAnimation(tile, mSEA.effect, mSEA.markCompleted, mSEA.ending);
				} else {
					mSEA.markCompleted();
				}
				break;
		}
	}

	// Brings the animations up to date. The MapView calls this once per frame from _Process, before the units are drawn, whether or not the
	// units are visible, since the engine waits for the animations it starts to finish. This method also processes all waiting messages b/c
	// some of them might pertain to animations.
	public void updateAnimations() {
		while (EngineStorage.TryDequeueNextAnimationMessage(out AnimationMessage msg))
			HandleEngineMessage(msg);

		animTracker.update();
	}

	public bool AnimationsEnabled => !animTracker.endAllImmediately;

	public void ToggleAnimationsEnabled() {
		SetAnimationsEnabled(!AnimationsEnabled);
	}

	public void SetAnimationsEnabled(bool enabled) {
		new MsgSetAnimationsEnabled(enabled).send();
		animTracker.endAllImmediately = !enabled;
	}
}
