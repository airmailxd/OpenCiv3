using C7Engine;
using C7Engine.AI;

namespace C7GameData;

// Join City: a unit with the ability adds its population cost to the city
// it stands in, and disappears: "The population cost of the unit will then
// be added the city population, and the unit will disappear" (Civ3 editor
// help, "Join City").
public partial class MapUnit {
	// Whether the unit can join the city it stands in. Civ3 offers the order
	// (FUN_005d0670, order 0x20001000, the BIQ's PRTO "Join City" worker
	// action) to a unit with moves left in a city that can still grow
	// (FUN_004b8d50: not at its aqueduct or hospital size cap) and isn't
	// starving (a food surplus of 0 or more): "Workers can no longer join
	// starving cities" (PTW 1.01f readme).
	public bool CanJoinCity() {
		return unitType.actions.Contains(UnitAction.JoinCity)
			&& movementPoints.canMove
			&& !IsLockedInArmy()
			&& location.cityAtTile is City city
			&& city.owner == owner
			&& city.CanBeJoined();
	}

	// Adds the unit's people to the city it stands in, and uses the unit up.
	public bool JoinCity() {
		if (!CanJoinCity()) {
			log.Warning("{Unit} can't join a city at {Tile}", this, location);
			return false;
		}
		AddPopulationTo(location.cityAtTile);
		return true;
	}

	// Adds the unit's population cost to the city one citizen at a time, as
	// Civ3 does (FUN_005c2320): each citizen is added only while the city
	// can still grow, so a join that would take a city past its aqueduct or
	// hospital cap fills it to the cap and the rest are lost. The citizens
	// keep the unit's nationality: captured settlers "if ordered to join a
	// city, become foreign nationals" (Civilopedia, GCON_Nationality). Then
	// the unit is gone.
	internal void AddPopulationTo(City city) {
		GameData gameData = EngineStorage.gameData;
		CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		for (int i = 0; i < unitType.populationCost && city.CanGrowPopulationByOne(gameData); ++i) {
			CityResident resident = new() {
				citizenType = defaultCitizen,
				nationality = nationality ?? owner.civilization,
				city = city
			};
			city.AddCitizen(resident);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, resident, manageMoods: city.governor.manageMoods);
		}
		city.RecalculateCitizenMoods(gameData);
		log.Information("{Unit} joined {City}", this, city);
		RemoveFromPlay();
	}
}
