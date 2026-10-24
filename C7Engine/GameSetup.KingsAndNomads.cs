using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// Kings & Nomads, a custom scenario (owner's design, 2026-10-23): no civ
// starts with a city. Each is a King and a wandering horde: two archers, two
// horsemen, a swordsman (owner's addition, 2026-10-23), a warrior and a
// worker. Independent city-states,
// garrisoned, are scattered over the map, held by the barbarians. Settlers
// can only be built in a city taken from someone else, so an empire is
// built by conquest. When a civ's King dies, the civ falls (Regicide), and
// its cities go back to being independent. The last King standing wins,
// as does a civ holding half of all the cities in the world. Played on
// continents or a pangaea, each civ on a landmass with city-states of its
// own, the bigger landmasses taking more civs. The city-states' garrisons
// grow, and they gain walls on turn 40 (see CityStates). Every civ is a
// Monarchy, for good (owner's ruling, 2026-10-24).
public partial class GameSetup {
	// How many independent city-states the map has for each civ (owner's
	// change, 2026-10-23, from 3).
	public const int CityStatesPerCiv = 4;

	// The civs a game has: about a quarter fewer than the world size's
	// usual number, so the Kings have room (owner's ruling, 2026-10-23).
	public static int KingsAndNomadsCivs(int numberOfCivs) {
		return Math.Max(2, (int)Math.Round(numberOfCivs * 0.75));
	}
	public const int CityStateSize = 3;
	private const string KingsAndNomadsGovernment = "Monarchy";
	// The closest a city-state may be to a civ's start.
	private const int CityStateDistanceFromStart = 5;
	// The fewest city-states a civ's landmass should have for it.
	private const int MinCityStatesPerStart = 2;

	private static readonly string[] CityStateGarrison = ["Spearman", "Spearman", "Archer"];
	private static readonly string[] KingsHorde = ["Archer", "Archer", "Horseman", "Horseman", "Swordsman", "Warrior", "Worker"];

	private static void SetUpKingsAndNomadsWorld(WorldCharacteristics world) {
		// Played on continents or a pangaea, not an archipelago's islets
		// (owner's ruling, 2026-10-23).
		if (world.landform == WorldCharacteristics.Landform.Archipelago) {
			world.landform = WorldCharacteristics.Landform.Continents;
		}
		// The map's city sites: one for each civ's start, and the rest for
		// city-states.
		world.citiesPerCiv = 1 + CityStatesPerCiv;
	}

	private static void SetUpKingsAndNomadsRules(SaveGame save) {
		save.Rules.SettlersOnlyInConqueredCities = true;
		save.Rules.FallenCitiesGoIndependent = true;
		if (!save.Governments.Any(g => g.name == KingsAndNomadsGovernment)) {
			throw new InvalidOperationException($"The Kings & Nomads scenario needs the {KingsAndNomadsGovernment} government, which these rules don't have");
		}
		save.Rules.OnlyGovernment = KingsAndNomadsGovernment;
		save.VictoryConditions ??= new VictoryConditions();
		save.VictoryConditions.Regicide = true;
		save.VictoryConditions.MassRegicide = false;
		save.VictoryConditions.AutoPlaceKings = true;
		save.VictoryConditions.AllowConquestVictory = true;
		save.VictoryConditions.AllowEmpireVictory = true;
	}

	// A civ's horde, on its start; its King comes with Regicide. The civ is
	// a Monarchy.
	private void AddKingsHorde(SaveGame save, SavePlayer player, TileLocation location) {
		player.governmentId = save.Governments.Find(g => g.name == KingsAndNomadsGovernment).id;
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
		// Each civ starts on a landmass with city-states of its own to take
		// (owner's ruling, 2026-10-23: no civ stranded on an islet): one
		// with sites for its start and at least MinCityStatesPerStart more.
		// HEURISTIC (owner's "bias toward large continents"): the civs go
		// one by one to the landmass with the most land for each civ already
		// there, among those with sites to spare.
		Dictionary<int, int> land = map.tiles.Where(t => t.baseTerrain is not ("coast" or "sea" or "ocean"))
			.GroupBy(t => t.continent).ToDictionary(g => g.Key, g => g.Count());
		Dictionary<int, List<SaveTile>> sitesOn = sites.GroupBy(t => t.continent).ToDictionary(g => g.Key, g => g.ToList());
		Dictionary<int, int> civsOn = sitesOn.Keys.ToDictionary(k => k, _ => 0);
		bool Room(int continent) => sitesOn[continent].Count >= (civsOn[continent] + 1) * (1 + MinCityStatesPerStart);
		List<SaveTile> starts = [];
		while (starts.Count < civs) {
			List<int> open = sitesOn.Keys.Where(Room).ToList();
			if (open.Count == 0) {
				// Too crowded to give everyone city-states at home: the
				// biggest landmasses with any site left.
				open = sitesOn.Keys.Where(k => sitesOn[k].Any(t => !starts.Contains(t))).ToList();
				if (open.Count == 0) {
					throw new InvalidOperationException($"The map has {sites.Count} city sites, too few for {civs} civilizations");
				}
			}
			int continent = open.MaxBy(k => land.GetValueOrDefault(k) / (double)(civsOn[k] + 1));
			List<SaveTile> free = sitesOn[continent].Where(t => !starts.Contains(t)).ToList();
			List<SaveTile> others = starts.Where(t => t.continent == continent).ToList();
			// As far as can be from the civs already on the landmass.
			starts.Add(others.Count == 0 ? free[rand.Next(free.Count)] : free.MaxBy(t => others.Min(o => Distance(o, t))));
			civsOn[continent]++;
		}

		SavePlayer independents = save.Players.Find(p => p.isBarbarian);
		// The city-states: first those on each civ's landmass, up to
		// CityStatesPerCiv for each civ there, then anywhere else on those
		// landmasses, then overseas, never too near a start.
		bool FarEnough(SaveTile t) => !starts.Contains(t) && starts.All(s => Distance(s, t) >= CityStateDistanceFromStart);
		List<SaveTile> cityStates = [];
		foreach ((int continent, int count) in civsOn.Where(kv => kv.Value > 0)) {
			cityStates.AddRange(sitesOn[continent].Where(FarEnough).Take(count * CityStatesPerCiv));
		}
		int wanted = civs * CityStatesPerCiv;
		cityStates.AddRange(sites.Where(t => FarEnough(t) && !cityStates.Contains(t) && civsOn[t.continent] > 0)
			.Take(Math.Max(0, wanted - cityStates.Count)));
		// Any still short go overseas, for later.
		cityStates.AddRange(sites.Where(t => FarEnough(t) && !cityStates.Contains(t))
			.Take(Math.Max(0, wanted - cityStates.Count)));
		// Named for the capitals of the civilizations not in the game.
		Queue<string> names = new(save.Civilizations
			.Where(c => !c.isBarbarian && !taken.Contains(c.name) && c.cityNames.Count > 0)
			.OrderBy(_ => rand.Next())
			.Select(c => c.cityNames[0]));
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
			save.Cities.Add(city);
			foreach (string unit in CityStateGarrison) {
				AddUnit(save, independents, unit, location);
			}
		}
		return starts;
	}
}
