using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {
	public class SaveUnitPrototype {
		public enum Flag {
			RotateBeforeAttack,
			CanCarryFootUnitsOnly,
			CanCarryAircraft,
			CanCarryTacticalMissiles,
			LethalLandBombardment,
			LethalSeaBombardment,
			Radar,
			Army,
			// The unit may attack more than once per turn.
			Blitz,
			// The unit may attack from a ship.
			Amphibious,
			// The unit gets a free attack on enemies moving past it.
			ZoneOfControl,
			// A victory by the unit over another civ starts a golden age.
			StartsGoldenAge,
			// The unit is a nuclear weapon: it detonates on its target,
			// and can only be built once the Manhattan Project exists.
			NuclearWeapon,
			// The unit is an intercontinental missile, which can strike any
			// tile on the map.
			ICBM,
			// The unit is a tactical missile, which can be carried by units
			// that can carry tactical missiles (Nuclear Submarines).
			TacticalMissile,
			// The ship can't enter Sea tiles (the BIQ's "Sinks in Sea"),
			// unless its owner has a safe sea travel wonder (the Great
			// Lighthouse).
			SinksInSea,
			// The ship can't enter Ocean tiles (the BIQ's "Sinks in Ocean").
			SinksInOcean,
			// The unit is a military great leader, created by elite victories.
			Leader,
			// The unit can't enter terrain that is impassable to wheeled
			// units (mountains, jungle...) except along a road.
			Wheeled,
			// The unit can be carried by transports that carry only foot
			// units (CanCarryFootUnitsOnly).
			FootSoldier,
			// Every tile costs the unit only as much as a road would.
			AllTerrainAsRoads,
			// The unit can't move.
			Immobile,
			// The aircraft is a stealth one, which enemy air superiority
			// fighters and SAM batteries intercept with the rules' chance
			// to intercept stealth missions rather than air missions.
			Stealth,
			// A city may draft the unit (the BIQ's PRTO "Draft" flag).
			Draftable,
			// The unit is invisible (the BIQ's PRTO "Invisible" flag, which
			// QueryCiv3 calls Submarine): other players see it only with a
			// unit that detects invisible units nearby.
			Invisible,
			// The unit sees invisible units (the BIQ's "Detect Invisible",
			// QueryCiv3's CanSeeSubmarines).
			DetectInvisible,
			// The unit flies no flag: it attacks, and is attacked, without
			// war (the BIQ's "Hidden Nationality"; the Privateer).
			HiddenNationality,
			// The unit picks which unit of a stack it attacks, from its
			// stealth targets (the BIQ's "Stealth Attack").
			StealthAttack,
		}

		public string name { get; set; }
		// The unit's key in the Civilopedia, like PRTO_Warrior.
		public string civilopediaEntry { get; set; }
		public Art art { get; set; }
		public int shieldCost { get; set; }
		public int populationCost { get; set; }
		public ID requiredTech { get; set; }
		public int attack { get; set; }
		public int defense { get; set; }
		public int bombard { get; set; }
		public int bombardRange { get; set; }
		public int rateOfFire { get; set; }
		public int movement { get; set; }
		public int capacity { get; set; }
		public int hpBonus { get; set; }
		// How far, in tiles, an aircraft flies on its missions from its
		// base (the BIQ's PRTO operational range). 0 for units that don't
		// fly, and in saves made before it was saved.
		public int operationalRange { get; set; }
		// How much work a worker does each turn, relative to a Worker (the
		// BIQ's PRTO worker strength). Saves made before it was saved have
		// 1.
		public float workerStrength { get; set; } = 1;

		public HashSet<string> producibleBy = [];

		public List<string> upgradesTo;
		public bool unproducible;

		// The unit this one may make of a foe it defeats (the BIQ's PRTO
		// "enslave results"), like the Javelin Thrower's workers. Null if it
		// doesn't enslave. See MapUnit.RollToEnslave.
		public string enslavesTo;

		// The units a stealth attack by this one may single out of a stack
		// (the BIQ's PRTO stealth targets), by name. Null if there are
		// none.
		public List<string> stealthTargets;

		// Assorted boolean flags for the unit prototype. They're stored in
		// this set rather than as booleans to avoid bloating the json file.
		public HashSet<Flag> flags = [];

		public HashSet<string> categories = new HashSet<string>();

		public HashSet<UnitAction> actions = [];

		public HashSet<string> attributes = new HashSet<string>();

		public HashSet<string> requiredResources = [];

		public HashSet<ID> terraformActions = [];

		public SaveUnitPrototype() { }

		public SaveUnitPrototype(UnitPrototype proto) {
			(name, art, shieldCost, populationCost, unproducible,
			attack, defense, bombard, bombardRange, rateOfFire, movement, capacity, hpBonus) =
			(proto.name, proto.art, proto.shieldCost, proto.populationCost, proto.unproducible,
			 proto.attack, proto.defense, proto.bombard, proto.bombardRange, proto.rateOfFire, proto.movement,
			 proto.capacity, proto.hpBonus);
			workerStrength = proto.workerStrength;
			operationalRange = proto.operationalRange;
			civilopediaEntry = proto.civilopediaEntry;
			enslavesTo = proto.enslavesTo;
			stealthTargets = proto.stealthTargets.Count > 0 ? proto.stealthTargets.ToList() : null;

			if (proto.requiredTech != null)
				requiredTech = proto.requiredTech.id;

			if (proto.upgradesTo != null)
				upgradesTo = proto.upgradesTo?.Select(x => x.name).OrderBy(x => x).ToList() ?? [];

			categories = new HashSet<string>(proto.categories);
			actions = new HashSet<UnitAction>(proto.actions);
			attributes = new HashSet<string>(proto.attributes);
			flags = new HashSet<Flag>(proto.flags);

			requiredResources = proto.requiredResources.Select(r => r.Key).ToHashSet();
			terraformActions = proto.terraformActions.Select(r => r.Id).ToHashSet();
			producibleBy = proto.producibleBy.Select(r => r.name).ToHashSet();
		}
	}
}
