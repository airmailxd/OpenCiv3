using System.Collections.Generic;
using System.Threading.Tasks;
using C7Engine;
using C7GameData.Save;
using static C7GameData.PlayerRelationship;

namespace C7GameData {
	// Aircraft and their missions. "Air units are not moved around the map
	// like other units. Instead, they are stationed in a city, and assigned
	// missions which they execute immediately against targets within their
	// operational range" (Conquests Civilopedia, GCON_Air_Missions). They
	// "may be based in any city or aircraft carrier on the map" (PRTO_Fighter,
	// PRTO_Bomber), and "Carriers act as floating airfields" (PRTO_Carrier).
	// Which missions an aircraft flies are the BIQ's PRTO air missions
	// (UnitAction.Bombing and the rest).
	//
	// UNVERIFIED (no Civ3 source found): a mission takes the aircraft's whole
	// turn. Every Conquests aircraft has a single move, so this is the same
	// as each mission costing one move, as a bombardment does.
	public partial class MapUnit {
		// Re-basing reaches "a distance equal to up to Six times the plane's
		// operational range" (GCON_Air_Missions).
		public const int RebaseRangeMultiplier = 6;

		// Whether this fighter is flying air superiority: until its owner's
		// next turn it may intercept enemy bombing runs (see
		// TryToIntercept). Reset at the start of each of its owner's turns.
		public bool isOnAirSuperiority;

		// Whether this fighter has intercepted a bombing run since its
		// owner's turn began: "Each fighter can make one interception per
		// turn" (T-hawk, https://forums.civfanatics.com/threads/aerial-combat-mechanics.90783/).
		public bool hasInterceptedThisTurn;

		public int OperationalRange() => unitType.operationalRange;

		// The tiles around a fighter on air superiority where it intercepts:
		// "The plane attempts to intercept enemy aircraft that attack targets
		// within the inner half of its operational range" (GCON_Air_Missions).
		public int InterceptionRadius() => unitType.operationalRange / 2;

		public int RebaseRange() => unitType.operationalRange * RebaseRangeMultiplier;

		// Whether the unit type flies the mission, i.e. has the BIQ air
		// mission. Precision strikes also need a tech with the BIQ's "enables
		// precision bombing" flag (Smart Weapons in Conquests: "Stealth units
		// and F-15s may conduct Precision Strikes", TECH_Smart_Weapons).
		public bool HasAirMission(UnitAction mission) {
			if (!IsAirUnit() || !unitType.actions.Contains(mission)) {
				return false;
			}
			return mission != UnitAction.PrecisionBombing || owner.HasTechEnabling(SaveTech.Flag.EnablesPrecisionBombing);
		}

		// Whether the tile is an air base for the player's aircraft: one of
		// the player's cities, or an airfield (see HasAirfieldFor). Carriers
		// are bases too, but they're units, not tiles (see IsAtAirBase).
		public static bool IsAirBaseTile(Tile tile, Player player) {
			if (!Tile.IsTileValid(tile)) {
				return false;
			}
			return (tile.HasCity() && tile.cityAtTile.owner == player) || HasAirfieldFor(tile, player);
		}

		// Whether the tile has an airfield the player may fly from:
		// "Airfields function in the same manner that a city with an airport
		// would for air units ... All air missions can be launched from an
		// airfield" (GCON_Airfields). C7 doesn't have airfields yet; this is
		// where they come in.
		public static bool HasAirfieldFor(Tile tile, Player player) {
			return false;
		}

		// Whether the aircraft is on a base it can fly missions from: one of
		// its owner's cities or airfields, or a carrier it is loaded on.
		public bool IsAtAirBase() {
			if (IsLoaded()) {
				MapUnit carrier = Carrier();
				return carrier != null && carrier.unitType.canCarryAircraft;
			}
			return IsAirBaseTile(location, owner);
		}

		// Whether the aircraft can fly the mission now, wherever it goes.
		public bool CanFlyMission(UnitAction mission) {
			return HasAirMission(mission) && movementPoints.canMove && IsAtAirBase() && !IsLockedInArmy();
		}

		// Bombing: "The plane flies to the target and attacks, sometimes
		// affecting city improvements, and sometimes population"
		// (GCON_Air_Missions). It is a bombardment (see MapUnit_Bombard.cs)
		// that reaches the aircraft's operational range.
		public bool CanAirBombTile(Tile tile, out BombardTarget bombardTarget) {
			bombardTarget = BombardTarget.None;
			if (!CanFlyMission(UnitAction.Bombing) || !Tile.IsTileValid(tile)) {
				return false;
			}
			return CanBombardTileWithin(tile, OperationalRange(), out bombardTarget);
		}

		public bool CanAirBombTile(Tile tile) => CanAirBombTile(tile, out _);

		// Precision strikes "destroy city improvements only, unless there
		// are none in which case population is affected"
		// (GCON_Air_Missions). UNVERIFIED: they can't be flown at anything
		// but a city here.
		public bool CanPrecisionBombTile(Tile tile, out BombardTarget bombardTarget) {
			bombardTarget = BombardTarget.None;
			if (!CanFlyMission(UnitAction.PrecisionBombing) || !Tile.IsTileValid(tile) || !tile.HasCity()) {
				return false;
			}
			if (location.DistanceTo(tile) > OperationalRange() || !CanAttackAgainThisTurn() || unitType.bombard == 0) {
				return false;
			}
			City city = tile.cityAtTile;
			if (city.owner == owner || EngineStorage.gameData.AreInLockedPeace(owner, city.owner)) {
				return false;
			}
			bombardTarget = BombardTarget.City;
			return true;
		}

		public bool CanPrecisionBombTile(Tile tile) => CanPrecisionBombTile(tile, out _);

		// Recon: "The plane flies to the target and reveals the map"
		// (GCON_Air_Missions). Any tile within range will do.
		public bool CanReconTile(Tile tile) {
			return CanFlyMission(UnitAction.Recon) && Tile.IsTileValid(tile) && tile != location
				&& location.DistanceTo(tile) <= OperationalRange();
		}

		// Re-basing: "The unit can be moved to another city or a carrier"
		// within range (GCON_Air_Missions).
		public bool CanRebaseTo(Tile tile) {
			if (!CanFlyMission(UnitAction.Rebase) || !Tile.IsTileValid(tile) || tile == location) {
				return false;
			}
			if (location.DistanceTo(tile) > RebaseRange()) {
				return false;
			}
			return IsAirBaseTile(tile, owner) || FindCarrierWithRoom(tile) != null;
		}

		// One of the owner's carriers on the tile with room for this
		// aircraft, or null.
		private MapUnit FindCarrierWithRoom(Tile tile) {
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u != this && u.owner == owner && u.unitType.canCarryAircraft && u.CanCarryUnits() && u.CanLoad(this)) {
					return u;
				}
			}
			return null;
		}

		// Air superiority: "The plane attempts to intercept enemy aircraft
		// that attack targets within the inner half of its operational
		// range" (GCON_Air_Missions). Only aircraft with the BIQ's intercept
		// mission fly it: stealth aircraft "can execute all air missions,
		// except air superiority" (PRTO_Stealth_Fighter).
		public bool CanFlyAirSuperiority() {
			return CanFlyMission(UnitAction.Intercept);
		}

		public void FlyAirSuperiority() {
			if (!CanFlyAirSuperiority()) {
				return;
			}
			isOnAirSuperiority = true;
			EndMission();
		}

		// Flies a bombing run at the tile. Returns whether the aircraft got
		// to bomb it, i.e. wasn't intercepted on the way.
		public async Task<bool> AirBomb(Tile tile) {
			if (!CanAirBombTile(tile)) {
				return false;
			}
			hasAttackedThisTurn = true;
			if (Intercepted(tile)) {
				return false;
			}
			await Bombard(tile);
			EndMission();
			return true;
		}

		public async Task<bool> PrecisionBomb(Tile tile) {
			if (!CanPrecisionBombTile(tile)) {
				return false;
			}
			hasAttackedThisTurn = true;
			if (Intercepted(tile)) {
				return false;
			}
			facingDirection = location.DirectionTo(tile);
			await BombardCity(tile, precision: true);
			EndMission();
			return true;
		}

		// Whether the run at the tile is intercepted. If so the mission is
		// over, and the aircraft may have been shot down.
		private bool Intercepted(Tile tile) {
			if (!TryToIntercept(tile, out bool shotDown)) {
				return false;
			}
			if (!shotDown) {
				EndMission();
			}
			return true;
		}

		// Shows the owner the tile and those around it, as they are now,
		// until the owner's next turn; they stay known afterwards.
		// UNVERIFIED (no Civ3 source found): how far around the target a
		// recon flight sees.
		public void Recon(Tile tile) {
			if (!CanReconTile(tile)) {
				return;
			}
			List<Tile> seen = [tile];
			foreach (Tile t in tile.neighbors.Values) {
				if (Tile.IsTileValid(t)) {
					seen.Add(t);
				}
			}
			foreach (Tile t in seen) {
				owner.tileKnowledge.AddTileToKnown(t);
			}
			owner.tileKnowledge.RecomputeActiveTiles();
			owner.tileKnowledge.Peek(seen);
			EndMission();
		}

		public void Rebase(Tile tile) {
			if (!CanRebaseTo(tile)) {
				return;
			}
			isOnAirSuperiority = false;
			// Landing in one of the owner's cities takes the aircraft off its
			// carrier, and landing on a carrier puts it aboard (see
			// RelocateTo).
			MoveStraightTo(tile);
			if (!IsLoaded() && !IsAirBaseTile(tile, owner)) {
				MapUnit carrier = FindCarrierWithRoom(tile);
				if (carrier != null) {
					BoardTransport(carrier);
				}
			}
			EndMission();
		}

		// Airdrops: "When a paratrooper is active in a city with an airport
		// it may perform an airdrop. Press [A] then click a target square
		// within range to launch the airdrop" (PRTO_Paratrooper). The range
		// is the unit's operational range. UNVERIFIED (no Civ3 source
		// found): the target must be land the paratrooper could walk onto
		// with no other civ's units, city or a barbarian camp on it, and the
		// drop takes the unit's turn. Only land units drop here; the
		// Helicopter, which has the BIQ's airdrop too, doesn't carry troops
		// yet.
		public bool CanAirdropFrom() {
			if (!IsLandUnit() || !unitType.actions.Contains(UnitAction.Airdrop) || unitType.operationalRange <= 0) {
				return false;
			}
			if (!movementPoints.canMove || IsLoaded() || !Tile.IsTileValid(location) || !location.HasCity()) {
				return false;
			}
			City city = location.cityAtTile;
			return city.owner == owner && city.Airport() != null;
		}

		public bool CanAirdropTo(Tile tile) {
			if (!CanAirdropFrom() || !Tile.IsTileValid(tile) || tile == location) {
				return false;
			}
			if (location.DistanceTo(tile) > OperationalRange() || !tile.IsLand() || tile.IsImpassable() || tile.hasBarbarianCamp) {
				return false;
			}
			if (!unitType.CanEnterTerrainOf(tile) || (tile.HasCity() && tile.cityAtTile.owner != owner)) {
				return false;
			}
			foreach (MapUnit u in tile.unitsOnTile) {
				if (u.owner != owner) {
					return false;
				}
			}
			return true;
		}

		public void Airdrop(Tile tile) {
			if (!CanAirdropTo(tile)) {
				return;
			}
			MoveStraightTo(tile);
			EndMission();
		}

		// Whether the unit can fly the mission (or make the airdrop) at the
		// tile now; bombardTarget is what a bombing run would hit there.
		public bool CanFlyMissionAt(UnitAction mission, Tile tile, out BombardTarget bombardTarget) {
			bombardTarget = BombardTarget.None;
			return mission switch {
				UnitAction.Bombing => CanAirBombTile(tile, out bombardTarget),
				UnitAction.PrecisionBombing => CanPrecisionBombTile(tile, out bombardTarget),
				UnitAction.Recon => CanReconTile(tile),
				UnitAction.Rebase => CanRebaseTo(tile),
				UnitAction.Airdrop => CanAirdropTo(tile),
				_ => false,
			};
		}

		public bool CanFlyMissionAt(UnitAction mission, Tile tile) => CanFlyMissionAt(mission, tile, out _);

		// How far the mission reaches, in tiles.
		public int MissionRange(UnitAction mission) {
			return mission == UnitAction.Rebase ? RebaseRange() : OperationalRange();
		}

		private static readonly UnitAction[] PeacefulMissions = [UnitAction.Recon, UnitAction.Intercept, UnitAction.Rebase];

		// The air missions, and the airdrop, the unit can be ordered to fly
		// now (see GetAvailableActions).
		private void AddAvailableAirMissions(List<UnitAction> actions) {
			if (CanAirdropFrom()) {
				actions.Add(UnitAction.Airdrop);
			}
			if (!IsAirUnit() || !movementPoints.canMove || !IsAtAirBase() || IsLockedInArmy()) {
				return;
			}
			bool canAttack = CanAttackAgainThisTurn();
			if (HasAirMission(UnitAction.Bombing) && canAttack && unitType.bombard > 0) {
				actions.Add(UnitAction.Bombing);
			}
			if (HasAirMission(UnitAction.PrecisionBombing) && canAttack && unitType.bombard > 0) {
				actions.Add(UnitAction.PrecisionBombing);
			}
			foreach (UnitAction mission in PeacefulMissions) {
				if (HasAirMission(mission)) {
					actions.Add(mission);
				}
			}
		}

		private void EndMission() {
			Wake();
			path = null;
			movementPoints.onConsumeAll();
			if (owner.isHuman) {
				new MsgUnitMoved(this).send();
			}
		}

		// Called at the start of the owner's turn.
		private void ResetAirMissions() {
			isOnAirSuperiority = false;
			hasInterceptedThisTurn = false;
		}

		// Whether the bombing run at the target is intercepted, by an enemy
		// fighter flying air superiority or a SAM battery in the target
		// city (see TryToBeShotDown). "Only bombing and precision bombing
		// can be intercepted. ... If an aircraft is intercepted it will
		// always fail to bomb the target even if it wins the air battle"
		// (Ozymandias,
		// https://forums.civfanatics.com/threads/air-units-bombing-mechanics-in-the-open.678152/).
		// Each fighter in reach gets "a 50% chance to attempt to intercept"
		// (the rules' chance to intercept air missions, or stealth missions
		// for stealth aircraft), in turn, until one does. "The chance of
		// shooting down the air unit is A/(A+D), as with all other forms of
		// combat in Civ3" (alexman), with "the fighter's attack stat" against
		// the bomber's defence (AnthonyBoscia,
		// https://forums.civfanatics.com/threads/questions-on-air-units.680192/).
		// UNVERIFIED: the bomber can't shoot the fighter down here, though
		// "attacker downs interceptor" is said to be possible (BillChin,
		// https://forums.civfanatics.com/threads/air-superiority.15449/).
		internal bool TryToIntercept(Tile target, out bool shotDown) {
			shotDown = false;
			GameData gameData = EngineStorage.gameData;
			Rules rules = gameData.rules;
			int chance = unitType.isStealth ? rules.ChanceToInterceptStealthMissions : rules.ChanceToInterceptAirMissions;
			foreach (Player player in gameData.players) {
				if (player == owner || !AtWar(owner, player)) {
					continue;
				}
				foreach (MapUnit fighter in player.units) {
					if (!fighter.CanIntercept(target)) {
						continue;
					}
					if (GameData.rng.Next(100) >= chance) {
						continue;
					}
					fighter.hasInterceptedThisTurn = true;
					shotDown = TryToBeShotDown(fighter.unitType.attack, fighter, target);
					return true;
				}
			}
			return false;
		}

		// Whether this fighter, flying air superiority, can intercept a
		// bombing run at the target.
		private bool CanIntercept(Tile target) {
			return isOnAirSuperiority && !hasInterceptedThisTurn && hitPointsRemaining > 0
				&& unitType.actions.Contains(UnitAction.Intercept) && IsAtAirBase()
				&& Tile.IsTileValid(location) && location.DistanceTo(target) <= InterceptionRadius();
		}

		// The interceptor shoots at this aircraft, with an attack strength
		// against its defence: A/(A+D) to shoot it down.
		private bool TryToBeShotDown(int attack, MapUnit interceptor, Tile target) {
			int defence = unitType.defense;
			bool shotDown = attack > 0 && GameData.rng.NextDouble() < (double)attack / (attack + defence);
			string what = interceptor != null ? $"{interceptor.owner.civilization.adjective} {interceptor.unitType.name}" : "anti-aircraft fire";
			if (owner.isHuman) {
				new MsgShowTemporaryPopup(shotDown ? $"Our {unitType.name} was shot down by {what}!" : $"Our {unitType.name} was intercepted by {what}.", target, owner).send();
			}
			Player defender = interceptor?.owner ?? target.cityAtTile?.owner;
			if (defender != null && defender.isHuman) {
				new MsgShowTemporaryPopup(shotDown ? $"We shot down an enemy {unitType.name}!" : $"We intercepted an enemy {unitType.name}.", target, defender).send();
			}
			if (shotDown) {
				interceptor?.RollToPromote(this);
				RemoveFromPlay();
			}
			return shotDown;
		}
	}
}
