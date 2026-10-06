using System.Linq;
using C7Engine.AI;

namespace C7Engine {
	using System;
	using System.Collections.Generic;
	using C7GameData;

	public class CityInteractions {
		private static Serilog.ILogger log = Serilog.Log.ForContext<CityInteractions>();

		public static City BuildCity(Tile tileWithNewCity, Player owner, string name) {
			GameData gameData = EngineStorage.gameData;

			City newCity = new City(tileWithNewCity, owner, name, gameData.ids.CreateID("city"));
			// A civ without a capital gets its palace in the next city it
			// founds, even if it already holds captured cities (which lose
			// their palace on capture).
			if (!owner.cities.Any(c => c.IsCapital())) {
				newCity.capital = true;
				newCity.AddBuilding(gameData.Buildings.Find(x => x.isCenterOfEmpire));
			}
			gameData.cities.Add(newCity);
			owner.cities.Add(newCity);
			owner.citiesFounded++;
			tileWithNewCity.cityAtTile = newCity;
			// Building the city may clear the terrain, which changes what units
			// nearby can see.
			TileChangeJournal.RecordTerrainChange(tileWithNewCity);

			CityResident firstResident = new CityResident();
			firstResident.city = newCity;
			firstResident.citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			newCity.AddCitizen(firstResident);

			// Update owners before we assign the citizen so the tile owners are
			// accurate. We do this after adding the resident though, because
			// cities with zero residents are considered destroyed.
			gameData.UpdateTileOwners();

			// Barbarian camps inside the new city's borders are dispersed.
			int campsDispersed = 0;
			foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
				if (camp.owningCity == newCity && BarbarianInteractions.DisperseCamp(gameData, camp, owner)) {
					++campsDispersed;
				}
			}
			if (campsDispersed > 0 && owner.isHuman) {
				string camps = campsDispersed == 1 ? "a barbarian encampment" : $"{campsDispersed} barbarian encampments";
				new MsgShowMilitaryAdvisorPopup(owner, $"Our new city {name} dispersed {camps} and earned {campsDispersed * BarbarianInteractions.CampDispersalGold} gold!", happy: true).send();
			}

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

		// Reassigns a city's citizens after its owner clicks a tile on the city
		// screen: a worked tile's citizen becomes a specialist, an unworked tile
		// takes the citizen on the worst tile, and the city center reassigns
		// everyone.
		public static void ReassignCitizen(GameData gameData, City city, Tile tile) {
			// We only support clicking on workable tiles or the city center.
			if (!city.GetWorkableTiles().Contains(tile) && tile != city.location) {
				return;
			}

			// We can't assign citizens to other cities.
			if (tile.cityAtTile != null && tile.cityAtTile != city) {
				return;
			}

			// We can't assign citizens to tiles worked by other cities.
			if (tile.personWorkingTile != null && tile.personWorkingTile.city != city) {
				return;
			}

			CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);

			if (tile.personWorkingTile != null && tile.personWorkingTile.city == city) {
				// If we're already working a tile and click on it, turn the
				// citizen into a specialist.
				CityResident resident = tile.personWorkingTile;
				tile.personWorkingTile = null;
				resident.tileWorked = Tile.NONE;
				resident.citizenType = city.owner.GetKnownSpecialists(gameData)[0];
			} else if (tile.cityAtTile == null) {
				// We've clicked on an unworked tile, move the "worst" citizen to
				// that tile.
				int worstYield = int.MaxValue;
				CityResident worst = null;

				foreach (CityResident cr in city.residents) {
					int tileYield = cr.tileWorked.FoodYield(city.owner).yield +
									cr.tileWorked.ProductionYield(city.owner).yield +
									cr.tileWorked.CommerceYield(city.owner).yield;
					if (tileYield < worstYield) {
						worstYield = tileYield;
						worst = cr;
					}
				}

				// Move the worst citizen to our new tile, being sure to update
				// backpointers from the tile.
				worst.tileWorked.personWorkingTile = null;
				worst.tileWorked = tile;
				tile.personWorkingTile = worst;
				worst.citizenType = defaultCitizen;
			} else {
				// We've clicked the city center, so re-assign all the citizens
				// using the basic AI, but specify that we want to manage moods by
				// using entertainers if necessary.
				//
				// TODO: This throws away existing nationalities, fix that.
				int numResidents = city.residents.Count;
				city.RemoveAllCitizens();

				for (int i = 0; i < numResidents; ++i) {
					CityResident newResident = new() {
						citizenType = defaultCitizen,
						nationality = city.owner.civilization,
						city = city
					};
					city.AddCitizen(newResident);
					CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident, manageMoods: true);
				}
			}

			city.RecalculateCitizenMoods(gameData);
		}

		// Changes a specialist to the next kind of specialist its owner knows.
		public static void CycleSpecialist(GameData gameData, City city, int residentIndex) {
			if (residentIndex < 0 || residentIndex >= city.residents.Count) {
				return;
			}
			CityResident resident = city.residents[residentIndex];
			List<CitizenType> specialistTypes = city.owner.GetKnownSpecialists(gameData);
			int index = specialistTypes.FindIndex(x => x.Id == resident.citizenType.Id);
			if (index < 0) {
				return;
			}
			resident.citizenType = specialistTypes[(index + 1) % specialistTypes.Count];
			city.RecalculateCitizenMoods(gameData);
		}

		// The chance that each ordinary building in a captured city is
		// destroyed in the fighting.
		private const double BuildingLossChanceOnCapture = 0.5;

		// Whether a city is worth keeping when taken: it has more than one
		// citizen, or its old owner's culture has pushed its borders out. Any
		// other city is destroyed when captured.
		public static bool SurvivesCapture(City city) {
			return city.residents.Count > 1 || city.GetBorderExpansionLevel() > 1;
		}

		// Hands a city over to the player who took it, unless it doesn't
		// survive capture, in which case it is destroyed. The city loses a
		// citizen (never its last), its production and some of its
		// buildings, and the captor plunders the city's share of the old
		// owner's treasury. Its borders fall back to the captor's own culture
		// there. A human captor is then asked whether to keep or raze it.
		public static void CaptureCity(City city, Player captor) {
			GameData gameData = EngineStorage.gameData;
			Player oldOwner = city.owner;
			Tile tile = city.location;

			// Losing the capital destroys the spaceship.
			if (city.capital) {
				SpaceRace.DestroySpaceship(oldOwner, captor);
			}

			if (!SurvivesCapture(city)) {
				DestroyCity(city);
				return;
			}

			tile.DisbandNonDefendingUnits(oldOwner);

			int totalPopulation = oldOwner.cities.Sum(c => c.residents.Count);
			int plunder = totalPopulation > 0 ? oldOwner.gold * city.residents.Count / totalPopulation : 0;
			oldOwner.gold -= plunder;
			captor.gold += plunder;

			if (city.residents.Count > 1) {
				city.RemoveCitizens(1);
			}

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
			gameData.OnCityOwnerChanged(city);
			city.isInCivilDisorder = false;
			city.hurriedThisTurn = false;
			city.SetStoredShields(0);

			gameData.UpdateTileOwners();
			gameData.InvalidateCachedTradeNetwork();

			// Choosing production needs the trade network to know the new owner.
			city.SetItemBeingProduced(ChooseProducible.Choose(city, captor));
			city.ClearProductionQueue();

			log.Information("{Captor} captured {City} from {OldOwner}, plundering {Plunder} gold", captor, city, oldOwner, plunder);
			new MsgCityCaptured(city, oldOwner).send();
			if (captor.isHuman) {
				new MsgShowMilitaryAdvisorPopup(captor, $"We have captured {city.name} and plundered {plunder} gold!", happy: true).send();
				new MsgDisplayRazeCityPopup(captor, city).send();
			}
			if (oldOwner.isHuman) {
				new MsgShowMilitaryAdvisorPopup(oldOwner, $"{city.name} has fallen to the {captor.civilization.noun}!", happy: false).send();
			}

			gameData.CheckForCivDestructionAndNotifyUi(oldOwner);
			if (wasCapital) {
				MovePalaceAfterLosingCapital(oldOwner, tile);
			}

			oldOwner.DoCorruptionCalculations(gameData);
			captor.DoCorruptionCalculations(gameData);
		}

		// Hands a city over to another player peacefully, as when it is
		// incited to revolt: unlike a capture it keeps its citizens and
		// buildings, except the palace and small wonders, which belong to the
		// old owner's empire. Units in it are left as they are.
		public static void TransferCity(City city, Player newOwner) {
			GameData gameData = EngineStorage.gameData;
			Player oldOwner = city.owner;
			Tile tile = city.location;

			foreach (CityBuilding cb in city.constructed_buildings.ToList()) {
				if (cb.building.isCenterOfEmpire || cb.building.isSmallWonder) {
					city.RemoveBuilding(cb);
				}
			}

			bool wasCapital = city.capital;
			city.capital = false;
			oldOwner.cities.Remove(city);
			newOwner.cities.Add(city);
			city.owner = newOwner;
			city.perPlayerCulture.TryAdd(newOwner, 0);
			gameData.OnCityOwnerChanged(city);
			city.isInCivilDisorder = false;
			city.hurriedThisTurn = false;

			gameData.UpdateTileOwners();
			gameData.InvalidateCachedTradeNetwork();

			city.SetItemBeingProduced(ChooseProducible.Choose(city, newOwner));

			log.Information("{City} changed hands from {OldOwner} to {NewOwner}", city, oldOwner, newOwner);
			new MsgCityCaptured(city, oldOwner).send();

			gameData.CheckForCivDestructionAndNotifyUi(oldOwner);
			if (wasCapital) {
				MovePalaceAfterLosingCapital(oldOwner, tile);
			}

			oldOwner.DoCorruptionCalculations(gameData);
			newOwner.DoCorruptionCalculations(gameData);
		}

		private static void MovePalaceAfterLosingCapital(Player player, Tile oldCapitalLocation) {
			City newCapital = player.RelocatePalace(EngineStorage.gameData, oldCapitalLocation);
			if (newCapital != null && player.isHuman) {
				new MsgShowMilitaryAdvisorPopup(player, $"With our capital lost, the palace has been rebuilt in {newCapital.name}.", happy: false).send();
			}
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
			bool wasCapital = tile.cityAtTile.capital;
			if (wasCapital) {
				SpaceRace.DestroySpaceship(owner, null);
			}

			// TODO: this will get removed eventually, since we will be capturing non-combat units,
			// plus, it doesn't what it says, if the city is abandoned for example, ALL units are removed.
			// I am leaving it as it is for the moment.
			tile.DisbandNonDefendingUnits(owner);

			City city = tile.cityAtTile;
			city.RemoveAllCitizens();
			owner.cities.Remove(city);
			gameData.cities.Remove(city);

			// Clear the tile before updating tile owners, which brings every
			// player's active tiles up to date: units on the tile see less
			// without the city.
			tile.cityAtTile = null;
			TileChangeJournal.RecordTerrainChange(tile);

			gameData.UpdateTileOwnersOnCityDestruction(city);

			new MsgCityDestroyed(city).send();

			gameData.CheckForCivDestructionAndNotifyUi(owner);

			if (wasCapital) {
				MovePalaceAfterLosingCapital(owner, tile);
			}

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
