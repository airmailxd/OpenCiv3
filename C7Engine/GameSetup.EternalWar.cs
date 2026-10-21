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
// corruption and a library each, and a quarter fewer citizens are born
// unhappy.
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
		int perCiv = worldCharacteristics.citiesPerCiv;
		List<SaveTile> all = save.Map.startingLocations;
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
		save.Rules.NoCorruption = true;
		save.Rules.PermanentWar = true;
		save.Rules.BornUnhappyPercent = EternalWarBornUnhappyPercent;

		foreach (SaveUnitPrototype settler in save.UnitPrototypes.Where(p => p.actions.Contains(UnitAction.BuildCity))) {
			settler.unproducible = true;
		}

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
			if (capital && palace != null) {
				city.buildings.Add(new SaveCityBuilding { building = palace.name, builtByPlayer = player.id });
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
