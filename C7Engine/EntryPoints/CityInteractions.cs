using System.Linq;
using C7Engine.AI;

namespace C7Engine {
	using System;
	using C7GameData;

	public class CityInteractions {
		private static Serilog.ILogger log = Serilog.Log.ForContext<CityInteractions>();

		public static City BuildCity(Tile tileWithNewCity, Player owner, string name) {
			GameData gameData = EngineStorage.gameData;
			City newCity = new City(tileWithNewCity, owner, name, gameData.ids.CreateID("city"));
			if (owner.cities.Count == 0) {
				newCity.capital = true;
				newCity.AddBuilding(gameData.Buildings.Find(x => x.isCenterOfEmpire));
			}
			gameData.cities.Add(newCity);
			owner.cities.Add(newCity);
			tileWithNewCity.cityAtTile = newCity;

			CityResident firstResident = new CityResident();
			firstResident.city = newCity;
			firstResident.citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			newCity.AddCitizen(firstResident);

			// Update owners before we assign the citizen so the tile owners are
			// accurate. We do this after adding the resident though, because
			// cities with zero residents are considered destroyed.
			gameData.UpdateTileOwners();

			// Now that the city exists and its borders have been established,
			// invalidate the trade network so it can be recomputed with this
			// new information.
			gameData.InvalidateCachedTradeNetwork();

			// Assigning citizens to tiles requires knowing luxuries, so this
			// has to happen after invalidating the trade network.
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, firstResident);

			newCity.SetItemBeingProduced(ChooseProducible.Choose(newCity, owner));

			// Redo corruption calculations after a city is created, since it
			// may change rank corruption values.
			owner.DoCorruptionCalculations(gameData);

			return newCity;
		}

		// The chance that each ordinary building in a captured city is
		// destroyed in the fighting.
		private const double BuildingLossChanceOnCapture = 0.5;

		// Hands a city over to the player who took it. Cities of size one are
		// destroyed instead. The city loses a citizen, its production and
		// some of its buildings, and the captor plunders the city's share of
		// the old owner's treasury.
		public static void CaptureCity(City city, Player captor) {
			GameData gameData = EngineStorage.gameData;
			Player oldOwner = city.owner;
			Tile tile = city.location;

			if (city.residents.Count <= 1) {
				DestroyCity(city);
				return;
			}

			tile.DisbandNonDefendingUnits(oldOwner);

			int totalPopulation = oldOwner.cities.Sum(c => c.residents.Count);
			int plunder = totalPopulation > 0 ? oldOwner.gold * city.residents.Count / totalPopulation : 0;
			oldOwner.gold -= plunder;
			captor.gold += plunder;

			city.RemoveCitizens(1);

			// The palace and small wonders don't survive a change of owner,
			// great wonders always do, and other buildings may be destroyed.
			foreach (CityBuilding cb in city.constructed_buildings.ToList()) {
				Building b = cb.building;
				bool lost = b.isCenterOfEmpire || b.isSmallWonder
					|| (!b.IsGreatWonder() && GameData.rng.NextDouble() < BuildingLossChanceOnCapture);
				if (lost) {
					city.RemoveBuilding(cb);
				}
			}

			bool wasCapital = city.capital;
			city.capital = false;
			oldOwner.cities.Remove(city);
			captor.cities.Add(city);
			city.owner = captor;
			city.perPlayerCulture.TryAdd(captor, 0);
			city.isInCivilDisorder = false;
			city.SetStoredShields(0);

			gameData.UpdateTileOwners();
			gameData.InvalidateCachedTradeNetwork();

			// Choosing production needs the trade network to know the new owner.
			city.SetItemBeingProduced(ChooseProducible.Choose(city, captor));

			log.Information($"{captor} captured {city} from {oldOwner}, plundering {plunder} gold");
			new MsgCityCaptured(city, oldOwner).send();
			if (captor.isHuman) {
				new MsgShowMilitaryAdvisorPopup(captor, $"We have captured {city.name} and plundered {plunder} gold!", happy: true).send();
			}
			if (oldOwner.isHuman) {
				new MsgShowMilitaryAdvisorPopup(oldOwner, $"{city.name} has fallen to the {captor.civilization.noun}!", happy: false).send();
			}

			gameData.CheckForCivDestructionAndNotifyUi(oldOwner);

			oldOwner.DoCorruptionCalculations(gameData);
			captor.DoCorruptionCalculations(gameData);
		}

		public static void DestroyCity(City city) {
			DestroyCity(city.location);
		}
		public static void DestroyCity(Tile tile) {
			DestroyCity(tile.XCoordinate, tile.YCoordinate);
		}

		public static void DestroyCity(int X, int Y) {
			GameData gameData = EngineStorage.gameData;
			Tile tile = gameData.map.tileAt(X, Y);
			Player owner = tile.cityAtTile.owner;

			// TODO: this will get removed eventually, since we will be capturing non-combat units,
			// plus, it doesn't what it says, if the city is abandoned for example, ALL units are removed.
			// I am leaving it as it is for the moment.
			tile.DisbandNonDefendingUnits(owner);

			tile.cityAtTile.RemoveAllCitizens();
			tile.cityAtTile.owner.cities.Remove(tile.cityAtTile);

			gameData.cities.Remove(tile.cityAtTile);
			gameData.UpdateTileOwnersOnCityDestruction(tile.cityAtTile);

			new MsgCityDestroyed(tile.cityAtTile).send();

			gameData.CheckForCivDestructionAndNotifyUi(owner);

			tile.cityAtTile = null;

			// Now that the city has been destroyed and tile owners updated,
			// invalidate the trade network in case removing this city cut off
			// resource access.
			gameData.InvalidateCachedTradeNetwork();

			// Redo corruption calculations after a city is destroyed, since it
			// may change rank corruption values.
			owner.DoCorruptionCalculations(gameData);
		}
	}
}
