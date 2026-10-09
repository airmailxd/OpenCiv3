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
		// UNVERIFIED (no Civ3 source found): air units on the ground count
		// as land units here.
		public bool IsBombardmentLethalAgainst(MapUnit target) {
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

			if (this.location.DistanceTo(tile) > this.unitType.bombardRange)
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
		}

		private static CityBuilding FindDestructibleWalls(City city) {
			foreach (CityBuilding cb in city.constructed_buildings) {
				if (cb.building.providesWalls && IsBombardableBuilding(city, cb)) {
					return cb;
				}
			}
			return null;
		}

		// Whether bombardment can destroy the building: anything built in the
		// city except wonders, great and small, and the palace. Per the
		// project owner, a building a wonder provides can't be bombarded
		// either: those granted by the wonder aren't stored in the city, and
		// one also built there is left alone, so the wonder's effect stays.
		private static bool IsBombardableBuilding(City city, CityBuilding cb) {
			return IsBombardableBuilding(cb) && !city.IsProvidedByWonders(cb.building);
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

			if (tile.cityAtTile.constructed_buildings.Contains(walls)) {
				await RunAnimatedBombard(tile, attackerOdds, () => {
					hitCount += 1;
					tile.cityAtTile.RemoveBuilding(walls);
				});
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

		private async Task BombardCity(Tile tile) {
			City city = tile.cityAtTile;

			// Only buildings actually built in the city can be destroyed, and
			// not wonders, the palace or what a wonder provides (see
			// IsBombardableBuilding). Walls are among them when a ship or
			// plane bombards.
			// TODO: probably not canon to exclude palace
			List<CityBuilding> eligibleBuildingsForBombardment = city.constructed_buildings.Where(cb => IsBombardableBuilding(city, cb)).ToList();

			// Per the project owner, bombardment never kills a city's last
			// citizen. What can't be hit isn't rolled for, so a size 1 city
			// only loses buildings, and a city with neither buildings to lose
			// nor citizens to spare takes the shot for nothing.
			bool canHitBuildings = eligibleBuildingsForBombardment.Count > 0;
			bool canHitCitizens = city.residents.Count > 1;
			if (!canHitBuildings && !canHitCitizens) {
				await RunAnimatedBombard(tile, 0, () => { });
				TriggerPopUp(0, tile, string.Empty);
				return;
			}
			bool targetBuildings = canHitBuildings && canHitCitizens
				? GameData.rng.NextDouble() < BuildingOrPopulationOdds
				: canHitBuildings;

			Rules rules = EngineStorage.gameData.rules;
			var defence = targetBuildings ? rules.BuildingDefensiveBonus : rules.CitizenDefensiveBonus;
			var destroyMsg = string.Empty;
			Action remover = targetBuildings
				? () =>
				{
					var building = eligibleBuildingsForBombardment
						.OrderBy(x => GameData.rng.Next()).First();
					city.RemoveBuilding(building);
					destroyMsg = $"The {building.building.name} of {city.name} has been destroyed!";
				}
			: () =>
				{
					city.RemoveRandomCitizen();
					destroyMsg = $"Some of {city.name}'s citizens have been killed!";
				};

			var hitCount = 0;

			double bombardStrength  = StrengthVersus(null, CombatRole.Bombard, facingDirection);
			double defenderStrength = defence;
			double attackerOdds = bombardStrength / (bombardStrength + defenderStrength);
			if (Double.IsNaN(attackerOdds))
				return;

			await RunAnimatedBombard(tile, attackerOdds, () => {
				hitCount += 1;
				remover();
			});

			TriggerPopUp(hitCount, tile, destroyMsg);
		}

		// The strength a tile improvement defends with against bombardment,
		// per the project owner. It is in no rule; an estimate from play:
		// "arty seems to wipe out improvement on 75% or more of the shots",
		// i.e. artillery's 12 against 3.
		private const int TileImprovementBombardDefence = 3;

		private async Task BombardTileImprovements(Tile tile) {
			const int tileImprovementDefence = TileImprovementBombardDefence;

			var hitCount = 0;

			var improvement = tile.overlays.GetManMadeImprovements()
				.OrderBy(x => GameData.rng.Next()).FirstOrDefault();

			// Anecdotal, just by observing the game; I think rate of fire doesn't apply to improvements
			double bombardStrength  = StrengthVersus(null, CombatRole.Bombard, facingDirection);
			double defenderStrength = tileImprovementDefence;
			double attackerOdds = bombardStrength / (bombardStrength + defenderStrength);
			if (Double.IsNaN(attackerOdds))
				return;

			await RunAnimatedBombard(tile, attackerOdds, () => {
				hitCount += 1;
				// Remove top improvement
				tile.overlays.Remove(improvement);
				// "Replace" with downgraded improvement if it exists
				tile.overlays.Add(improvement?.upgradesFrom);
				if (improvement != null) {
					tile.OwningPlayer()?.AddWarWeariness(owner, Player.WarWearinessForPillagedOrBombedImprovement);
				}
				// TODO: Re-target?
			});

			TriggerPopUp(hitCount, tile, $"Artillery bombardment successful! Destroyed {improvement?.key}.");
		}

		private async Task RunAnimatedBombard(Tile tile, double attackerOdds, Action callback) {
			await animateAsync(AnimatedAction.ATTACK1);
			movementPoints.onUnitMove(1);
			if (GameData.rng.NextDouble() < attackerOdds) {
				await tile.AnimateAsync(hitList[GameData.rng.Next(0, hitList.Length)]);
				callback();
			} else
				await tile.AnimateAsync(tile.IsWater() ? AnimatedEffect.WaterMiss : AnimatedEffect.Miss);
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
