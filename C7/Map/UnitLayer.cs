using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7.Textures;
using C7GameData;
using Godot;

public partial class UnitLayer : LooseLayer {
	private ImageTexture unitMovementIndicators;

	// The unit animations, effect animations, and cursor are all drawn as children attached to the looseView but aren't created and attached in
	// any particular order so we must use the ZIndex property to ensure they're properly layered.
	public const int effectAnimZIndex = 2;
	public const int unitAnimZIndex = 1;
	public const int cursorZIndex = -1;

	public UnitLayer() {
		unitMovementIndicators = TextureLoader.Load("ui.unit_control.movement_indicators");
	}

	public static Color GetHpColor(float remaining, float total) {
		if (remaining >= 0.67f) {
			return Color.Color8(0, 255, 0);
		} else if (remaining >= 0.34f && total > 2) {
			return Color.Color8(255, 255, 0);
		} else {
			return Color.Color8(255, 0, 0);
		}
	}

	// AnimationInstance represents an animation appearing on the screen. It's specific to a unit, action, and direction. AnimationInstances have
	// two sprites: the base sprite and the tint sprite, which is drawn with the unit tint shader (UnitTint.gdshader) to color it with the civ
	// color. The shader materials are shared by all units of a civ, so the sprites can be batched. AnimationInstances are only active for one
	// frame at a time but they live as long as the UnitLayer. They are retrieved or created as needed by getBlankAnimationInstance during the
	// drawing of units, and the ones left unused at the end of a frame are hidden. To spare calls into the engine, an instance remembers what
	// it's showing and only updates its sprites when that changes.

	// should hold animation players instead of animations
	public partial class AnimationInstance {

		public AnimatedSprite2D sprite;
		public AnimatedSprite2D spriteTint;
		// The material of the tint sprite, shared with other instances.
		public ShaderMaterial material;

		// The material for tint sprites that haven't drawn a unit yet, which
		// has the shader's default tint.
		private static ShaderMaterial untintedMaterial;

		private StringName currentAnimation = null;
		private int currentFrame = -1;
		private int currentCivColor = -1;
		private Vector2 currentPosition;
		private bool visible = true;

		public void SetPosition(Vector2 position) {
			if (position == currentPosition)
				return;
			currentPosition = position;
			this.sprite.Position = position;
			this.spriteTint.Position = position;
		}

		public int GetNextFrameByProgress(string animation, float progress) {
			// AnimatedSprite2D has a settable FrameProgress field, which I expected to
			// update the current frame of the animation upon setting, but it did not
			// when I tried it, so instead, calculate what the next frame should be
			// based on the progress.
			int frameCount = this.sprite.SpriteFrames.GetFrameCount(animation);
			int nextFrame = (int)((float)frameCount * progress);
			return nextFrame >= frameCount ? frameCount - 1 : (nextFrame < 0 ? 0 : nextFrame);
		}

		public void SetFrame(int frame) {
			currentFrame = frame;
			this.sprite.Frame = frame;
			this.spriteTint.Frame = frame;
		}

		public void SetAnimation(string name) {
			// Not tracked, so the next SetAnimationFrame updates the sprites.
			currentAnimation = null;
			this.sprite.Animation = name;
			this.spriteTint.Animation = name;
		}

		// Shows a frame of an animation, only updating the sprites if needed.
		public void SetAnimationFrame(StringName animation, int frame) {
			if (!ReferenceEquals(animation, currentAnimation)) {
				currentAnimation = animation;
				this.sprite.Animation = animation;
				this.spriteTint.Animation = animation;
				// Changing the animation resets the frame, so always set it.
				SetFrame(frame);
			} else if (frame != currentFrame) {
				SetFrame(frame);
			}
		}

		// Tints the tint sprite with a civ color.
		public void SetCivColor(int civColorIndex) {
			if (civColorIndex == currentCivColor)
				return;
			currentCivColor = civColorIndex;
			this.material = PlayerTextureUtil.GetShaderMaterialForUnit(civColorIndex);
			this.spriteTint.Material = this.material;
		}

		public void Show() {
			if (visible)
				return;
			visible = true;
			this.sprite.Show();
			this.spriteTint.Show();
		}

		public void Hide() {
			if (!visible)
				return;
			visible = false;
			this.sprite.Hide();
			this.spriteTint.Hide();
		}

		public Vector2 FrameSize(string animation) {
			return this.sprite.SpriteFrames.GetFrameTexture(animation, 0).GetSize();
		}

		public AnimationInstance(LooseView looseView) {
			AnimationManager manager = looseView.mapView.game.animationController.civ3AnimData;

			this.sprite = new AnimatedSprite2D();
			this.sprite.ZIndex = unitAnimZIndex;
			this.sprite.SpriteFrames = manager.spriteFrames;

			this.spriteTint = new AnimatedSprite2D();
			this.spriteTint.ZIndex = unitAnimZIndex;
			this.spriteTint.SpriteFrames = manager.tintFrames;

			if (untintedMaterial == null) {
				untintedMaterial = new ShaderMaterial();
				untintedMaterial.Shader = GD.Load<Shader>("res://UnitTint.gdshader");
			}
			this.material = untintedMaterial;
			this.spriteTint.Material = this.material;

			looseView.AddChild(sprite);
			looseView.AddChild(spriteTint);
		}
	}

	private List<AnimationInstance> animInsts = new List<AnimationInstance>();
	private int nextBlankAnimInst = 0;
	// How many instances were used in the previous frame.
	private int animInstsUsedLastFrame = 0;

	// Returns the next unused AnimationInstance or creates & returns a new one if none are available.
	public AnimationInstance getBlankAnimationInstance(LooseView looseView) {
		if (nextBlankAnimInst >= animInsts.Count) {
			animInsts.Add(new AnimationInstance(looseView));
		}
		AnimationInstance inst = animInsts[nextBlankAnimInst];
		nextBlankAnimInst++;
		return inst;
	}

	public void drawUnitAnimFrame(LooseView looseView, MapUnit unit, MapUnit.Appearance appearance, Vector2 tileCenter) {
		AnimationInstance inst = getBlankAnimationInstance(looseView);
		AnimationManager.AnimationInfo animation = looseView.mapView.game.animationController.civ3AnimData
			.GetUnitAnimation(unit, appearance.action, appearance.direction);

		inst.SetPosition(GetFramePosition(appearance, animation, tileCenter));
		inst.SetCivColor(unit.owner.GetPlayerColor());
		inst.SetAnimationFrame(animation.animationName, animation.FrameAtProgress(appearance.progress));
		inst.Show();
	}

	private Vector2 GetFramePosition(MapUnit.Appearance appearance, AnimationManager.AnimationInfo animation, Vector2 tileCenter) {
		// 1. Place unit in the center of the tile.
		//	  This places the *center* of the sprite frame at the center point of the tile.
		//
		// 2. Apply animation offset.
		//    This applies the offset of the animation when a unit moves from one tile to another, it "follows" the movement
		//    it's pretty much a noop for the default or other animations that stay in the same tile.
		//
		// 3. Apply the offsets from the flic file to place them correctly in a 240x240 rect
		//    (which starts at the center of the tile extending to the right and bottom).
		//    The offsets we read from the flic file represent the offsets from the top left corner of the frame,
		//    so this is why here we need to add half the width and height since the transforms
		//    in our frames are applied at the center of the image
		//
		// 4. Offset the frame by half the width and height of the original size of the animation (which is usually 240x240 in pixels)
		//    to place it correctly on the tile.
		//
		// 5. Finally add (or subtract if negative in .ini) any custom offset from the ini file

		Vector2 animOffset = MapView.cellSize * new Vector2(appearance.offsetX, appearance.offsetY);
		Vector2I flicOffset = animation.flicOffset;
		Vector2 flicOffsetWithAlignedFrame = new ((animation.frameSize.X / 2) + flicOffset.X,
												  (animation.frameSize.Y / 2) + flicOffset.Y);
		Vector2I flicOriginalSize = animation.flicOriginalSize;

		return tileCenter + animOffset + flicOffsetWithAlignedFrame - (flicOriginalSize / 2);
	}

	public void drawEffectAnimFrame(LooseView looseView, C7Animation anim, float progress, Vector2 tileCenter) {
		AnimationInstance inst = getBlankAnimationInstance(looseView);
		inst.sprite.ZIndex = effectAnimZIndex;
		inst.spriteTint.ZIndex = effectAnimZIndex;
		inst.SetPosition(tileCenter);

		AnimationManager.AnimationInfo animation = looseView.mapView.game.animationController.civ3AnimData.GetEffectAnimation(anim);

		inst.SetAnimationFrame(animation.animationName, animation.FrameAtProgress(progress));
		inst.Show();
	}

	private AnimatedSprite2D cursorSprite = null;

	public void drawCursor(LooseView looseView, Vector2 position) {
		// Initialize cursor if necessary
		if (cursorSprite == null) {
			cursorSprite = new AnimatedSprite2D();
			cursorSprite.SpriteFrames = TextureLoader.LoadAnimation("animations.cursor", "cursor");
			cursorSprite.Animation = "cursor";
			looseView.AddChild(cursorSprite);
			cursorSprite.Play("cursor");
		}

		cursorSprite.Position = position;
		cursorSprite.Show();
	}

	public override void onBeginDraw(LooseView looseView, GameData gameData) {
		// Reuse the animation instances from the start. The ones that don't
		// get used this frame are hidden in onEndDraw.
		animInstsUsedLastFrame = Math.Max(animInstsUsedLastFrame, nextBlankAnimInst);
		nextBlankAnimInst = 0;

		// Hide cursor if it's been initialized
		cursorSprite?.Hide();

		looseView.mapView.game.animationController.updateAnimations();

		// The displayed units are worked out again every turn, and for every game.
		if (gameData != displayedUnitsGame || gameData.turn != displayedUnitsTurn || displayedUnits.Count > MaxDisplayedUnitsCached) {
			displayedUnits.Clear();
			displayedUnitsGame = gameData;
			displayedUnitsTurn = gameData.turn;
		}
	}

	public override void onEndDraw(LooseView looseView, GameData gameData) {
		for (int n = nextBlankAnimInst; n < animInstsUsedLastFrame; n++) {
			animInsts[n].Hide();
		}
		animInstsUsedLastFrame = nextBlankAnimInst;
	}

	// The unit shown on a tile, and its hit points, which only need working out
	// again when something on the tile changes. See displayedUnitStamp for what
	// the choice depends on.
	private sealed class DisplayedUnit {
		public long stamp;
		public MapUnit unit;
		public int hitPoints, maxHitPoints;
	}

	private readonly Dictionary<Tile, DisplayedUnit> displayedUnits = new(ReferenceEqualityComparer.Instance);
	private GameData displayedUnitsGame = null;
	private int displayedUnitsTurn = -1;
	private const int MaxDisplayedUnitsCached = 8192;

	// Returns a value that changes whenever anything that selectUnitToDisplay
	// and the hit point bar depend on changes for a tile's units: which units
	// are there and in what order, their types, hit points, fortification and
	// loading, the selected unit and whether each unit is at peace with it, and
	// the tile's city. Defense bonuses from terrain and buildings only change
	// between turns, when the cache is cleared anyway. Returns null if a unit
	// is animating, since the choice then depends on the animation's progress.
	private long? displayedUnitStamp(AnimationTracker animTracker, Tile tile, List<MapUnit> units, MapUnit currentlySelectedUnit) {
		Player opponent = currentlySelectedUnit?.owner;
		Player lastOwner = null;
		bool lastOwnerAtPeace = false;

		long stamp = Mix(Mix(17, units.Count), RuntimeHelpers.GetHashCode(currentlySelectedUnit));
		stamp = Mix(stamp, RuntimeHelpers.GetHashCode(tile.cityAtTile));
		foreach (MapUnit u in units) {
			if (animTracker.hasCurrentAction(u))
				return null;

			if (u.owner != lastOwner || lastOwner == null) {
				lastOwner = u.owner;
				lastOwnerAtPeace = opponent?.IsAtPeaceWith(u.owner) ?? true;
			}

			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(u));
			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(u.unitType));
			stamp = Mix(stamp, ((long)u.hitPointsRemaining << 32) | (uint)u.maxHitPoints);
			stamp = Mix(stamp, (u.isFortified ? 1 : 0) | (lastOwnerAtPeace ? 2 : 0) | (opponent == null ? 4 : 0));
			stamp = Mix(stamp, u.loadedOnUnitId?.GetHashCode() ?? 0);
		}
		return stamp;
	}

	private static long Mix(long hash, long value) {
		unchecked {
			hash ^= value * -7046029254386353131L; // 0x9E3779B97F4A7C15
			hash = (hash ^ (hash >> 29)) * -4658895280553007687L; // 0xBF58476D1CE4E5B9
			return hash ^ (hash >> 32);
		}
	}

	// Returns the unit to draw on a tile with units, and its hit points.
	private DisplayedUnit getDisplayedUnit(LooseView looseView, Tile tile) {
		Game game = looseView.mapView.game;
		MapUnit currentlySelectedUnit = game.CurrentlySelectedUnit;
		long? stamp = displayedUnitStamp(game.animationController.animTracker, tile, tile.unitsOnTile, currentlySelectedUnit);

		if (stamp is long s && displayedUnits.TryGetValue(tile, out DisplayedUnit cached) && cached.stamp == s) {
			return cached;
		}

		MapUnit unit = computeUnitToDisplay(looseView, tile, tile.unitsOnTile);
		DisplayedUnit displayed = new DisplayedUnit {
			unit = unit,
			// An army shows the combined hit points of its members.
			hitPoints = unit.CompositeHitPoints(),
			maxHitPoints = unit.CompositeMaxHitPoints(),
		};
		if (stamp is long newStamp) {
			displayed.stamp = newStamp;
			displayedUnits[tile] = displayed;
		}
		return displayed;
	}

	// Returns which unit should be drawn from among a list of units. The list is assumed to be non-empty.
	public MapUnit selectUnitToDisplay(LooseView looseView, Tile tile, List<MapUnit> units) {
		if (units == tile.unitsOnTile) {
			return getDisplayedUnit(looseView, tile).unit;
		}
		return computeUnitToDisplay(looseView, tile, units);
	}

	private MapUnit computeUnitToDisplay(LooseView looseView, Tile tile, List<MapUnit> units) {
		// From the list, pick out which units are (1) the strongest defender vs the currently selected unit, (2) the currently selected unit
		// itself if it's in the list, and (3) any unit that is playing an animation that the player would want to see.
		// If every regular defender is in some transport, the best defender _is_ a transport

		MapUnit bestDefender = null;
		foreach (var u in units) {
			if (u.loadedOnUnitId == null) {
				bestDefender = u;
				break;
			}
		}
		bestDefender ??= units[0];
		MapUnit selected = null, transporter = null, doingInterestingAnimation = null;
		var currentlySelectedUnit = looseView.mapView.game.CurrentlySelectedUnit;
		AnimationTracker animTracker = looseView.mapView.game.animationController.animTracker;

		foreach (var u in units) {
			if (u.loadedOnUnitId != null && u != currentlySelectedUnit) continue;

			if (u == currentlySelectedUnit) {
				selected = u;
				break;
			}

			if (animTracker.hasCurrentAction(u) && animTracker.getUnitAppearance(u).DeservesPlayerAttention()) {
				doingInterestingAnimation = u;
				break;
			}

			if (u.HasPriorityAsDefender(bestDefender, currentlySelectedUnit))
				bestDefender = u;
		}

		// On water, show the transport of the best defender (hopefully stable)
		if (tile.IsWater()) {
			foreach (var u in units) {
				if (u.CanTransport() && bestDefender.IsLoadedIn(u))
					transporter = u;
			}
		}

		// Prefer showing the selected unit, then animation, then transport, otherwise show the top defender
		return selected ?? doingInterestingAnimation ?? transporter ?? bestDefender;
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		if (!UnitsVisible(gameData, looseView.mapView.game.controller, tile)) {
			return;
		}

		if (looseView.IsTileCoveredByTileInfo(tile)) {
			return;
		}

		// First draw animated effects. These will always appear over top of units regardless of draw order due to z-index.
		AnimationTracker animTracker = looseView.mapView.game.animationController.animTracker;
		if (animTracker.hasTileEffects) {
			C7Animation tileEffect = animTracker.getTileEffect(tile);
			if (tileEffect != null) {
				(_, float progress, _) = animTracker.getCurrentActionAndProgress(tile);
				drawEffectAnimFrame(looseView, tileEffect, progress, tileCenter);
			}
		}

		if (tile.unitsOnTile.Count == 0) {
			return;
		}

		var white = Color.Color8(255, 255, 255);

		DisplayedUnit displayed = getDisplayedUnit(looseView, tile);
		MapUnit unit = displayed.unit;
		MapUnit.Appearance appearance = animTracker.getUnitAppearance(unit);
		Vector2 animOffset = new Vector2(appearance.offsetX, appearance.offsetY) * MapView.cellSize;

		// If the unit we're about to draw is currently selected, draw the cursor first underneath it
		if ((unit != MapUnit.NONE) && (unit == looseView.mapView.game.CurrentlySelectedUnit)) {
			drawCursor(looseView, tileCenter + animOffset);
		}

		drawUnitAnimFrame(looseView, unit, appearance, tileCenter);

		// TODO: Figure out how we can draw the unit's HP bar above the unit and the cursor

		// Option A: Support all kind of zoom levels. The downside is at large zoom distances, the HP indicators dominate the screen
		// float cameraZoom = Math.Min(looseView.mapView.cameraZoom, 1.0f);

		// Option B: 2 Zoom levels regular, and double size.
		// At larger distances it stays relatively small, but at this point I don't think we need to see the HP
		float cameraZoom = Math.Clamp(looseView.mapView.cameraZoom, 0.5f, 1.0f);

		float offsetXFromCenter = 26;
		Vector2 hpStartingLocation = tileCenter - new Vector2(offsetXFromCenter, 0) + animOffset;

		// An army shows the combined hit points of its members.
		int maxHp = displayed.maxHitPoints;
		int hp = displayed.hitPoints;
		float hpIndHeight = GetHpFractionHeight(maxHp) / cameraZoom;
		float hpIndWidth = 2 / cameraZoom;
		float hpBarTotal = (hpIndHeight * maxHp + (maxHp - 1)/cameraZoom);
		Vector2 movementLedCropping = new Vector2(6, 6);
		Vector2 movementLedSize = movementLedCropping / cameraZoom;
		float fortifiedLineExpand = 0.5f / cameraZoom;
		float lineWidth = 1 / cameraZoom;

		int offsetYFromCenter = 8;
		Rect2 hpIndBackgroundRect = new Rect2(hpStartingLocation - new Vector2(0, offsetXFromCenter), Vector2.One);
		if (unit.IsCombatUnit()) {
			hpIndBackgroundRect = new Rect2((hpStartingLocation - new Vector2(0, hpBarTotal) - new Vector2(0, offsetYFromCenter)), new Vector2(hpIndWidth, hpBarTotal));
			float hpFraction = (float)hp / maxHp;
			looseView.DrawRect(hpIndBackgroundRect, Color.Color8(0, 0, 0));
			Color hpColor = GetHpColor(hpFraction, maxHp);
			for (int i = 0; i < hp; i++) {
				Rect2 hpContentsRect = new Rect2(hpIndBackgroundRect.Position + new Vector2(0, hpBarTotal) - new Vector2(0, hpIndHeight + (hpIndHeight+lineWidth)*i), new Vector2(hpIndWidth, hpIndHeight));
				looseView.DrawRect(hpContentsRect, hpColor);
			}
			if (unit.isFortified) {
				Rect2 fortifiedRect = hpIndBackgroundRect.Grow(fortifiedLineExpand);
				looseView.DrawRect(fortifiedRect, white, false, width: lineWidth);
			}
		}

		// TODO: Maybe add this is as a player configuration for a "harder" mode,
		// where players can't see how many enemy units there are even in tiles that don't have a city.
		// RightClickMenu functionality would need to be made configurable if this gets implemented in the future.
		if (unit.location.HasCity() && unit.owner != looseView.mapView.game.controller)
			return;

		// Draw movement indicator for our units
		if (looseView.mapView.game.controller == unit.owner) {
			int moveIndIndex = (!unit.movementPoints.canMove) ? 4 : ((unit.movementPoints.remaining >= unit.MaxMovementPoints()) ? 0 : 2);
			Vector2 moveIndUpperLeft = new Vector2((1 + 7 * moveIndIndex), 1);
			Rect2 moveIndRect = new Rect2(moveIndUpperLeft, movementLedCropping);
			Rect2 screenRect = new Rect2(hpIndBackgroundRect.Position - (new Vector2(2, 6) / cameraZoom), movementLedSize);
			looseView.DrawTextureRectRegion(unitMovementIndicators, screenRect, moveIndRect);
		}

		float lineMarginFromBar = 3 / cameraZoom;

		// Draw lines to show that there are more units on this tile
		if (tile.unitsOnTile.Count > 1) {
			int lineCount = tile.unitsOnTile.Count;
			// TODO: Make configurable to taste, with cap of 8?
			if (lineCount > 8)
				lineCount = 8;
			for (int n = 0; n < lineCount; n++) {
				Vector2 lineStart = hpStartingLocation - new Vector2(lineWidth, offsetYFromCenter - lineMarginFromBar - lineMarginFromBar*n);
				looseView.DrawLine(lineStart, lineStart + new Vector2(4, 0) / cameraZoom, white, width: lineWidth);
				looseView.DrawLine(lineStart + new Vector2(0, 1) / cameraZoom, lineStart + new Vector2(4, 1) / cameraZoom, Color.Color8(75, 75, 75));
			}
		}
	}

	// Draw smaller pixels for the hp fractions as the max hp grows
	private int GetHpFractionHeight(int h) {
		if (h <= 6)
			return 4;
		if (h <= 12)
			return 2;
		return 1;
	}

	private bool UnitsVisible(GameData gameData, Player player, Tile t) {
		if (gameData.observerMode) {
			return true;
		}

		// Only draw units on active tiles - otherwise if the tile is only known
		// but not actively seen, we can't see units.
		return player.tileKnowledge.isActiveTile(t);
	}
}
