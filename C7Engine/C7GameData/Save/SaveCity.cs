using System.Collections.Generic;
using Serilog;

namespace C7GameData.Save {
	public class SaveCityResident {
		public ID citizenType;
		public string nationality;
		public ID city;
		public TileLocation tileWorked;
	}

	public class SaveCityBuilding {
		public string building;
		public ID builtByPlayer;
		public int year;
		public int totalCulture;
		public int turnsTowardFreeUnit;

		public SaveCityBuilding() { }

		public SaveCityBuilding(CityBuilding cityBuilding) {
			building = cityBuilding.building.name;
			builtByPlayer = cityBuilding.builtByPlayer.id;
			year = cityBuilding.year;
			totalCulture = cityBuilding.totalCulture;
			turnsTowardFreeUnit = cityBuilding.turnsTowardFreeUnit;
		}

		public CityBuilding ToCityBuilding(List<Building> buildings, List<Player> players) {
			return new CityBuilding {
				building = buildings.Find(buildingType => buildingType.name == building),
				builtByPlayer = players.Find(player => player.id == builtByPlayer),
				year = year,
				totalCulture = totalCulture,
				turnsTowardFreeUnit = turnsTowardFreeUnit,
			};
		}

		internal CityBuilding ToCityBuilding(SaveCity.Lookups lookups) {
			return new CityBuilding {
				building = SaveCity.Lookups.Find(lookups.buildingsByName, building),
				builtByPlayer = SaveCity.Lookups.Find(lookups.playersById, builtByPlayer),
				year = year,
				totalCulture = totalCulture,
				turnsTowardFreeUnit = turnsTowardFreeUnit,
			};
		}

	}

	public enum ProducibleType { INFLOW, BUILDING, UNIT };

	// An item in a city's production queue.
	public class SaveQueuedProducible {
		public string name;
		public ProducibleType type;
	}

	public class SaveCity : IHasID {
		public ID id { get; set; }
		public ID owner;
		public bool capital;
		public TileLocation location;
		public string producible;
		public ProducibleType producibleType;
		public string name;
		public int size;
		public Dictionary<string, int> perPlayerCulture = new();
		public int shieldsStored;
		public int foodStored;
		public int turnsOfUnhappinessDueToPopRushing;
		public bool celebrating;
		public bool isInCivilDisorder;
		// Resistance in a conquered city: the citizens resisting and the
		// player they're loyal to.
		public int resisters;
		public ID resistanceFrom;
		// Production was hurried this turn and can't be changed until it ends.
		public bool hurriedThisTurn;
		public List<SaveCityResident> residents = new List<SaveCityResident>();
		public List<SaveCityBuilding> buildings = [];
		public List<SaveQueuedProducible> productionQueue = [];

		public SaveCity() { }

		// A copy sharing everything with this one, for a LAN host to change
		// a little for one guest (see C7Engine.Network.SnapshotFilter).
		internal SaveCity ShallowCopy() {
			return (SaveCity)MemberwiseClone();
		}

		public SaveCity(City city) {
			id = city.id;
			owner = city.owner.id;
			capital = city.capital;
			location = new TileLocation(city.location);
			name = city.name;
			producible = city.itemBeingProduced.name;
			producibleType = TypeOf(city.itemBeingProduced);
			productionQueue = city.productionQueue.ConvertAll(p => new SaveQueuedProducible { name = p.name, type = TypeOf(p) });
			shieldsStored = city.shieldsStored;
			foodStored = city.foodStored;
			turnsOfUnhappinessDueToPopRushing = city.turnsOfUnhappinessDueToPopRushing;
			celebrating = city.celebrating;
			isInCivilDisorder = city.isInCivilDisorder;
			resisters = city.resisters;
			resistanceFrom = city.resistanceFrom?.id;
			hurriedThisTurn = city.hurriedThisTurn;
			residents = city.residents.ConvertAll(resident => {
				return new SaveCityResident {
					nationality = resident.nationality?.name,
					city = resident.city.id,
					tileWorked = new TileLocation(resident.tileWorked),
					citizenType = resident.citizenType.Id,
				};
			});
			buildings = city.constructed_buildings.ConvertAll(building => new SaveCityBuilding(building));

			foreach (KeyValuePair<Player, int> keyValuePair in city.perPlayerCulture) {
				perPlayerCulture.Add(keyValuePair.Key.id.ToString(), keyValuePair.Value);
			}
		}

		// Lookup tables for converting many cities, built once rather than
		// searching the lists for every city. Like List.Find, the first match
		// wins.
		internal class Lookups {
			internal readonly Dictionary<ID, Player> playersById = new();
			internal readonly Dictionary<string, Player> playersByIdString = new();
			internal readonly Dictionary<string, UnitPrototype> unitPrototypesByName = new();
			internal readonly Dictionary<string, Civilization> civilizationsByName = new();
			internal readonly Dictionary<string, Building> buildingsByName = new();
			internal readonly Dictionary<ID, CitizenType> citizenTypesById = new();
			internal readonly Dictionary<string, Inflow> inflowsByName = new();
			internal readonly CitizenType defaultCitizenType;

			internal Lookups(List<Player> players,
							List<UnitPrototype> unitPrototypes,
							List<Civilization> civilizations,
							List<Building> buildings,
							List<CitizenType> citizenTypes,
							List<Inflow> inflows) {
				foreach (Player p in players) {
					if (p.id is not null) {
						playersById.TryAdd(p.id, p);
						playersByIdString.TryAdd(p.id.ToString(), p);
					}
				}
				foreach (UnitPrototype up in unitPrototypes) {
					if (up.name != null) unitPrototypesByName.TryAdd(up.name, up);
				}
				foreach (Civilization civ in civilizations) {
					if (civ.name != null) civilizationsByName.TryAdd(civ.name, civ);
				}
				foreach (Building b in buildings) {
					if (b.name != null) buildingsByName.TryAdd(b.name, b);
				}
				foreach (CitizenType ct in citizenTypes) {
					if (ct.Id is not null) citizenTypesById.TryAdd(ct.Id, ct);
				}
				foreach (Inflow inflow in inflows) {
					if (inflow.name != null) inflowsByName.TryAdd(inflow.name, inflow);
				}
				defaultCitizenType = citizenTypes.Find(x => x.IsDefaultCitizen);
				fallbackProducible = inflows.Count > 0 ? inflows[0]
					: unitPrototypes.Count > 0 ? unitPrototypes[0]
					: null;
			}

			// What a city producing something unknown produces instead.
			internal readonly IProducible fallbackProducible;

			internal static T Find<K, T>(Dictionary<K, T> dict, K key) where T : class {
				return key is not null && dict.TryGetValue(key, out T value) ? value : null;
			}
		}

		private static ProducibleType TypeOf(IProducible producible) {
			return producible switch {
				Inflow => ProducibleType.INFLOW,
				UnitPrototype => ProducibleType.UNIT,
				Building => ProducibleType.BUILDING,
			};
		}

		private static IProducible FindProducible(Lookups lookups, ProducibleType type, string name) {
			return type switch {
				ProducibleType.INFLOW => Lookups.Find(lookups.inflowsByName, name),
				ProducibleType.UNIT => Lookups.Find(lookups.unitPrototypesByName, name),
				ProducibleType.BUILDING => Lookups.Find(lookups.buildingsByName, name),
			};
		}

		public City ToCity(GameMap gameMap,
							List<Player> players,
							List<UnitPrototype> unitPrototypes,
							List<Civilization> civilizations,
							List<Building> buildings,
							List<CitizenType> citizenTypes,
							List<Inflow> inflows) {
			return ToCity(gameMap, new Lookups(players, unitPrototypes, civilizations, buildings, citizenTypes, inflows));
		}

		internal City ToCity(GameMap gameMap, Lookups lookups) {
			Player cityOwner = Lookups.Find(lookups.playersById, owner)
				?? throw new KeyNotFoundException($"City {name} ({id}) is owned by unknown player {owner}");
			IProducible itemBeingProduced = FindProducible(lookups, producibleType, producible);
			if (itemBeingProduced == null) {
				itemBeingProduced = lookups.fallbackProducible
					?? throw new KeyNotFoundException($"City {name} ({id}) is producing unknown {producibleType} {producible}");
				Log.Warning("City {City} is producing unknown {Type} {Producible}; producing {Fallback} instead", name, producibleType, producible, itemBeingProduced.name);
			}
			City city = new City{
				id = id,
				location = gameMap.tileAt(location.X, location.Y),
				owner = cityOwner,
				name = name,
				itemBeingProduced = itemBeingProduced,
				foodStored = foodStored,
				turnsOfUnhappinessDueToPopRushing = turnsOfUnhappinessDueToPopRushing,
				celebrating = celebrating,
				isInCivilDisorder = isInCivilDisorder,
				resisters = resisters,
				resistanceFrom = Lookups.Find(lookups.playersById, resistanceFrom),
				hurriedThisTurn = hurriedThisTurn,
				capital = capital,
				constructed_buildings = [],
			};
			foreach (SaveCityBuilding building in this.buildings) {
				CityBuilding cityBuilding = building.ToCityBuilding(lookups);
				if (cityBuilding.building == null) {
					Log.Warning("City {City} has unknown building {Building}, which is left out", name, building.building);
					continue;
				}
				if (cityBuilding.builtByPlayer == null) {
					Log.Warning("The {Building} in {City} was built by unknown player {Player}; crediting the city's owner", building.building, name, building.builtByPlayer);
					cityBuilding.builtByPlayer = cityOwner;
				}
				city.constructed_buildings.Add(cityBuilding);
			}

			city.SetStoredShields(shieldsStored);
			foreach (SaveQueuedProducible queued in productionQueue ?? []) {
				IProducible item = FindProducible(lookups, queued.type, queued.name);
				if (item != null) {
					city.productionQueue.Add(item);
				} else {
					Log.Warning("City {City} has unknown {Type} {Producible} queued, which is left out", name, queued.type, queued.name);
				}
			}

			foreach (KeyValuePair<string, int> keyValuePair in perPlayerCulture) {
				Player player = Lookups.Find(lookups.playersByIdString, keyValuePair.Key);
				if (player == null) {
					Log.Warning("City {City} has culture from unknown player {Player}, which is left out", name, keyValuePair.Key);
					continue;
				}
				city.perPlayerCulture[player] = keyValuePair.Value;
			}

			city.residents = residents.ConvertAll(resident => {
				CitizenType citizenType = Lookups.Find(lookups.citizenTypesById, resident.citizenType);
				if (citizenType == null) {
					Log.Warning("A citizen of {City} is of unknown type {Type}; making them a worker", name, resident.citizenType);
					citizenType = lookups.defaultCitizenType;
				}
				// Older saves can lack a nationality, which is left unknown.
				Civilization nationality = Lookups.Find(lookups.civilizationsByName, resident.nationality);
				if (nationality == null && resident.nationality != null) {
					Log.Warning("A citizen of {City} has unknown nationality {Nationality}; giving them the owner's", name, resident.nationality);
					nationality = cityOwner.civilization;
				}
				return new CityResident {
					citizenType = citizenType,
					nationality = nationality,
					tileWorked = gameMap.tileAt(resident.tileWorked.X, resident.tileWorked.Y),
					city = city,
				};
			});

			// Fill in the back pointers. A citizen who isn't working a tile
			// (a specialist, or one not yet given a tile) has Tile.NONE, which
			// all such citizens share, so it gets none.
			foreach (CityResident cr in city.residents) {
				if (cr.tileWorked != Tile.NONE) {
					cr.tileWorked.personWorkingTile = cr;
				}
			}

			// Scenarios don't specify the citizens of each city, only the city
			// size. So we need to add the appropriate residents here.
			//
			// We wait to assign them to tiles until after tile ownership has
			// been established in SaveGame.cs.
			if (city.residents.Count == 0) {
				CitizenType ct = lookups.defaultCitizenType;
				for (int i = 0; i < size; ++i) {
					CityResident newResident = new() {
						citizenType = ct,
						nationality = city.owner.civilization,
						city = city
					};
					city.residents.Add(newResident);
				}
			}

			return city;
		}
	}
}
