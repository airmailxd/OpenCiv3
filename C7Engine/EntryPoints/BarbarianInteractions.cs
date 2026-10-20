using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

public class BarbarianInteractions {
	// Barbarians with nothing to do stay in their camp, so stop spawning at a
	// camp once it holds this many units, rather than piling them up forever.
	// TODO: Make configurable
	internal const int MaxUnitsPerCamp = 3;

	// The gold a civ earns for dispersing a barbarian camp.
	// TODO: make this configurable
	public const int CampDispersalGold = 25;

	// The name of the barbarian tribe at the tile, for Civ3's news of huts
	// and camps ("We got $NUM0 gold from the $BARBARIAN0 tribe's village.",
	// Conquests' Text/script.txt, #GOODY_MONEY). Civ3's tribe names are the
	// barbarians' list in the BIQ, where other civs have city names: "in the
	// "Civilizations" tab of the editor, you can select "Barbarians" from
	// the dropdown"
	// (https://forums.civfanatics.com/threads/names-of-barb-tribed.30374/),
	// 76 of them in Conquests ("Chanca" to "Kushite", then "Barbarian"),
	// and "the names of tribes are chosen at random from the list"
	// (https://civilization.fandom.com/wiki/Barbarian_tribes_(Civ3)).
	// UNVERIFIED: how Civ3 picks one for a hut or camp. Here it is picked by
	// the tile, so that the same hut or camp always has the same tribe and
	// the game's random numbers are left alone. "Barbarian" if the rules
	// have no list.
	public static string TribeName(GameData gameData, Tile tile) {
		List<string> names = gameData?.civilizations.FirstOrDefault(c => c.isBarbarian)?.cityNames;
		if (names == null || names.Count == 0) {
			names = gameData?.players.FirstOrDefault(p => p.isBarbarians)?.civilization?.cityNames;
		}
		if (names == null || names.Count == 0) {
			return "Barbarian";
		}
		long x = tile?.XCoordinate ?? 0;
		long y = tile?.YCoordinate ?? 0;
		long hash = Math.Abs(x * 7919 + y * 104729);
		return names[(int)(hash % names.Count)];
	}

	// Removes the barbarian camp on the tile, if any, and pays the player who
	// dispersed it. Returns whether there was a camp to disperse.
	public static bool DisperseCamp(GameData gameData, Tile tile, Player player) {
		if (!tile.hasBarbarianCamp || player.isBarbarians) {
			return false;
		}
		gameData.map.barbarianCamps.Remove(tile);
		tile.hasBarbarianCamp = false;
		player.gold += CampDispersalGold;
		return true;
	}

	// Barbarian camps can't stand within a civ's borders, so any that have
	// come to be inside them, by borders growing with culture or a city
	// changing hands, are dispersed. As when a new city's borders take in a
	// camp, the owner of the borders is paid for each one. Call this after
	// updating tile owners. (Founding a city does its own dispersal, to name
	// the city in its message.)
	public static void DisperseCampsWithinBorders(GameData gameData) {
		Dictionary<Player, int> dispersedBy = new();
		foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
			Player owner = camp.owningCity?.owner;
			if (owner == null || !DisperseCamp(gameData, camp, owner)) {
				continue;
			}
			dispersedBy.TryGetValue(owner, out int count);
			dispersedBy[owner] = count + 1;
		}
		foreach ((Player owner, int count) in dispersedBy) {
			if (owner.IsToldNews) {
				string camps = count == 1 ? "a barbarian encampment" : $"{count} barbarian encampments";
				new MsgShowMilitaryAdvisorPopup(owner, $"Our borders have taken in {camps}, dispersing them and earning {count * CampDispersalGold} gold!", happy: true).send();
			}
		}
	}

	// The barbarians' part of the end of a turn: new camps, then units
	// from the camps.
	public static void DoTurn(GameData gameData) {
		MaybeStartUprisings(gameData);
		MaybeAddCamp(gameData);
		SpawnBarbarians(gameData);
	}

	// Barbarian uprisings: once, when the second civ reaches the Middle
	// Ages, and again when the second reaches the Industrial Age.
	//   "When the second civ leaves the ancient era there is a massive
	//   barbarian uprising" (Scyphax,
	//   https://forums.civfanatics.com/threads/dealing-with-the-massive-barbarian-uprising.42404/)
	//   "Barbarian uprisings occur when 2 civs have hit the middle ages."
	//   (Arrian) "if you still have uncolonized tiles by the time 2 civs
	//   reach the industrial era... you will have a barb uprising too."
	//   (Konquest02, https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/72550-barbarians-wtf-my-first-civ-iii-experience)
	// The units come out of the camps: "On uprising, each camp existing at
	// that time gets a number of units extra. The number depends on the
	// selected level." (Lt. 'Killer' M.) "These units always seem to be
	// horsemen" (ProPain), i.e. the advanced barbarian unit
	// (https://forums.civfanatics.com/threads/barbarian-camps.42410/,
	// and "the hordes appear in those camps when 2 civs have entered the
	// middle age", Amask, https://forums.civfanatics.com/threads/barbarian-hordes.44003/).
	// Camps only stand on unclaimed land, so an Industrial Age uprising
	// comes to nothing once the land is settled, as Konquest02 says. Civ3
	// announces it with "We have heard reports of a massive barbarian
	// uprising near $CITY0!" (script.txt, SUMMARY_BARBARIAN_EXPLOSION_CITY).
	// UNVERIFIED (no Civ3 source found): how many units each camp gets at
	// each level. Players report 10 to 30+ horsemen in all, e.g. "4 packs
	// of 8 horsemen", so these are guesses in that range. Also unverified
	// is whether the Modern Era brings one too; it doesn't here.
	public static int UprisingUnitsPerCamp(BarbarianActivity activity) {
		return activity switch {
			BarbarianActivity.Sedentary => 2,
			BarbarianActivity.Roaming => 4,
			BarbarianActivity.Restless => 6,
			BarbarianActivity.Raging => 8,
			_ => 0,
		};
	}

	// How many civs must reach an era to set off its uprising.
	public const int CivsInEraForUprising = 2;

	// The number of civs still in the game that have reached the era.
	public static int CivsInEra(GameData gameData, int eraIndex) {
		int count = 0;
		foreach (Player player in gameData.players) {
			if (!player.isBarbarians && !player.defeated && player.isIncludedInGame && player.EraIndex() >= eraIndex) {
				++count;
			}
		}
		return count;
	}

	// Sets off the Middle Ages and Industrial Age uprisings once each, when
	// the second civ reaches the era. Returns the number of units spawned.
	public static int MaybeStartUprisings(GameData gameData) {
		BarbarianInfo info = gameData.barbarianInfo;
		int spawned = 0;

		bool middleAges = CivsInEra(gameData, EraUtils.GetEraIndex(EraUtils.MIDDLE_AGES_CVLPD)) >= CivsInEraForUprising;
		if (info.middleAgesUprisingHappened == null) {
			// Not checked before: an era already reached counts as done.
			info.middleAgesUprisingHappened = middleAges;
		} else if (middleAges && info.middleAgesUprisingHappened == false) {
			info.middleAgesUprisingHappened = true;
			spawned += StartUprising(gameData);
		}

		bool industrialAge = CivsInEra(gameData, EraUtils.GetEraIndex(EraUtils.INDUSTRIAL_AGE_CVLPD)) >= CivsInEraForUprising;
		if (info.industrialAgeUprisingHappened == null) {
			info.industrialAgeUprisingHappened = industrialAge;
		} else if (industrialAge && info.industrialAgeUprisingHappened == false) {
			info.industrialAgeUprisingHappened = true;
			spawned += StartUprising(gameData);
		}

		return spawned;
	}

	// Every camp gets a band of advanced barbarians (normally horsemen), and
	// each civ with a city nearest one of the camps hears of it.
	public static int StartUprising(GameData gameData) {
		Player barbarians = gameData.players.Find(p => p.isBarbarians);
		BarbarianInfo info = gameData.barbarianInfo;
		int perCamp = UprisingUnitsPerCamp(info.barbarianActivity);
		UnitPrototype unitType = info.advancedBarbarian ?? info.basicBarbarian;
		if (barbarians == null || perCamp <= 0 || unitType == null) {
			return 0;
		}

		int spawned = 0;
		Dictionary<Player, City> toldOf = new();
		foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
			for (int i = 0; i < perCamp; ++i) {
				gameData.SpawnUnit(barbarians, unitType, camp);
				++spawned;
			}

			City nearest = null;
			foreach (Player player in gameData.players) {
				if (player.isBarbarians) {
					continue;
				}
				foreach (City city in player.cities) {
					if (nearest == null || city.location.DistanceTo(camp) < nearest.location.DistanceTo(camp)) {
						nearest = city;
					}
				}
			}
			if (nearest != null) {
				toldOf.TryAdd(nearest.owner, nearest);
			}
		}

		foreach ((Player player, City city) in toldOf) {
			if (player.IsToldNews) {
				new MsgShowMilitaryAdvisorPopup(player, $"We have heard reports of a massive barbarian uprising near {city.name}!", happy: false).send();
			}
		}
		return spawned;
	}

	// New barbarian camps appear during the game, and dispersed ones are
	// replaced, wherever no civ can see:
	//   "They always appear in the place which no unit can see at the
	//   moment but it can be in explored terrain as well. They also
	//   regenerate, that means when somebody disperses a camp, another one
	//   is created (assuming there is available place in the fog of war)."
	//   (Gen, https://forums.civfanatics.com/threads/barbarian-camps.42410/)
	// The Civ3 Civilopedia (GCON_Barbarians) agrees: after a camp is
	// destroyed "the survivors will move to a new site in explored or
	// unexplored territory and begin raiding from there". Camps can't stand
	// inside a civ's borders (see DisperseCampsWithinBorders), so they only
	// appear on unowned land.
	// UNVERIFIED (no Civ3 source found): how often a camp appears at each
	// barbarian activity level. These per-turn chances are guesses, rolled
	// once a turn, with the camp spacing rules of map generation limiting
	// how many there can be. Also unverified is ProPain's observation in the
	// same thread that only military units keep camps away; here any unit
	// or city's sight does.
	public static double NewCampChance(BarbarianActivity activity) {
		return activity switch {
			BarbarianActivity.Sedentary => 0.02,
			BarbarianActivity.Roaming => 0.04,
			BarbarianActivity.Restless => 0.06,
			BarbarianActivity.Raging => 0.08,
			_ => 0,
		};
	}

	// Limits on camps and barbarians, from community testing (TimBentley,
	// https://forums.civfanatics.com/threads/barbarian-information.267101/):
	//   "No barb camps will appear until everyone has settled and there are
	//   at least (2*NumPlayers) cities." "The maximum number of camps is the
	//   number of players". The maximum number of barbarians is "2, 3, or 4"
	//   "(for roaming, restless, and raging) * NumPlayers."
	// NumPlayers is taken as the civs still in the game, barbarians aside.
	// UNVERIFIED: the post gives no unit limit for Sedentary barbarians, so
	// they have none here.
	public static int NumPlayers(GameData gameData) {
		int count = 0;
		foreach (Player player in gameData.players) {
			if (!player.isBarbarians && !player.defeated && player.isIncludedInGame) {
				++count;
			}
		}
		return count;
	}

	// The most camps there may be: one per player.
	public static int MaxCamps(GameData gameData) {
		return NumPlayers(gameData);
	}

	// Whether new camps may appear yet: once every civ has a city, and there
	// are at least two cities per player.
	public static bool CampsMayAppear(GameData gameData) {
		int cities = 0;
		foreach (Player player in gameData.players) {
			if (player.isBarbarians || player.defeated || !player.isIncludedInGame) {
				continue;
			}
			if (player.cities.Count == 0) {
				return false;
			}
			cities += player.cities.Count;
		}
		return cities >= 2 * NumPlayers(gameData);
	}

	// The most barbarian units camps may spawn up to, or -1 for no limit.
	public static int MaxBarbarianUnits(GameData gameData) {
		int perPlayer = gameData.barbarianInfo.barbarianActivity switch {
			BarbarianActivity.Roaming => 2,
			BarbarianActivity.Restless => 3,
			BarbarianActivity.Raging => 4,
			_ => -1,
		};
		return perPlayer < 0 ? -1 : perPlayer * NumPlayers(gameData);
	}

	// Rolls for a new barbarian camp and places it if the roll succeeds and
	// there is a suitable tile, while there are fewer camps than the limit
	// and enough cities (see MaxCamps and CampsMayAppear). Returns the new
	// camp's tile, or null.
	public static Tile MaybeAddCamp(GameData gameData) {
		double chance = NewCampChance(gameData.barbarianInfo.barbarianActivity);
		if (chance <= 0 || GameData.rng.NextDouble() >= chance) {
			return null;
		}
		if (gameData.map.barbarianCamps.Count >= MaxCamps(gameData) || !CampsMayAppear(gameData)) {
			return null;
		}
		List<Tile> sites = PossibleNewCampSites(gameData);
		if (sites.Count == 0) {
			return null;
		}
		Tile site = sites[GameData.rng.Next(sites.Count)];
		AddCamp(gameData, site);
		return site;
	}

	// A camp a unit has destroyed moves elsewhere: "By destroying a tribal
	// village you can stop the raids from that village. But the survivors
	// will move to a new site in explored or unexplored territory and begin
	// raiding from there." (Conquests Civilopedia, GCON_Barbarians). The new
	// camp goes on a random site where one may appear (see
	// PossibleNewCampSites), within the limit on camps. Camps dispersed by
	// borders or a new city don't move. Returns the new camp's tile, or null.
	// UNVERIFIED: whether the move waits for the conditions for new camps
	// (CampsMayAppear); here it doesn't, as the tribe already had a camp.
	public static Tile RelocateCamp(GameData gameData) {
		BarbarianActivity activity = gameData.barbarianInfo.barbarianActivity;
		if (activity is BarbarianActivity.None or BarbarianActivity.Sedentary) {
			return null;
		}
		if (gameData.map.barbarianCamps.Count >= MaxCamps(gameData)) {
			return null;
		}
		List<Tile> sites = PossibleNewCampSites(gameData);
		if (sites.Count == 0) {
			return null;
		}
		Tile site = sites[GameData.rng.Next(sites.Count)];
		AddCamp(gameData, site);
		return site;
	}

	// Places a camp on the tile, guarded like the camps placed with the map
	// by a basic barbarian unit.
	public static void AddCamp(GameData gameData, Tile tile) {
		tile.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(tile);
		Player barbarians = gameData.players.Find(p => p.isBarbarians);
		if (barbarians != null && gameData.barbarianInfo.basicBarbarian != null) {
			gameData.SpawnUnit(barbarians, gameData.barbarianInfo.basicBarbarian, tile);
		}
	}

	// The tiles a new camp may appear on: unowned land, empty of units,
	// cities and goody huts, that no civ can see, which meet the terrain and
	// spacing rules of the camps placed with the map.
	public static List<Tile> PossibleNewCampSites(GameData gameData) {
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();
		foreach (Player civ in civs) {
			civ.tileKnowledge.RecomputeActiveTiles();
		}

		// The continents' sizes aren't kept with a saved game, so count them.
		Dictionary<int, int> continentSizes = new();
		foreach (Tile t in gameData.map.tiles) {
			continentSizes.TryGetValue(t.continent, out int size);
			continentSizes[t.continent] = size + 1;
		}

		int spacing = gameData.rules?.MaxRankOfBarbarianCampTiles ?? 0;
		List<Tile> result = new();
		foreach (Tile t in gameData.map.tiles) {
			if (!t.IsLand() || t.hasBarbarianCamp || t.hasGoodyHut || t.cityAtTile != null
				|| t.owningCity != null || t.unitsOnTile.Count > 0) {
				continue;
			}
			if (civs.Exists(civ => civ.tileKnowledge.isActiveTile(t))) {
				continue;
			}
			if (MapGenerator.IsCampSiteSuitable(t, c => continentSizes.GetValueOrDefault(c), spacing)) {
				result.Add(t);
			}
		}
		return result;
	}

	public static int SpawnBarbarians(GameData gameData) {
		Player barbPlayer = gameData.players.Find(player => player.isBarbarians);
		var activity = gameData.barbarianInfo.barbarianActivity;

		if (activity == BarbarianActivity.None)
			return 0;

		// Each camp has a chance to spawn a unit each turn, set by the
		// barbarian activity level, and held back while civs are still
		// founding their second city.
		var spawnRate = DetermineSpawnRate(activity) * SettledCivShare(gameData);
		if (spawnRate <= 0) {
			return 0;
		}
		int barbariansSpawned = 0;
		bool advancedAllowed = AdvancedBarbariansAvailable(gameData);
		// No more come out of the camps once the barbarians have as many
		// units as the limit (see MaxBarbarianUnits).
		int maxUnits = MaxBarbarianUnits(gameData);
		int units = barbPlayer?.units.Count ?? 0;
		foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
			if (maxUnits >= 0 && units >= maxUnits) {
				break;
			}
			if (camp.unitsOnTile.Count >= MaxUnitsPerCamp) {
				continue;
			}
			if (GameData.rng.NextDouble() >= spawnRate) {
				continue;
			}

			UnitPrototype unitType = SelectBarbarianUnitType(gameData.barbarianInfo, camp, advancedAllowed);
			Tile tile = SelectSpawnTile(barbPlayer, camp, unitType);
			if (tile != null) {
				gameData.SpawnUnit(barbPlayer, unitType, tile);
				++barbariansSpawned;
				++units;
			}
		}

		return barbariansSpawned;
	}

	// The share of civs still in the game that have at least two cities.
	// Barbarians spawn at their full rate once every civ has, and not at all
	// until one has, so they don't overrun civs that are just starting out.
	public static float SettledCivShare(GameData gameData) {
		int civs = 0, settled = 0;
		foreach (Player player in gameData.players) {
			if (player.isBarbarians || player.defeated || !player.isIncludedInGame) {
				continue;
			}
			++civs;
			if (player.cities.Count >= 2) {
				++settled;
			}
		}
		return civs == 0 ? 1 : (float)settled / civs;
	}

	/// <summary>
	/// Apply barbarian activity level to barbarian unit spawn rate. Currently NOT based on Civ3 values.
	/// TODO: Make configurable
	/// TODO: Determine what these values are in Civ3 (could be a constant across all) 
	/// </summary>
	private static float DetermineSpawnRate(BarbarianActivity activity) {
		switch (activity) {
			case BarbarianActivity.None:
				return 0;
			case BarbarianActivity.Sedentary:
				return 0.03f;
			case BarbarianActivity.Roaming:
				return 0.05f;
			case BarbarianActivity.Restless:
				return 0.08f;
			case BarbarianActivity.Raging:
				return 0.12f;
			default:
				throw new ArgumentOutOfRangeException(nameof(activity), activity, null);
		}

	}

	// The tech that lets barbarians field their advanced unit (normally
	// horsemen), and how many civs must know it first: "once two civs know
	// horseback riding, Barb camps can spawn horsemen" (Padma,
	// https://forums.civfanatics.com/threads/barbarian-horsemen.50956/).
	public const string AdvancedBarbarianTechName = "Horseback Riding";
	public const int CivsKnowingTechForAdvancedBarbarians = 2;

	// Whether barbarians may field their advanced unit yet: once two civs in
	// the game know Horseback Riding. A ruleset without that tech has
	// nothing to gate them on, so they may.
	public static bool AdvancedBarbariansAvailable(GameData gameData) {
		Tech horsebackRiding = gameData.techs?.Find(t => string.Equals(t.Name, AdvancedBarbarianTechName, StringComparison.OrdinalIgnoreCase));
		if (horsebackRiding == null) {
			return true;
		}
		int civsKnowing = 0;
		foreach (Player player in gameData.players) {
			if (!player.isBarbarians && player.knownTechs.Contains(horsebackRiding.id)) {
				++civsKnowing;
			}
		}
		return civsKnowing >= CivsKnowingTechForAdvancedBarbarians;
	}

	public static UnitPrototype SelectBarbarianUnitType(BarbarianInfo barbInfo, Tile tile, bool advancedAllowed = true) {
		// Coastal camps have a 20% chance of spawning a sea unit
		if (tile.NeighborsWater() && GameData.rng.Next(100) < 20) {
			return barbInfo.barbarianSeaUnitProto;
		}

		// Land units are generated in a 1:3 ratio, one advanced unit for every three basic barbarians.
		// UNVERIFIED (no Civ3 source found): Civ3 uses only the RULE's basic and advanced barbarian
		// and barbarian sea unit (https://forums.civfanatics.com/threads/barbarians-best-way-to-upgrade.47200/),
		// but no source gives a ratio between them.
		bool advanced = GameData.rng.Next(100) < 25;
		return advanced && advancedAllowed && barbInfo.advancedBarbarian != null ? barbInfo.advancedBarbarian : barbInfo.basicBarbarian;
	}

	public static Tile SelectSpawnTile(Player player, Tile camp, UnitPrototype unitType) {
		// Spawn land units at the camp
		if (unitType.IsLandUnit())
			return camp;

		// Spawn sea units on a coast tile, but not in a lake or on a tile occupied by another player
		bool CanSpawnSeaUnits(Tile t) =>
			t.IsCoast() && !t.isFreshWater && t.unitsOnTile.TrueForAll(u => u.owner == player);

		if (unitType.IsSeaUnit()) {
			// Check first two ranks around camp for a suitable tile, or bail with a null 
			return camp.FindInRing(1, CanSpawnSeaUnits)
				?? camp.FindInRing(2, CanSpawnSeaUnits);
		}

		// Default to camp 
		return camp;
	}
}
