using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine {
	// Civ3's volcanic eruptions.
	//
	// - "A volcano is a disaster waiting to happen, as it can suddenly erupt
	//   with no warning... When a volcano erupts it spews forth lava -- in
	//   the form of pollution -- that will mar the landscape of many of the
	//   tiles adjacent to the volcanic mountain. Volcanic "fallout" can be
	//   cleaned up by workers" (Conquests Civilopedia, GCON_Volcanic_Eruptions).
	// - How often: "Each volcano on the map will erupt at least once within
	//   the indicated eruption period (shown in years)", the editor's
	//   eruption period, at least 100 years
	//   (https://forums.civfanatics.com/threads/questions-regarding-volcanos.330741/);
	//   conquests.biq's is 5000 (Rules.VolcanoEruptionPeriod).
	// - Where: "between 1 and 8 tiles damaged" of "the 8 tiles immediately
	//   surrounding the volcano", and "it doesn't go on the coast" (same
	//   thread); "only tiles touching the volcano will be affected"
	//   (https://forums.civfanatics.com/threads/volcanos-why.68861/).
	// - What: "everything in those tiles is completely destroyed (no
	//   survivors)", and "The volcano destroys the city leaving ruins" (the
	//   thread above); "The lava flow destroys the city entirely -- units,
	//   improvements and all", "only cities/roads/units directly next to...
	//   the volcano are destroyed" and the eruption pollutes
	//   (https://forums.civfanatics.com/threads/i-hate-volcanos.96133/);
	//   "they will kill troops on affected tiles", "When they erupt they
	//   certainly pollute surrounding squares" (the volcanos-why thread).
	// - The messages are Civ3's (Conquests/Text/Script.txt): VLC_ERUPTS
	//   "Volcano has erupted!", VLC_KILLS_UNIT "Our units were burned to a
	//   crisp by a lava flow!" and VLC_KILLS_CITY "$CITY0 is torched by the
	//   volcanic eruption!".
	//
	// UNVERIFIED (simplest consistent behaviour):
	// - The chance. Each turn, each volcano erupts with a chance of the years
	//   the turn spans over the eruption period (50 years over 5000, 1%, in
	//   the first turns of a standard game), so on average once a period.
	// - Which tiles. The number of tiles hit is even from 1 to all the land
	//   tiles around the volcano, and which ones is random. Lava reaching two
	//   tiles out "on rare occasions" (the volcanos-why thread; others have
	//   only seen the 8 tiles around it) isn't modelled.
	// - What's destroyed on a tile hit: every unit, the city, and the
	//   man-made improvements (roads, mines, irrigation, fortresses,
	//   outposts, colonies); the tile is left polluted.
	// - Who is told: VLC_ERUPTS goes to every civ that has explored the
	//   volcano.
	// - The "Volcano is now active!" (VLC_ACTIVE) smoking warning a few
	//   turns before ("You usually get a warning before eruption, but...
	//   it can also go off without warning", the volcanos-why thread) isn't
	//   modelled: eruptions come without warning.
	public static class Volcano {
		private static readonly ILogger log = Log.ForContext(typeof(Volcano));

		public class EruptionResult {
			public Tile volcano;
			// The tiles the lava hit.
			public List<Tile> tilesHit = new();
			public int unitsDestroyed;
			public List<string> citiesDestroyed = new();
		}

		// Rolls for every volcano; called once per round, after every player
		// has played. Volcanoes are gone through in map order, so the rolls
		// are the same each time a game is replayed.
		public static List<EruptionResult> ProcessEndOfRound(GameData gameData) {
			List<EruptionResult> results = new();
			List<Tile> tiles = gameData.map?.tiles;
			int period = gameData.rules?.VolcanoEruptionPeriod ?? 0;
			if (tiles == null || period <= 0) {
				return results;
			}
			(int timeUnits, int unitsPerYear) = TimeThisTurn(gameData);
			if (timeUnits <= 0) {
				return results;
			}
			// The volcanoes are found first, so that one erupting doesn't
			// change which others roll.
			List<Tile> volcanoes = tiles.Where(t => t.overlayTerrainType.IsVolcano).ToList();
			foreach (Tile volcano in volcanoes) {
				if (GameData.rng.Next(period * unitsPerYear) < timeUnits) {
					results.Add(Erupt(gameData, volcano));
				}
			}
			return results;
		}

		// The time units this turn spans, and how many of them make a year.
		private static (int, int) TimeThisTurn(GameData gameData) {
			TimeOptions time = gameData.timeOptions;
			if (time == null) {
				// No calendar: a year a turn.
				return (1, 1);
			}
			int units = time.GetRawNumber(gameData.turn) - time.GetRawNumber(gameData.turn - 1);
			int perYear = time.baseUnit switch {
				TimeUnit.Months => 12,
				TimeUnit.Weeks => 52,
				TimeUnit.Days => 365,
				TimeUnit.Hours => 365 * 24,
				_ => 1,
			};
			return (units, perYear);
		}

		// The tiles lava from the volcano can reach: the land tiles around
		// it, in a fixed order.
		public static List<Tile> TilesInReach(Tile volcano) {
			List<Tile> result = new();
			foreach (Tile t in volcano.neighbors.Values) {
				if (t != null && t != Tile.NONE && !t.IsWater() && !result.Contains(t)) {
					result.Add(t);
				}
			}
			return result;
		}

		// Erupts the volcano: lava hits from one to all of the land tiles
		// around it.
		public static EruptionResult Erupt(GameData gameData, Tile volcano) {
			List<Tile> reach = TilesInReach(volcano);
			List<Tile> hit = new();
			if (reach.Count > 0) {
				int count = GameData.rng.Next(1, reach.Count + 1);
				// A partial shuffle picks the tiles hit.
				for (int i = 0; i < count; ++i) {
					int j = GameData.rng.Next(i, reach.Count);
					(reach[i], reach[j]) = (reach[j], reach[i]);
					hit.Add(reach[i]);
				}
			}
			return Erupt(gameData, volcano, hit);
		}

		// Erupts the volcano, the lava hitting the given tiles, and tells the
		// players.
		public static EruptionResult Erupt(GameData gameData, Tile volcano, List<Tile> tilesHit) {
			EruptionResult result = new() { volcano = volcano };
			log.Information("The volcano at {Tile} erupts, hitting {Count} tiles", volcano, tilesHit.Count);

			// Told before the cities go, so a civ that loses its last one
			// still hears why.
			foreach (Player player in gameData.players) {
				if (player.IsToldNews && player.HasExploredTile(volcano)) {
					new MsgShowTemporaryPopup("Volcano has erupted!", volcano, player).send();
				}
			}

			HashSet<Player> lostUnits = new();
			foreach (Tile t in tilesHit) {
				result.tilesHit.Add(t);
				foreach (MapUnit unit in t.unitsOnTile.ToArray()) {
					// Units removed with their transport or army are already
					// gone, but count as destroyed all the same.
					if (unit.hitPointsRemaining > 0) {
						unit.RemoveFromPlay();
					}
					++result.unitsDestroyed;
					if (unit.owner != null) {
						lostUnits.Add(unit.owner);
					}
				}

				if (t.HasCity()) {
					City city = t.cityAtTile;
					Player owner = city.owner;
					result.citiesDestroyed.Add(city.name);
					log.Information("{City} is destroyed by the eruption", city.name);
					if (owner != null && owner.IsToldNews) {
						new MsgShowTemporaryPopup($"{city.name} is torched by the volcanic eruption!", t, owner).send();
					}
					CityInteractions.DestroyCity(city);
				}

				foreach (TerrainImprovement improvement in t.overlays.GetManMadeImprovements().ToList()) {
					t.overlays.Remove(improvement);
				}
				if (!t.HasPollution()) {
					Tile.TryAddPollution(t);
				}
				TileChangeJournal.Record(t);
			}

			foreach (Player player in lostUnits) {
				if (player.IsToldNews) {
					new MsgShowTemporaryPopup("Our units were burned to a crisp by a lava flow!", volcano, player).send();
				}
			}
			return result;
		}
	}
}
