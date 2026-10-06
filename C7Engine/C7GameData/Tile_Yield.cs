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
			player.government.tileModifier?.Invoke(this);

			// A golden age adds a shield and a commerce to tiles already
			// producing them.
			if (player.InGoldenAge && type != YieldType.Food && baseYield > 0) {
				bonus += 1;
			}
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

		if (this.HasCraters())
			yield--;

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

		// The despotism penalty spares an agricultural city's extra food when
		// the city is on a river or by a lake, or has reached size 7.
		if (result.penalty > 0 && cityAtTile == city
			&& city.owner.civilization?.traits.Contains(Civilization.Trait.Agricultural) == true
			&& (BordersRiver() || NeighborsFreshWater() || city.residents.Count > EngineStorage.gameData.rules.MaximumLevel1CitySize)) {
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

		if (HasCity()) {
			// City centers add a size bonus to the terrain's shields: towns
			// only make sure there is at least 1, cities get +1 and
			// metropolises +2. A city on a flood plain makes 1 shield, not 2.
			// See https://www.civ-wiki.de/wiki/Stadtfeldertrag_(Civ3).
			Rules rules = EngineStorage.gameData.rules;
			int citySize = cityAtTile.residents.Count;
			if (citySize <= rules.MaximumLevel1CitySize) {
				yield = Math.Max(yield, 1);
			} else if (citySize <= rules.MaximumLevel2CitySize) {
				yield += 1;
			} else {
				yield += 2;

				// Industrious civs get +1 production in metropolises
				if (cityAtTile.owner.civilization.traits.Contains(Civilization.Trait.Industrious)) {
					yield += 1;
				}
			}
		}

		// Bonus resources provide a boost in yield regardless of whether
		// there is a city.
		if (Resource != Resource.NONE && player.KnowsAboutResource(Resource)) {
			yield += this.Resource.ShieldsBonus;
		}

		yield += BaseYieldBonus(YieldType.Production);

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

		return yield;
	}
	public Yield CommerceYield(Player player) {
		int yield = BaseCommerceYield(player);

		// TODO: handle the commerce bonus for costal cities+seafaring
		// TODO: handle the commerce bonus for commerial civs
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
