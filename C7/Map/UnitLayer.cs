using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7.Textures;
using C7GameData;
using Godot;
using Serilog;

public partial class UnitLayer : LooseLayer {
	private static readonly ILogger log = LogManager.ForContext<UnitLayer>();

	private ImageTexture unitMovementIndicators;

	// The unit animations, effect animations, and cursor are all drawn as children attached to the looseView but aren't created and attached in
	// any particular order so we must use the ZIndex property to ensure they're properly layered. Z indices are shared by all the map's views,
	// so the cursor can't go below zero without going under the terrain. Instead it stays at the view's own level and is drawn behind the view
	// (ShowBehindParent), which puts it under the units. The hit point bars, movement LEDs and stack lines go on their own canvas item above the
	// units, as in Civ3, so wide art like ships and armies doesn't cover them.
	public const int effectAnimZIndex = 3;
	public const int indicatorZIndex = 2;
	public const int unitAnimZIndex = 1;
	public const int cursorZIndex = 0;

	// The canvas item the unit indicators are drawn to, a child of the view's own. See indicatorZIndex.
	private Rid indicatorItem;
	private LooseView indicatorView;

	private void BeginIndicators(LooseView looseView) {
		if (indicatorView != looseView) {
			indicatorView = looseView;
			indicatorItem = RenderingServer.CanvasItemCreate();
			RenderingServer.CanvasItemSetParent(indicatorItem, looseView.GetCanvasItem());
			RenderingServer.CanvasItemSetZIndex(indicatorItem, indicatorZIndex);
			Rid item = indicatorItem;
			looseView.TreeExiting += () => RenderingServer.FreeRid(item);
		}
		RenderingServer.CanvasItemClear(indicatorItem);
	}

	private void DrawIndicatorRect(Rect2 rect, Color color) {
		RenderingServer.CanvasItemAddRect(indicatorItem, rect, color);
	}

	// An outline like CanvasItem.DrawRect(filled: false), centered on the rect's edges.
	private void DrawIndicatorOutline(Rect2 rect, Color color, float width) {
		Rect2 outer = rect.Grow(width / 2);
		DrawIndicatorRect(new Rect2(outer.Position, new Vector2(outer.Size.X, width)), color);
		DrawIndicatorRect(new Rect2(outer.Position + new Vector2(0, outer.Size.Y - width), new Vector2(outer.Size.X, width)), color);
		DrawIndicatorRect(new Rect2(outer.Position, new Vector2(width, outer.Size.Y)), color);
		DrawIndicatorRect(new Rect2(outer.Position + new Vector2(outer.Size.X - width, 0), new Vector2(width, outer.Size.Y)), color);
	}

	private void DrawIndicatorLine(Vector2 from, Vector2 to, Color color, float width = -1) {
		RenderingServer.CanvasItemAddLine(indicatorItem, from, to, color, width);
	}

	public UnitLayer() {
		unitMovementIndicators = TextureLoader.Load("ui.unit_control.movement_indicators");
	}

	// The buttons of the worker jobs by texture key, null where a ruleset has
	// no texture for one.
	private readonly Dictionary<string, ImageTexture> jobBadges = new();

	private ImageTexture GetJobBadge(Terraform job) {
		if (!jobBadges.TryGetValue(job.ButtonTexture, out ImageTexture badge)) {
			try {
				badge = TextureLoader.Load(job.ButtonTexture + ".normal");
			} catch (Exception e) {
				log.Warning(e, "No badge texture for {Job}", job.Name);
				badge = null;
			}
			jobBadges[job.ButtonTexture] = badge;
		}
		return badge;
	}

	// Marks a worker with its job's button when its art has no animation of
	// the job, like for airfields, or no art for it at all.
	private void drawWorkerJobBadge(AnimationManager manager, AnimationManager.UnitArt art, MapUnit unit, Vector2 unitCenter) {
		Terraform job = unit.WorkerJob;
		if (job == null) {
			return;
		}
		if (job.Animation is MapUnit.AnimatedAction action && manager.HasUnitAction(art.artName, action)) {
			return;
		}
		ImageTexture badge = GetJobBadge(job);
		if (badge == null) {
			return;
		}
		const float size = 16;
		Rect2 rect = new Rect2(unitCenter + new Vector2(6, -24), new Vector2(size, size));
		RenderingServer.CanvasItemAddTextureRect(indicatorItem, rect, badge.GetRid());
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
		private int currentZIndex = unitAnimZIndex;
		private Vector2 currentPosition;
		private bool visible = true;

		// Instances are reused for units and effects, so whatever is drawn sets its own layer.
		public void SetZIndex(int zIndex) {
			if (zIndex == currentZIndex)
				return;
			currentZIndex = zIndex;
			this.sprite.ZIndex = zIndex;
			this.spriteTint.ZIndex = zIndex;
		}

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

		// Gives the tint sprite the shader's default tint, for what doesn't belong to a civ, like effects.
		public void SetUntinted() {
			if (ReferenceEquals(this.material, untintedMaterial))
				return;
			currentCivColor = -1;
			this.material = untintedMaterial;
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
		AnimationManager manager = looseView.mapView.game.animationController.civ3AnimData;
		drawUnitAnimFrame(looseView, unit, manager.GetUnitAnimation(unit, appearance.action, appearance.direction), appearance, tileCenter);
	}

	private void drawUnitAnimFrame(LooseView looseView, MapUnit unit, AnimationManager.AnimationInfo animation, MapUnit.Appearance appearance,
			Vector2 tileCenter) {
		AnimationInstance inst = getBlankAnimationInstance(looseView);
		inst.SetZIndex(unitAnimZIndex);
		inst.SetPosition(GetFramePosition(appearance, animation, tileCenter));
		inst.SetCivColor(unit.owner.GetPlayerColor());
		inst.SetAnimationFrame(animation.animationName, animation.FrameAtProgress(appearance.progress));
		inst.Show();
	}

	// Returns the art to draw a tile's displayed unit with, worked out again only when it may have changed: when the unit's owner enters a new
	// era (the unit, its type and owner are part of the displayed unit's stamp), or every time for units whose art can change otherwise.
	private AnimationManager.UnitArt GetDisplayedUnitArt(AnimationManager manager, DisplayedUnit displayed) {
		MapUnit unit = displayed.unit;
		string era = unit.owner?.eraCivilopediaName;
		if (displayed.art == null || displayed.artMayChange || era != displayed.artEra) {
			displayed.art = manager.GetUnitArt(AnimationManager.ArtNameFor(unit));
			displayed.artEra = era;
			displayed.artMayChange = AnimationManager.ArtMayChange(unit);
		}
		return displayed.art;
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
		inst.SetZIndex(effectAnimZIndex);
		// Effects don't belong to a civ, so don't keep the tint of whatever unit the instance drew before.
		inst.SetUntinted();
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
			// Under the unit and the hit point bar drawn over it; see cursorZIndex.
			cursorSprite.ZIndex = cursorZIndex;
			cursorSprite.ShowBehindParent = true;
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
		BeginIndicators(looseView);

		AnimationController animationController = looseView.mapView.game.animationController;
		animationController.updateAnimations();

		// The displayed units are worked out again every turn, and for every game.
		bool newTurnOrGame = gameData != displayedUnitsGame || gameData.turn != displayedUnitsTurn;
		if (newTurnOrGame || displayedUnits.Count > MaxDisplayedUnitsCached) {
			displayedUnits.Clear();
			displayedUnitsGame = gameData;
			displayedUnitsTurn = gameData.turn;
		}

		// Animations that pause or repeat, like a death or a worker at work, are kept until they're replaced, so drop the ones of units that
		// are gone every turn and every few seconds.
		long now = animationController.animTracker.getCurrentTimeMS();
		if (newTurnOrGame || now - lastAnimationPruneMS >= AnimationPruneIntervalMS) {
			lastAnimationPruneMS = now;
			animationController.animTracker.forgetRemovedUnits(gameData);
		}
	}

	private long lastAnimationPruneMS = 0;
	private const long AnimationPruneIntervalMS = 2000;

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

		// The art the unit is drawn with, filled in when it's first drawn, and what it was worked out for. See GetDisplayedUnitArt.
		public AnimationManager.UnitArt art;
		public string artEra;
		public bool artMayChange;
	}

	private readonly Dictionary<Tile, DisplayedUnit> displayedUnits = new(ReferenceEqualityComparer.Instance);
	private GameData displayedUnitsGame = null;
	private int displayedUnitsTurn = -1;
	private const int MaxDisplayedUnitsCached = 8192;

	// Returns a value that changes whenever anything that selectUnitToDisplay
	// and the hit point bar depend on changes for a tile's units: which units
	// are there and in what order, their types, hit points, fortification and
	// loading, experience and owners, the selected unit and whether each unit
	// is at peace with it, and the tile's city. Defense bonuses from terrain
	// and buildings only change between turns, when the cache is cleared
	// anyway. Returns null if a unit is playing an animation the player
	// should see, since the choice then depends on the animation's progress.
	// Other animations, like a worker at work, don't change the choice.
	private long? displayedUnitStamp(AnimationTracker animTracker, Tile tile, List<MapUnit> units, MapUnit currentlySelectedUnit) {
		Player opponent = currentlySelectedUnit?.owner;
		Player lastOwner = null;
		bool lastOwnerAtPeace = false;

		long stamp = Mix(Mix(17, units.Count), RuntimeHelpers.GetHashCode(currentlySelectedUnit));
		stamp = Mix(stamp, RuntimeHelpers.GetHashCode(tile.cityAtTile));
		foreach (MapUnit u in units) {
			if (animTracker.hasAnimationDeservingAttention(u))
				return null;

			if (u.owner != lastOwner || lastOwner == null) {
				lastOwner = u.owner;
				lastOwnerAtPeace = opponent?.IsAtPeaceWith(u.owner) ?? true;
			}

			stamp = Mix(stamp, u.id?.GetHashCode() ?? 0);
			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(u.unitType));
			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(u.owner));
			stamp = Mix(stamp, RuntimeHelpers.GetHashCode(u.experienceLevel));
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

		AnimationManager manager = looseView.mapView.game.animationController.civ3AnimData;
		AnimationManager.UnitArt art = GetDisplayedUnitArt(manager, displayed);
		drawUnitAnimFrame(looseView, unit, manager.GetUnitAnimation(art, appearance.action, appearance.direction), appearance, tileCenter);
		drawWorkerJobBadge(manager, art, unit, tileCenter + animOffset);

		// The indicators go on their own canvas item, above the units; see indicatorZIndex.

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
		// Past MaxSegmentedHp, like an army of veterans, the segments would
		// shrink to slivers, so the bar is drawn as one continuous fill as
		// tall as the tallest segmented bar instead.
		bool segmented = maxHp <= MaxSegmentedHp;
		float hpIndHeight = GetHpFractionHeight(maxHp) / cameraZoom;
		float hpIndWidth = 2 / cameraZoom;
		float hpBarTotal = segmented
			? (hpIndHeight * maxHp + (maxHp - 1)/cameraZoom)
			: (GetHpFractionHeight(MaxSegmentedHp) * MaxSegmentedHp + (MaxSegmentedHp - 1)) / cameraZoom;
		Vector2 movementLedCropping = new Vector2(6, 6);
		Vector2 movementLedSize = movementLedCropping / cameraZoom;
		float fortifiedLineExpand = 0.5f / cameraZoom;
		float lineWidth = 1 / cameraZoom;

		int offsetYFromCenter = 8;
		Rect2 hpIndBackgroundRect = new Rect2(hpStartingLocation - new Vector2(0, offsetXFromCenter), Vector2.One);
		if (unit.IsCombatUnit()) {
			hpIndBackgroundRect = new Rect2((hpStartingLocation - new Vector2(0, hpBarTotal) - new Vector2(0, offsetYFromCenter)), new Vector2(hpIndWidth, hpBarTotal));
			float hpFraction = (float)hp / maxHp;
			DrawIndicatorRect(hpIndBackgroundRect, Color.Color8(0, 0, 0));
			Color hpColor = GetHpColor(hpFraction, maxHp);
			if (!segmented) {
				float fill = hpBarTotal * Math.Clamp(hpFraction, 0f, 1f);
				DrawIndicatorRect(new Rect2(hpIndBackgroundRect.Position + new Vector2(0, hpBarTotal - fill), new Vector2(hpIndWidth, fill)), hpColor);
			}
			for (int i = 0; segmented && i < hp; i++) {
				Rect2 hpContentsRect = new Rect2(hpIndBackgroundRect.Position + new Vector2(0, hpBarTotal) - new Vector2(0, hpIndHeight + (hpIndHeight+lineWidth)*i), new Vector2(hpIndWidth, hpIndHeight));
				DrawIndicatorRect(hpContentsRect, hpColor);
			}
			if (unit.isFortified) {
				Rect2 fortifiedRect = hpIndBackgroundRect.Grow(fortifiedLineExpand);
				DrawIndicatorOutline(fortifiedRect, white, lineWidth);
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
			RenderingServer.CanvasItemAddTextureRectRegion(indicatorItem, screenRect, unitMovementIndicators.GetRid(), moveIndRect);
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
				DrawIndicatorLine(lineStart, lineStart + new Vector2(4, 0) / cameraZoom, white, lineWidth);
				DrawIndicatorLine(lineStart + new Vector2(0, 1) / cameraZoom, lineStart + new Vector2(4, 1) / cameraZoom, Color.Color8(75, 75, 75));
			}
		}
	}

	// The most hit points the bar is drawn with one segment each.
	private const int MaxSegmentedHp = 12;

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
