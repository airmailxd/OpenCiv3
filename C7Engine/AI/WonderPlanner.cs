using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// How the AI plans great wonders, on trial (see AiExperiments):
	//
	//   wonder-value  values a great wonder by what its effect is worth to
	//                 this civ now, rather than mostly by its culture.
	//   wonder-timing while the civ is expanding, builds great wonders only
	//                 in its most productive city, one at a time.
	//
	// Without the switches the AI scores great wonders like any other
	// building. Other ideas tried and dropped, as they did no better (see
	// the commit that removed them): building wonders only in the more
	// productive cities, a few at a time; only ones finished within 30
	// turns; shying away from wonders many known rivals can build; and
	// moving the shields of a wonder lost to a rival to the best item that
	// holds them rather than the most expensive one.
	public static class WonderPlanner {
		public const string Value = "wonder-value";
		public const string Timing = "wonder-timing";

		// Under wonder-timing, while the civ is still expanding only its most
		// productive city builds a great wonder, one at a time: the others
		// build the settlers, workers and escorts that claim the land.
		internal static bool TimingAllows(ChooseProducible.ProducibleStats stats, City city, Building wonder) {
			if (city.itemBeingProduced == wonder || !stats.InExpansionPhase) {
				return true;
			}
			Plan plan = stats.WonderPlan;
			return plan.Rank(city) == 0 && plan.otherWonderBuilds == 0;
		}

		// What a civ's cities are doing, gathered once per production
		// decision.
		internal sealed class Plan {
			// Useful shields per turn of each city, most productive first.
			public readonly List<(City city, int shields)> byProduction;
			// Cities other than the deciding one building a great wonder.
			public readonly int otherWonderBuilds;

			public Plan(City city, Player player) {
				byProduction = player.cities.Select(c => (c, c.CurrentProductionYield().useful))
					.OrderByDescending(x => x.Item2).ToList();
				foreach (City c in player.cities) {
					if (c != city && c.itemBeingProduced is Building b && b.IsGreatWonder()) {
						++otherWonderBuilds;
					}
				}
			}

			public int Rank(City city) {
				return byProduction.FindIndex(x => x.city == city);
			}
		}

		// The score of a great wonder for its effect for the civ, in place of
		// the culture and happiness terms that ChooseProducible gives every
		// building. About 40 for a wonder whose effect is worth its cost.
		internal static float EffectScore(ChooseProducible.ProducibleStats stats, City city, Player player, Building wonder) {
			float cost = Math.Max(1, player.ShieldCost(wonder));
			return 40 * Math.Min(EffectWorth(stats, city, player, wonder) / cost, 2.5f);
		}

		// Roughly how many shields the wonder's effect is worth to the civ
		// over the next 40 or so turns. Effects worth little to the AI (it
		// doesn't use them well, or they help rivals as much) count little.
		internal static float EffectWorth(ChooseProducible.ProducibleStats stats, City city, Player player, Building wonder) {
			const int Horizon = 40;
			// About what the AI would pay for a content citizen: a temple
			// makes one or two.
			const float ContentFaceWorth = 20;
			GameData gameData = EngineStorage.gameData;
			bool atWar = stats.atWar;
			float worth = 0;

			List<City> sameContinent = player.cities.Where(c => c.location.continent == city.location.continent).ToList();

			// Culture grows borders and the score.
			worth += wonder.culturePerTurn * Horizon * 0.5f;

			// A building in every city, or every city on the continent: what
			// it would cost to build where it is missing, by how much the AI
			// needs it.
			if (wonder.greatWonderProperties is Building.GreatWonderProperties props) {
				foreach ((Building granted, List<City> where) in new[] {
					(props.buildingGainedInEveryCity, player.cities),
					(props.buildingGainedInEveryCityOnContinent, sameContinent),
				}) {
					if (granted == null) {
						continue;
					}
					int missing = where.Count(c => !c.HasEffectiveBuilding(granted));
					// Cities still to be founded will get it too.
					if (stats.InExpansionPhase) {
						missing += 2;
					}
					worth += missing * (player.ShieldCost(granted) + granted.maintenanceCost * Horizon / 2f) * GrantedBuildingNeed(granted, atWar);
				}
			}

			// Content faces: in every city (on the continent, if continental),
			// and in the wonder's city, counting only cities big enough to
			// have unhappy citizens.
			if (wonder.contentFacesAllCities > 0) {
				List<City> where = wonder.continentalMoodEffects ? sameContinent : player.cities;
				worth += where.Sum(c => Math.Min(wonder.contentFacesAllCities, c.residents.Count / 3)) * ContentFaceWorth;
			}
			if (wonder.contentFacesInCity > 0) {
				worth += Math.Min(wonder.contentFacesInCity, Math.Max(1, city.residents.Count / 2)) * ContentFaceWorth;
			}
			if (wonder.doublesHappinessOf is Building doubled) {
				int with = player.cities.Count(c => c.HasEffectiveBuilding(doubled));
				worth += (with + player.cities.Count) / 2f * Math.Max(1, doubled.contentFacesInCity) * ContentFaceWorth;
			}

			// Research: a commerce point counts about like a shield.
			if (wonder.doublesResearch || wonder.increasesResearch) {
				int beakers = city.CurrentCommerceYield().beakers;
				worth += beakers * (wonder.doublesResearch ? 1 : 0.5f) * Horizon;
			}
			if (wonder.twoFreeAdvances) {
				worth += 2 * CurrentTechCost(player, gameData);
			}
			if (wonder.gainAnyTechKnownByTwoCivs) {
				worth += GreatLibraryTechs(player, gameData) * CurrentTechCost(player, gameData);
			}

			// The Colossus: a commerce more on each tile of the city that
			// already makes some (see NativeBehaviors.IncreasedTrade). Ocean
			// tiles always do; count most of the others, and the city's
			// growth.
			if (wonder.ToSaveBuilding().flags.Contains(C7GameData.Save.SaveBuilding.Flag.IncreasedTrade)) {
				int oceanTiles = city.residents.Count(r => r.tileWorked != Tile.NONE && r.tileWorked.IsWater());
				int otherTiles = city.residents.Count(r => r.tileWorked != Tile.NONE && !r.tileWorked.IsWater());
				worth += (oceanTiles + otherTiles * 0.5f + 2) * Horizon;
			}

			// Free units every few turns.
			if (!string.IsNullOrEmpty(wonder.unitProducedName) && wonder.unitFrequency > 0) {
				UnitPrototype unit = gameData.unitPrototypes.FirstOrDefault(u => u.name == wonder.unitProducedName);
				if (unit != null) {
					worth += player.ShieldCost(unit) * (float)Horizon / wonder.unitFrequency * (atWar ? 1 : 0.6f);
				}
			}

			// The building upkeep Smith's pays.
			if (wonder.paysTradeMaintenance) {
				int upkeep = player.cities.Sum(c => c.constructed_buildings.Count(cb => cb.building.maintenanceCost == 1));
				worth += upkeep * Horizon;
			}

			if (wonder.doublesCityGrowthEverywhere) {
				worth += player.cities.Count * 15;
			}
			if (wonder.reducesWarWearinessEverywhere) {
				worth += player.cities.Count * (atWar ? 15 : 5);
			}
			if (wonder.cheaperUpgrades) {
				worth += player.units.Count(u => u.unitType.attack > 0 || u.unitType.defense > 0) * 5;
			}
			if (wonder.doubleCombatVsBarbarians) {
				worth += 30;
			}
			// The AI barely sails, and nuclear weapons help every civ that
			// has uranium.
			if (wonder.safeSeaTravel || wonder.increasedShipMovement || wonder.plusTwoShipMovement) {
				worth += 40;
			}
			if (wonder.allowDiplomaticVictory) {
				worth += 100;
			}

			return worth;
		}

		// How much the AI needs a building a wonder gives its cities, as a
		// share of what the building would cost.
		private static float GrantedBuildingNeed(Building b, bool atWar) {
			if (b.providesWalls) {
				return atWar ? 0.6f : 0.25f;
			}
			if (b.providesVeteranGroundUnits) {
				return atWar ? 0.8f : 0.4f;
			}
			return 0.7f;
		}

		private static float CurrentTechCost(Player player, GameData gameData) {
			Tech tech = Tech.FindById(gameData.techs, player.currentlyResearchedTech) ?? player.GetAvailableTechsToResearch(gameData.techs).FirstOrDefault();
			return tech == null ? 0 : gameData.TechCostFor(tech, player);
		}

		// About how many techs the Great Library would give: those two civs
		// we know have and we don't, plus a few more over the coming turns.
		private static int GreatLibraryTechs(Player player, GameData gameData) {
			List<Player> contacts = gameData.players.Where(p => p != player && !p.isBarbarians && !p.defeated
				&& player.playerRelationships.ContainsKey(p.id)).ToList();
			if (contacts.Count < 2) {
				return 0;
			}
			int behind = gameData.techs.Count(t => !player.knownTechs.Contains(t.id)
				&& contacts.Count(p => p.knownTechs.Contains(t.id)) >= 2);
			return behind + 1;
		}
	}
}
