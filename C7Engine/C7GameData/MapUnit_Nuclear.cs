using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using static C7GameData.PlayerRelationship;

namespace C7GameData {
	// Nuclear weapons (Tactical Nukes and ICBMs).
	//
	// A nuke is fired with the Bombard order at a tile in range: any tile on
	// the map for an ICBM, within its bombard (operational) range for other
	// nukes, which can also be fired from the Nuclear Submarine carrying
	// them. The missile is used up. Unless the target's owner has a Strategic
	// Missile Defense that shoots it down, it detonates over the target and
	// the 8 tiles around it:
	//  - every unit in the blast area is destroyed, whoever owns it;
	//  - every city in it loses half its population and some of its buildings;
	//  - fallout is left on some of the land tiles;
	//  - the civs hit go to war with the attacker, and every civ that knows
	//    the attacker holds it against them (an atrocity).
	public partial class MapUnit {
		// Civ3: the Strategic Missile Defense small wonder gives a 75% chance
		// of shooting down a nuke aimed at its owner.
		public const double MissileDefenseInterceptChance = 0.75;

		// Assumption: Civ3 destroys "some" of a nuked city's buildings; we
		// give each building that bombardment could destroy (not wonders or
		// the palace) an even chance.
		public const double NuclearBuildingDestructionChance = 0.5;

		// Assumption: each land tile in the blast area without a city has an
		// even chance of getting fallout.
		public const double NuclearFalloutChance = 0.5;

		public class NuclearStrikeResult {
			public bool intercepted;
			public int unitsDestroyed;
			public int citizensKilled;
			public List<City> citiesHit = new();
			public List<string> buildingsDestroyed = new();
			public int falloutTiles;
			// The civs whose units, cities or territory were hit.
			public HashSet<Player> victims = new();
		}

		public bool IsNuclearWeapon() {
			return unitType.isNuclearWeapon;
		}

		// ICBMs reach any tile. Other nukes reach as far as their bombard
		// (operational) range, at least to adjacent tiles.
		public bool CanReachForNuclearStrike(Tile tile) {
			if (unitType.isICBM) {
				return true;
			}
			return location.DistanceTo(tile) <= System.Math.Max(1, unitType.bombardRange);
		}

		// The tiles a nuke detonating over the target destroys: the target
		// and the tiles around it.
		public static List<Tile> NuclearBlastArea(Tile target) {
			List<Tile> result = new() { target };
			foreach (Tile neighbor in target.neighbors.Values) {
				if (neighbor != null && neighbor != Tile.NONE && !result.Contains(neighbor)) {
					result.Add(neighbor);
				}
			}
			return result;
		}

		public bool CanNukeTile(Tile tile) {
			return CanNukeTile(tile, out _);
		}

		// Whether this nuclear weapon can strike the tile. The target says
		// what's hit on the target tile, so the UI can ask whether to declare
		// war on its owner first, as for a bombardment.
		public bool CanNukeTile(Tile tile, out BombardTarget bombardTarget) {
			bombardTarget = BombardTarget.None;

			if (!IsNuclearWeapon() || tile == null || tile == Tile.NONE || location == null || location == Tile.NONE)
				return false;

			if (hitPointsRemaining <= 0 || !movementPoints.canMove || !CanAttackAgainThisTurn())
				return false;

			if (tile == location || !CanReachForNuclearStrike(tile))
				return false;

			// Only tiles the owner has seen can be targeted.
			if (owner != null && !owner.isBarbarians && !owner.tileKnowledge.knownTiles.Contains(tile))
				return false;

			if (tile.HasCity() && tile.cityAtTile.owner == owner)
				return false;

			// Nothing may attack a locked ally, so no nuke may hit one.
			GameData gameData = EngineStorage.gameData;
			foreach (Tile t in NuclearBlastArea(tile)) {
				Player territoryOwner = t.OwningPlayer();
				if (territoryOwner != null && territoryOwner != owner && gameData.AreInLockedPeace(owner, territoryOwner))
					return false;
				foreach (MapUnit unit in t.unitsOnTile) {
					if (unit.owner != owner && gameData.AreInLockedPeace(owner, unit.owner))
						return false;
				}
			}

			MapUnit firstUnit = tile.unitsOnTile.FirstOrDefault();
			if (tile.HasCity())
				bombardTarget = BombardTarget.City;
			else if (firstUnit != null && firstUnit.owner != owner)
				bombardTarget = BombardTarget.Unit;
			else
				bombardTarget = BombardTarget.Improvement;
			return true;
		}

		// The civs a nuke over the target would hit: the owners of the
		// territory, cities and units in the blast area, other than the
		// attacker.
		public HashSet<Player> NuclearStrikeVictims(Tile target) {
			HashSet<Player> victims = new();
			foreach (Tile t in NuclearBlastArea(target)) {
				Player territoryOwner = t.OwningPlayer();
				if (territoryOwner != null && territoryOwner != owner) {
					victims.Add(territoryOwner);
				}
				foreach (MapUnit unit in t.unitsOnTile) {
					if (unit.owner != null && unit.owner != owner) {
						victims.Add(unit.owner);
					}
				}
			}
			return victims;
		}

		// The civs a nuke over the target would put at war with the attacker,
		// so the UI can ask about each before firing.
		public List<Player> NuclearStrikeWarDeclarations(Tile target) {
			GameData gameData = EngineStorage.gameData;
			return NuclearStrikeVictims(target).Where(v => WouldDeclareWarOverNuke(owner, v, gameData)).ToList();
		}

		// Whether a nuclear attack on the victim puts the attacker at war
		// with it.
		private static bool WouldDeclareWarOverNuke(Player attacker, Player victim, GameData gameData) {
			if (attacker == null || attacker.isBarbarians || attacker.defeated) {
				return false;
			}
			if (victim == attacker || victim.isBarbarians || victim.defeated) {
				return false;
			}
			return !AtWar(attacker, victim) && !gameData.AreInLockedPeace(attacker, victim);
		}

		// Whether the player has a building that shoots down missiles (the
		// Strategic Missile Defense).
		public static bool HasMissileDefense(Player player) {
			if (player == null) {
				return false;
			}
			foreach (City city in player.cities) {
				foreach (CityBuilding cb in city.constructed_buildings) {
					if (cb.building.decreasesMissileSuccess) {
						return true;
					}
				}
			}
			return false;
		}

		// The civ defending against a nuke aimed at the tile: the owner of the
		// city there or of the territory, or else of the units there.
		// Assumption: Civ3's Strategic Missile Defense protects its owner's
		// cities; we extend that to the owner's territory and units.
		private Player NuclearTargetDefender(Tile tile) {
			Player defender = tile.OwningPlayer();
			if (defender == null || defender == owner) {
				defender = tile.unitsOnTile.FirstOrDefault(u => u.owner != owner)?.owner;
			}
			return defender == owner ? null : defender;
		}

		// Fires the nuke at the tile, animating it and telling the players.
		public async Task NuclearStrike(Tile tile) {
			if (!CanNukeTile(tile)) {
				return;
			}

			facingDirection = location.DirectionTo(tile);
			await animateAsync(AnimatedAction.ATTACK1);

			// The messages go out after awaiting the animations, when the
			// engine no longer knows who gave the order, so each one names its
			// recipient.
			Player attacker = owner;
			bool tellAttacker = attacker.isHuman;
			NuclearStrikeResult result = LaunchNuke(tile);

			if (result.intercepted) {
				await tile.AnimateAsync(tile.IsWater() ? AnimatedEffect.WaterMiss : AnimatedEffect.Miss);
				if (tellAttacker) {
					new MsgShowTemporaryPopup("Our nuclear missile was shot down by a Strategic Missile Defense!", tile, attacker).send();
				}
				foreach (Player victim in result.victims.Where(v => v.isHuman)) {
					new MsgShowTemporaryPopup("Our Strategic Missile Defense shot down an incoming nuclear missile!", tile, victim).send();
				}
				return;
			}

			foreach (Tile t in NuclearBlastArea(tile)) {
				await t.AnimateAsync(AnimatedEffect.Hit5);
			}

			string summary = $"Nuclear detonation! {result.unitsDestroyed} units destroyed";
			if (result.citiesHit.Count > 0) {
				summary += $", {string.Join(", ", result.citiesHit.Select(c => c.name))} devastated";
			}
			summary += ".";
			if (tellAttacker) {
				new MsgShowTemporaryPopup(summary, tile, attacker).send();
			}
			foreach (Player victim in result.victims.Where(v => v.isHuman && v != attacker)) {
				new MsgShowTemporaryPopup($"The {attacker.civilization?.noun ?? "enemy"} have attacked us with nuclear weapons! " + summary, tile, victim).send();
			}
		}

		// Fires the nuke at the tile and resolves the strike, without any
		// animation or messages. The missile is used up either way.
		public NuclearStrikeResult LaunchNuke(Tile tile) {
			GameData gameData = EngineStorage.gameData;
			Player attacker = owner;
			NuclearStrikeResult result = new();

			hasAttackedThisTurn = true;

			Player defender = NuclearTargetDefender(tile);
			if (defender != null) {
				result.victims.Add(defender);
			}

			// The missile is spent whether or not it gets through.
			RemoveFromPlay();

			if (HasMissileDefense(defender) && GameData.rng.NextDouble() < MissileDefenseInterceptChance) {
				result.intercepted = true;
				return result;
			}

			foreach (Tile t in NuclearBlastArea(tile)) {
				foreach (MapUnit unit in t.unitsOnTile.ToArray()) {
					if (unit.owner != attacker && unit.owner != null) {
						result.victims.Add(unit.owner);
					}
					// Units removed with their transport or army are already
					// gone, but count as destroyed all the same.
					if (unit.hitPointsRemaining > 0) {
						unit.RemoveFromPlay();
					}
					++result.unitsDestroyed;
				}

				Player territoryOwner = t.OwningPlayer();
				if (territoryOwner != null && territoryOwner != attacker) {
					result.victims.Add(territoryOwner);
				}

				if (t.HasCity()) {
					City city = t.cityAtTile;
					result.citiesHit.Add(city);
					int lost = city.residents.Count / 2;
					for (int i = 0; i < lost; i++) {
						city.RemoveRandomCitizen();
						++result.citizensKilled;
					}
					foreach (CityBuilding cb in city.constructed_buildings.ToArray()) {
						if (IsBombardableBuilding(cb) && GameData.rng.NextDouble() < NuclearBuildingDestructionChance) {
							city.RemoveBuilding(cb);
							result.buildingsDestroyed.Add(cb.building.name);
						}
					}
				} else if (t.IsLand() && !t.HasFallout() && GameData.rng.NextDouble() < NuclearFalloutChance) {
					if (Tile.TryAddFallout(t)) {
						++result.falloutTiles;
					}
				}
				TileChangeJournal.Record(t);
			}

			result.victims.Remove(attacker);
			ApplyNuclearDiplomacy(attacker, result.victims, gameData);
			return result;
		}

		// A nuclear attack is an act of war against every civ hit, and an
		// atrocity every civ that knows the attacker remembers.
		//
		// Assumption: Civ3 makes every civ that has contact with the attacker
		// angry over a nuclear attack; we record it in the relationship, and
		// the civs hit, if at peace, are at war with the attacker.
		private static void ApplyNuclearDiplomacy(Player attacker, HashSet<Player> victims, GameData gameData) {
			if (attacker == null || attacker.isBarbarians) {
				return;
			}
			foreach (Player victim in victims) {
				if (WouldDeclareWarOverNuke(attacker, victim, gameData)) {
					attacker.DeclareWarOn(victim, gameData.turn);
				}
			}
			foreach (Player other in gameData.players) {
				if (other == attacker || other.isBarbarians || other.defeated) {
					continue;
				}
				if (other.playerRelationships.TryGetValue(attacker.id, out PlayerRelationship relationship)) {
					++relationship.nuclearAtrocityCount;
				}
			}
		}
	}
}
