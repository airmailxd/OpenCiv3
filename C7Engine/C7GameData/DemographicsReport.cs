using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C7GameData {
	// A line of Civ3's Demographics screen (F11): the viewer's value and
	// where it ranks among the civs.
	public record DemographicsLine(string label, long value, string text, int rank);

	// What Civ3's Demographics screen (F11) shows, worked out from the game
	// data. The screen's rows and units are Civ3's own, in its order
	// (Conquests/Text/labels.txt, #DEMO_WIN: "Approval Rating", "Population",
	// "GNP", "Mfg. Goods", "Land Area", "Literacy", "Disease", "Pollution",
	// "Life Expectancy", "Family Size", "Military Service", "Annual Income",
	// "Productivity", then the units "%", "$", "million", "megatons",
	// "square miles", "tons", "years", "children", "per capita" and the
	// column "Rank"). Each value is worked out as found by testing in Civ3's
	// debug mode (https://civfanatics.com/civ3/strategy/game-mechanics/how-the-demographics-works/),
	// with the formulas written out in
	// https://apolyton.net/forum/civilization-series/civilization-iii/136558-civ-iii-demographics-screen-and-statistics.
	public static class DemographicsReport {
		public enum Row {
			ApprovalRating,
			Population,
			GNP,
			MfgGoods,
			LandArea,
			Literacy,
			Disease,
			Pollution,
			LifeExpectancy,
			FamilySize,
			MilitaryService,
			AnnualIncome,
			Productivity,
		}

		public static readonly string[] Labels = [
			"Approval Rating",
			"Population",
			"GNP",
			"Mfg. Goods",
			"Land Area",
			"Literacy",
			"Disease",
			"Pollution",
			"Life Expectancy",
			"Family Size",
			"Military Service",
			"Annual Income",
			"Productivity",
		];

		public static int RowCount => Labels.Length;

		// The value with its unit, as the screen shows it. Which unit goes
		// with which row is read from the order of the units in labels.txt
		// and the sources above ("1 gold = 1 million", "1 shield = 1
		// megaton", "1 Tile = 100 square miles", "1 ton = 1 polluted
		// territory tile", "2 children"). UNVERIFIED: that Population and
		// Productivity are plain numbers (labels.txt has no unit left over
		// for them).
		public static string Format(Row row, long value) {
			string n = value.ToString("N0", CultureInfo.InvariantCulture);
			return row switch {
				Row.ApprovalRating or Row.Literacy or Row.Disease => $"{n}%",
				Row.GNP => $"${n} million",
				Row.MfgGoods => $"{n} megatons",
				Row.LandArea => $"{n} square miles",
				Row.Pollution => $"{n} tons",
				Row.LifeExpectancy or Row.MilitaryService => $"{n} years",
				Row.FamilySize => $"{n} children",
				Row.AnnualIncome => $"${n} per capita",
				_ => n,
			};
		}

		// UNVERIFIED: which way each row ranks. No source says; more disease
		// and more pollution are taken to rank lower, and more of anything
		// else to rank higher.
		public static bool LowerIsBetter(Row row) => row is Row.Disease or Row.Pollution;

		// The rank of a value among all the civs' values (its own included):
		// 1 for the best, with ties sharing a rank (two civs tied for best are
		// both 1st, and the next is 3rd).
		public static int Rank(long value, IEnumerable<long> allValues, bool lowerIsBetter) {
			return 1 + allValues.Count(v => lowerIsBetter ? v < value : v > value);
		}

		// The screen's lines for the viewer, ranked among every civ still in
		// the game. UNVERIFIED: whether Civ3 ranks among all the civs or only
		// those the viewer has met; all of them are counted here, which is
		// the simpler.
		public static List<DemographicsLine> For(GameData gameData, Player viewer) {
			List<Player> civs = [viewer, .. gameData.GetRivals(viewer)];
			Dictionary<Player, long[]> values = ValuesOf(gameData, civs);
			long[] ours = values[viewer];
			List<DemographicsLine> result = new();
			for (int i = 0; i < RowCount; ++i) {
				Row row = (Row)i;
				int rank = Rank(ours[i], civs.Select(c => values[c][i]), LowerIsBetter(row));
				result.Add(new DemographicsLine(Labels[i], ours[i], Format(row, ours[i]), rank));
			}
			return result;
		}

		// Each civ's values, in the order of Row. A LAN guest isn't sent all
		// of a rival's cities, units and territory, so the host works them
		// out (see HostFacts).
		public static Dictionary<Player, long[]> ValuesOf(GameData gameData, List<Player> civs) {
			Dictionary<Player, Territory> territories = TerritoriesOf(gameData, civs);
			Dictionary<Player, long[]> result = new();
			foreach (Player civ in civs) {
				result[civ] = civ.hostFacts?.demographics is long[] facts && facts.Length == RowCount
					? facts
					: Values(gameData, civ, territories[civ]);
			}
			return result;
		}

		public struct Territory {
			public int tiles;
			public int diseased;
			public int polluted;
		}

		// The size of each civ's territory, sea included, with its flood
		// plains and jungle and its polluted tiles, in one pass over the map.
		public static Dictionary<Player, Territory> TerritoriesOf(GameData gameData, List<Player> civs) {
			Dictionary<Player, Territory> result = new();
			foreach (Player civ in civs) {
				result[civ] = new Territory();
			}
			foreach (Tile tile in gameData.map?.tiles ?? []) {
				Player owner = tile.OwningPlayer();
				if (owner == null || !result.TryGetValue(owner, out Territory t)) {
					continue;
				}
				++t.tiles;
				if (IsDiseased(tile)) {
					++t.diseased;
				}
				if (tile.HasPollution()) {
					++t.polluted;
				}
				result[owner] = t;
			}
			return result;
		}

		// "Disease: % of the tiles in your territory that is floodplains or
		// jungle."
		private static bool IsDiseased(Tile tile) {
			return tile.baseTerrainType.IsFloodPlain || tile.baseTerrainType.IsJungle
				|| tile.overlayTerrainType.IsFloodPlain || tile.overlayTerrainType.IsJungle;
		}

		public static long[] Values(GameData gameData, Player civ, Territory territory) {
			long[] v = new long[RowCount];

			int citizens = 0, happy = 0, unhappy = 0;
			int militaryUnits = 0;
			double population = 0;
			long gnp = 0, corruption = 0, shields = 0, food = 0;
			double literate = 0, longLived = 0;
			bool literature = civ.KnowsTechWhere(t => t.Name == "Literature");

			foreach (City city in civ.cities) {
				int size = city.residents.Count;
				citizens += size;
				foreach (CityResident r in city.residents) {
					// "the specialists count as only content people"
					if (r.citizenType == null || !r.citizenType.IsDefaultCitizen) {
						continue;
					}
					if (r.mood == CityResident.Mood.Happy) {
						++happy;
					} else if (r.mood == CityResident.Mood.Unhappy) {
						++unhappy;
					}
				}

				// "Pop in City = {Sum (j=1..(City Size)) {j} + (Food in
				// Storage) / (Storage Size)} * 10000"
				int foodBox = Math.Max(1, city.FoodNeededToGrow());
				population += (size * (size + 1) / 2 + (double)city.foodStored / foodBox) * 10000;

				CommerceBreakdown commerce = city.CurrentCommerceYield();
				gnp += commerce.corrupted + commerce.taxes + commerce.beakers + commerce.happiness + commerce.wealth;
				corruption += commerce.corrupted;
				shields += city.CurrentProductionYield().useful;
				food += city.FoodGrowthPerTurn();

				literate += size * LiteracyShare(city);
				longLived += size * LifeShare(city);
			}

			foreach (MapUnit unit in civ.units) {
				// "Military units are units with an attack and defense value,
				// so workers, scouts and princesses don't count. Kings do."
				if (unit.unitType.attack > 0 && unit.unitType.defense > 0) {
					++militaryUnits;
				}
			}

			// "Rounddown (50% + ((Number of happy citizens) - (Number of
			// unhappy citizens)) / (Total number of citizens) / 2 * 100%)"
			v[(int)Row.ApprovalRating] = citizens == 0 ? 0 : (long)Math.Floor(50 + (happy - unhappy) * 50.0 / citizens);
			v[(int)Row.Population] = (long)Math.Round(population);
			// "Total gold in all your cities before corruption takes a bite
			// out of it. 1 gold= 1 million". UNVERIFIED: whether it is
			// counted before or after the marketplaces, libraries and the like
			// multiply it; it is after here, as the commerce the city screen
			// shows.
			v[(int)Row.GNP] = gnp;
			// "Total unwasted shields in all your cities. 1 shield = 1 megaton."
			v[(int)Row.MfgGoods] = shields;
			// "# of tiles in your territory * 100 ... Sea is included".
			v[(int)Row.LandArea] = territory.tiles * 100L;
			// "(Citizens who live in a city with a Library + ... University +
			// ... Research Lab) / 3 / (Total Number of Citizens) * 100% + (3%
			// if Literature discovered)". UNVERIFIED: that it stops at 100%.
			v[(int)Row.Literacy] = citizens == 0 ? 0
				: Math.Min(100, (long)Math.Floor(literate * 100 / citizens) + (literature ? 3 : 0));
			// "(Number of Floodplains + Jungle in territory) / (Total Number
			// of territory tiles) * 100%"
			v[(int)Row.Disease] = territory.tiles == 0 ? 0 : territory.diseased * 100L / territory.tiles;
			// "# of tiles that are currently polluted. 1 ton = 1 polluted
			// territory tile"
			v[(int)Row.Pollution] = territory.polluted;
			// "20 + {(Citizens who live in a city with a Granary + ... an
			// Aqueduct + ... a Hospital) / 3 / (Total Number of Citizens) *
			// 80}" and "Minimum is 20, maximum is 99."
			v[(int)Row.LifeExpectancy] = citizens == 0 ? 20
				: Math.Clamp(20 + (long)Math.Floor(longLived * 80 / citizens), 20, 99);
			// "The average amount of excess food that each city is
			// producing/2 ... Minimum is 1". UNVERIFIED: that it is rounded
			// down.
			v[(int)Row.FamilySize] = civ.cities.Count == 0 ? 1
				: Math.Max(1, (long)Math.Floor(food / (double)civ.cities.Count / 2));
			// "10 years * # of military units / # of citizens"
			v[(int)Row.MilitaryService] = citizens == 0 ? 0 : 10L * militaryUnits / citizens;
			v[(int)Row.AnnualIncome] = AnnualIncome(gameData, civ);
			// "(Total Shields) - (Total Waste) + (Total Commerce) - (Total
			// Corruption) + (Net Total Food)"
			v[(int)Row.Productivity] = shields + (gnp - corruption) + food;
			return v;
		}

		// The part of a city's citizens Civ3 counts as literate: a third for
		// each of a library, a university and a research lab, all of it for a
		// scientific great wonder, and half for the Apollo Program or the
		// Intelligence Agency. The source calls these "1 or more scientific
		// Great Wonders (Great Library, Newtons, SETI, Theory of Evolution,
		// Cure for Cancer, and Internet) Copernicus's does not count because
		// despite it helps science, it isn't given the scientific flag ...
		// Two small wonders (Apollo and Intelligence Agency) give you credit
		// for having 50% science in that city", so the buildings are told by
		// their scientific flag. UNVERIFIED: how half credit from a small
		// wonder adds to the city's libraries and universities; the larger
		// of the two is taken.
		internal static double LiteracyShare(City city) {
			int improvements = 0;
			bool smallWonder = false;
			foreach (CityBuilding cb in city.GetBuildings()) {
				Building b = cb.building;
				if (!b.traits.Contains(Civilization.Trait.Scientific)) {
					continue;
				}
				if (b.IsGreatWonder()) {
					return 1;
				} else if (b.isSmallWonder) {
					smallWonder = true;
				} else {
					++improvements;
				}
			}
			return Math.Max(Math.Min(improvements, 3) / 3.0, smallWonder ? 0.5 : 0);
		}

		// The part of a city's citizens counted as long lived: a third for
		// each of a granary, an aqueduct and a hospital, told by what they do
		// (doubling growth, allowing a city past size 6 and past size 12).
		internal static double LifeShare(City city) {
			bool granary = false, aqueduct = false, hospital = false;
			foreach (CityBuilding cb in city.GetBuildings()) {
				granary |= cb.building.doublesCityGrowthRate;
				aqueduct |= cb.building.allowsCitySize2;
				hospital |= cb.building.allowsCitySize3;
			}
			return ((granary ? 1 : 0) + (aqueduct ? 1 : 0) + (hospital ? 1 : 0)) / 3.0;
		}

		// "The number of connected strategic/luxury resource types in your
		// territory. The minimum value is 1 and you get a +1 bonus for your
		// first trade route with another civ." UNVERIFIED: what makes a
		// trade route; a resource or luxury traded with another civ, either
		// way, is taken to be one.
		internal static long AnnualIncome(GameData gameData, Player civ) {
			if (civ.cities.Count == 0) {
				return 1;
			}
			int types = TradeReport.LocalResources(gameData, civ).Count;
			bool tradeRoute = TradeReport.ResourceDeals(gameData, civ, DealDetails.Inbound).Count > 0
				|| TradeReport.ResourceDeals(gameData, civ, DealDetails.Outbound).Count > 0;
			return Math.Max(1, types) + (tradeRoute ? 1 : 0);
		}

		// Civ3's Top 5 Cities, beside the demographics: the cities with the
		// most culture, "ranked by the local culture of the current city
		// owner"; "Culture, and if culture is equal it is the population"
		// (https://forums.civfanatics.com/threads/top-5-cities-how-does-it-work.97506/,
		// https://forums.civfanatics.com/threads/top-5-cities.54901/).
		// UNVERIFIED: whether cities of civs the viewer hasn't met are listed;
		// every city the game has is.
		public static List<City> TopCities(GameData gameData, int count = 5) {
			return gameData.cities
				.Where(c => c.owner != null && !c.owner.isBarbarians)
				.OrderByDescending(c => c.GetCultureFor(c.owner))
				.ThenByDescending(c => c.residents.Count)
				.ThenBy(c => c.name, StringComparer.Ordinal)
				.Take(count)
				.ToList();
		}

		// "1st", "2nd", "3rd", "4th", ...
		public static string Ordinal(int n) {
			int lastTwo = n % 100;
			string suffix = lastTwo is >= 11 and <= 13 ? "th" : (n % 10) switch {
				1 => "st",
				2 => "nd",
				3 => "rd",
				_ => "th",
			};
			return $"{n}{suffix}";
		}
	}
}
