using System.Collections.Generic;
using System.Linq;

namespace C7GameData;

// Stealth attack: "Stealth attack allows a unit to specify which enemy unit --
// in a stack -- it will attack. As soon as a unit with stealth capability
// attacks a stack of enemy units, a selection box appears listing the target
// units. From this box, you may select the unit to attack. The stealth unit
// then conducts its attack normally against the unit you've targeted"
// (Conquests Civilopedia, GCON_Stealth_Attack). The targets are the unit types
// in the attacker's stealth target list, and "it only works, if you have at
// least two preset stealth attack targets in a stack", and "doesn't work for
// bombardement"; "the AI has a tendency to stealth-attack the preset target
// with the highest attack factor"
// (https://forums.civfanatics.com/threads/stealth-attack.406555/).
public partial class MapUnit {
	// The units on the tile this unit may single out with a stealth attack,
	// in the tile's order: the enemies that could defend the tile against it
	// whose types are among its stealth targets, and that its owner sees.
	// Empty unless it has stealth attack and there are at least two.
	public List<MapUnit> StealthAttackTargets(Tile tile) {
		if (!unitType.hasStealthAttack || unitType.stealthTargets.Count == 0 || !Tile.IsTileValid(tile)) {
			return [];
		}
		List<MapUnit> targets = tile.unitsOnTile
			.Where(u => u.owner != owner && !owner.IsAtPeaceWith(u.owner)
				&& u.CanDefendAgainst(this) && !u.IsInArmy() && u.IsSeenBy(owner)
				&& unitType.stealthTargets.Contains(u.unitType.name))
			.ToList();
		return targets.Count >= 2 ? targets : [];
	}

	// The target the AI picks for a stealth attack: the one with the highest
	// attack, and of those the one it has the best odds against. Null if it
	// can't stealth attack the tile.
	public MapUnit ChooseStealthAttackTarget(Tile tile) {
		MapUnit best = null;
		foreach (MapUnit u in StealthAttackTargets(tile)) {
			if (best == null || u.unitType.attack > best.unitType.attack
					|| (u.unitType.attack == best.unitType.attack
						&& u.TotalDefensiveStrengthVersus(this) < best.TotalDefensiveStrengthVersus(this))) {
				best = u;
			}
		}
		return best;
	}

	// The unit this one fights on attacking the tile, given the target its
	// owner chose for a stealth attack, or null: the chosen target if it is
	// one, the AI's pick for a computer player that chose none, and otherwise
	// the tile's top defender.
	internal MapUnit DefenderAgainstAttackOn(Tile tile, ID stealthTarget) {
		List<MapUnit> targets = StealthAttackTargets(tile);
		if (targets.Count > 0) {
			MapUnit chosen = stealthTarget == null ? null : targets.FirstOrDefault(u => u.id == stealthTarget);
			if (chosen != null) {
				return chosen;
			}
			if (!owner.isHuman) {
				return ChooseStealthAttackTarget(tile);
			}
		}
		return tile.FindTopDefender(this);
	}
}
