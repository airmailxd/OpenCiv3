using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// The scenarios a new game on a generated map may be set up as.
public enum GameScenario {
	None,
	EternalWar,
}

// Eternal War, a custom scenario (owner's design, 2026-10-21): on one
// landmass with no islands, every civ starts in the Middle Ages with every
// Ancient Times advance, under Monarchy, with its cities scattered at
// random among its enemies' (how many is chosen at setup, by default more
// on a bigger map). A seafaring civ's capital is on the coast. Every civ
// is at war with every other for good, and none may talk to another.
// No one can build settlers, so cities change hands only by conquest. There
// are no goody huts, and barbarians come only as knights. Cities have no
// corruption and a library each, which costs nothing to keep up and is never
// lost when the city is taken, and a quarter fewer citizens are born
// unhappy. The first capital also has a granary and an aqueduct. Workers
// defend (2) but can't attack, and every government supports three times
// the units for free.
// (Owner's additions, 2026-10-21.)
// Citizens work tiles as far as the borders' second expansion (radius 3).
// Cities grow to 12 without fresh water or an aqueduct, and need a hospital
// only past 24. Every tile makes a commerce more, and forest on tundra a
// food more. A worker beaten in battle becomes a slave worker in the
// winner's nearest city. (Owner's additions, 2026-10-21.)
// Every turn after the first starts with Staging, which gives every civ a
// unit of the leading age for the city of its choice (see Staging). Coastal
// buildings, such as harbors and the Colossus, may be built within 3 tiles
// of the coast. (Owner's additions, 2026-10-22.)
// Desert makes a shield more, though flood plains don't. (Owner's addition,
// 2026-10-23.)
public partial class GameSetup {
	public const int EternalWarCitySize = 3;
	public const int MinEternalWarCitiesPerCiv = 1;
	public const int MaxEternalWarCitiesPerCiv = 12;
	public const int EternalWarBornUnhappyPercent = 75;

	private const string EternalWarDefender = "Pikeman";
	private const string EternalWarWorker = "Worker";
	private const string EternalWarBarbarian = "Knight";
	private const string EternalWarBuilding = "Library";
	private const string EternalWarGovernment = "Monarchy";
	private static readonly string[] EternalWarCapitalBuildings = ["Granary", "Aqueduct"];
	private const int EternalWarWorkerDefense = 2;
	private const int EternalWarUnitSupportFactor = 3;
	// Citizens work as far as a city's borders reach after their second
	// expansion, rather than their first.
	public const int EternalWarWorkRadius = 3;
	// Coastal buildings, such as harbors and the Colossus, may be built
	// within this many tiles of the coast (owner's addition, 2026-10-22).
	public const int EternalWarCoastalBuildingRange = 3;

	// The cities each civ starts with by default on each world size (owner's
	// choice, 2026-10-21).
	private static readonly Dictionary<string, int> EternalWarCitiesByWorldSize = new(StringComparer.OrdinalIgnoreCase) {
		["Tiny"] = 2,
		["Small"] = 4,
		["Standard"] = 6,
		["Large"] = 8,
		["Huge"] = 10,
	};

	// The cities each civ starts with by default on the world size: by its
	// name, or for a size the list doesn't name, two more for each size up
	// the rules' list.
	public static int DefaultEternalWarCitiesPerCiv(List<WorldSize> sizes, WorldSize size) {
		if (size?.name != null && EternalWarCitiesByWorldSize.TryGetValue(size.name, out int cities)) {
			return cities;
		}
		int index = sizes?.FindIndex(s => s.name == size?.name) ?? -1;
		return index >= 0 ? Math.Min(MaxEternalWarCitiesPerCiv, 2 * (index + 1)) : 4;
	}

	// Shapes the map before it's generated.
	private void SetUpEternalWarWorld(SaveGame save, WorldCharacteristics world) {
		world.landform = WorldCharacteristics.Landform.Pangaea;
		world.singleLandmass = true;
		world.goodyHuts = false;
		world.citiesPerCiv = Math.Clamp(
			eternalWarCitiesPerCiv ?? DefaultEternalWarCitiesPerCiv(save.WorldSizes, world.worldSize),
			MinEternalWarCitiesPerCiv, MaxEternalWarCitiesPerCiv);
	}

	// Deals the map's city sites out to the planned players, the first of
	// each one's sites its capital: in turn, so each civ has its share
	// scattered among its enemies'. A seafaring civ's capital is one of its
	// sites on the coast, or if it has none, a coastal site traded for with
	// another civ, which may be the capital of a civ that isn't seafaring.
	private List<List<SaveTile>> DealEternalWarSites(SaveGame save, List<PlannedPlayer> planned) {
		List<SaveTile> all = save.Map.startingLocations;
		// When the map has too few sites for the cities chosen, every civ
		// gets the same smaller number that fits.
		int perCiv = Math.Min(worldCharacteristics.citiesPerCiv, all.Count / planned.Count);
		if (perCiv < worldCharacteristics.citiesPerCiv) {
			log.Warning($"Only {all.Count} city sites fit on the map, so each civ starts with {perCiv} cities rather than {worldCharacteristics.citiesPerCiv}");
		}
		List<List<SaveTile>> sites = Enumerable.Range(0, planned.Count)
			.Select(i => all.Where((_, n) => n % planned.Count == i).Take(perCiv).ToList()).ToList();
		bool Coastal(SaveTile t) => IsNextToSea(save.Map, t);

		// First the seafaring civs with a coastal site of their own, then
		// the others, which may trade for any coastal site but those
		// capitals.
		List<int> seafaring = Enumerable.Range(0, planned.Count).Where(i => IsSeafaring(planned[i].civ) && sites[i].Count > 0).ToList();
		foreach (int i in seafaring) {
			int own = sites[i].FindIndex(Coastal);
			if (own > 0) {
				(sites[i][0], sites[i][own]) = (sites[i][own], sites[i][0]);
			}
		}
		foreach (int i in seafaring.Where(i => !Coastal(sites[i][0]))) {
			(int civ, int site) = Enumerable.Range(0, planned.Count)
				.Where(j => j != i)
				.SelectMany(j => Enumerable.Range(0, sites[j].Count).Select(n => (j, n)))
				.Where(p => Coastal(sites[p.Item1][p.Item2]) && !(p.Item2 == 0 && IsSeafaring(planned[p.Item1].civ)))
				// Another civ's spare city before its capital.
				.OrderBy(p => p.Item2 == 0)
				.FirstOrDefault((-1, -1));
			if (civ < 0) {
				log.Warning($"No coastal city site for seafaring {planned[i].civ.name}'s capital");
				continue;
			}
			(sites[i][0], sites[civ][site]) = (sites[civ][site], sites[i][0]);
		}
		return sites;
	}

	// The rules, which are saved with the game.
	private static void SetUpEternalWarRules(SaveGame save) {
		// Once only, as unit support is multiplied, should setting up the
		// game be tried again on the same rules.
		if (save.Rules.PermanentWar) {
			return;
		}
		save.Rules.NoCorruption = true;
		save.Rules.PermanentWar = true;
		save.Rules.BornUnhappyPercent = EternalWarBornUnhappyPercent;
		save.Rules.MaxRankOfWorkableTiles = EternalWarWorkRadius;
		save.Rules.SizeWithoutAqueduct = save.Rules.MaximumLevel2CitySize;
		save.Rules.SizeWithoutHospital = 2 * save.Rules.MaximumLevel2CitySize;
		save.Rules.TileCommerceBonus = 1;
		save.Rules.TundraForestFoodBonus = 1;
		save.Rules.DesertShieldBonus = 1;
		save.Rules.DefeatedWorkersBecomeSlaves = true;
		save.Rules.CoastalBuildingRange = EternalWarCoastalBuildingRange;
		save.Rules.StagingUnits = true;

		foreach (SaveUnitPrototype settler in save.UnitPrototypes.Where(p => p.actions.Contains(UnitAction.BuildCity))) {
			settler.unproducible = true;
		}

		foreach (SaveUnitPrototype worker in save.UnitPrototypes.Where(p => p.name == EternalWarWorker || p.name == save.Rules.SlaveUnitType)) {
			worker.attack = 0;
			worker.defense = EternalWarWorkerDefense;
		}

		foreach (Government government in save.Governments) {
			government.freeUnitsPerTown *= EternalWarUnitSupportFactor;
			government.freeUnitsPerCity *= EternalWarUnitSupportFactor;
			government.freeUnitsPerMetropolis *= EternalWarUnitSupportFactor;
		}

		SaveBuilding library = save.Buildings.Find(b => b.name == EternalWarBuilding)
			?? throw new InvalidOperationException($"The Eternal War scenario needs a {EternalWarBuilding}, which these rules don't have");
		library.maintenanceCost = 0;
		save.Rules.BuildingsKeptOnCapture = [library.name];

		RequireUnit(save, EternalWarBarbarian);
		save.BarbarianInfo.basicBarbarianUnit = EternalWarBarbarian;
		save.BarbarianInfo.advancedBarbarianUnit = EternalWarBarbarian;
		save.BarbarianInfo.barbarianSeaUnit = null;
		// Every civ starts in the Middle Ages, which would set off its
		// uprising on the first turn.
		save.BarbarianInfo.middleAgesUprisingHappened = true;
	}

	private static void RequireUnit(SaveGame save, string name) {
		if (!save.UnitPrototypes.Any(p => p.name == name)) {
			throw new InvalidOperationException($"The Eternal War scenario needs a {name} unit, which these rules don't have");
		}
	}

	// The civ's era, advances and government, its cities on the given
	// sites (the first its capital), and the units in them.
	private void SetUpEternalWarPlayer(SaveGame save, SavePlayer player, Civilization civ, List<SaveTile> sites) {
		player.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		player.knownTechs.UnionWith(save.Techs.Where(t => t.EraCivilopediaName == EraUtils.ANCIENT_TIMES_CVLPD).Select(t => t.id));
		Government monarchy = save.Governments.Find(g => g.name == EternalWarGovernment)
			?? throw new InvalidOperationException($"The Eternal War scenario needs the {EternalWarGovernment} government, which these rules don't have");
		player.governmentId = monarchy.id;

		RequireUnit(save, EternalWarDefender);
		RequireUnit(save, EternalWarWorker);
		SaveBuilding library = save.Buildings.Find(b => b.name == EternalWarBuilding)
			?? throw new InvalidOperationException($"The Eternal War scenario needs a {EternalWarBuilding}, which these rules don't have");
		SaveBuilding palace = save.Buildings.Find(b => b.flags.Contains(SaveBuilding.Flag.IsCenterOfEmpire));
		// Only the first capital has these; a palace moved later doesn't
		// bring them.
		List<SaveBuilding> capitalBuildings = EternalWarCapitalBuildings.Select(name => save.Buildings.Find(b => b.name == name)
			?? throw new InvalidOperationException($"The Eternal War scenario needs a {name}, which these rules don't have")).ToList();

		for (int i = 0; i < sites.Count; ++i) {
			bool capital = i == 0;
			TileLocation location = new(sites[i].X, sites[i].Y);
			SaveCity city = new() {
				id = ids.CreateID("city"),
				owner = player.id,
				location = location,
				capital = capital,
				name = i < civ.cityNames.Count ? civ.cityNames[i] : $"{civ.name} {i + 1}",
				size = EternalWarCitySize,
				producible = EternalWarDefender,
				producibleType = ProducibleType.UNIT,
			};
			city.perPlayerCulture.Add(player.id.ToString(), 0);
			city.buildings.Add(new SaveCityBuilding { building = library.name, builtByPlayer = player.id });
			if (capital) {
				foreach (SaveBuilding building in capitalBuildings.Prepend(palace).Where(b => b != null)) {
					city.buildings.Add(new SaveCityBuilding { building = building.name, builtByPlayer = player.id });
				}
			}
			save.Cities.Add(city);

			AddUnit(save, player, EternalWarDefender, location);
			AddUnit(save, player, EternalWarWorker, location);
		}
		player.citiesFounded = sites.Count;
	}

	// Every civ has met every other, and is at war with it.
	private static void StartEternalWar(SaveGame save) {
		List<SavePlayer> civs = save.Players.Where(p => !p.isBarbarian).ToList();
		foreach (SavePlayer player in civs) {
			foreach (SavePlayer other in civs.Where(o => o != player)) {
				player.playerRelationships[other.id.ToString()] = new PlayerRelationship { firstContactTurn = 0 };
			}
		}
	}
}
