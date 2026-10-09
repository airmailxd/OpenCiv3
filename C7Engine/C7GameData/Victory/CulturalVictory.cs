using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Won by culture: a civ wins when it has "either 20,000 culture in one
// city, or 100,000 culture across your empire"
// (https://civfanatics.com/civ3/faq/), and for the latter "you need to have
// twice as much culture as the second place civilization"
// (https://forums.civfanatics.com/threads/cultural-victory.114328/). The
// 20,000 one-city win is Conquests' (https://civfanatics.com/civ3/strategy/empire-management/guide-to-single-city-20k-cultural-victory/).
// The thresholds are the BIQ GAME section's "one city culture win" and "all
// cities culture win" (https://codehappy.net/apolyton/threads/105623-1.htm);
// the whole-civ one is scaled by the world's size (see ScaleForWorldSize).
public class CulturalVictory : IVictory {
	// Conquests' defaults, from the sources above.
	public const int DefaultOneCityWin = 20000;
	public const int DefaultAllCitiesWin = 100000;

	// How many times the culture of every rival a civ needs as well as
	// allCitiesWin: "twice as much culture as the second place
	// civilization" (above). UNVERIFIED: taken to be fixed, as no BIQ field
	// for it was found.
	public const int RivalMultiple = 2;

	// "The required amount of culture for the '100K' cultural victory
	// condition is variable in C3C, depending on the map size. The
	// requirement for a cultural victory is 60K on a tiny map, 80K on small,
	// 100K on standard, 130K on large, and 160K on huge"
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/,
	// also https://civfanatics.com/civ3/faq/), as a percentage of the
	// standard map's, by world size (tiny to huge).
	public static readonly int[] WorldSizePercent = [60, 80, 100, 130, 160];

	// The culture a whole civ needs on a world of the given size (its index
	// among worldSizes of them), from what it needs on a standard map.
	// UNVERIFIED: how Civ3 scales a BIQ's own "all cities culture win"; it
	// is scaled in the same proportion. Rulesets without Civ3's five world
	// sizes aren't scaled.
	public static int ScaleForWorldSize(int standardAllCitiesWin, int worldSize, int worldSizes) {
		if (worldSizes != WorldSizePercent.Length || worldSize < 0 || worldSize >= worldSizes) {
			return standardAllCitiesWin;
		}
		return (int)((long)standardAllCitiesWin * WorldSizePercent[worldSize] / 100);
	}

	private readonly int oneCityWin;
	private readonly int allCitiesWin;

	public CulturalVictory(int oneCityWin, int allCitiesWin) {
		this.oneCityWin = oneCityWin;
		this.allCitiesWin = allCitiesWin;
	}

	public string Header() => "Cultural";

	// The most culture any one of the player's cities has.
	public static int TopCityCulture(Player player) {
		if (player.hostFacts != null) {
			return player.hostFacts.topCityCulture;
		}
		int top = 0;
		foreach (City city in player.cities) {
			top = System.Math.Max(top, city.GetCulture());
		}
		return top;
	}

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		int topRival = 0;
		foreach (Player p in gameData.players) {
			if (p != player && !p.isBarbarians && !p.defeated) {
				topRival = System.Math.Max(topRival, CultureReport.TotalCulture(p));
			}
		}
		return new VictoryStatus {
			Player = player,
			Culture = CultureReport.TotalCulture(player),
			TopCityCulture = TopCityCulture(player),
			TopRivalCulture = topRival,
		};
	}

	public bool HasVictory(VictoryStatus status) {
		if (oneCityWin > 0 && status.TopCityCulture >= oneCityWin) {
			return true;
		}
		return allCitiesWin > 0 && status.Culture >= allCitiesWin
			&& (long)status.Culture >= (long)RivalMultiple * status.TopRivalCulture;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topCulture = rivalStatuses.OrderByDescending(r => r.Culture).FirstOrDefault();
		VictoryStatus topCity = rivalStatuses.OrderByDescending(r => r.TopCityCulture).FirstOrDefault();
		yield return [
			"Culture needed:",
			$"{allCitiesWin}",
			"Culture:",
			$"{status.Culture}",
			topCulture?.Player?.civilization?.name ?? "",
			topCulture == null ? "" : $"{topCulture.Culture}",
		];
		yield return [
			"One city needs:",
			$"{oneCityWin}",
			"Top city:",
			$"{status.TopCityCulture}",
			topCity?.Player?.civilization?.name ?? "",
			topCity == null ? "" : $"{topCity.TopCityCulture}",
		];
	}
}
