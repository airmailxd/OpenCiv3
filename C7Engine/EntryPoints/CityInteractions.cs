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
			owner.neverHadCityOrSettler = false;
			tileWithNewCity.cityAtTile = newCity;
			// Building the city may clear the terrain, which changes what units
			// nearby can see.
			TileChangeJournal.RecordTerrainChange(tileWithNewCity);

			CityResident firstResident = new CityResident();
			firstResident.city = newCity;
			firstResident.nationality = owner.civilization;
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
				ReassignAllCitizens(gameData, city);
			}

			city.RecalculateCitizenMoods(gameData);
		}

		// Takes every citizen of the city off its tile or specialty and
		// assigns them afresh, managing moods with entertainers if need be.
		// The citizens keep their nationalities.
		internal static void ReassignAllCitizens(GameData gameData, City city) {
			CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			List<Civilization> nationalities = city.residents.Select(r => r.nationality ?? city.owner.civilization).ToList();
			// The resisters are the last citizens, and stay so (see
			// City.StartResistance).
			int resisters = city.resisters;
			Player resistanceFrom = city.resistanceFrom;
			city.RemoveAllCitizens();

			// Nothing the assignments depend on changes while the citizens
			// are reassigned, apart from which tiles are worked, so the tile
			// yields can be shared between them.
			CityTileAssignmentAI.AssignmentContext context = new(gameData, city);
			foreach (Civilization nationality in nationalities) {
				CityResident newResident = new() {
					citizenType = defaultCitizen,
					nationality = nationality,
					city = city
				};
				city.AddCitizen(newResident);
				CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident, manageMoods: true, context);
			}
			city.resisters = resisters;
			city.resistanceFrom = resistanceFrom;
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

			// Citizens of unknown nationality (e.g. from older saves) are
			// taken to be the old owner's, so that they may resist.
			foreach (CityResident r in city.residents) {
				r.nationality ??= oldOwner.civilization;
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
			captor.neverHadCityOrSettler = false;
			city.owner = captor;
			city.perPlayerCulture.TryAdd(captor, 0);
			gameData.OnCityOwnerChanged(city);
			city.isInCivilDisorder = false;
			city.hurriedThisTurn = false;
			city.SetStoredShields(0);
			// With its shields gone, so are any from a cleared forest.
			city.receivedForestShields = false;

			gameData.UpdateTileOwners();
			BarbarianInteractions.DisperseCampsWithinBorders(gameData);
			gameData.InvalidateCachedTradeNetwork();

			// Choosing production needs the trade network to know the new owner.
			city.SetItemBeingProduced(ChooseProducible.Choose(city, captor));
			city.ClearProductionQueue();

			log.Information("{Captor} captured {City} from {OldOwner}, plundering {Plunder} gold", captor, city, oldOwner, plunder);
			new MsgCityCaptured(city, oldOwner).send();
			if (captor.isHuman) {
				new MsgShowMilitaryAdvisorPopup(captor, $"We have captured {city.name} and plundered {plunder} gold!", happy: true).send();
				// A captor can't raze what is now their only city.
				if (MayAbandon(captor, city, gameData)) {
					new MsgDisplayRazeCityPopup(captor, city).send();
				}
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
		// old owner's empire. As in Civ3, the old owner's units in it join the
		// new owner along with the city.
		//
		// viaDeal is for a city given away in a deal (see
		// Player.ExecuteDeal), which per the project owner doesn't resist its
		// new owner. Its old owner keeps their units, which per the project
		// owner move to the old owner's nearest city (see
		// MapUnit.FindNearestOwnCity). With no other city for them, they
		// leave for the nearest free tile, and are lost if there's nowhere
		// for them to go (UNVERIFIED, no Civ3 source found).
		public static void TransferCity(City city, Player newOwner, bool viaDeal = false) {
			GameData gameData = EngineStorage.gameData;
			Player oldOwner = city.owner;
			Tile tile = city.location;

			List<MapUnit> oldOwnersUnits = tile.unitsOnTile.Where(u => u.owner == oldOwner).ToList();
			// Where the units given notice go is found while the city and the
			// land around it are still the old owner's, so their paths out
			// aren't blocked by the new owner's borders. Units loaded on
			// another (in an army or aboard a ship) go with it.
			Dictionary<MapUnit, City> destinations = new();
			if (viaDeal) {
				foreach (MapUnit unit in oldOwnersUnits) {
					if (unit.Carrier() == null) {
						destinations[unit] = unit.FindNearestOwnCity(except: city);
					}
				}
			} else {
				foreach (MapUnit unit in oldOwnersUnits) {
					gameData.CaptureUnit(unit, newOwner);
				}
			}

			foreach (CityBuilding cb in city.constructed_buildings.ToList()) {
				if (cb.building.isCenterOfEmpire || cb.building.isSmallWonder) {
					city.RemoveBuilding(cb);
				}
			}

			bool wasCapital = city.capital;
			city.capital = false;
			oldOwner.cities.Remove(city);
			newOwner.cities.Add(city);
			newOwner.neverHadCityOrSettler = false;
			city.owner = newOwner;
			city.perPlayerCulture.TryAdd(newOwner, 0);
			gameData.OnCityOwnerChanged(city);
			city.isInCivilDisorder = false;
			city.hurriedThisTurn = false;

			gameData.UpdateTileOwners();
			BarbarianInteractions.DisperseCampsWithinBorders(gameData);
			gameData.InvalidateCachedTradeNetwork();

			if (viaDeal) {
				// Those carrying others first, so they take them along.
				foreach (MapUnit unit in oldOwnersUnits.OrderBy(u => destinations.ContainsKey(u) ? 0 : 1)) {
					// Units carried away with another are already gone.
					if (unit.location != tile) {
						continue;
					}
					if (!destinations.TryGetValue(unit, out City destination)) {
						destination = unit.FindNearestOwnCity(except: city);
					}
					if (destination != null && destination.owner == oldOwner) {
						unit.WithdrawToCity(destination);
					} else if (!unit.WithdrawToNearestFreeTile()) {
						gameData.RemoveUnit(unit);
					}
				}
			}

			city.SetItemBeingProduced(ChooseProducible.Choose(city, newOwner));
			city.ClearProductionQueue();

			log.Information("{City} changed hands from {OldOwner} to {NewOwner}{ViaDeal}", city, oldOwner, newOwner, viaDeal ? " in a deal" : "");
			new MsgCityCaptured(city, oldOwner).send();

			gameData.CheckForCivDestructionAndNotifyUi(oldOwner);
			if (wasCapital) {
				MovePalaceAfterLosingCapital(oldOwner, tile);
			}

			oldOwner.DoCorruptionCalculations(gameData);
			newOwner.DoCorruptionCalculations(gameData);
		}

		// Whether the player may abandon (or raze) their city: a city they
		// captured, which they are asked whether to keep, or any city of
		// theirs from its menu, which Civ3 offers: "right-click on
		// the cities you want to remove, and you'll get a pop-up menu that
		// has 'Abandon city' way down at the bottom of the menu"
		// (https://forums.civfanatics.com/threads/getting-rid-of-unwanted-cities.353125/).
		// A player may never abandon their only city, per the project owner.
		public static bool MayAbandon(Player player, City city, GameData gameData) {
			return WhyCannotAbandon(player, city) == null;
		}

		// Why the player may not abandon the city, for the UI to show, or
		// null if they may.
		public static string WhyCannotAbandon(Player player, City city) {
			if (city == null || city.owner != player) {
				return "It is not our city.";
			}
			if (player.cities.Count <= 1) {
				return "We cannot abandon our only city.";
			}
			return null;
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
			BarbarianInteractions.DisperseCampsWithinBorders(gameData);

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
