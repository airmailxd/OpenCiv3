using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;

namespace C7GameData {
	public partial class MapUnit {
		// Shared by all units: it is only ever read.
		private static readonly AnimatedEffect[] hitList = [AnimatedEffect.Hit, AnimatedEffect.Hit2, AnimatedEffect.Hit3, AnimatedEffect.Hit5];

		public enum BombardTarget {
			None,
			City,
			Unit,
			Improvement
		}

		public bool HasBombardAbility() {
			return this.unitType.actions.Contains(UnitAction.Bombard);
		}

		// Whether this unit's bombardment can kill the target, rather than
		// only bring it down to its last hit point. Per the project owner,
		// the unit type's lethal land bombardment kills land units and its
		// lethal sea bombardment kills ships: "Lethal bombardment can
		// bombard units that are red-lined and therefore kill them"
		// (https://forums.civfanatics.com/threads/what-is-lethal-bombardment.129927/).
		// Aircraft at their base are killed by any bombardment: "Air units
		// can be destroyed by bombardment." / "There is no such attribute
		// as "lethal air bombardment"." (justanick, C3C 1.22,
		// https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/, #1).
		public bool IsBombardmentLethalAgainst(MapUnit target) {
			if (target.IsAirUnit()) {
				return true;
			}
			return target.IsWaterUnit() ? unitType.isSeaBombardmentLethal : unitType.isLandBombardmentLethal;
		}

		public bool CanBombardTile(Tile tile) {
			return CanBombardTile(tile, out _);
		}

		/// <summary>
		/// Whether this unit can bombard a tile, regardless of status, except locked alliance, because nothing can break it.<br/>
		/// Locked alliances are more protective of tiles/units etc, than even our own;<br/>
		/// While we can pillage our own tiles, we can't pillage a tile belonging to a locked ally.
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="bombardTarget"></param>
		/// <returns></returns>
		public bool CanBombardTile(Tile tile, out BombardTarget bombardTarget) {
			bombardTarget = BombardTarget.None;

			// Nuclear weapons are fired with the bombard order.
			if (IsNuclearWeapon())
				return CanNukeTile(tile, out bombardTarget);

			return CanBombardTileWithin(tile, this.unitType.bombardRange, out bombardTarget);
		}

		// CanBombardTile for a bombardment that reaches `range` tiles: the
		// unit's bombard range, or an aircraft's operational range for a
		// bombing mission (see MapUnit_Air.cs).
		private bool CanBombardTileWithin(Tile tile, int range, out BombardTarget bombardTarget) {
			bombardTarget = BombardTarget.None;

			if (this.location.DistanceTo(tile) > range)
				return false;

			if (this.unitType.bombard == 0)
				return false;

			if (!CanAttackAgainThisTurn())
				return false;

			if (tile.HasCity() && tile.cityAtTile.owner == this.owner)
				return false;

			if (tile.HasCity() && EngineStorage.gameData.AreInLockedPeace(this.owner, tile.cityAtTile.owner))
				return false;

			if (tile.HasCity())
				bombardTarget = BombardTarget.City;

			MapUnit target = tile.FindTopDefenderForBombard(this);

			// TODO: Consider colony on neutral tile (allies && potential enemies)

			if (tile.overlays.HasBeenImproved()) {
				if (target == NONE) {
					if (tile.OwningPlayer() != null && EngineStorage.gameData.AreInLockedPeace(this.owner, tile.OwningPlayer()))
						return false;
				}

				if (target != NONE) {
					bombardTarget = BombardTarget.Unit;
					if (EngineStorage.gameData.AreInLockedPeace(this.owner, target.owner))
						return false;

					if (target.IsCombatUnit() && target.owner != this.owner) {
						return true;
					}
				}

				if (tile.OwningPlayer() != null && EngineStorage.gameData.AreInLockedPeace(this.owner, tile.OwningPlayer()))
					return false;

				bombardTarget = BombardTarget.Improvement;
			} else {
				if (target != NONE) {
					if (!target.IsCombatUnit())
						return false;
					if (target.IsCombatUnit() && target.owner == this.owner)
						return false;
					if (EngineStorage.gameData.AreInLockedPeace(this.owner, target.owner))
						return false;

					bombardTarget = BombardTarget.Unit;
				}
			}

			if (bombardTarget == BombardTarget.None)
				return false;

			return true;
		}

		public async Task Bombard(Tile tile) {
			// Could check canBombardTile(..) again, but no need really

			if (IsNuclearWeapon()) {
				await NuclearStrike(tile);
				return;
			}

			MapUnit target = tile.FindTopDefenderForBombard(this);

			var hasTargetUnit = target != NONE && target.owner != owner;
			var hasForeignCity = tile.HasCity() && tile.cityAtTile.owner != owner;
			// Only land bombarders hit the walls first; ships and planes hit
			// them like any other building (see BombardCity), per the project
			// owner. Only walls built in the city can be knocked down: walls
			// a wonder provides (e.g. the Great Wall) are indestructible, so
			// with only those the bombardment goes on to the units or the
			// city.
			CityBuilding destructibleWalls = hasForeignCity && IsLandUnit() ? FindDestructibleWalls(tile.cityAtTile) : null;
			var hasCityWalls = destructibleWalls != null;
			var hasTileImprovements = tile.HasImprovements;

			if (!(hasTargetUnit || hasTileImprovements || hasForeignCity))
				return; // Nothing to bombard

			facingDirection = location.DirectionTo(tile);
			hasAttackedThisTurn = true;

			// After the walls (see BombardCityWalls), units are hit before
			// the city itself: "starting in Conquests
			// the bombardment units always hit units first in cities, never
			// improvements or population"
			// (https://forums.civfanatics.com/threads/citizen-and-buildings-defense-bonus.693504/).
			// Per the project owner, only lethal bombardment kills a unit;
			// otherwise the units are hit until all are down to their last
			// hit point (see Tile.FindTopDefenderForBombard).
			if (hasCityWalls)
				await BombardCityWalls(tile, destructibleWalls);
			else if (hasTargetUnit)
				await BombardUnits(tile, target);
			else if (hasForeignCity)
				await BombardCity(tile);
			else
				await BombardTileImprovements(tile);

			// A cruise missile is used up: "[Missile attacks] are like
			// bombardment, but the unit never returns to its base and the
			// attacks can destroy enemy units" (Conquests Civilopedia,
			// GCON_Combat). Which units are missiles is the BIQ's PRTO "Cruise
			// Missile" flag (the Cruise Missile in conquests.biq).
			if (unitType.isCruiseMissile && hitPointsRemaining > 0) {
				RemoveFromPlay();
			}
		}

		// The walls a land bombardment has to break first, or null. Only a
		// town's walls shield it: "If the walls win, then all improvements,
		// citizens, and units in the city are safe." / "This happens only if
		// the city is size 6 or smaller." (alexman,
		// https://codehappy.net/apolyton/threads/84569-1.htm, #1); in C3C 1.22,
		// "If there is are no city walls or the settlement has already reached
		// city level, then land units are hit." (justanick,
		// https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/, #1).
		// In a bigger city the walls are an ordinary building (see
		// BombardCity).
		private static CityBuilding FindDestructibleWalls(City city) {
			if (city.residents.Count > EngineStorage.gameData.rules.MaximumLevel1CitySize) {
				return null;
			}
			foreach (CityBuilding cb in city.constructed_buildings) {
				if (cb.building.providesWalls && IsBombardableBuilding(city, cb)) {
					return cb;
				}
			}
			return null;
		}

		// Whether bombardment can destroy the building: anything built in the
		// city except small wonders and the palace, and great wonders once
		// the owner is industrial (below). Per the
		// project owner, a building a wonder provides can't be bombarded
		// either: those granted by the wonder aren't stored in the city, and
		// one also built there is left alone, so the wonder's effect stays.
		//
		// A great wonder can be bombarded away too, until its owner reaches
		// the Industrial Age: "If the bombardement occurs against a settlement
		// of a civilization that has reached the industrial age," / "then any
		// world wonder in this settlement is immune against any bombardment."
		// / "Before the target civilization has reached the industrial age
		// this is not the case" (justanick, C3C 1.22,
		// https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/, #1).
		// Small wonders and the palace stay out of reach.
		private static bool IsBombardableBuilding(City city, CityBuilding cb) {
			if (city.IsProvidedByWonders(cb.building)) {
				return false;
			}
			if (cb.building.IsGreatWonder()) {
				return city.owner == null || city.owner.EraIndex() < EraUtils.GetEraIndex(EraUtils.INDUSTRIAL_AGE_CVLPD);
			}
			return IsBombardableBuilding(cb);
		}

		// Whether the building is a Civil Defense: one that adds to its
		// units' defense at any size, without being walls. When bombardment
		// hits an ordinary building, it hits the Civil Defense instead: "If a
		// regular building(meaning neither a coastal fortress nor city walls)
		// is hit and a civil defence is in the settlement," / "then it is the
		// civil defence that is hit." (justanick, C3C 1.22,
		// https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/, #1).
		private static bool IsCivilDefense(Building b) {
			return b.combatDefenseBonus != null && !b.providesWalls && !b.onlyUsefulInTowns
				&& b.bombardDefense == 0 && !b.isSmallWonder && !b.IsGreatWonder() && !b.isCenterOfEmpire;
		}

		// Whether the building is walls or a coastal fortress, the two that
		// bombardment hits for themselves rather than through a Civil
		// Defense (see IsCivilDefense).
		private static bool IsWallsOrCoastalFortress(Building b) {
			return b.providesWalls || b.bombardDefense > 0 || b.navalPower > 0 || b.navalDefenseBonus != null;
		}

		// The building a bombardment hit on the chosen building destroys: the
		// city's Civil Defense, if it has one that can be hit and the chosen
		// one is an ordinary building, else the chosen one.
		private static CityBuilding BuildingHitBy(CityBuilding chosen, List<CityBuilding> hittable) {
			if (IsWallsOrCoastalFortress(chosen.building)) {
				return chosen;
			}
			foreach (CityBuilding cb in hittable) {
				if (IsCivilDefense(cb.building)) {
					return cb;
				}
			}
			return chosen;
		}

		// Whether the building can be destroyed at all, by bombardment or a
		// nuclear strike: anything but wonders and the palace.
		private static bool IsBombardableBuilding(CityBuilding cb) {
			Building b = cb.building;
			return !b.isCenterOfEmpire && !b.isSmallWonder && !b.IsGreatWonder();
		}

		private async Task BombardCityWalls(Tile tile, CityBuilding walls) {
			// Walls are hit first, defending with the BIQ's bombard defense
			// (8 for city walls in the standard rules, see
			// Building.bombardDefense): "When you use a ground unit to
			// bombard a city with walls, you have to first destroy the walls
			// (by damaging them as if they were a unit with a defensive
			// strength of 8) before you can harm anything else"
			// (alexman, https://codehappy.net/apolyton/threads/84569-1.htm).
			// Only land units: he adds that for ships and planes "walls are
			// just as likely to be destroyed as any other improvement", which
			// is how they hit them here, per the project owner (see Bombard).
			int wallDefence = walls.building.bombardDefense > 0 ? walls.building.bombardDefense : Building.DefaultWallBombardDefense;

			var hitCount = 0;

			double bombardStrength  = StrengthVersus(null, CombatRole.Bombard, facingDirection);
			double defenderStrength = wallDefence;
			double attackerOdds = bombardStrength / (bombardStrength + defenderStrength);
			if (Double.IsNaN(attackerOdds))
				return;

			// Each shot of the unit's rate of fire is rolled on its own (see
			// RateOfFire). Once the walls are down there is nothing left for
			// a shot at them to hit, so the bombardment ends there.
			await BeginBombard();
			for (int shot = 0; shot < RateOfFire() && tile.cityAtTile.constructed_buildings.Contains(walls); ++shot) {
				if (await FireShot(tile, attackerOdds)) {
					hitCount += 1;
					tile.cityAtTile.RemoveBuilding(walls);
				}
			}

			TriggerPopUp(hitCount, tile, $"The Walls of {tile.cityAtTile.name} have been destroyed.");
		}

		private async Task BombardUnits(Tile tile, MapUnit target) {
			// TODO: Make configurable

			movementPoints.onUnitMove(1);
			await animateAsync(AnimatedAction.ATTACK1);

			// TODO: Figure out the bombard defense that walls grant.
			double bombardStrength  = StrengthVersus(target, CombatRole.Bombard, facingDirection);
			double defenderStrength = target.StrengthVersus(this, CombatRole.BombardDefense, facingDirection);
			double attackerOdds = bombardStrength / (bombardStrength + defenderStrength);
			if (Double.IsNaN(attackerOdds))
				return;

			var tries = 0;
			var hitCount = 0;

			// Per the project owner, bombardment that isn't lethal against
			// the target can only bring it down to its last hit point;
			// lethal bombardment stops once the target is dead.
			bool lethal = IsBombardmentLethalAgainst(target);
			while (tries < unitType.rateOfFire) {
				tries++;
				if (target.CompositeHitPoints() - hitCount <= (lethal ? 0 : 1))
					break;

				var r = GameData.rng.NextDouble();
				if (r < attackerOdds) {
					hitCount++;
				}
			}

			bool targetDestroyed = false;
			bool wasAboveOneHitPoint = target.CompositeHitPoints() > 1;
			if (hitCount > 0) {
				for (int i = 0; i < hitCount && !targetDestroyed; ++i) {
					targetDestroyed = target.AbsorbBombardHit(lethal);
					await tile.AnimateAsync(hitList[GameData.rng.Next(0, hitList.Length)]);
				}

			} else
				await tile.AnimateAsync(tile.IsWater() ? AnimatedEffect.WaterMiss : AnimatedEffect.Miss);

			if (targetDestroyed) {
				RollToPromote(target);
				await target.animateAsync(AnimatedAction.DEATH, AnimationEnding.Pause);
				target.RemoveFromPlay();
				// Target destroyed, skip remaining fire -- TODO: Re-target?
			} else if (wasAboveOneHitPoint && target.CompositeHitPoints() == 1) {
				target.owner.AddWarWeariness(owner, Player.WarWearinessForUnitBombardedToOneHitPoint);
			}

			TriggerPopUp(hitCount, tile, "Artillery bombardment successful! Enemy units injured.");
		}

		// Once a city has no walls or defenders left to hit, its buildings
		// and citizens are hit with the same probability: "a 25% chance of
		// targetting population, and a 25% chance of targetting
		// improvements" (alexman, https://codehappy.net/apolyton/threads/84569-1.htm),
		// a 50/50 roll per the project owner. They defend with the rules'
		// building and citizen defensive bonuses (see
		// Rules.BuildingDefensiveBonus).
		private const float BuildingOrPopulationOdds = 0.5f;

		// A precision strike (see MapUnit_Air.cs) hits only the city's
		// buildings, or its citizens if it has none to hit.
		private async Task BombardCity(Tile tile, bool precision = false) {
			City city = tile.cityAtTile;
			Rules rules = EngineStorage.gameData.rules;
			double bombardStrength = StrengthVersus(null, CombatRole.Bombard, facingDirection);
			var hitCount = 0;
			List<string> destroyMsgs = new();

			// Each shot of the unit's rate of fire (see RateOfFire) picks its
			// own target, as a single shot would: the 50/50 roll between
			// buildings and citizens is made again, among what is still left
			// to hit. A precision strike stays a single shot, as before rate
			// of fire applied here: UNVERIFIED (no Civ3 source found) whether
			// it fires the aircraft's rate of fire, and if so whether later
			// shots go on to the citizens once the buildings are gone.
			int shots = precision ? 1 : RateOfFire();
			await BeginBombard();
			for (int shot = 0; shot < shots; ++shot) {
				// Only buildings actually built in the city can be destroyed,
				// and not wonders, the palace or what a wonder provides (see
				// IsBombardableBuilding). Walls are among them when a ship or
				// plane bombards.
				// TODO: probably not canon to exclude palace
				List<CityBuilding> eligibleBuildingsForBombardment = city.constructed_buildings.Where(cb => IsBombardableBuilding(city, cb)).ToList();

				// Per the project owner, bombardment never kills a city's
				// last citizen. What can't be hit isn't rolled for, so a size
				// 1 city only loses buildings, and a city with neither
				// buildings to lose nor citizens to spare takes the shot for
				// nothing. Later shots with nothing left to hit aren't fired.
				bool canHitBuildings = eligibleBuildingsForBombardment.Count > 0;
				bool canHitCitizens = city.residents.Count > 1;
				if (!canHitBuildings && !canHitCitizens) {
					if (shot == 0) {
						await FireShot(tile, 0);
					}
					break;
				}
				bool targetBuildings = canHitBuildings && canHitCitizens && !precision
					? GameData.rng.NextDouble() < BuildingOrPopulationOdds
					: canHitBuildings;

				double defenderStrength = targetBuildings ? rules.BuildingDefensiveBonus : rules.CitizenDefensiveBonus;
				double attackerOdds = bombardStrength / (bombardStrength + defenderStrength);
				if (Double.IsNaN(attackerOdds))
					break;

				if (!await FireShot(tile, attackerOdds))
					continue;
				hitCount += 1;
				if (targetBuildings) {
					var building = BuildingHitBy(eligibleBuildingsForBombardment
						.OrderBy(x => GameData.rng.Next()).First(), eligibleBuildingsForBombardment);
					city.RemoveBuilding(building);
					destroyMsgs.Add($"The {building.building.name} of {city.name} has been destroyed!");
				} else {
					city.RemoveRandomCitizen();
					string msg = $"Some of {city.name}'s citizens have been killed!";
					if (!destroyMsgs.Contains(msg))
						destroyMsgs.Add(msg);
				}
			}

			TriggerPopUp(hitCount, tile, string.Join(" ", destroyMsgs));
		}

		// The strength a tile improvement defends with against bombardment,
		// per the project owner. It is in no rule; an estimate from play:
		// "arty seems to wipe out improvement on 75% or more of the shots",
		// i.e. artillery's 12 against 3.
		private const int TileImprovementBombardDefence = 3;

		private async Task BombardTileImprovements(Tile tile) {
			const int tileImprovementDefence = TileImprovementBombardDefence;

			var hitCount = 0;
			List<string> destroyed = new();

			double bombardStrength  = StrengthVersus(null, CombatRole.Bombard, facingDirection);
			double defenderStrength = tileImprovementDefence;
			double attackerOdds = bombardStrength / (bombardStrength + defenderStrength);
			if (Double.IsNaN(attackerOdds))
				return;

			// Each shot of the unit's rate of fire (see RateOfFire) picks one
			// of the improvements still on the tile, as a single shot would.
			// A road left by a destroyed railroad can be hit by the next
			// shot. Once nothing is left, the remaining shots aren't fired.
			await BeginBombard();
			for (int shot = 0; shot < RateOfFire(); ++shot) {
				var improvement = tile.overlays.GetManMadeImprovements()
					.OrderBy(x => GameData.rng.Next()).FirstOrDefault();
				if (improvement == null && shot > 0)
					break;

				if (!await FireShot(tile, attackerOdds))
					continue;
				hitCount += 1;
				// Remove top improvement
				tile.overlays.Remove(improvement);
				// "Replace" with downgraded improvement if it exists
				tile.overlays.Add(improvement?.upgradesFrom);
				if (improvement != null) {
					tile.OwningPlayer()?.AddWarWeariness(owner, Player.WarWearinessForPillagedOrBombedImprovement);
					destroyed.Add(improvement.key);
				}
			}

			TriggerPopUp(hitCount, tile, $"Artillery bombardment successful! Destroyed {string.Join(", ", destroyed)}.");
		}

		// How many shots the unit fires in one bombardment. The BIQ's rate of
		// fire is "how many times a bombarding unit will attack per turn"
		// (Civ3 editor help, Rate of Fire), and each shot is rolled on its
		// own: "You fire twice. Each shot is tallied up individually"
		// (warpstorm, https://forums.civfanatics.com/threads/bombard-question.19648).
		// At least one, so a unit without a rate of fire still bombards once.
		private int RateOfFire() {
			return Math.Max(1, unitType.rateOfFire);
		}

		// The start of a bombardment: the unit's attack animation, and the
		// move it costs, once however many shots it fires.
		private async Task BeginBombard() {
			await animateAsync(AnimatedAction.ATTACK1);
			movementPoints.onUnitMove(1);
		}

		// One shot, a hit with the given odds. Returns whether it hit.
		private async Task<bool> FireShot(Tile tile, double attackerOdds) {
			if (GameData.rng.NextDouble() < attackerOdds) {
				await tile.AnimateAsync(hitList[GameData.rng.Next(0, hitList.Length)]);
				return true;
			}
			await tile.AnimateAsync(tile.IsWater() ? AnimatedEffect.WaterMiss : AnimatedEffect.Miss);
			return false;
		}

		private void TriggerPopUp(int hitCount, Tile tile, string successMessage) {
			if (owner.isHuman) {
				if (hitCount > 0)
					new MsgShowTemporaryPopup(successMessage, tile).send();
				else
					new MsgShowTemporaryPopup($"Artillery bombardment failed.", tile).send();
			}
		}

	}
}
