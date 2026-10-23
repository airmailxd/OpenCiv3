using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// Kings & Nomads, a custom scenario (owner's design, 2026-10-23): no civ
// starts with a city. Each is a King and a wandering horde: two archers, two
// horsemen, a warrior and a worker. Independent city-states, walled and
// garrisoned, are scattered over the map, held by the barbarians. Settlers
// can only be built in a city taken from someone else, so an empire is
// built by conquest. When a civ's King dies, the civ falls (Regicide), and
// its cities go back to being independent. The last King standing wins,
// as does a civ holding half of all the cities in the world. Any map type.
public partial class GameSetup {
	// How many independent city-states the map has for each civ.
	public const int CityStatesPerCiv = 3;
	public const int CityStateSize = 3;
	// The closest a city-state may be to a civ's start.
	private const int CityStateDistanceFromStart = 5;

	private const string CityStateWalls = "Walls";
	private static readonly string[] CityStateGarrison = ["Spearman", "Spearman", "Archer"];
	private static readonly string[] KingsHorde = ["Archer", "Archer", "Horseman", "Horseman", "Warrior", "Worker"];

	private static void SetUpKingsAndNomadsWorld(WorldCharacteristics world) {
		// The map's city sites: one for each civ's start, and the rest for
		// city-states.
		world.citiesPerCiv = 1 + CityStatesPerCiv;
	}

	private static void SetUpKingsAndNomadsRules(SaveGame save) {
		save.Rules.SettlersOnlyInConqueredCities = true;
		save.Rules.FallenCitiesGoIndependent = true;
		save.VictoryConditions ??= new VictoryConditions();
		save.VictoryConditions.Regicide = true;
		save.VictoryConditions.MassRegicide = false;
		save.VictoryConditions.AutoPlaceKings = true;
		save.VictoryConditions.AllowConquestVictory = true;
		save.VictoryConditions.AllowEmpireVictory = true;
	}

	// A civ's horde, on its start; its King comes with Regicide.
	private void AddKingsHorde(SaveGame save, SavePlayer player, TileLocation location) {
		foreach (string unit in KingsHorde) {
			AddUnit(save, player, unit, location);
		}
	}

	// Spreads the civs over the map's city sites, as far from each other as
	// can be, and makes city-states of the rest that aren't too near a
	// civ's start. Returns the civs' starts.
	private List<SaveTile> SetUpKingsAndNomadsSites(SaveGame save, Random rand, int civs, HashSet<string> taken) {
		SaveMap map = save.Map;
		List<SaveTile> sites = map.startingLocations.ToList();
		if (sites.Count < civs) {
			throw new InvalidOperationException($"The map has {sites.Count} city sites, too few for {civs} civilizations");
		}
		int Distance(SaveTile a, SaveTile b) {
			int dx = Math.Abs(a.X - b.X);
			if (map.wrapHorizontally) {
				dx = Math.Min(dx, map.tilesWide - dx);
			}
			return Math.Max(dx, Math.Abs(a.Y - b.Y));
		}
		List<SaveTile> starts = [sites[rand.Next(sites.Count)]];
		while (starts.Count < civs) {
			starts.Add(sites.Where(t => !starts.Contains(t)).MaxBy(t => starts.Min(s => Distance(s, t))));
		}

		SavePlayer independents = save.Players.Find(p => p.isBarbarian);
		List<SaveTile> cityStates = sites
			.Where(t => !starts.Contains(t) && starts.All(s => Distance(s, t) >= CityStateDistanceFromStart))
			.Take(civs * CityStatesPerCiv).ToList();
		// Named for the capitals of the civilizations not in the game.
		Queue<string> names = new(save.Civilizations
			.Where(c => !c.isBarbarian && !taken.Contains(c.name) && c.cityNames.Count > 0)
			.OrderBy(_ => rand.Next())
			.Select(c => c.cityNames[0]));
		SaveBuilding walls = save.Buildings.Find(b => b.name == CityStateWalls);
		for (int i = 0; i < cityStates.Count && independents != null; i++) {
			TileLocation location = new(cityStates[i].X, cityStates[i].Y);
			SaveCity city = new() {
				id = ids.CreateID("city"),
				owner = independents.id,
				location = location,
				name = names.Count > 0 ? names.Dequeue() : $"Free City {i + 1}",
				size = CityStateSize,
				producible = CityStateGarrison[0],
				producibleType = ProducibleType.UNIT,
			};
			city.perPlayerCulture.Add(independents.id.ToString(), 0);
			if (walls != null) {
				city.buildings.Add(new SaveCityBuilding { building = walls.name, builtByPlayer = independents.id });
			}
			save.Cities.Add(city);
			foreach (string unit in CityStateGarrison) {
				AddUnit(save, independents, unit, location);
			}
		}
		return starts;
	}
}
