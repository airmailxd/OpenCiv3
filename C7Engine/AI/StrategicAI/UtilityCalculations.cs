using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine.AI.StrategicAI {

	/// <summary>
	/// For now, this is an area where methods shared between multiple strategic AI classes can live.
	/// The structure of this may change over time...
	/// </summary>
	public class UtilityCalculations {

		private static readonly int PossibleCityLocationScore = 2;   //how much weight to give to each possible city location
		private static readonly float TileScoreDivider = 10f;    //how much to divide each location's tile score by

		// While priorities are being weighed nothing changes, so the land
		// score is only computed once per player then.
		private static Dictionary<Player, float> sharedLandScores;

		public static float CalculateAvailableLandScore(Player player) {
			if (sharedLandScores != null && sharedLandScores.TryGetValue(player, out float shared)) {
				return shared;
			}

			//Figure out if there's land to settle, and how much
			var possibleLocations = SettlerLocationAI.GetScoredSettlerCandidates(player.cities[0].location, player);
			var availableLand = possibleLocations.Count * PossibleCityLocationScore;
			var settlementQuality = possibleLocations.Values.Sum(i => i / TileScoreDivider);
			float result = settlementQuality + availableLand;

			sharedLandScores?.Add(player, result);
			return result;
		}

		// Until the returned object is disposed, CalculateAvailableLandScore
		// returns the score it first computed for each player. Only use this
		// while nothing the score depends on can change.
		internal static IDisposable ShareLandScores() {
			if (sharedLandScores != null) {
				// Already sharing; the outer scope ends it.
				return new SharingScope(owner: false);
			}
			sharedLandScores = new();
			return new SharingScope(owner: true);
		}

		private sealed class SharingScope(bool owner) : IDisposable {
			public void Dispose() {
				if (owner) {
					sharedLandScores = null;
				}
			}
		}
	}
}
