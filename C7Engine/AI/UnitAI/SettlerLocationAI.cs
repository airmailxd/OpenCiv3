using System;
using C7GameData;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace C7Engine {
	public class SettlerLocationAI {
		private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<SettlerLocationAI>();

		//Figures out where to plant Settlers.
		public static Tile FindSettlerLocation(Tile start, Player player, HashSet<Tile> excludedTiles = null) {
			Dictionary<Tile, float> scores = GetScoredSettlerCandidates(start, player, excludedTiles);
			if (scores.Count == 0 || scores.Values.Max() <= 0) {
				return Tile.NONE;   //nowhere to settle
			}

			Tile result = scores.MaxBy(t => t.Value).Key;
			return result;
		}

		// Scores the known tiles on the start tile's continent where the player
		// could found a city. The result lists the tiles in the order of the
		// player's known tiles.
		//
		// This runs for every production decision and settler plan, so the
		// parts that rarely change are cached between calls: which tiles are
		// too close to a city (until a city is founded or destroyed) and each
		// tile's yield score (until something its yields depend on changes).
		// The results are the same as computing everything from scratch.
		public static Dictionary<Tile, float> GetScoredSettlerCandidates(Tile start, Player player, HashSet<Tile> excludedTiles = null) {
			List<MapUnit> playerSettlers = player.units.FindAll(u => u.unitType.name == "Settler");
			Dictionary<Tile, bool> invalidLocations = InvalidLocationMemo();
			HashSet<Tile> tilesNearSettlerDestinations = null;

			// TODO: handle settling other continents
			List<Tile> candidates = new();
			foreach (Tile t in player.tileKnowledge.knownTiles) {
				// Cheapest checks first.
				if (t.continent != start.continent || IsInvalidCityLocation(t, invalidLocations)) {
					continue;
				}

				// Skip tiles near where our settlers are already heading, and
				// excludedTiles (the tiles a settler failed to reach, issue
				// #213).
				tilesNearSettlerDestinations ??= TilesNearSettlerDestinations(playerSettlers);
				if (tilesNearSettlerDestinations.Contains(t) || !t.IsAllowCities() || (excludedTiles != null && excludedTiles.Contains(t))) {
					continue;
				}

				// Cities can't be founded in another civ's territory.
				if (IsOwnedByRival(t, player)) {
					continue;
				}
				candidates.Add(t);
			}

			return AssignTileScores(start, player, candidates);
		}

		private static Dictionary<Tile, float> AssignTileScores(Tile startTile, Player player, List<Tile> candidates) {
			Dictionary<Tile, float> scores = new();
			if (candidates.Count == 0) {
				return scores;
			}

			PlayerScoreCache cache = GetPlayerScoreCache(player);
			var maxRank = player.rules.MaxRankOfWorkableTiles;
			Civilization.SettlerTileAdjustments adjustments = player.civilization.Adjustments;

			// Distance is measured from our nearest city, so that new cities
			// grow the empire outwards rather than wherever the settler was
			// built. Without cities yet, it's measured from the settler.
			List<Tile> ownCities = new();
			foreach (City c in player.cities) {
				ownCities.Add(c.location);
			}
			List<Tile> rivalCities = new();
			foreach (Player p in EngineStorage.gameData?.players ?? new List<Player>()) {
				if (p == player) {
					continue;
				}
				foreach (City c in p.cities) {
					if (c.location.map == startTile.map) {
						rivalCities.Add(c.location);
					}
				}
			}

			foreach (Tile t in candidates) {
				float score = cache.TileYieldScore(t);

				// Consider all tiles within the BFC for total score.
				// Score contribution decreases linearly with distance, by 1/R with each step:
				// e.g., with four ranks of workable tiles, R=4:
				//	  city | 100% | 75% | 50% | 25% | 0% | 0% | ..
				BigFatCross bfc = cache.GetBigFatCross(t, maxRank);
				for (int i = 0; i < bfc.tiles.Length; ++i) {
					// Tiles in a rival's territory, and their resources, are
					// theirs to work, not ours.
					if (IsOwnedByRival(bfc.tiles[i], player)) {
						continue;
					}
					score += cache.TileYieldScore(bfc.tiles[i]) * bfc.adjustments[i];
				}

				//Prefer hills for defense, and coast for boats and such.
				if (t.baseTerrainType.Key == "hills") {
					score += player.civilization.Adjustments.HillsBonus;
				}
				if (t.NeighborsWater()) {
					score += player.civilization.Adjustments.WaterBonus;
				}

				// Let defensibility play a role
				score += (float)t.baseTerrainType.defenseBonus.amount * 20.0f;

				// Rival cities nearby will compete for the tiles, and their
				// borders will grow over them.
				foreach (Tile rivalCity in rivalCities) {
					if (t.DistanceTo(rivalCity) <= adjustments.RivalCityRadius) {
						score += adjustments.RivalCityPenalty;
					}
				}

				//Lower scores if they are far away
				float preDistanceScore = score;
				float distancePenalty = DistancePenalty(DistanceFromEmpire(t, startTile, ownCities), adjustments);
				score += distancePenalty;

				//Distance can never make a site worth something worthless; the AI will always try to settle those worthless tundras.
				//(This could actually be modified in the future, but for now is also a safety rail)
				// Such sites get a small score in (0, 1/2] instead, which still
				// falls with distance and rises with the site's worth, so that
				// among far sites the nearer and better ones are preferred
				// rather than whichever is known first.
				if (preDistanceScore > 0 && score <= 0) {
					score = preDistanceScore / (preDistanceScore - distancePenalty);
				}
				if (score > 0)
					scores[t] = score;
			}
			return scores;
		}

		// How far the tile is from our nearest city, or from `start` if we
		// have no cities.
		internal static int DistanceFromEmpire(Tile t, Tile start, List<Tile> ownCities) {
			if (ownCities.Count == 0) {
				return start.DistanceTo(t);
			}
			int result = int.MaxValue;
			foreach (Tile city in ownCities) {
				result = Math.Min(result, t.DistanceTo(city));
			}
			return result;
		}

		// The (negative) score adjustment for a site this far from the
		// empire. It grows with the square of the distance beyond the radius,
		// so that a far site needs to be much better to be worth the trip
		// and the gap in the empire it leaves.
		internal static float DistancePenalty(int distance, Civilization.SettlerTileAdjustments adjustments) {
			int beyond = distance - adjustments.DistancePenaltyRadius;
			if (beyond <= 0) {
				return 0;
			}
			return adjustments.DistancePenalty * (distance + beyond * beyond);
		}

		internal static bool IsOwnedByRival(Tile t, Player player) {
			Player owner = t.OwningPlayer();
			return owner != null && owner != player;
		}

		private static float CalculateTileYieldScore(Tile t, Player owner) {
			float score = owner.civilization.Adjustments.FoodYieldBonus * t.FoodYield(owner).yield;
			score += owner.civilization.Adjustments.ProductionYieldBonus * t.ProductionYield(owner).yield;
			score += owner.civilization.Adjustments.CommerceYieldBonus * t.CommerceYield(owner).yield;
			if (owner.KnowsAboutResource(t.Resource)) {
				if (t.Resource.Category == ResourceCategory.STRATEGIC) {
					score += owner.civilization.Adjustments.StrategicResourceBonus;
				} else if (t.Resource.Category == ResourceCategory.LUXURY) {
					score += owner.civilization.Adjustments.LuxuryResourceBonus;
				}
			}
			return score;
		}

		// The workable tiles around a city site (excluding the site itself),
		// with how much each contributes to the site's score.
		private sealed class BigFatCross {
			public Tile[] tiles;
			public float[] adjustments;
		}

		// Everything a tile's yield score depends on, besides the player's
		// techs, government and golden age, which are checked for the whole
		// cache at once.
		private sealed class TileYieldEntry {
			public float score;
			public TerrainType overlayTerrainType;
			public Resource resource;
			public bool isBonusShield;
			public bool hasCity;
			public City city;
			// The city's owner's traits (e.g. Industrious) change the city
			// tile's yield, and a captured city keeps its City object.
			public Player cityOwner;
			public int cityResidents;
			public bool cityIsCapital;
			public TerrainImprovement[] improvements = Array.Empty<TerrainImprovement>();

			public void Capture(Tile t) {
				overlayTerrainType = t.overlayTerrainType;
				resource = t.Resource;
				isBonusShield = t.isBonusShield;
				hasCity = t.HasCity();
				city = t.cityAtTile;
				cityOwner = hasCity ? city.owner : null;
				cityResidents = hasCity ? city.residents.Count : 0;
				cityIsCapital = hasCity && city.IsCapital();

				// Reuse the array when the number of improvements didn't
				// change, as is usual when an improvement is replaced.
				Dictionary<TerrainImprovement.Layer, TerrainImprovement>.ValueCollection current = t.overlays.GetImprovements();
				if (improvements.Length != current.Count) {
					improvements = new TerrainImprovement[current.Count];
				}
				current.CopyTo(improvements, 0);
			}

			public bool Matches(Tile t) {
				if (overlayTerrainType != t.overlayTerrainType || resource != t.Resource
					|| isBonusShield != t.isBonusShield || city != t.cityAtTile || hasCity != t.HasCity()) {
					return false;
				}
				if (hasCity && (cityOwner != city.owner || cityResidents != city.residents.Count || cityIsCapital != city.IsCapital())) {
					return false;
				}
				int i = 0;
				foreach (TerrainImprovement ti in t.overlays.GetImprovements()) {
					if (i >= improvements.Length || improvements[i] != ti) {
						return false;
					}
					++i;
				}
				return i == improvements.Length;
			}
		}

		private sealed class PlayerScoreCache {
			private readonly Player player;
			private Government government;
			private bool inGoldenAge;
			private int knownTechCount = -1;
			private readonly Dictionary<Tile, TileYieldEntry> yieldScores = new();
			private readonly Dictionary<Tile, BigFatCross> bigFatCrosses = new();
			private int bigFatCrossRank = -1;

			public PlayerScoreCache(Player player) {
				this.player = player;
			}

			// Drops all yield scores if something that affects the yields of
			// every tile changed.
			public void Validate() {
				if (government != player.government || inGoldenAge != player.InGoldenAge || knownTechCount != player.knownTechs.Count) {
					yieldScores.Clear();
					government = player.government;
					inGoldenAge = player.InGoldenAge;
					knownTechCount = player.knownTechs.Count;
				}
			}

			public float TileYieldScore(Tile t) {
				if (yieldScores.TryGetValue(t, out TileYieldEntry entry)) {
					if (entry.Matches(t)) {
						return entry.score;
					}
				} else {
					entry = new TileYieldEntry();
					yieldScores[t] = entry;
				}
				entry.Capture(t);
				entry.score = CalculateTileYieldScore(t, player);
				return entry.score;
			}

			// The map doesn't change shape, so these are computed once.
			public BigFatCross GetBigFatCross(Tile t, int maxRank) {
				if (bigFatCrossRank != maxRank) {
					bigFatCrosses.Clear();
					bigFatCrossRank = maxRank;
				}
				if (bigFatCrosses.TryGetValue(t, out BigFatCross bfc)) {
					return bfc;
				}

				List<Tile> tiles = new();
				List<float> adjustments = new();
				foreach (Tile workable in t.GetTilesWithinRankDistance(maxRank)) {
					if (workable == Tile.NONE)
						continue;
					var rank = t.RankDistanceTo(workable);
					if (rank <= 0)
						continue;
					tiles.Add(workable);
					adjustments.Add(Math.Max(0, (maxRank - rank + 1f) / maxRank));
				}
				bfc = new BigFatCross() { tiles = tiles.ToArray(), adjustments = adjustments.ToArray() };
				bigFatCrosses[t] = bfc;
				return bfc;
			}
		}

		private static readonly ConditionalWeakTable<Player, PlayerScoreCache> playerScoreCaches = new();

		private static PlayerScoreCache GetPlayerScoreCache(Player player) {
			PlayerScoreCache cache = playerScoreCaches.GetValue(player, p => new PlayerScoreCache(p));
			cache.Validate();
			return cache;
		}

		// Whether a tile is too close to a city to found one, remembered until
		// the set of cities changes. Kept per game in a weak table, so that a
		// finished game's tiles aren't kept alive.
		private sealed class InvalidLocations {
			public readonly Dictionary<Tile, bool> memo = new();
			public readonly List<Tile> cityTiles = new();
		}

		private static readonly ConditionalWeakTable<GameData, InvalidLocations> invalidLocationsByGame = new();

		private static Dictionary<Tile, bool> InvalidLocationMemo() {
			GameData gameData = EngineStorage.gameData;
			if (gameData == null) {
				return null;
			}
			InvalidLocations memo = invalidLocationsByGame.GetValue(gameData, _ => new InvalidLocations());
			List<Tile> cityTiles = memo.cityTiles;

			// Compare the city locations with the ones the memo was made for.
			bool changed = false;
			int i = 0;
			foreach (Player p in gameData.players) {
				foreach (City c in p.cities) {
					if (!changed && (i >= cityTiles.Count || cityTiles[i] != c.location)) {
						changed = true;
					}
					++i;
				}
			}
			changed |= i != cityTiles.Count;

			if (changed) {
				memo.memo.Clear();
				cityTiles.Clear();
				foreach (Player p in gameData.players) {
					foreach (City c in p.cities) {
						cityTiles.Add(c.location);
					}
				}
			}
			return memo.memo;
		}

		private static bool IsInvalidCityLocation(Tile tile, Dictionary<Tile, bool> memo) {
			if (memo == null) {
				return IsInvalidCityLocation(tile);
			}
			if (!memo.TryGetValue(tile, out bool result)) {
				result = IsInvalidCityLocation(tile);
				memo[tile] = result;
			}
			return result;
		}

		private static bool IsInvalidCityLocation(Tile tile) {
			if (tile == Tile.NONE || tile.HasCity())
				return true;
			foreach (Tile neighbor in tile.neighbors.Values) {
				if (neighbor.HasCity()) {
					return true;
				}
				foreach (Tile neighborOfNeighbor in neighbor.neighbors.Values) {
					if (neighborOfNeighbor.HasCity()) {
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Returns the tiles near where the settlers in the list (which should be the current AI's settlers) are
		/// already heading: their destinations and the land tiles up to two steps away.
		/// Another AI's settlers don't count, as the AI shouldn't know the other AI's plans.
		/// </summary>
		/// <param name="playerSettlers">The settlers owned by the AI considering building a city.</param>
		private static HashSet<Tile> TilesNearSettlerDestinations(List<MapUnit> playerSettlers) {
			HashSet<Tile> result = new();
			foreach (MapUnit otherSettler in playerSettlers) {
				if (otherSettler.currentAI is SettlerAI otherSettlerAI) {
					Tile otherDestination = otherSettlerAI.data.destination;
					result.Add(otherDestination);
					foreach (Tile innerRingTile in otherDestination.GetLandNeighbors()) {
						result.Add(innerRingTile);
						foreach (Tile outerRingTile in innerRingTile.GetLandNeighbors()) {
							result.Add(outerRingTile);
						}
					}
				}
			}
			return result;
		}
	}
}
