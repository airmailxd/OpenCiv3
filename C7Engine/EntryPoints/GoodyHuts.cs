using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine;

// Goody huts: tribal villages scattered over the map at the start of the
// game. The first civ to walk a land unit into one gets what's inside, and the
// hut is gone.
//
// As in Civ3, a hut holds gold, a tech, barbarians, a settler, a conscript
// Warrior, or a city. What's inside is picked at random, on a sliding scale:
// the easier the difficulty level the better the odds of a good outcome and
// the fewer the barbarians, and civs that have fallen behind the average are
// more likely to be helped to catch up, with a settler or a city when they
// have fewer cities, and a tech when they know fewer techs. AIs get the huts
// of a middling difficulty level.
//
// Which outcomes are possible follows the conditions Firaxis gave for Civ3
// (see https://forums.civfanatics.com/threads/goody-huts.60142/ and
// https://civfanatics.com/civ3/strategy/game-mechanics/probabilities-of-goody-huts-c3c/):
// gold only where there's no resource, techs only in the Ancient era,
// settlers only for civs with fewer cities than average and no settler of
// their own, and barbarians only for non-expansionist civs with a city and a
// military unit, away from any city.
public static class GoodyHuts {
	private static readonly ILogger log = Log.ForContext(typeof(GoodyHuts));

	public enum Outcome {
		Gold,
		Tech,
		Unit,
		Settler,
		City,
		Barbarians,
	}

	// The unit a hut's warriors join as, if the rules have it.
	internal const string HutUnitName = "Warrior";

	// The gold a hut holds in the Ancient era, and how much more in each
	// later era. Civ3's amounts aren't documented; this follows Civ2's
	// smallest treasure, 25 gold, which grew later in the game.
	internal const int AncientGold = 25;
	internal const int GoldPerLaterEra = 25;

	// How hard the game is for the player, from 0 (the easiest difficulty
	// level) to 1 (the hardest). AIs, and games whose level isn't among the
	// listed ones, count as halfway.
	internal static double Hardness(GameData gameData, Player player) {
		if (!player.isHuman) {
			return 0.5;
		}
		List<Difficulty> levels = gameData.difficulties;
		Difficulty current = gameData.gameDifficulty;
		int index = levels.FindIndex(d => d == current || (current?.Name != null && d.Name == current.Name));
		if (index < 0 || levels.Count < 2) {
			return 0.5;
		}
		return index / (double)(levels.Count - 1);
	}

	// Opens the hut on the tile for the unit that entered it, if there is
	// one and the unit may. Returns what was found, or null if nothing
	// happened.
	public static Outcome? Enter(GameData gameData, MapUnit unit, Tile tile) {
		if (!tile.hasGoodyHut || unit.owner.isBarbarians || !unit.IsLandUnit()) {
			return null;
		}
		tile.hasGoodyHut = false;
		TileChangeJournal.Record(tile);

		Player player = unit.owner;
		double hardness = Hardness(gameData, player);
		Dictionary<Outcome, double> weights = Weigh(gameData, unit, tile, hardness);
		Outcome outcome = Pick(weights);
		log.Information("{Player}'s {Unit} entered a goody hut at {Tile} and found {Outcome}", player, unit, tile, outcome);

		string message = Apply(gameData, unit, tile, ref outcome, hardness);
		if (player.isHuman) {
			new MsgShowMilitaryAdvisorPopup(player, message, happy: outcome != Outcome.Barbarians).send();
		}

		// What the hut brings up after the news goes after it, as the UI drops
		// the news if another popup is already showing.
		switch (outcome) {
			case Outcome.Tech:
				player.AskWhatToResearch(gameData);
				break;
			case Outcome.City when player.isHuman && tile.cityAtTile != null:
				// As for a city the player founds, open its screen.
				new MsgCityCreated(tile.cityAtTile) { recipient = player }.send();
				break;
		}
		return outcome;
	}

	// The odds of each outcome for this player. Outcomes that can't happen,
	// like a tech when there's nothing left to learn, have none.
	internal static Dictionary<Outcome, double> Weigh(GameData gameData, MapUnit unit, Tile tile, double hardness) {
		Player player = unit.owner;
		// Close to 1.25 on the easiest level, 0.75 on the hardest.
		double generosity = 1.25 - 0.5 * hardness;
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();

		Dictionary<Outcome, double> weights = new() {
			[Outcome.Gold] = tile.Resource == null || tile.Resource == Resource.NONE ? 30 : 0,
			[Outcome.Unit] = HutUnitFor(gameData) != null ? 15 * generosity : 0,
		};

		double techBehind = Average(civs, p => p.knownTechs.Count) - player.knownTechs.Count;
		weights[Outcome.Tech] = player.EraIndex() == 0 && TechsToLearn(gameData, player).Count > 0
			? (15 + 10 * Math.Clamp(techBehind, 0, 2)) * generosity
			: 0;

		// Settlers and cities only go to civs with fewer cities than average.
		double citiesBehind = Average(civs, p => p.cities.Count) - player.cities.Count;
		double catchUp = citiesBehind > 0 ? (10 + 10 * Math.Min(citiesBehind, 2)) * generosity : 0;
		weights[Outcome.Settler] = SettlerFor(gameData, player) != null && !HasSettler(player) ? catchUp : 0;
		weights[Outcome.City] = CanFoundCity(player, tile) ? catchUp : 0;

		weights[Outcome.Barbarians] = CanReleaseBarbarians(gameData, unit, tile) ? 5 + 20 * hardness : 0;
		return weights;
	}

	private static double Average(List<Player> civs, Func<Player, int> measure) {
		return civs.Count == 0 ? 0 : civs.Average(measure);
	}

	private static Outcome Pick(Dictionary<Outcome, double> weights) {
		double total = weights.Values.Sum();
		double roll = GameData.rng.NextDouble() * total;
		// Enum order, so the same roll always picks the same outcome.
		foreach (Outcome outcome in Enum.GetValues<Outcome>()) {
			if (!weights.TryGetValue(outcome, out double weight) || weight <= 0) {
				continue;
			}
			if (roll < weight) {
				return outcome;
			}
			roll -= weight;
		}
		return Outcome.Gold;
	}

	// Gives the player what they found, returning the news for a human. It
	// sends nothing to the UI, so the news can go first (see Enter). If
	// what was picked can't happen after all (barbarians with nowhere to
	// stand), the hut holds gold instead, and the outcome says so.
	private static string Apply(GameData gameData, MapUnit unit, Tile tile, ref Outcome outcome, double hardness) {
		Player player = unit.owner;
		switch (outcome) {
			case Outcome.Tech: {
				List<Tech> techs = TechsToLearn(gameData, player);
				Tech tech = techs[GameData.rng.Next(techs.Count)];
				player.AcquireTech(gameData, tech);
				return $"The villagers have taught us the secrets of {tech.Name}!";
			}
			case Outcome.Unit: {
				UnitPrototype type = HutUnitFor(gameData);
				MapUnit recruit = gameData.SpawnUnit(player, type, tile);
				// Conscripts, the least experienced level.
				if (gameData.experienceLevels.Count > 0) {
					recruit.experienceLevel = gameData.experienceLevels[0];
					recruit.experienceLevelKey = recruit.experienceLevel.key;
					recruit.hitPointsRemaining = recruit.maxHitPoints;
				}
				return $"Warriors from the village have joined our cause!";
			}
			case Outcome.Settler: {
				UnitPrototype type = SettlerFor(gameData, player);
				gameData.SpawnUnit(player, type, tile);
				return "Nomads from the village have joined us, eager to found a new city!";
			}
			case Outcome.City: {
				City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
				return $"The villagers have joined our civilization, founding the city of {city.name}!";
			}
			case Outcome.Barbarians: {
				if (ReleaseBarbarians(gameData, tile, hardness) > 0) {
					return "The village was full of barbarians, and they're out for blood!";
				}
				outcome = Outcome.Gold;
				goto default;
			}
			case Outcome.Gold:
			default: {
				int gold = GoldFor(player);
				player.gold += gold;
				return $"The villagers have given us {gold} gold in tribute!";
			}
		}
	}

	internal static int GoldFor(Player player) {
		return AncientGold + GoldPerLaterEra * Math.Max(0, player.EraIndex());
	}

	private static List<Tech> TechsToLearn(GameData gameData, Player player) {
		return player.GetAvailableTechsToResearch(gameData.techs).ToList();
	}

	// The Warrior, or failing that the cheapest fighting land unit that
	// needs no tech.
	internal static UnitPrototype HutUnitFor(GameData gameData) {
		return gameData.unitPrototypes.FirstOrDefault(p => p.name == HutUnitName)
			?? gameData.unitPrototypes
				.Where(p => p.IsLandUnit() && p.requiredTech == null && p.attack > 0
					&& !p.isSettler && !p.isWorker && !p.isLeader && !p.isArmy && p.populationCost == 0)
				.OrderBy(p => p.shieldCost)
				.FirstOrDefault();
	}

	private static UnitPrototype SettlerFor(GameData gameData, Player player) {
		return gameData.unitPrototypes
			.Where(p => p.isSettler && p.IsLandUnit() && p.producibleBy.Contains(player.civilization) && player.HasRequiredTechnology(p))
			.OrderBy(p => p.shieldCost)
			.FirstOrDefault();
	}

	// Whether the player has a settler, or a city building one.
	private static bool HasSettler(Player player) {
		return player.units.Any(u => u.unitType.isSettler)
			|| player.cities.Any(c => c.itemBeingProduced is UnitPrototype p && p.isSettler);
	}

	// Whether the villagers can found a city where the hut stood: somewhere
	// a settler could, outside any other civ's borders.
	private static bool CanFoundCity(Player player, Tile tile) {
		if (tile.HasCity() || !tile.IsAllowCities()) {
			return false;
		}
		if (tile.owningCity != null && tile.owningCity.owner != player) {
			return false;
		}
		return tile.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity());
	}

	// Barbarians only come out of huts when they're in the game at all, for
	// units that can fight (so not scouts), away from the player's own lands
	// and from any city, and for civs that aren't expansionist and have a
	// city and a military unit.
	private static bool CanReleaseBarbarians(GameData gameData, MapUnit unit, Tile tile) {
		if (gameData.barbarianInfo.barbarianActivity == BarbarianActivity.None || gameData.barbarianInfo.basicBarbarian == null) {
			return false;
		}
		Player player = unit.owner;
		if (unit.unitType.attack <= 0 || tile.owningCity?.owner == player) {
			return false;
		}
		if (player.civilization?.traits.Contains(Civilization.Trait.Expansionist) == true
			|| player.cities.Count == 0 || !player.units.Any(u => u.unitType.attack > 0)) {
			return false;
		}
		if (tile.neighbors.Values.Any(n => n != Tile.NONE && n.HasCity())) {
			return false;
		}
		return gameData.players.Any(p => p.isBarbarians) && BarbarianTiles(tile).Count > 0;
	}

	private static List<Tile> BarbarianTiles(Tile tile) {
		return tile.neighbors.Values
			.Where(n => n != Tile.NONE && n.IsLand() && !n.IsImpassable() && !n.HasCity() && n.unitsOnTile.Count == 0)
			.ToList();
	}

	// Spawns barbarians around the hut: more of them on harder levels, and an
	// advanced one on the hardest. Returns how many.
	private static int ReleaseBarbarians(GameData gameData, Tile tile, double hardness) {
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		BarbarianInfo info = gameData.barbarianInfo;
		int count = 1 + (hardness >= 0.5 ? 1 : 0) + (hardness >= 0.85 ? 1 : 0);
		List<Tile> tiles = BarbarianTiles(tile);
		int spawned = 0;
		for (int i = 0; i < count && tiles.Count > 0; ++i) {
			Tile at = tiles[GameData.rng.Next(tiles.Count)];
			tiles.Remove(at);
			UnitPrototype type = hardness >= 0.85 && i == 0 && info.advancedBarbarian != null ? info.advancedBarbarian : info.basicBarbarian;
			gameData.SpawnUnit(barbarians, type, at);
			++spawned;
		}
		return spawned;
	}
}
