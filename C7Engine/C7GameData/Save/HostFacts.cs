using System.Collections.Generic;

namespace C7GameData.Save;

// What a LAN host works out for a guest's machine that the guest can't work
// out from what it's sent: the host leaves out the cities on tiles the
// guest's players have never seen (see C7Engine.Network.SnapshotFilter),
// which borders, scores and the like are made from. Only snapshots for such
// a guest have them; saves and whole snapshots don't.
public class HostFacts {
	// Whose territory each tile the guest's players know is in: for each
	// player, by their ID, the tiles encoded as SavePlayer.knownTileIndices.
	// Known tiles in nobody's territory aren't listed.
	public Dictionary<string, string> territory = new();

	// What the scores and the victory screens show of each player, by ID.
	public Dictionary<string, PlayerFacts> players = new();

	// The tiles in the world that count towards domination.
	public int dominationTiles;

	// The great wonders built, and where.
	public List<WonderFacts> wonders = new();

	// The ID of the player with the United Nations, or null if it isn't
	// built.
	public string unitedNationsOwner;

	// Gives the game made from the snapshot what the host worked out: each
	// player's totals, and the territory of the known tiles, in place of
	// what the game worked out from the cities it has. A known tile in the
	// territory of a city it doesn't have is in its owner's territory
	// without being any city's.
	internal void ApplyTo(GameData data) {
		data.hostFacts = this;
		Dictionary<string, Player> byID = new();
		foreach (Player player in data.players) {
			string id = player.id.ToString();
			byID[id] = player;
			player.hostFacts = players.GetValueOrDefault(id);
		}
		Player[] owners = new Player[data.map.tiles.Count];
		foreach ((string id, string encoded) in territory) {
			if (encoded == null || !byID.TryGetValue(id, out Player owner)) {
				continue;
			}
			foreach (int index in SavePlayer.DecodeTileIndices(encoded)) {
				if (index >= 0 && index < owners.Length) {
					owners[index] = owner;
				}
			}
		}
		for (int i = 0; i < owners.Length; ++i) {
			Tile tile = data.map.tiles[i];
			Player owner = owners[i];
			if (tile.owningCity != null && tile.owningCity.owner != owner) {
				tile.owningCity = null;
			}
			tile.territoryOf = tile.owningCity == null ? owner : null;
		}
	}
}

// A player's totals, worked out from all their cities and territory.
public class PlayerFacts {
	public int cities;
	public int population;
	public int culture;
	// Their territory that counts towards domination.
	public int dominationTiles;
	// What this turn would add to their score (see ScoreVictory).
	public float turnScore;
}

// A great wonder, the ID of the player who has it, and the name of its city.
// Everyone hears where each wonder is built, as in Civ3's Wonders screen.
public record WonderFacts(string wonder, string owner, string city);
