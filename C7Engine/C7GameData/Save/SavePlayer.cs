using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace C7GameData.Save {

	public class SavePlayer {
		public ID id;
		public int primaryColorIndex;
		public int secondaryColorIndex;
		public bool human = false;
		public bool hasPlayedCurrentTurn = false;
		public bool defeated = false;
		public bool isIncludedInGame = true;
		public bool canBePicked = true;
		public bool skipFirstTurn = false;

		// The name of the person playing, for human players in a hotseat game.
		public string name;

		public string civilization;

		// The tiles this player knows about, in the order they learned of
		// them. Older saves (and imported Civ3 games) list them here.
		public List<TileLocation> tileKnowledge = new List<TileLocation>();

		// The tiles this player knows about, in the order they learned of
		// them, in a compact form: the (URL-safe) base64 encoding of each tile's index in
		// the map's list of tiles, as the difference from the previous index
		// (zigzag encoded) written as a variable-length integer. Used instead
		// of tileKnowledge when every known tile is on the map.
		public string knownTileIndices;

		// A map from player id to the relationship this player has with the other player.
		public Dictionary<string, PlayerRelationship> playerRelationships = new();

		// The list of techs known by this player.
		public HashSet<ID> knownTechs = new();

		// The tech the player is currently researching.
		public ID currentlyResearchedTech;

		// The tech queue the player is currently researching.
		public List<ID> researchQueue = new();

		// The civilopedia name of the era this player is in.
		//
		// The civilopedia name is what is used for art lookups, not the actual
		// name.
		public string eraCivilopediaName;

		public int turnsUntilPriorityReevaluation = 0;

		// The values of the science/happiness/tax sliders (tax is implicit)
		// A value of 1 => 10%, a value of 10 => 100%.
		//
		// INVARIANT: LuxuryRate + ScienceRate + TaxRate = 10
		public int luxuryRate = 0;
		public int scienceRate = 5;
		public int taxRate = 5;

		// The amount of gold this player has.
		public int gold = 0;

		public int warWeariness;
		public bool hadGoldenAge;
		public int goldenAgeTurnsRemaining;
		public int citiesFounded;

		// The number of "beakers" (gold) spent on the currently researched
		// tech.
		public int beakers = 0;

		// The number of turns the player has been researching the current tech.
		public int turnsResearched = 0;

		// If the government is anarchy (or a govt with the transition bool set
		// to true), the turn number at which switching governments is allowed.
		public int inAnarchyUntilTurn = 0;

		// The current government of the player.
		public ID governmentId;

		public string alliance;

		// Whether one of this player's armies has won a battle.
		public bool hasVictoriousArmy = false;

		// Used when importing from .biq, to make it easier to distinguish barbarians from other players.
		// It's not meant to be saved in the json.
		[JsonIgnore]
		public bool isBarbarian { get; init; }

		public Player ToPlayer(GameMap map, List<Civilization> civilizations, List<Government> governments, List<Tech> techs, Rules rules, HashSet<Alliance> alliances) {
			Player player = new Player{
				id = id,
				isHuman = human,
				isIncludedInGame = isIncludedInGame,
				canBePicked = canBePicked,
				alliance = alliance is not null ? alliances.First(a => a.name == alliance) : null,
				hasPlayedThisTurn = hasPlayedCurrentTurn,
				skipFirstTurn = skipFirstTurn,
				name = name,
				defeated = defeated,
				primaryColorIndex = primaryColorIndex,
				secondaryColorIndex = secondaryColorIndex,
				civilization = civilization is not null ? civilizations.Find(civ => civ.name == civilization) : null,
				// Copied, so that the game doesn't change the save (or other
				// games made from it) as the player learns techs.
				knownTechs = knownTechs == null ? new HashSet<ID>() : new HashSet<ID>(knownTechs),
				eraCivilopediaName = eraCivilopediaName,
				luxuryRate = luxuryRate,
				scienceRate = scienceRate,
				taxRate = taxRate,
				gold = gold,
				warWeariness = warWeariness,
				hadGoldenAge = hadGoldenAge,
				goldenAgeTurnsRemaining = goldenAgeTurnsRemaining,
				citiesFounded = citiesFounded,
				turnsUntilPriorityReevaluation = turnsUntilPriorityReevaluation,
				inAnarchyUntilTurn = inAnarchyUntilTurn,
				government = governments.Find(x => x.id == governmentId),
				rules = rules,
				hasVictoriousArmy = hasVictoriousArmy,
			};
			if (!string.IsNullOrEmpty(knownTileIndices)) {
				foreach (int index in DecodeTileIndices(knownTileIndices)) {
					if (index < 0 || index >= map.tiles.Count) {
						throw new FormatException($"Known tile index {index} is outside the map");
					}
					player.tileKnowledge.AddTileToKnown(map.tiles[index]);
				}
			}
			foreach (TileLocation tile in tileKnowledge) {
				player.tileKnowledge.AddTileToKnown(map.tileAt(tile.X, tile.Y));
			}
			foreach (ID techId in player.civilization.startingTechs) {
				if (!player.knownTechs.Contains(techId)) {
					player.knownTechs.Add(techId);
				}
			}
			foreach (KeyValuePair<string, PlayerRelationship> keyValuePair in this.playerRelationships) {
				player.playerRelationships.Add(ID.FromString(keyValuePair.Key), keyValuePair.Value);
			}

			// Because of the custom setter we need to set the researched tech
			// and then set the beakers and turns researched - otherwise they'd
			// be reset by the setter.
			player.SetCurrentlyResearchedTech(currentlyResearchedTech);
			player.beakers = beakers;
			player.turnsResearched = turnsResearched;

			foreach (ID techId in researchQueue) {
				Tech tech = techs.Find(x => x.id == techId);
				player.AddTechItemToResearchQueue(tech);
			}

			if (player.civilization.isBarbarian) {
				player.canBePicked = false;
			}

			return player;
		}

		public SavePlayer() { }

		public SavePlayer(Player player) : this(player, null) { }

		// With a map, the known tiles are saved in the compact form.
		public SavePlayer(Player player, GameMap map) {
			id = player.id;
			isIncludedInGame = player.isIncludedInGame;
			canBePicked = player.canBePicked;
			alliance = (player.isBarbarians || player.alliance == null) ? null : player.alliance.name;
			primaryColorIndex = player.primaryColorIndex;
			secondaryColorIndex = player.secondaryColorIndex;
			human = player.isHuman;
			hasPlayedCurrentTurn = player.hasPlayedThisTurn;
			name = player.name;
			defeated = player.defeated;
			civilization = player.civilization?.name;
			// TODO: this should be computed by looking at cities defined in the save
			// so that adding cities in the save structure doesn't require updating this value
			string encoded = map == null ? null : EncodeTileIndices(player.tileKnowledge.knownTiles, map);
			if (encoded != null) {
				knownTileIndices = encoded;
			} else {
				tileKnowledge = player.tileKnowledge.AllKnownTiles().ConvertAll(tile => new TileLocation(tile));
			}
			turnsUntilPriorityReevaluation = player.turnsUntilPriorityReevaluation;
			knownTechs = new HashSet<ID>(player.knownTechs);
			currentlyResearchedTech = player.currentlyResearchedTech;
			researchQueue = new List<ID>(player.ResearchQueue.Select(t => t.id));
			eraCivilopediaName = player.eraCivilopediaName;
			luxuryRate = player.luxuryRate;
			scienceRate = player.scienceRate;
			taxRate = player.taxRate;
			gold = player.gold;
			warWeariness = player.warWeariness;
			hadGoldenAge = player.hadGoldenAge;
			goldenAgeTurnsRemaining = player.goldenAgeTurnsRemaining;
			citiesFounded = player.citiesFounded;
			beakers = player.beakers;
			turnsResearched = player.turnsResearched;
			inAnarchyUntilTurn = player.inAnarchyUntilTurn;
			governmentId = player.government.id;
			hasVictoriousArmy = player.hasVictoriousArmy;

			foreach (KeyValuePair<ID, PlayerRelationship> keyValuePair in player.playerRelationships) {
				playerRelationships.Add(keyValuePair.Key.ToString(), keyValuePair.Value);
			}
		}

		// Returns null if some tile isn't on the map, in which case the tiles
		// have to be saved by location.
		internal static string EncodeTileIndices(IEnumerable<Tile> tiles, GameMap map) {
			List<byte> bytes = new();
			Dictionary<Tile, int> indexByTile = null;
			int previous = 0;
			foreach (Tile tile in tiles) {
				int index = IndexOf(tile, map, ref indexByTile);
				if (index < 0) {
					return null;
				}
				int delta = index - previous;
				previous = index;
				uint zigzag = (uint)((delta << 1) ^ (delta >> 31));
				while (zigzag >= 0x80) {
					bytes.Add((byte)(zigzag | 0x80));
					zigzag >>= 7;
				}
				bytes.Add((byte)zigzag);
			}
			if (bytes.Count == 0) {
				return null;
			}
			// Use the URL-safe alphabet, since JSON escapes '+'.
			return Convert.ToBase64String(bytes.ToArray()).Replace('+', '-').Replace('/', '_');
		}

		private static int IndexOf(Tile tile, GameMap map, ref Dictionary<Tile, int> indexByTile) {
			if (tile == null || tile == Tile.NONE) {
				return -1;
			}
			int x = tile.XCoordinate, y = tile.YCoordinate;
			if (x >= 0 && y >= 0 && x < map.numTilesWide && y < map.numTilesTall) {
				int index = map.tileCoordsToIndex(x, y);
				if (index >= 0 && index < map.tiles.Count && map.tiles[index] == tile) {
					return index;
				}
			}
			// The tiles aren't laid out as expected, so look the tile up.
			if (indexByTile == null) {
				indexByTile = new Dictionary<Tile, int>(map.tiles.Count);
				for (int i = 0; i < map.tiles.Count; ++i) {
					indexByTile.TryAdd(map.tiles[i], i);
				}
			}
			return indexByTile.TryGetValue(tile, out int found) ? found : -1;
		}

		internal static List<int> DecodeTileIndices(string encoded) {
			byte[] bytes = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/'));
			List<int> result = new();
			int previous = 0;
			int i = 0;
			while (i < bytes.Length) {
				uint zigzag = 0;
				int shift = 0;
				while (true) {
					if (i >= bytes.Length || shift > 28) {
						throw new FormatException("Malformed known tile indices");
					}
					byte b = bytes[i++];
					// The fifth byte holds the top 4 bits of a 32-bit value,
					// and must be the last.
					if (shift == 28 && (b & 0xf0) != 0) {
						throw new FormatException("Malformed known tile indices");
					}
					zigzag |= (uint)(b & 0x7f) << shift;
					if ((b & 0x80) == 0) {
						break;
					}
					shift += 7;
				}
				int delta = (int)(zigzag >> 1) ^ -(int)(zigzag & 1);
				previous += delta;
				result.Add(previous);
			}
			return result;
		}

		public override string ToString() {
			if (civilization != null)
				return $"{civilization} [{this.id}]";
			return "";
		}
	}
}
