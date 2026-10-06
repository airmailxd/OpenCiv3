using System.Linq;
using C7Engine;

namespace C7GameData;

// Military great leaders. When an elite land unit wins a battle against
// another civ, there's a chance a leader appears on its tile. A leader in one
// of its owner's cities can form an army, or finish the city's current
// improvement.
public partial class MapUnit {
	// The chance of an elite victory producing a leader: 1 in 16, or 1 in 12
	// with the Heroic Epic. These are the figures usually given for Conquests;
	// the game doesn't document them.
	internal const double LeaderChance = 1.0 / 16.0;
	internal const double IncreasedLeaderChance = 1.0 / 12.0;

	public static double LeaderChanceFor(Player player) {
		return player.HasIncreasedLeaderChance() ? IncreasedLeaderChance : LeaderChance;
	}

	public bool IsLeader() {
		return unitType.isLeader;
	}

	public bool IsElite() {
		return experienceLevel != null && EngineStorage.gameData.GetExperienceLevelAfter(experienceLevel) == null;
	}

	// Whether a victory by this unit over the opponent could produce a
	// leader, before the roll. Only elite land units fighting for themselves
	// (not as members of an army) qualify, victories over barbarians don't
	// count, each unit produces at most one leader, and a civ has at most one
	// leader at a time.
	internal bool CouldProduceLeaderBeating(MapUnit opponent, bool wasElite) {
		if (!wasElite || hasProducedLeader || IsInArmy() || IsArmy() || !IsLandUnit())
			return false;
		if (owner == null || owner.isBarbarians || opponent?.owner == null || opponent.owner.isBarbarians)
			return false;
		if (LeaderPrototype() == null)
			return false;
		return !owner.units.Any(u => u.IsLeader());
	}

	private static UnitPrototype LeaderPrototype() {
		GameData gD = EngineStorage.gameData;
		string name = gD?.rules?.BattleCreatedUnit;
		if (string.IsNullOrEmpty(name))
			return null;
		return gD.unitPrototypes.Find(p => p.name == name);
	}

	// Rolls for a leader after this unit won a fight. wasElite is whether
	// the unit was elite going into the fight: a unit promoted to elite by
	// this victory doesn't also get the chance. Returns the new leader, or
	// null.
	internal MapUnit RollForLeader(MapUnit opponent, bool wasElite) {
		if (!CouldProduceLeaderBeating(opponent, wasElite))
			return null;
		if (GameData.rng.NextDouble() >= LeaderChanceFor(owner))
			return null;
		return ProduceLeader();
	}

	internal MapUnit ProduceLeader() {
		UnitPrototype proto = LeaderPrototype();
		if (proto == null || !Tile.IsTileValid(location))
			return null;
		hasProducedLeader = true;
		MapUnit leader = EngineStorage.gameData.SpawnUnit(owner, proto, location);
		log.Information("{Unit}'s victory has produced a great leader, {Leader}", this, leader);
		if (owner.isHuman) {
			new MsgShowMilitaryAdvisorPopup(owner, $"Our {name}'s great victory has inspired a new leader!\nA leader can form an army or hurry production in one of our cities.", happy: true).send();
		}
		return leader;
	}

	// A leader in one of its owner's cities can form an army there. Unlike
	// armies built by the Military Academy, these don't need cities to
	// support them (an assumption, from how Conquests plays).
	public bool CanFormArmy() {
		if (!IsLeader() || !movementPoints.canMove || !InOwnCity())
			return false;
		return ArmyPrototype() != null;
	}

	private static UnitPrototype ArmyPrototype() {
		GameData gD = EngineStorage.gameData;
		string name = gD?.rules?.BuildArmyUnit;
		if (string.IsNullOrEmpty(name))
			return null;
		return gD.unitPrototypes.Find(p => p.name == name);
	}

	private bool InOwnCity() {
		return Tile.IsTileValid(location) && location.HasCity() && location.cityAtTile.owner == owner;
	}

	// The leader becomes an empty army in its city. Returns the army, or
	// null if the leader can't form one.
	public MapUnit FormArmy() {
		if (!CanFormArmy())
			return null;
		GameData gD = EngineStorage.gameData;
		Tile tile = location;
		Player player = owner;
		gD.RemoveUnit(this);
		MapUnit army = gD.SpawnUnit(player, ArmyPrototype(), tile);
		log.Information("{Leader} formed {Army}", this, army);
		return army;
	}

	// A leader in one of its owner's cities can finish the city's current
	// improvement or small wonder. In Conquests military leaders can't hurry
	// great wonders (that's for scientific leaders) or units (an assumption).
	public bool CanHurryProduction() {
		return CanOfferHurryProduction() && HurryProductionBlocker() == null;
	}

	// Whether to show the hurry action at all: the leader is in one of its
	// owner's cities with moves left. The city's current item may still rule
	// it out, and HurryProductionBlocker says why.
	public bool CanOfferHurryProduction() {
		return IsLeader() && movementPoints.canMove && InOwnCity();
	}

	// Why this leader can't hurry its city's current item, or null if it can.
	public string HurryProductionBlocker() {
		if (!CanOfferHurryProduction())
			return "A leader must be in one of our cities, with moves left, to hurry production.";
		City city = location.cityAtTile;
		return city.itemBeingProduced switch {
			null => $"{city.name} isn't building anything.",
			UnitPrototype => $"A leader can't hurry units. Switch {city.name}'s production to an improvement or small wonder first.",
			Building b when b.IsGreatWonder() => $"A military leader can't hurry great wonders like the {b.name}. Switch {city.name}'s production to an improvement or small wonder first.",
			Building b when city.shieldsStored >= owner.ShieldCost(b) => $"{city.name} will already finish its {b.name} this turn.",
			Building => null,
			_ => $"A leader can't hurry {city.name}'s current production.",
		};
	}

	// Fills the city's production box, so that the item is finished at the
	// end of the turn, and uses up the leader.
	public bool HurryProductionAsLeader() {
		if (!CanHurryProduction())
			return false;
		City city = location.cityAtTile;
		city.FillProductionBox();
		log.Information("{Leader} hurried {Item} in {City}", this, city.itemBeingProduced, city);
		EngineStorage.gameData.RemoveUnit(this);
		return true;
	}
}
