using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using C7Engine;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7GameData {
	/**
	 * A unit on the map.  Not to be confused with a unit prototype.
	 **/
	public partial class MapUnit {
		private static ILogger log = Log.ForContext<MapUnit>();
		public ID id { get; internal set; }
		public string name { get; internal set; }
		public Civilization nationality { get; set; }
		public UnitPrototype unitType { get; set; }
		public Player owner { get; set; }
		public Tile previousLocation { get; internal set; }
		private Tile currentLocation;

		public Tile location {
			get => currentLocation;
			set {
				previousLocation = location;
				currentLocation = value;
			}
		}
		public TilePath path { get; set; }

		public string experienceLevelKey;
		[JsonIgnore]
		public ExperienceLevel experienceLevel { get; set; }

		public MovementPoints movementPoints = new MovementPoints();
		public int hitPointsRemaining { get; set; }
		public int maxHitPoints {
			get {
				return this.experienceLevel.baseHitPoints + this.unitType.hpBonus;
			}
		}
		public bool isFortified { get; set; }

		// True if the unit held this turn before moving, so it still heals.
		public bool heldWithoutMoving { get; set; }
		// A sentried unit sleeps until a unit it should notice comes next to
		// it: any foreign unit, or with sentryEnemyOnly only an enemy one.
		public bool isSentried { get; set; }
		public bool sentryEnemyOnly { get; set; }

		public bool isAutomated { get; set; }

		//sentry, etc. will come later.  For now, let's just have a couple things so we can cycle through units that aren't fortified.
		public int defensiveBombardsRemaining;

		// Whether the unit has attacked or bombarded this turn. Only units with
		// blitz can attack more than once per turn.
		public bool hasAttackedThisTurn;

		public bool CanAttackAgainThisTurn() {
			return !hasAttackedThisTurn || unitType.hasBlitz;
		}

		public TileDirection facingDirection = TileDirection.SOUTHEAST;

		public float WorkerProgressTowardsJob { get; set; }
		public Terraform WorkerJob { get; set; }

		public ID loadedOnUnitId { get; set; }

		public UnitAI currentAI;

		public MapUnit(ID id) {
			this.id = id;
		}

		internal MapUnit() { }

		public static MapUnit NONE = new MapUnit(ID.None("unit"));

		public static bool IsMapUnitValid(MapUnit mapUnit) {
			return mapUnit != null && mapUnit != NONE;
		}

		public bool IsBusy() {
			return isFortified || isSentried || (path != null && path.PathLength() > 0) || WorkerJob != null || isAutomated;
		}

		public bool IsLandUnit() {
			return this.unitType.IsLandUnit();
		}
		public bool IsWaterUnit() {
			return this.unitType.IsSeaUnit();
		}
		public bool IsAirUnit() {
			return this.unitType.IsAirUnit();
		}

		public bool CanDefendOnLand() {
			return IsLandUnit() && CombatBaseStrength(CombatRole.Defense) > 0;
		}

		public bool IsCombatUnit() {
			// An army fights with its members, so an empty one can't fight.
			if (IsArmy()) {
				if (!Tile.IsTileValid(location))
					return false;
				foreach (MapUnit u in location.unitsOnTile) {
					if (IsPassenger(u) && u.IsCombatUnit())
						return true;
				}
				return false;
			}
			return this.unitType.attack > 0 || this.unitType.defense > 0;
		}

		// Whether this unit can start a fight.
		public bool CanAttack() {
			return CombatBaseStrength(CombatRole.Attack) > 0;
		}

		public bool CanBeActive() {
			return !this.IsBusy() && this.movementPoints.canMove;
		}

		public bool IsCaptive() {
			Civilization civ = this.owner.civilization;
			// Almost always the very same civilization (or name), which can't
			// be a captive; only otherwise compare the names as before.
			if (ReferenceEquals(this.nationality, civ))
				return false;
			string nationalityName = this.nationality.name, ownerName = civ.name;
			if (ReferenceEquals(nationalityName, ownerName))
				return false;
			return !string.Equals(nationalityName, ownerName, StringComparison.CurrentCultureIgnoreCase);
		}

		public bool IsArmy() {
			return this.unitType.isArmy;
		}

		// Whether this unit can carry other units: transports and armies.
		public bool CanCarryUnits() {
			return this.unitType.capacity > 0 && (this.unitType.HasUnloadAction() || IsArmy());
		}

		// Whether this unit is a transport its passengers can be unloaded
		// from. Armies are rigid unless the game option allows unloading them,
		// in which case they're treated as having the unload action.
		public bool CanTransport() {
			if (this.unitType.capacity <= 0)
				return false;
			if (IsArmy())
				return owner?.rules?.AllowUnloadFromArmy ?? false;
			return this.unitType.HasUnloadAction();
		}

		// The units loaded on this one. They always share its tile.
		public List<MapUnit> Passengers() {
			if (!Tile.IsTileValid(location))
				return [];
			List<MapUnit> passengers = new();
			foreach (MapUnit u in location.unitsOnTile) {
				if (IsPassenger(u))
					passengers.Add(u);
			}
			return passengers;
		}

		// Whether u, a unit on this unit's tile, is one of Passengers().
		private bool IsPassenger(MapUnit u) {
			return u != this && u.IsLoadedIn(this);
		}

		// Passengers().Count, without building the list.
		public int PassengerCount() {
			if (!Tile.IsTileValid(location))
				return 0;
			int count = 0;
			foreach (MapUnit u in location.unitsOnTile) {
				if (IsPassenger(u))
					count++;
			}
			return count;
		}

		// The unit this one is loaded on, or null.
		public MapUnit Carrier() {
			if (!IsLoaded() || !Tile.IsTileValid(location))
				return null;
			foreach (MapUnit u in location.unitsOnTile) {
				if (u != this && IsLoadedIn(u))
					return u;
			}
			return null;
		}

		public bool IsInArmy() {
			return Carrier()?.IsArmy() ?? false;
		}

		// A unit in an army that can't be unloaded takes no orders of its own;
		// it goes where the army goes.
		public bool IsLockedInArmy() {
			MapUnit carrier = Carrier();
			return carrier != null && carrier.IsArmy() && !carrier.CanTransport();
		}

		// How many units this unit can carry. The Pentagon (or any building
		// that allows larger armies) lets each of the owner's armies take one
		// more.
		public int Capacity() {
			int capacity = this.unitType.capacity;
			if (IsArmy() && capacity > 0 && (owner?.HasLargerArmies() ?? false))
				capacity += 1;
			return capacity;
		}

		// The movement allowance this unit gets each turn. An army moves at the
		// pace of its slowest member; an empty army just has its own allowance.
		public int MaxMovementPoints() {
			if (IsArmy() && Tile.IsTileValid(location)) {
				bool any = false;
				int min = 0;
				foreach (MapUnit m in location.unitsOnTile) {
					if (!IsPassenger(m))
						continue;
					if (!any || m.unitType.movement < min)
						min = m.unitType.movement;
					any = true;
				}
				if (any)
					return min;
			}
			return this.unitType.movement;
		}

		// An army's hit points are the total of its members'. An empty army
		// falls back to its own, as does any other unit.
		public int CompositeHitPoints() {
			if (IsArmy() && Tile.IsTileValid(location)) {
				bool any = false;
				int sum = 0;
				foreach (MapUnit m in location.unitsOnTile) {
					if (!IsPassenger(m))
						continue;
					sum = checked(sum + m.hitPointsRemaining);
					any = true;
				}
				if (any)
					return sum;
			}
			return this.hitPointsRemaining;
		}

		public int CompositeMaxHitPoints() {
			if (IsArmy() && Tile.IsTileValid(location)) {
				bool any = false;
				int sum = 0;
				foreach (MapUnit m in location.unitsOnTile) {
					if (!IsPassenger(m))
						continue;
					sum = checked(sum + m.maxHitPoints);
					any = true;
				}
				if (any)
					return sum;
			}
			return this.maxHitPoints;
		}

		public bool IsLoadable() {
			return this.unitType.HasLoadAction();
		}

		public bool IsLoaded() {
			return this.loadedOnUnitId != null;
		}

		public bool IsLoadedIn(MapUnit transport) {
			return transport.id == this.loadedOnUnitId;
		}

		public override string ToString() {
			if (this != NONE) {
				return $"{this.owner} {this.GetDisplayName()} at [{this.location.XCoordinate}, {this.location.YCoordinate}] " +
					   $"with {this.movementPoints.getMixedNumber()} MP and {this.hitPointsRemaining} HP, id = {id}";
			} else {
				return "This is the NONE unit";
			}
		}

		public string GetDisplayName() {
			return this.IsCaptive() ? $"{this.name} ({this.nationality.name})" : this.name;
		}

		// TODO: best move this to lua at some point
		public string GetArtName() {
			Dictionary<string, string> variations = this.unitType.art.mainArt.variations;
			if (variations != null) {
				if (this.unitType.isWorker && this.IsCaptive()) {
					string slaveArt = this.unitType.GetSlaveArtName();
					if (slaveArt != null)
						return slaveArt;
				}

				if (variations.TryGetValue(this.owner.eraCivilopediaName ?? "", out var value))
					return value;

				//TODO: add military + science leader variation
			}

			return this.unitType.art.mainArt.defaultName;
		}

		public string Describe() {
			UnitPrototype type = this.unitType;
			string exp = this.IsCombatUnit() ? $"{this.experienceLevel.displayName}" : "";
			string hPDesc = IsCombatUnit() ? $" ({this.CompositeHitPoints()}/{this.CompositeMaxHitPoints()})" : "";
			string displayName = this.IsCaptive() ? $" ({this.nationality.adjective}) {this.name}" : $" {this.name}";
			string attackDesc = (type.bombard > 0) ? $"{type.attack}({type.bombard})" : type.attack.ToString();
			string stats = $" ({attackDesc}.{type.defense}.{(EngineStorage.uiControllerID == this.owner.id ? $"{this.movementPoints.getMixedNumber()}/" : "")}{MaxMovementPoints()})";
			return $"{exp}{hPDesc}{displayName}{stats}".Trim();
		}

		// TODO: The contents of this enum are copy-pasted from UnitAction in Civ3UnitSprite.cs. We should unify these so we don't have two different
		// but virtually identical enums.
		public enum AnimatedAction {
			BLANK,
			DEFAULT,
			WALK,
			RUN,
			ATTACK1,
			ATTACK2,
			ATTACK3,
			DEFEND,
			DEATH,
			DEAD,
			FORTIFY,
			FORTIFYHOLD,
			FIDGET,
			VICTORY,
			TURNLEFT,
			TURNRIGHT,
			BUILD,
			ROAD,
			MINE,
			IRRIGATE,
			FORTRESS,
			CAPTURE,
			JUNGLE,
			FOREST,
			PLANT
		}

		public struct Appearance {
			public AnimatedAction action;
			public TileDirection direction;
			public float progress; // Varies 0 to 1
			public float offsetX, offsetY; // Offset is in grid cells from the unit's location
			public AnimationEnding ending;

			// When true, indicates that the animation is still playing (f.e. a unit is still running between tiles) so the UI shouldn't yet
			// autoselect another unit.
			public bool DeservesPlayerAttention() {
				// TODO: Special rules for different animations. We don't need to see workers do their thing but we do want to watch units
				// move. IMO we should also not show units fortifying even though I know the original game does.
				// This may also be the culprit behind why we can fortify a unit that is in motion.
				if (ending == AnimationEnding.Repeat) {
					return false;
				}
				return progress < 1.0;
			}
		}

		private const int JOB_PROGRESS_WORKER = 2;
		private const int JOB_PROGRESS_SLAVE = 1;

		private static int GetWorkerJobCost(Tile tile, Terraform workerJob) {
			// For the movement cost multiplier, see note 7
			// (https://apolyton.net/forum/civilization-series/civilization-iii/59815-civilization-iii-bic-file-format-2nd-thread?p=1362768#post1362768)
			// For example, clearing a forest has a cost of 4, but with a normal
			// worker that would take 2 turns. In order for the job to take the
			// expected 4 turns we need to multiply by the movement cost of the
			// terrain. This also makes roading hills/mountains more expensive.
			return tile.overlayTerrainType.movementCost * workerJob.TurnsToComplete;
		}

		public async Task animateAsync(AnimatedAction action, AnimationEnding ending = AnimationEnding.Stop) {
			var animationsEnabled = EngineStorage.animationsEnabled && !EngineStorage.gameData.observerMode;
			var skipAnimations = SkipAnimations(action);

			if (animationsEnabled && !skipAnimations && IsAnimationVisibleToUIPlayer()) {
				var msg = new MsgStartUnitAnimation(this, action, ending);
				msg.send();

				await EngineStorage.WaitForAnimationFinished(msg.animationId);
			}
			if (this.owner.isHuman)
				new MsgUnitMoved(this).send();
		}

		// Whether the UI would play an animation of this unit. The UI only
		// shows unit animations on tiles its player can see (the unit's tile
		// or, for a move, the tile it left) and just marks the others
		// completed on its next frame. Skipping those here saves the engine,
		// and so every AI unit's step, from waiting a frame for nothing. The
		// UI's player's own units are always animated.
		private bool IsAnimationVisibleToUIPlayer() {
			Player uiPlayer = EngineStorage.gameData.GetUIControllerPlayer();
			if (uiPlayer?.tileKnowledge == null || uiPlayer == this.owner)
				return true;
			return uiPlayer.tileKnowledge.isActiveTile(this.location) || uiPlayer.tileKnowledge.isActiveTile(this.previousLocation);
		}

		public void animate(AnimatedAction action, AnimationEnding ending = AnimationEnding.Stop) {
			_ = animateAsync(action, ending);
		}

		private bool SkipAnimations(AnimatedAction action) {
			if (action != AnimatedAction.RUN) return false;

			// as soon as we move, the tile we were just on becomes the previous tile
			var isOnRailroad = Tile.IsTileValid(this.previousLocation) && this.previousLocation.HasRailroad();
			if (!isOnRailroad) return false;

			// and the tile we are moving towards, becomes the current tile
			var movingOnRailroad = Tile.IsTileValid(this.location) && this.location.HasRailroad();
			if (!movingOnRailroad) return false;

			var canMoveFreely = Player.CanMoveFreely(this.owner, this.previousLocation, this.location);
			if (!canMoveFreely) return false;

			return true;
		}

		public void ResetFacingDirection() {
			facingDirection = TileDirection.SOUTHEAST;
		}

		public IEnumerable<StrengthBonus> ListStrengthBonusesVersus(MapUnit opponent, CombatRole role, TileDirection? attackDirection) {
			GameData gD = EngineStorage.gameData;

			if (role.Defending()) {
				if (isFortified)
					yield return gD.fortificationBonus;

				yield return location.overlayTerrainType.defenseBonus;

				foreach (StrengthBonus sb in location.overlays.GetDefenseBonuses()) {
					yield return sb;
				}

				if ((!role.Bombarding()) && (attackDirection is TileDirection dir) && location.HasRiverCrossing(dir.Reversed()))
					yield return gD.riverCrossingBonus;

				if (location.cityAtTile != null) {
					foreach (StrengthBonus sb in location.cityAtTile.GetDefenseBonuses()) {
						yield return sb;
					}
				}
			}
		}

		public double StrengthVersus(MapUnit opponent, CombatRole role, TileDirection? attackDirection) {
			return CombatBaseStrength(role) * StrengthBonus.ListToMultiplier(ListStrengthBonusesVersus(opponent, role, attackDirection));
		}

		// The unit's base strength in a fight. An army has none of its own and
		// uses that of the member currently fighting for it. Bonuses still come
		// from the army itself (its tile, whether it's fortified), so its
		// members' own fortification adds nothing.
		public double CombatBaseStrength(CombatRole role) {
			return Combatant(role).unitType.BaseStrength(role);
		}

		// The unit that actually fights when this one is in combat. For most
		// units that's the unit itself. An army commits one member at a time,
		// using as few members as the fight needs: members with more than one
		// hit point go first, strongest first, and a member keeps fighting
		// until it's down to its last hit point before a fresh one is rotated
		// in. Only when every member is down to one does the army fight on
		// with them, and members start to die. An empty army has nobody to
		// fight for it, so it returns itself.
		public MapUnit Combatant(CombatRole role) {
			if (!IsArmy())
				return this;

			MapUnit best = null;
			if (!Tile.IsTileValid(location))
				return this;
			foreach (MapUnit member in location.unitsOnTile) {
				if (!IsPassenger(member) || member.hitPointsRemaining <= 0)
					continue;
				if (best == null || IsBetterCombatant(member, best, role))
					best = member;
			}
			return best ?? this;
		}

		private static bool IsBetterCombatant(MapUnit a, MapUnit b, CombatRole role) {
			bool aHealthy = a.hitPointsRemaining > 1, bHealthy = b.hitPointsRemaining > 1;
			if (aHealthy != bHealthy)
				return aHealthy;
			double aStrength = a.unitType.BaseStrength(role), bStrength = b.unitType.BaseStrength(role);
			if (aStrength != bStrength)
				return aStrength > bStrength;
			// Between equals, stick with the one that's already been fighting,
			// so fresh members stay out of the fight as long as possible.
			int aDamage = a.maxHitPoints - a.hitPointsRemaining, bDamage = b.maxHitPoints - b.hitPointsRemaining;
			if (aDamage != bDamage)
				return aDamage > bDamage;
			return a.hitPointsRemaining > b.hitPointsRemaining;
		}

		// Takes one hit point of damage from a fight. In an army the hit goes
		// to the member doing the fighting, and a member that dies is removed.
		// Returns true if this unit has nothing left to fight with.
		internal bool AbsorbCombatHit(MapUnit combatant) {
			if (combatant == this || !IsArmy()) {
				hitPointsRemaining -= 1;
				return hitPointsRemaining <= 0;
			}

			combatant.hitPointsRemaining -= 1;
			if (combatant.hitPointsRemaining <= 0) {
				log.Information($"{combatant} died fighting for {this}");
				EngineStorage.gameData.RemoveUnit(combatant);
			}
			return LoseArmyIfEmpty();
		}

		// Takes one hit point of bombardment damage. An army spreads it over
		// its members, hitting whichever has the most hit points; unless the
		// bombardment is lethal, it can't kill a member.
		internal bool AbsorbBombardHit(bool lethal) {
			// The first of the members with the most hit points.
			MapUnit victim = null;
			if (IsArmy() && Tile.IsTileValid(location)) {
				foreach (MapUnit m in location.unitsOnTile) {
					if (IsPassenger(m) && (victim == null || m.hitPointsRemaining > victim.hitPointsRemaining))
						victim = m;
				}
			}
			if (victim == null) {
				hitPointsRemaining -= 1;
				return hitPointsRemaining <= 0;
			}
			if (victim.hitPointsRemaining <= 1 && !lethal)
				return false;
			return AbsorbCombatHit(victim);
		}

		// An army whose last member has died is beaten. Its own hit points are
		// zeroed so that it reads as dead, like any other unit.
		private bool LoseArmyIfEmpty() {
			if (Tile.IsTileValid(location)) {
				foreach (MapUnit m in location.unitsOnTile) {
					if (IsPassenger(m) && m.hitPointsRemaining > 0)
						return false;
				}
			}
			hitPointsRemaining = 0;
			return true;
		}

		public bool CanDefendAgainst(MapUnit attacker) {
			//Basically, unit type must match.  Sea/air units in a city/airfield can't defend against land units.
			//Land units on a boat or planes on a carrier can't defend against boats.  Anti-air is another category that should be checked before the direct combat.
			//Potential future hybrid units that have multiple categories (e.g. amphibious vehicles) may contain more than one category.
			if (attacker.unitType.IsLandUnit() && !unitType.IsLandUnit()) {
				return false;
			}
			if (attacker.unitType.IsSeaUnit() && !unitType.IsSeaUnit()) {
				return false;
			}
			if (attacker.unitType.IsAirUnit() && !unitType.IsAirUnit()) {
				return false;
			}
			return true;
		}

		// Answers the question: if "opponent" is attacking the tile that this unit is standing on, does this unit defend instead of "otherDefender"?
		// Note that otherDefender does not necessarily belong to the same civ as this  Under standard Civ 3 rules you can't have units belonging
		// to two different civs on the same tile, but we don't want to assume that. In that case, whoever is an enemy of "opponent" should get
		// priority. Otherwise it's just whoever is stronger on defense.
		public bool HasPriorityAsDefender(MapUnit otherDefender, MapUnit opponent) {
			bool weAreEnemy           = IsEnemyDefenderAgainst(opponent);
			bool otherDefenderIsEnemy = otherDefender.IsEnemyDefenderAgainst(opponent);
			if (weAreEnemy != otherDefenderIsEnemy)
				return weAreEnemy;

			return HasPriorityAsDefender(weAreEnemy, TotalDefensiveStrengthVersus(opponent),
				otherDefenderIsEnemy, otherDefender.TotalDefensiveStrengthVersus(opponent));
		}

		// HasPriorityAsDefender, given what it compares for both units, so
		// that choosing among many defenders works each unit's values out
		// only once (see Tile.FindTopDefender).
		internal static bool HasPriorityAsDefender(bool weAreEnemy, double ourTotalStrength, bool otherDefenderIsEnemy, double theirTotalStrength) {
			if (weAreEnemy && !otherDefenderIsEnemy)
				return true;
			if (otherDefenderIsEnemy && !weAreEnemy)
				return false;
			return ourTotalStrength > theirTotalStrength;
		}

		// Whether the opponent attacking this unit's tile is not at peace with
		// this unit's owner.
		internal bool IsEnemyDefenderAgainst(MapUnit opponent) {
			return !opponent.owner?.IsAtPeaceWith(owner) ?? false;
		}

		// This unit's defensive strength against the opponent, times its hit
		// points.
		internal double TotalDefensiveStrengthVersus(MapUnit opponent) {
			return StrengthVersus(opponent, CombatRole.Defense, null) * CompositeHitPoints();
		}


		// Rolls for a promotion after winning a fight. The animation plays on
		// animatedUnit if given, for example the army a member fought for.
		public void RollToPromote(MapUnit opponent, MapUnit animatedUnit = null) {
			// Barbarians can't promote.
			if (owner.isBarbarians) {
				return;
			}

			double promotionChance = experienceLevel.promotionChance;
			if (opponent.owner.isBarbarians)
				promotionChance /= 2.0;
			if (owner.civilization.traits.Contains(Civilization.Trait.Militaristic))
				promotionChance *= 2;
			if (GameData.rng.NextDouble() < promotionChance) {
				Promote();
				(animatedUnit ?? this).animate(AnimatedAction.VICTORY);
			}
		}

		public void Promote() {
			ExperienceLevel nextLevel = EngineStorage.gameData.GetExperienceLevelAfter(experienceLevel);
			if (nextLevel != null) {
				experienceLevelKey = nextLevel.key;
				experienceLevel = nextLevel;
				hitPointsRemaining++;
			}
		}

		public double RetreatChance(MapUnit opponent, bool isAttacking) {
			if ((MaxMovementPoints() <= 1) || (opponent.MaxMovementPoints() > 1))
				return 0.0;
			MapUnit combatant = Combatant(isAttacking ? CombatRole.Attack : CombatRole.Defense);
			return combatant.experienceLevel.retreatChance;
		}

		internal TileDirection GetAttackAnimationDirection(TileDirection attackDirection) {
			return unitType.rotateBeforeAttack ? attackDirection.RotatedCounterClockwise90Degrees() : attackDirection;
		}

		internal TileDirection GetDefenseAnimationDirection(TileDirection attackDirection) {
			return GetAttackAnimationDirection(attackDirection.Reversed());
		}

		public int HealRateAt(Tile location) {
			GameData gD = EngineStorage.gameData;
			City city = location.cityAtTile;
			bool inFriendlyCity = (city != null) && (city != City.NONE) && owner.IsAtPeaceWith(city.owner);
			if (inFriendlyCity) {
				// Barracks fully heal land units in their own city, and harbors
				// do the same for ships.
				if (city.owner == owner && city.GetBuildings().Any(cb =>
						(IsLandUnit() && cb.building.providesVeteranGroundUnits)
						|| (IsWaterUnit() && cb.building.providesVeteranSeaUnits))) {
					return maxHitPoints;
				}
				return gD.healRateInCity;
			}
			if (unitType.IsSeaUnit())
				return 0;

			// Units heal faster in their own territory and not at all in the
			// territory of a civ they're at war with.
			Player territoryOwner = location.OwningPlayer();
			if (territoryOwner == owner)
				return gD.healRateInFriendlyField;
			if (territoryOwner != null && AtWar(owner, territoryOwner) && !CanHealInEnemyTerritory())
				return gD.healRateInHostileField;
			return gD.healRateInNeutralField;
		}

		// Battlefield Medicine lets a civ's units heal in enemy territory.
		private bool CanHealInEnemyTerritory() {
			// Plain loops: no closures or enumerator boxing, and it stops at
			// the first such building. Only reached for units in the
			// territory of a civ their owner is at war with.
			List<City> cities = owner.cities;
			for (int i = 0; i < cities.Count; i++) {
				List<CityBuilding> buildings = cities[i].constructed_buildings;
				for (int j = 0; j < buildings.Count; j++) {
					if (buildings[j].building.allowsEnemyTerritoryHealing)
						return true;
				}
			}
			return false;
		}

		public enum Intent {
			Disabled,         // do nothing, don't move
			MoveFreely,       // enter tile
			Fight,            // move and fight an enemy unit, capture city/unit, pillage freely, etc
			Load,             // load a unit on a friendly transport on this tile
			Unload,           // unload a unit from a transport on this tile
			NoticeUnit,       // a non-combat unit tries to move on another owner's unit tile
			NoticeCity,       // a non-combat unit tries to move on another owner's city tile
			NoticeAlliance,   // a combat unit tries to move on an ally's city/unit tile
			WarDeclaration,   // a combat unit tries to move on another owner's city/unit tile (TODO: pillage non-enemy road/colony etc for example)
		}

		/// <summary>
		/// Returns a unit's intent trying to move in on a tile.
		/// </summary>
		/// <param name="tile"></param>
		/// <returns></returns>
		// private Intent ResolveIntent(Tile tile, bool tset = false) {
		private Intent ResolveIntent(Tile tile) {
			if (!Tile.IsTileValid(tile))
				return Intent.Disabled;

			if (IsLockedInArmy())
				return Intent.Disabled;

			// Impassable terrain (e.g. some mods' deserts) cannot be entered
			// by any unit, but a barbarian camp tile stays enterable (like a
			// city) so the camp's garrison can move out and back in.
			if (tile.IsImpassable() && !tile.hasBarbarianCamp)
				return Intent.Disabled;

			var unitOwner = this.owner;

			// TODO: Perhaps this is not sufficient, but it is for now,
			// since otherwise we can move air units on land and sea
			if (this.IsAirUnit())
				return Intent.Disabled;

			if (unitOwner.isHuman && !unitOwner.HasExploredTile(tile))
				return Intent.MoveFreely;

			var hasOwnCity = HasOwnCity(tile, unitOwner);

			// Keep land units on land and sea units on water
			if (this.IsWaterUnit() && tile.IsLand()) {
				if (hasOwnCity) return Intent.MoveFreely;

				return Intent.Disabled;
			}

			if (this.CanBoardTransportOnTile(tile))
				return Intent.Load;

			if (this.CanUnloadToTile(tile))
				return Intent.Unload;

			if (this.IsLandUnit() && !tile.IsLand())
				return Intent.Disabled;

			// Nothing on the tile to notice or fight: everything below comes
			// down to moving freely.
			if (tile.unitsOnTile.Count == 0 && !tile.HasCity() && !tile.hasBarbarianCamp)
				return Intent.MoveFreely;

			var hasForeignCity = HasForeignCity(tile, unitOwner);
			var isHumanOwner = this.owner.isHuman;
			var isActiveTile = this.owner.tileKnowledge.isActiveTile(tile);

			if (isHumanOwner && !isActiveTile && hasForeignCity) {
				return Intent.Disabled;
			}

			if (isHumanOwner && !isActiveTile) {
				return Intent.MoveFreely;
			}

			var hasHostileUnits = HasHostileUnits(tile, unitOwner);
			var hasForeignUnits = HasForeignUnits(tile, unitOwner);
			var foreignOwner = ForeignOwner(tile);
			var hasHostileCity = HasHostileCity(tile, unitOwner);
			var cityOwner = tile.cityAtTile?.owner;
			var hasBarbCamp = tile.hasBarbarianCamp;
			var isCombatUnit = this.IsCombatUnit();
			var distanceToTile = this.location.DistanceTo(tile);

			if (isHumanOwner && (hasForeignCity || hasHostileCity || hasForeignUnits || hasHostileUnits) && distanceToTile > 1) {
				return Intent.Disabled;
			}

			if (!isCombatUnit) {
				if (hasForeignUnits || hasHostileUnits) {
					return Intent.NoticeUnit;
				}
				if (hasForeignCity || hasHostileCity || hasBarbCamp) {
					return Intent.NoticeCity;
				}
			}

			// TODO: add check for when we want to pillage something

			if (isCombatUnit) {
				if (hasHostileUnits || hasHostileCity) {
					// Only amphibious units can attack straight off a ship.
					if (this.IsLandUnit() && !this.location.IsLand() && !this.unitType.isAmphibious) {
						return Intent.Disabled;
					}
					return Intent.Fight;
				}
				if (hasForeignUnits) {
					if (distanceToTile == 1) {
						if (EngineStorage.gameData.AreInLockedPeace(unitOwner, foreignOwner)) {
							return Intent.NoticeAlliance;
						}
						return Intent.WarDeclaration;
					}

					if (isActiveTile) {
						return Intent.Disabled;
					}
				}
				if (hasForeignCity) {
					if (distanceToTile == 1) {
						if (EngineStorage.gameData.AreInLockedPeace(unitOwner, cityOwner)) {
							return Intent.NoticeAlliance;
						}
						return Intent.WarDeclaration;
					}
					return Intent.Disabled;
				}
			}

			return Intent.MoveFreely;
		}

		public bool CanEnterPeacefully(Tile tile) {
			return CanEnterPeacefully(tile, out _);
		}
		public bool CanEnterPeacefully(Tile tile, out Intent intent) {
			intent = this.ResolveIntent(tile);
			return intent == Intent.MoveFreely || intent == Intent.Load || intent == Intent.Unload;
		}

		public bool CanEnter(Tile tile) {
			return CanEnter(tile, out _);
		}
		public bool CanEnter(Tile tile, out Intent intent) {
			var canEnterPeacefully = CanEnterPeacefully(tile, out var it);
			intent = it;
			return canEnterPeacefully || it == Intent.Fight;
		}

		public bool CanEnterForcefully(Tile tile) {
			return CanEnterForcefully(tile, out _);
		}
		public bool CanEnterForcefully(Tile tile, out Intent intent) {
			var canEnter = CanEnter(tile, out var it);
			intent = it;
			return canEnter || it == Intent.WarDeclaration;
		}

		// Whether this unit can board a transport on the tile. Units board
		// ships just by moving onto them, but only join an army when ordered
		// to (explicitLoad), so walking onto an army's tile doesn't load them.
		private bool CanBoardTransportOnTile(Tile tile, bool explicitLoad = false) {
			if (!IsLoadable())
				return false;

			foreach (MapUnit transport in tile.unitsOnTile) {
				if (transport != this && transport.CanCarryUnits() && (explicitLoad || !transport.IsArmy()) && transport.CanLoad(this))
					return true;
			}

			return false;
		}

		private bool CanUnboardTransportToTile(Tile tile) {
			return IsLoadable() && IsLoaded() && tile.IsLand();
		}

		private MapUnit SelectTransportToBoard(Tile tile, bool explicitLoad = false) {
			// TODO: Let human player choose via UI which transport to load unit in

			var availableTransports = tile.unitsOnTile
				.Where(u => u != this && u.CanCarryUnits() && (explicitLoad || !u.IsArmy()))
				.Where(u => !u.IsFull());

			// Sort candidates by free capacity, but prefer transports that already have units
			availableTransports = availableTransports
				.OrderBy(t => !t.IsEmpty())
				.ThenByDescending(t => t.FreeCapacity());

			foreach (var transport in availableTransports) {
				if (transport.CanLoad(this))
					return transport;
			}

			return null;
		}

		private MapUnit FindTransportToUnboard(Tile tile, ID transport) {
			return tile.unitsOnTile.FirstOrDefault(t => t.id == transport);
		}

		private bool CanLoad(MapUnit mapUnit) {
			if (owner != mapUnit.owner)
				return false;

			if (!mapUnit.IsLoadable())
				return false;

			if (mapUnit == this)
				return false;

			var hasRoom = !IsFull();

			// TODO: type restrictions: only subs can carry nukes, carriers take aircraft, etc.
			// Armies only take land combat units; other transports take any land unit for now.
			var suitableUnit = IsArmy() ? mapUnit.unitType.CanJoinArmy() : mapUnit.IsLandUnit();
			return hasRoom && suitableUnit;
		}

		// TODO: Transport chaining
		// TODO: Amphibious assault

		private bool CanUnloadToTile(Tile tile) {
			if (!CanTransport())
				return false;

			var isValidLanding = tile.IsLand();
			return !IsEmpty() && isValidLanding;
		}

		public int FreeCapacity() {
			return Capacity() - PassengerCount();
		}

		private bool IsEmpty() => Capacity() > 0 && PassengerCount() == 0;
		private bool IsFull() => Capacity() > 0 && FreeCapacity() <= 0;

		private static bool HasHostileUnits(Tile tile, Player player) {
			foreach (MapUnit other in tile.unitsOnTile) {
				if (player != other.owner && AtWar(player, other.owner))
					return true;
			}
			return false;
		}
		private static bool HasForeignUnits(Tile tile, Player player) {
			foreach (MapUnit other in tile.unitsOnTile) {
				if (player != other.owner)
					return true;
			}
			return false;
		}

		private static Player ForeignOwner(Tile tile) {
			return tile.unitsOnTile.Count > 0 ? tile.unitsOnTile[0]?.owner : null;
		}

		private static bool HasHostileCity(Tile tile, Player player) {
			return tile.HasCity() && AtWar(player, tile.cityAtTile.owner);
		}
		private static bool HasForeignCity(Tile tile, Player player) {
			return tile.HasCity() && tile.cityAtTile.owner != player;
		}
		private static bool HasOwnCity(Tile tile, Player player) {
			return tile.HasCity() && tile.cityAtTile.owner == player;
		}

		private float SumWorkerProgress(Tile tile, Terraform workerJob) {
			float result = 0;
			foreach (MapUnit unit in tile.unitsOnTile) {
				if (unit.WorkerJob == workerJob) {
					result += unit.WorkerProgressTowardsJob;
				}
			}
			return result;
		}

		public int TurnsToCompleteTerraform(Terraform t) {
			// Figure out how much work remains to do on this particular job.
			int remainingTerraformCost = GetWorkerJobCost(location, t) - (int)this.SumWorkerProgress(location, t);

			// Figure out how fast all of the wokers doing this particular
			// terraform will work.
			float combinedWorkerSpeed = this.workerSpeed();
			foreach (MapUnit unit in location.unitsOnTile) {
				if (unit.id != this.id && unit.WorkerJob == t) {
					combinedWorkerSpeed += unit.workerSpeed();
				}
			}

			// Divide the two, rounding up.
			return (int)Math.Ceiling(remainingTerraformCost / combinedWorkerSpeed);
		}

		public bool canBuildCity() {
			if (!unitType.actions.Contains(UnitAction.BuildCity)) {
				return false;
			}
			if (location.HasCity() || !location.IsAllowCities()) {
				return false;
			}
			foreach (Tile tile in location.neighbors.Values) {
				if (tile.HasCity())
					return false;
			}
			return true;
		}

		public bool CanPerformTerraformAction(Terraform terraform) {
			return CanPerformTerraformAction(terraform, location);
		}

		public bool CanPerformTerraformAction(Terraform terraform, Tile tile) {
			// The cheap checks go first: the requirements can run Lua and
			// look up trade access.
			return unitType.terraformActions.Contains(terraform)
				&& !tile.HasCity()
				&& terraform.MeetsRequirements(owner, tile);
		}

		public float workerSpeed() {
			float progressPerTurn = this.IsCaptive() ? JOB_PROGRESS_SLAVE : JOB_PROGRESS_WORKER;
			if (owner.civilization.traits.Contains(Civilization.Trait.Industrious)) {
				progressPerTurn *= 1.5f;
			}
			return progressPerTurn;
		}

		public void resetWorkerJob() {
			WorkerJob = null;
			WorkerProgressTowardsJob = 0;
			animate(AnimatedAction.BLANK, AnimationEnding.Repeat);
		}

		public bool canAutomate() {
			return unitType.actions.Contains(UnitAction.Automate);
		}

		public bool canExplore() {
			return unitType.actions.Contains(UnitAction.Explore);
		}

		public List<Terraform> GetAvailableTerraforms() {
			return EngineStorage.gameData.Terraforms.Where(CanPerformTerraformAction).ToList();
		}

		/**
		 * Helper function to get the available actions for a unit
		 * based on what terrain it is on.
		 **/
		public List<UnitAction> GetAvailableActions() {
			List<UnitAction> result = new();

			// A unit in a rigid army takes no orders of its own.
			if (IsLockedInArmy())
				return result;

			// Eventually, we should look this up somewhere to see what all actions we have (and mods might add more)
			// For now, this is still an improvement over the last iteration.
			UnitAction[] implementedActions = { UnitAction.Hold, UnitAction.Wait, UnitAction.Fortify, UnitAction.Disband, UnitAction.Goto, UnitAction.Bombard, UnitAction.Sentry };
			foreach (UnitAction action in implementedActions) {
				if (unitType.actions.Contains(action)) {
					result.Add(action);
				}
			}

			if (unitType.actions.Contains(UnitAction.Sentry)) {
				result.Add(UnitAction.SentryEnemyOnly);
			}
			if (canBuildCity()) {
				result.Add(UnitAction.BuildCity);
			}
			if (canExplore()) {
				result.Add(UnitAction.Explore);
			}
			if (canAutomate()) {
				result.Add(UnitAction.Automate);
			}

			if (CanBoardTransportOnTile(this.location, explicitLoad: true) && this.loadedOnUnitId == null) {
				result.Add(UnitAction.Load);
			}
			// Ships unload in port; an army that allows unloading can do so anywhere.
			if (CanUnloadToTile(this.location) && (this.location.HasCity() || IsArmy())) {
				result.Add(UnitAction.Unload);
			}
			if (GetAvailableUpgrade() != null) {
				result.Add(UnitAction.Upgrade);
			}
			if (CanPillage()) {
				result.Add(UnitAction.Pillage);
			}

			// Eventually we will have advanced actions too, whose availability will rely on their base actions' availability.
			// unit.availableActions.Add("rename");

			return result;
		}
	}
}
