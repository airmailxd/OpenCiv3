using System;
using System.Collections.Generic;
using C7Engine;

namespace C7GameData;

public partial class Tile {
	public enum YieldType {
		Commerce,
		Food,
		Production
	}

	public class Yield {
		public readonly Tile tile;
		public readonly YieldType type;
		public int penalty = 0;
		public int bonus = 0;
		public readonly int baseYield = 0;
		public int yield { get => baseYield + bonus - penalty; }

		public static Yield CalculateForCity(Tile tile, int yield, YieldType type, City city) {
			return CalculateForCity(tile, yield, type, city, city.EffectiveBuildings());
		}

		// Like CalculateForCity, with the city's buildings (as returned by
		// City.EffectiveBuildings) looked up once by the caller instead of
		// once per tile.
		internal static Yield CalculateForCity(Tile tile, int yield, YieldType type, City city, List<CityBuilding> cityBuildings) {
			// Building bonuses (Colossus, Harbor, Offshore Platform) come after
			// the government's modifiers, so the despotism penalty doesn't
			// cancel them out: a Colossus ocean tile makes 3 commerce under
			// despotism in Civ3, not 2.
			return new Yield(tile, yield, type)
				.ApplyTerrainImprovementModifiers(tile)
				.ApplyPlayerModifiers(city.owner)
				.ApplyCityModifiers(cityBuildings);
		}

		public static Yield CalculateForPlayer(Tile tile, int yield, YieldType type, Player player) {
			return new Yield(tile, yield, type)
				.ApplyTerrainImprovementModifiers(tile)
				.ApplyPlayerModifiers(player);
		}

		public Yield(Tile tile, int baseYield, YieldType type) {
			this.tile = tile;
			this.baseYield = baseYield;
			this.type = type;
		}

		private Yield ApplyPlayerModifiers(Player player) {
			// A golden age adds a shield and a commerce to tiles already
			// producing them, before the government's modifiers: "The bonuses
			// from a Golden Age, however, are still subject to the Despotism
			// tile penalty" (https://civfanatics.com/civ3/faq/).
			if (player.InGoldenAge && type != YieldType.Food && baseYield > 0) {
				bonus += 1;
			}

			player.government.tileModifier?.Invoke(this);
			return this;
		}

		private Yield ApplyCityModifiers(List<CityBuilding> cityBuildings) {
			foreach (CityBuilding b in cityBuildings) {
				b.building.tileModifier?.Invoke(this);
			}
			return this;
		}

		private Yield ApplyTerrainImprovementModifiers(Tile tile) {
			// The same improvements, in the same order, as GetImprovements,
			// but without allocating an enumerator or a list.
			foreach (TerrainImprovement ti in tile.overlays.terrainImprovementByLayer.Values) {
				ti.tileModifier?.Invoke(this);
			}
			return this;
		}
	}

	//Convenience method for printing the yield
	public string YieldString(Player player) {
		return $"{this.FoodYield(player).yield}/{this.ProductionYield(player).yield}/{this.CommerceYield(player).yield}";
	}

	// The same as overlays.GetBaseYieldBonus, without the LINQ allocations.
	private int BaseYieldBonus(YieldType type) {
		int result = 0;
		foreach (TerrainImprovement ti in overlays.terrainImprovementByLayer.Values) {
			result += ti.GetYieldBonus(overlayTerrainType, type);
		}
		return result;
	}

	// Food yield
	private int BaseFoodYield(Player player) {
		if (this.HasPollution() || this.HasFallout()) return 0;
		int yield = overlayTerrainType.baseFoodProduction;
		if (this.Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.FoodBonus;
		}

		// Fresh water lakes make a food more than the coast.
		if (isFreshWater && baseTerrainType.IsWater) {
			yield += 1;
		}

		if (this.HasCity()) {
			// All city centers have a food yield of 2, regardless of bonus
			// food. See https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3).
			yield = 2;

			// Agricultural civilizations get a third. (The despotism penalty
			// takes it away again, except in the cases FoodYield exempts.)
			if (cityAtTile.owner.civilization?.traits.Contains(Civilization.Trait.Agricultural) == true) {
				yield += 1;
			}
		}

		yield += BaseYieldBonus(YieldType.Food);

		// Agricultural civilizations get an extra food from irrigated desert.
		if (!HasCity() && overlayTerrainType.IsDesert && HasIrrigation()
			&& player.civilization?.traits.Contains(Civilization.Trait.Agricultural) == true) {
			yield += 1;
		}

		// "Like pollution, the presence of craters reduces a square's
		// production, commerce, and food output by one." (Conquests
		// Civilopedia, GCON_Craters). The same for shields and commerce below.
		if (this.HasCraters())
			yield = Math.Max(0, yield - 1);

		return yield;
	}
	public Yield FoodYield(Player player) {
		int yield = BaseFoodYield(player);
		return Yield.CalculateForPlayer(this, yield, YieldType.Food, player);
	}
	public Yield FoodYield(City city) {
		return FoodYield(city, city.EffectiveBuildings());
	}
	internal Yield FoodYield(City city, List<CityBuilding> cityBuildings) {
		int yield = BaseFoodYield(city.owner);
		Yield result = Yield.CalculateForCity(this, yield, YieldType.Food, city, cityBuildings);

		// The despotism penalty spares an agricultural city's extra food only
		// when the city is on a river or by a lake: "In despotism, the bonus
		// food only appears if the city is by fresh water"
		// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/).
		// The German wiki also spares cities of size 7 or more, but Civ3
		// saves disagree: their agricultural cities of size 7+ away from
		// fresh water make 2 food in the center under Despotism and Anarchy
		// (42 of 44 such cities), as do their towns.
		if (result.penalty > 0 && cityAtTile == city
			&& city.owner.civilization?.traits.Contains(Civilization.Trait.Agricultural) == true
			&& (BordersRiver() || NeighborsFreshWater())) {
			result.penalty -= 1;
		}
		return result;
	}

	// Production yield
	private int BaseProductionYield(Player player) {
		if (this.HasPollution() || this.HasFallout()) return 0;
		int yield = overlayTerrainType.baseShieldProduction;
		if (this.isBonusShield && overlayTerrainType.IsGrassland) {
			yield++;
		}

		// Bonus resources provide a boost in yield regardless of whether
		// there is a city.
		if (Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.ShieldsBonus;
		}

		if (HasCity()) {
			// City centers add a size bonus to the terrain's shields: towns
			// only make sure there is at least 1, cities get +1 and
			// metropolises +2. A city on a flood plain makes 1 shield, not 2.
			// See https://www.civ-wiki.de/wiki/Stadtfeldertrag_(Civ3).
			// A town's minimum of 1 is checked after the resource's shields
			// are added, so a town on a shield resource gets no extra one:
			// DaveMcW lists "Shields from unimproved terrain", then
			// "+Shields from bonus resources", and only last "+1 if
			// shields==0 so far", adding "you only get the 0-shield-terrain
			// bonus if the city is size 6 or less"
			// (https://codehappy.net/apolyton/threads/78438-1.htm).
			Rules rules = EngineStorage.gameData.rules;
			int citySize = cityAtTile.residents.Count;
			if (citySize <= rules.MaximumLevel1CitySize) {
				yield = Math.Max(yield, 1);
			} else if (citySize <= rules.MaximumLevel2CitySize) {
				yield += 1;
			} else {
				yield += 2;

				// Industrious civs get +1 production in metropolises, as
				// civ-wiki.de and community tests have it for PTW and C3C
				// (the Civilopedia's "in cities and metros" is the original
				// game's rule). Against 24,425 cities from Civ3 saves, exact
				// shield matches are 98.7% this way against 87.2% from size 7;
				// the project owner chose it (2026-10-20).
				if (cityAtTile.owner.civilization.traits.Contains(Civilization.Trait.Industrious)) {
					yield += 1;
				}
			}
		}

		yield += BaseYieldBonus(YieldType.Production);

		if (this.HasCraters())
			yield = Math.Max(0, yield - 1);

		return yield;
	}
	public Yield ProductionYield(Player player) {
		int yield = BaseProductionYield(player);
		return Yield.CalculateForPlayer(this, yield, YieldType.Production, player);
	}
	public Yield ProductionYield(City city) {
		return ProductionYield(city, city.EffectiveBuildings());
	}
	internal Yield ProductionYield(City city, List<CityBuilding> cityBuildings) {
		int yield = BaseProductionYield(city.owner);
		return Yield.CalculateForCity(this, yield, YieldType.Production, city, cityBuildings);
	}

	// Commerce yield
	private int BaseCommerceYield(Player player) {
		if (this.HasPollution() || this.HasFallout()) return 0;
		int yield = overlayTerrainType.baseCommerceProduction;
		if (this.Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.CommerceBonus;
		}

		bool borderRiver = this.BordersRiver();

		if (borderRiver) {
			yield += 1;
		}

		// See https://wiki.civforum.de/wiki/Stadtfeldertrag_(Civ3)
		if (HasCity()) {
			int regularCityYield;
			Rules rules = EngineStorage.gameData.rules;
			int citySize = cityAtTile.residents.Count;
			if (citySize <= rules.MaximumLevel1CitySize) {
				regularCityYield = 1;
			} else if (citySize <= rules.MaximumLevel2CitySize) {
				regularCityYield = 2;
			} else {
				regularCityYield = 3;
			}
			// Commercial civilizations' cities get 2 more commerce, and their
			// metropolises 3 (found by comparing with Civ3 saves).
			if (citySize > rules.MaximumLevel1CitySize
				&& cityAtTile.owner.civilization?.traits.Contains(Civilization.Trait.Commercial) == true) {
				regularCityYield += citySize <= rules.MaximumLevel2CitySize ? 2 : 3;
			}
			if (borderRiver) {
				regularCityYield += 1;
			}
			if (this.Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
				regularCityYield += this.Resource.CommerceBonus;
			}

			int capitalCityYield = 0;
			if (cityAtTile.IsCapital()) {
				capitalCityYield = 4;
			}

			// City centers don't get commerce from their road (or other
			// terrain improvements), only the bonuses above.
			int centerYield = Math.Max(regularCityYield, capitalCityYield);

			// Seafaring civilizations' coastal cities get one more (found by
			// comparing with Civ3 saves; a lake doesn't count).
			if (cityAtTile.owner.civilization?.traits.Contains(Civilization.Trait.Seafaring) == true && NeighborsOcean()) {
				centerYield += 1;
			}
			return centerYield;
		}

		yield += BaseYieldBonus(YieldType.Commerce);

		if (this.HasCraters())
			yield = Math.Max(0, yield - 1);

		return yield;
	}
	public Yield CommerceYield(Player player) {
		int yield = BaseCommerceYield(player);
		return Yield.CalculateForPlayer(this, yield, YieldType.Commerce, player);
	}
	public Yield CommerceYield(City city) {
		return CommerceYield(city, city.EffectiveBuildings());
	}
	internal Yield CommerceYield(City city, List<CityBuilding> cityBuildings) {
		int yield = BaseCommerceYield(city.owner);

		return Yield.CalculateForCity(this, yield, YieldType.Commerce, city, cityBuildings);
	}
}
