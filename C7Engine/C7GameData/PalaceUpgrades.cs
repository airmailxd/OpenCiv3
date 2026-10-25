using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData {

	// A civ's palace upgrades: the achievements that have earned them, the
	// upgrades not yet placed, and the pieces of the palace built so far.
	//
	// Civ3 keeps this per player in its PALV record (C3X's Civ3Conquests.h,
	// Palace_View_Info: field_1C, Items_Cultures[32], field_A0, Builds_Done,
	// Items_Flags, Builds_Remained). Each achievement earns one upgrade, once
	// per game (Civ3 FUN_00589a10 and FUN_00589ae0); the human chooses which
	// piece of the palace to build with it in the palace view (FUN_005896c0).
	// The AI earns upgrades too; only the palace view spends them, and it is
	// only ever opened for a human.
	//
	// Left out, for want of a sure reading of the code: FUN_00589ae0's bit
	// 0x80, apparently for holding every source of some luxury or strategic
	// resource on the map, and its second way to the 0x4000000 bit (more
	// than one city with a player_difficulty of 2).
	public class PalaceUpgrades {
		private static readonly ILogger log = Log.ForContext<PalaceUpgrades>();

		// The achievements, with Civ3's own bits for them (FUN_00589ae0 unless
		// noted).
		[Flags]
		public enum Achievement : uint {
			None = 0,
			// Being the first to enter the Middle Ages, the Industrial Age
			// or the Modern Era: no civ is in a later era as we advance
			// (FUN_0056d7f0).
			FirstIntoMiddleAges = 0x1,
			FirstIntoIndustrialAge = 0x2,
			FirstIntoModernEra = 0x4,
			// Knowing every tech of the Modern Era needed to advance, which
			// FUN_0056d7f0 treats as advancing beyond it.
			CompletedModernEra = 0x8,
			// The cities' commerce, corruption included, over 500 (FUN_00569720).
			CommerceOver500 = 0x10,
			// Every luxury, or every strategic resource, in the rules
			// available to the civ, and in the capital.
			AllLuxuries = 0x20,
			AllLuxuriesInCapital = 0x40,
			AllStrategicResources = 0x100,
			AllStrategicResourcesInCapital = 0x200,
			// The capital reaching size 10, 20 and 30.
			CapitalSize10 = 0x400,
			CapitalSize20 = 0x800,
			CapitalSize30 = 0x1000,
			// More than four wonders (great or small) in the capital.
			WondersInCapital = 0x2000,
			// The capital celebrating We Love the King Day.
			CapitalCelebrates = 0x4000,
			// Any other city reaching size 10, 20 and 30.
			CitySize10 = 0x8000,
			CitySize20 = 0x10000,
			CitySize30 = 0x20000,
			// More than four wonders in another city.
			WondersInCity = 0x40000,
			// More than ten great wonders in the civ's cities.
			GreatWonders = 0x80000,
			// Every city celebrating, with more than five cities.
			AllCitiesCelebrate = 0x100000,
			// The capital's culture reaching 1000 (its border level past 3).
			CapitalCulture = 0x200000,
			// More than 20 cities with 10 culture, more than 10 with 100, and
			// more than 10 with 1000.
			CitiesWithCulture10 = 0x400000,
			CitiesWithCulture100 = 0x800000,
			CitiesWithCulture1000 = 0x1000000,
			// The civ's culture reaching 5000 and 10000.
			Culture5000 = 0x2000000,
			Culture10000 = 0x4000000,
			// Destroying another civ (FUN_004e5870).
			DestroyedACiv = 0x8000000,
			// Having explored every tile of the map.
			ExploredTheWorld = 0x20000000,
			// A military alliance with another civ.
			MilitaryAlliance = 0x40000000,
			// A peace treaty or mutual protection pact with another civ.
			PeaceOrPact = 0x80000000,
		}

		// Civ3 stops earning upgrades once those built and those waiting add
		// up to 32, the most pieces a palace can have (FUN_00589a10:
		// "Builds_Done + Builds_Remained < 0x20"; PalaceView.txt: "no more
		// than 32 screens per culture").
		public const int MaxUpgrades = 32;

		// The people offer to expand the palace only while fewer upgrades
		// than this have been announced: the number of pieces in the stock
		// PalaceView.txt, 17, which FUN_00589a70 uses unless the file has
		// more.
		public const int MaxAnnouncedUpgrades = 17;

		// Civ3's palace cultures, in its order (FUN_00588430):
		// PVFNAME_AMERICAN, _EUROPEAN, _MEDITERRANEAN, _MIDEAST, _ASIAN, which
		// is also the order of the culture groups in the BIQ.
		public const int CultureCount = 5;

		// The achievements that have earned an upgrade.
		public Achievement achievements;

		// The upgrades earned but not yet built (Builds_Remained).
		public int upgradesToPlace;

		// The pieces built so far (Builds_Done).
		public int piecesBuilt;

		// How many of the upgrades earned the player has been told of
		// (field_A0).
		public int upgradesAnnounced;

		// The culture each piece was built in, by piece; -1 (or missing) for
		// a piece not built.
		public List<int> pieceCultures = new();

		public int UpgradesEarned => piecesBuilt + upgradesToPlace;

		public bool HasEarned(Achievement achievement) => (achievements & achievement) != 0;

		public bool IsBuilt(int piece) => CultureOf(piece) >= 0;

		// The culture a piece was built in, or -1 if it hasn't been.
		public int CultureOf(int piece) {
			return piece >= 0 && piece < pieceCultures.Count ? pieceCultures[piece] : -1;
		}

		// The built pieces, as PalaceView.txt's rules number them.
		public uint BuiltPieces() {
			uint built = 0;
			for (int i = 0; i < pieceCultures.Count && i < MaxUpgrades; ++i) {
				if (pieceCultures[i] >= 0) {
					built |= 1u << i;
				}
			}
			return built;
		}

		// Earns an upgrade for an achievement, unless it already has or the
		// palace has all the upgrades it can. Returns whether it did.
		public bool Earn(Achievement achievement) {
			if (HasEarned(achievement) || UpgradesEarned >= MaxUpgrades) {
				return false;
			}
			achievements |= achievement;
			++upgradesToPlace;
			return true;
		}

		// Whether the people have offered an upgrade the player hasn't yet
		// been told of (FUN_00589a70).
		public bool HasUpgradeToAnnounce => upgradesAnnounced < UpgradesEarned && upgradesAnnounced < MaxAnnouncedUpgrades;

		// Marks every upgrade earned so far as told of (FUN_00589a50): one
		// offer covers all the upgrades earned since the last.
		public void MarkAnnounced() {
			upgradesAnnounced = UpgradesEarned;
		}

		// Whether an upgrade may build the piece: one is waiting and the
		// piece isn't built yet. The pieces it needs first come from
		// PalaceView.txt's rules, which the palace view checks.
		public bool CanBuild(int piece, int culture) {
			return upgradesToPlace > 0 && piece >= 0 && piece < MaxUpgrades && !IsBuilt(piece)
				&& culture >= 0 && culture < CultureCount;
		}

		// Spends an upgrade on building the piece in the culture
		// (FUN_005896c0). Returns whether it could.
		public bool Build(int piece, int culture) {
			if (!CanBuild(piece, culture)) {
				return false;
			}
			while (pieceCultures.Count <= piece) {
				pieceCultures.Add(-1);
			}
			pieceCultures[piece] = culture;
			--upgradesToPlace;
			++piecesBuilt;
			return true;
		}

		public bool IsEmpty => achievements == Achievement.None && upgradesToPlace == 0 && piecesBuilt == 0
			&& upgradesAnnounced == 0 && pieceCultures.All(c => c < 0);

		public PalaceUpgrades Copy() {
			return new PalaceUpgrades {
				achievements = achievements,
				upgradesToPlace = upgradesToPlace,
				piecesBuilt = piecesBuilt,
				upgradesAnnounced = upgradesAnnounced,
				pieceCultures = new List<int>(pieceCultures ?? new()),
			};
		}

		// Checks the achievements Civ3 checks for each civ every turn
		// (FUN_00589ae0, called for each player from FUN_0056c5a0).
		public static void CheckAchievements(Player player, GameData gameData) {
			if (player.IsIndependent || player.defeated) {
				return;
			}
			PalaceUpgrades palace = player.palace;
			if (palace.UpgradesEarned >= MaxUpgrades) {
				return;
			}

			int commerce = player.cities.Sum(c => c.TotalCommerce());
			Check(player, Achievement.CommerceOver500, commerce > 500);

			int culture = player.cities.Sum(c => c.GetCulture());
			Check(player, Achievement.Culture5000, culture >= 5000);
			Check(player, Achievement.Culture10000, culture >= 10000);

			int greatWonders = 0;
			int culture10 = 0, culture100 = 0, culture1000 = 0;
			bool allCelebrate = true;
			foreach (City city in player.cities) {
				// The border level is Civ3's cultural_level: 2 from 10 culture,
				// 3 from 100 and 4 from 1000.
				int level = city.GetBorderExpansionLevel();
				int size = city.residents.Count;
				int wonders = 0;
				foreach (CityBuilding cb in city.constructed_buildings) {
					if (cb.building.IsGreatWonder()) {
						++wonders;
						++greatWonders;
					} else if (cb.building.isSmallWonder) {
						++wonders;
					}
				}
				if (city.IsCapital()) {
					CheckCapitalResources(player, city, gameData);
					Check(player, Achievement.CapitalSize10, size > 9);
					Check(player, Achievement.CapitalSize20, size > 19);
					Check(player, Achievement.CapitalSize30, size > 29);
					Check(player, Achievement.CapitalCulture, level > 3);
					Check(player, Achievement.CapitalCelebrates, city.celebrating);
					Check(player, Achievement.WondersInCapital, wonders > 4);
				} else {
					Check(player, Achievement.CitySize10, size > 9);
					Check(player, Achievement.CitySize20, size > 19);
					Check(player, Achievement.CitySize30, size > 29);
					Check(player, Achievement.WondersInCity, wonders > 4);
				}
				allCelebrate &= city.celebrating;
				culture10 += level > 1 ? 1 : 0;
				culture100 += level > 2 ? 1 : 0;
				culture1000 += level > 3 ? 1 : 0;
			}
			Check(player, Achievement.GreatWonders, greatWonders > 10);
			Check(player, Achievement.AllCitiesCelebrate, allCelebrate && player.cities.Count > 5);
			Check(player, Achievement.CitiesWithCulture10, culture10 > 20);
			Check(player, Achievement.CitiesWithCulture100, culture100 > 10);
			Check(player, Achievement.CitiesWithCulture1000, culture1000 > 10);

			if (!palace.HasEarned(Achievement.ExploredTheWorld) && gameData.map?.tiles != null) {
				Check(player, Achievement.ExploredTheWorld,
					gameData.map.tiles.Count > 0 && player.tileKnowledge.knownTiles.Count >= gameData.map.tiles.Count);
			}

			foreach (Player other in gameData.players) {
				if (other == player || other.IsIndependent || other.defeated) {
					continue;
				}
				Check(player, Achievement.PeaceOrPact,
					Pacts.HasDeal(player, other, DealSubType.Peace) || Pacts.HasDeal(player, other, DealSubType.MutualProtectionPact));
				Check(player, Achievement.MilitaryAlliance, Pacts.HasDeal(player, other, DealSubType.MilitaryAlliance));
			}
		}

		// Civ3 checks every luxury and strategic resource in the rules,
		// whether or not the map has it: the capital's available resources
		// for the capital's achievements, and those of the civ (from any of
		// its cities) for the civ's.
		private static void CheckCapitalResources(Player player, City capital, GameData gameData) {
			PalaceUpgrades palace = player.palace;
			const Achievement resourceAchievements = Achievement.AllLuxuries | Achievement.AllLuxuriesInCapital
				| Achievement.AllStrategicResources | Achievement.AllStrategicResourcesInCapital;
			if ((palace.achievements & resourceAchievements) == resourceAchievements || gameData.Resources == null) {
				return;
			}
			List<Resource> luxuries = gameData.Resources.Where(r => r.Category == ResourceCategory.LUXURY).ToList();
			List<Resource> strategic = gameData.Resources.Where(r => r.Category == ResourceCategory.STRATEGIC).ToList();
			Dictionary<Resource, int> inCapital = new();
			HashSet<Resource> inCiv = new();
			if (luxuries.Count + strategic.Count > 0) {
				inCapital = capital.GetAvailableResources(gameData);
				inCiv.UnionWith(inCapital.Keys);
				foreach (City city in player.cities) {
					if (city != capital) {
						inCiv.UnionWith(city.GetAvailableResources(gameData).Keys);
					}
				}
			}
			// A kind of resource the rules have none of counts as all had.
			Check(player, Achievement.AllLuxuries, luxuries.All(inCiv.Contains));
			Check(player, Achievement.AllLuxuriesInCapital, luxuries.All(inCapital.ContainsKey));
			Check(player, Achievement.AllStrategicResources, strategic.All(inCiv.Contains));
			Check(player, Achievement.AllStrategicResourcesInCapital, strategic.All(inCapital.ContainsKey));
		}

		private static void Check(Player player, Achievement achievement, bool achieved) {
			if (achieved && player.palace.Earn(achievement)) {
				log.Information("{Player} earns a palace upgrade: {Achievement}", player, achievement);
			}
		}

		// As a civ advances from an era (FUN_0056d7f0): if no civ is in a
		// later one, it is the first into the next era (or, from the last
		// era, has completed it).
		public static void OnEraCompleted(Player player, GameData gameData, int era) {
			if (player.IsIndependent || era < 0 || era > 3) {
				return;
			}
			if (gameData.players.Any(p => p != player && !p.defeated && p.EraIndex() > era)) {
				return;
			}
			Check(player, (Achievement)(1u << era), true);
		}

		// The destroyer of a civ earns an upgrade (FUN_004e5870).
		public static void OnCivDestroyed(Player destroyed, Player destroyer) {
			if (destroyer == null || destroyer.IsIndependent || destroyed == null || destroyed.IsIndependent) {
				return;
			}
			Check(destroyer, Achievement.DestroyedACiv, true);
		}

		// At the start of a human's turn, tells them of upgrades earned since
		// last told (FUN_0056c5a0: the PALACE_VIEW_UPGRADE popup, after which
		// the palace view opens to place them).
		public static void AnnounceToHuman(Player player) {
			if (!player.isHuman || !player.palace.HasUpgradeToAnnounce) {
				return;
			}
			player.palace.MarkAnnounced();
			new MsgShowPalaceUpgrade(player).send();
		}
	}
}
