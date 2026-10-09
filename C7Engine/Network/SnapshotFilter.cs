using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine.Network;

// What a LAN host sends a guest's machine when it hides what the guest's
// players can't see (LanHost.HideUnseen): the snapshot, less what none of
// the players at that machine may know. They share a screen, so the machine
// sees what any of them knows. Spectators, and the host, have the whole game.
//
// The guest runs its UI, and parts of the engine, over what it's sent, and
// works much out again from it: tile owners and borders from the cities, what
// each player sees from their units, citizens' moods, trade networks and so
// on. So only what nothing on the guest needs is taken out, and the rest is
// kept even where it tells a little more than the player would know:
//
// - Other players' units on tiles none of the players can see now are left
//   out, and those they can see lose their paths.
// - Tiles none of the players has ever seen lose their resource, barbarian
//   camp and improvements. Their terrain, rivers and continent are kept: the
//   map draws the edges of the known tiles over them, what a unit can see
//   depends on them, and domination counts them.
// - Other civilizations' cities keep their name, size, owner, buildings,
//   culture and whether they're the capital, celebrating or in disorder,
//   which their borders, the map, the Wonders view, the United Nations and
//   the scores need, wherever they are. Unless one of the players has an
//   embassy with their civilization, which shows their cities' insides, they
//   lose their citizens (the guest places as many on its own), stored food
//   and shields, production queue and what they're building, unless it's a
//   great wonder (which the Wonders view shows) or a city improvement like
//   wealth that counts towards their borders.
// - Other players' research is left out unless one of the players has an
//   embassy with them, and their gold, techs and spaceship unless they've
//   met, which the diplomacy screens and the space race need. What they
//   know of the map is cut to their own territory, which their score and
//   their share of the world need.
// - What other players think of each other is left out unless the players
//   have met both, as are their reports on each other's capitals, and the
//   votes for the United Nations the other humans have cast.
public static class SnapshotFilter {
	// What the players at one machine know, worked out on the main thread
	// from the game itself, for Filter to use on another thread.
	public sealed class View {
		internal readonly HashSet<ID> players;
		internal readonly HashSet<(int, int)> known = new();
		internal readonly HashSet<(int, int)> visible = new();
		// The players they've met, and those they have an embassy with.
		internal readonly HashSet<ID> met = new();
		internal readonly HashSet<ID> embassies = new();
		// What each player knows of their own territory (see OwnTerritory).
		internal readonly Dictionary<ID, string> ownTerritory;

		internal View(HashSet<ID> players, Dictionary<ID, string> ownTerritory) {
			this.players = players;
			this.ownTerritory = ownTerritory;
		}
	}

	// What the players know. Their active tiles are brought up to date
	// first, as the engine would when it next needs them. ownTerritory can be
	// shared by the views of one snapshot; it's worked out if not given.
	public static View ViewOf(GameData gameData, IEnumerable<ID> playerIDs, Dictionary<ID, string> ownTerritory = null) {
		View view = new(playerIDs.ToHashSet(), ownTerritory ?? OwnTerritory(gameData));
		foreach (ID id in view.players) {
			Player player = gameData.GetPlayer(id);
			if (player == null) {
				continue;
			}
			TileKnowledge knowledge = player.tileKnowledge;
			knowledge.RecomputeActiveTiles();
			foreach (Tile tile in knowledge.knownTiles) {
				view.known.Add((tile.XCoordinate, tile.YCoordinate));
			}
			foreach (Tile tile in knowledge.VisibleTiles()) {
				if (tile != null && tile != Tile.NONE) {
					view.visible.Add((tile.XCoordinate, tile.YCoordinate));
				}
			}
			foreach ((ID other, PlayerRelationship relationship) in player.playerRelationships) {
				view.met.Add(other);
				if (relationship.hasEmbassy) {
					view.embassies.Add(other);
				}
			}
		}
		return view;
	}

	// The tiles each player knows within their own borders, encoded as in
	// SavePlayer.knownTileIndices.
	public static Dictionary<ID, string> OwnTerritory(GameData gameData) {
		Dictionary<ID, string> result = new();
		foreach (Player player in gameData.players) {
			result[player.id] = SavePlayer.EncodeTileIndices(
				player.tileKnowledge.knownTiles.Where(t => t.OwningPlayer() == player), gameData.map);
		}
		return result;
	}

	// The snapshot as the view's players may see it. The snapshot isn't
	// changed, and what's returned shares what it doesn't change with it, so
	// several views can be filtered from one snapshot at once, on any
	// thread, as long as nothing changes the snapshot meanwhile.
	public static SaveGame Filter(SaveGame snapshot, View view) {
		SaveGame filtered = snapshot.ShallowCopy();
		filtered.Map = FilterMap(snapshot.Map, view);
		filtered.Units = snapshot.Units
			.Where(u => view.players.Contains(u.owner) || view.visible.Contains((u.currentLocation.X, u.currentLocation.Y)))
			.Select(u => view.players.Contains(u.owner) || u.path == null ? u : WithoutPath(u))
			.ToList();
		filtered.Cities = FilterCities(snapshot, view);
		filtered.Players = snapshot.Players.ConvertAll(p => FilterPlayer(p, view));
		filtered.UnitedNations = FilterVotes(snapshot.UnitedNations, view);
		return filtered;
	}

	private static SaveMap FilterMap(SaveMap map, View view) {
		SaveMap filtered = map.ShallowCopy();
		filtered.tiles = map.tiles.ConvertAll(tile => {
			if (view.known.Contains((tile.X, tile.Y))
				|| (tile.resource == null && tile.overlays.Count == 0 && !tile.features.Contains("barbarianCamp"))) {
				return tile;
			}
			SaveTile unseen = tile.ShallowCopy();
			unseen.resource = null;
			unseen.overlays = [];
			unseen.features = tile.features.Where(f => f != "barbarianCamp").ToList();
			return unseen;
		});
		return filtered;
	}

	private static SaveUnit WithoutPath(SaveUnit unit) {
		SaveUnit copy = unit.ShallowCopy();
		copy.path = null;
		return copy;
	}

	private static List<SaveCity> FilterCities(SaveGame snapshot, View view) {
		HashSet<string> greatWonders = snapshot.Buildings.Where(b => b.greatWonderProperties != null).Select(b => b.name).ToHashSet();
		// What a city whose production is hidden is said to build: any
		// unit, which only an embassy's city screen would show, and it
		// shows the real thing.
		string placeholder = snapshot.UnitPrototypes.FirstOrDefault()?.name;
		return snapshot.Cities.ConvertAll(city => {
			if (view.players.Contains(city.owner) || view.embassies.Contains(city.owner)) {
				return city;
			}
			SaveCity hidden = city.ShallowCopy();
			hidden.size = city.residents.Count;
			hidden.residents = [];
			hidden.foodStored = 0;
			hidden.shieldsStored = 0;
			hidden.turnsOfUnhappinessDueToPopRushing = 0;
			hidden.hurriedThisTurn = false;
			hidden.productionQueue = [];
			bool shown = city.producibleType == ProducibleType.INFLOW
				|| (city.producibleType == ProducibleType.BUILDING && greatWonders.Contains(city.producible));
			if (!shown && placeholder != null) {
				hidden.producible = placeholder;
				hidden.producibleType = ProducibleType.UNIT;
			}
			return hidden;
		});
	}

	private static SavePlayer FilterPlayer(SavePlayer player, View view) {
		if (view.players.Contains(player.id)) {
			return player;
		}
		SavePlayer filtered = player.ShallowCopy();

		filtered.knownTileIndices = view.ownTerritory.GetValueOrDefault(player.id);
		filtered.tileKnowledge = [];
		filtered.outdatedTiles = [];

		if (!view.embassies.Contains(player.id)) {
			filtered.currentlyResearchedTech = null;
			filtered.researchQueue = [];
			filtered.beakers = 0;
			filtered.turnsResearched = 0;
			filtered.freeTechsRemaining = 0;
		}
		filtered.turnsUntilPriorityReevaluation = 0;
		if (!view.met.Contains(player.id)) {
			filtered.gold = 0;
			// Loading gives them their civilization's starting techs.
			filtered.knownTechs = [];
			filtered.spaceshipParts = [];
		}

		Dictionary<string, PlayerRelationship> relationships = new();
		foreach ((string otherID, PlayerRelationship relationship) in player.playerRelationships) {
			ID other = ID.FromString(otherID);
			if (view.players.Contains(other)) {
				relationships[otherID] = relationship;
			} else if (view.met.Contains(player.id) && view.met.Contains(other)) {
				// Their spies' reports are theirs alone.
				PlayerRelationship copy = relationship.ShallowCopy();
				copy.embassyReport = null;
				relationships[otherID] = copy;
			}
		}
		filtered.playerRelationships = relationships;
		return filtered;
	}

	private static UnitedNationsState FilterVotes(UnitedNationsState state, View view) {
		if (state?.humanVotes == null || state.humanVotes.Keys.All(voter => view.players.Contains(ID.FromString(voter)))) {
			return state;
		}
		return new UnitedNationsState {
			votingTurn = state.votingTurn,
			humanVotes = state.humanVotes.Where(v => view.players.Contains(ID.FromString(v.Key)))
				.ToDictionary(v => v.Key, v => v.Value),
			candidateA = state.candidateA,
			candidateB = state.candidateB,
			secretaryGeneral = state.secretaryGeneral,
		};
	}
}
