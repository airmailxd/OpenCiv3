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
		}

		public string name { get; set; }
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

		// The BIQ's worker strength: how much faster than a plain worker this
		// unit does terrain jobs (2 for Engineers). Zero if not given, which
		// counts as 1.
		public float workerStrength { get; set; }

		public HashSet<string> producibleBy = [];

		public List<string> upgradesTo;
		public bool unproducible;

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
