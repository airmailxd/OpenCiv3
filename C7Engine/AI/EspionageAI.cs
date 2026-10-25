using System.Linq;
using C7Engine;
using C7GameData;

namespace C7Engine.AI {
	// A simple espionage policy for the AI: establish embassies with every
	// civ it has met (again after a war, which closes them; see
	// PlayerRelationship.DeclareWar), plant spies once it has the
	// Intelligence Agency, and now and then steal technology through its
	// embassies. Steal Technology needs "only an embassy", which war closes,
	// so it is a mission against civs at peace (compendium 6.11). It always
	// keeps a reserve of gold. The other spy missions (sabotage,
	// propaganda, stealing plans or maps, exposing spies) are left to human
	// players for now.
	public static class EspionageAI {
		private const int GoldReserve = 100;
		// Theft is only tried when the treasury is comfortably above the
		// reserve, and only some turns.
		private const int TheftReserve = 300;
		private const int TheftChancePercent = 25;

		public static void PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.IsIndependent || player.cities.Count == 0) {
				return;
			}
			foreach (Player other in gameData.players.ToList()) {
				if (other == player || !PlayerRelationship.TryGetRelationship(player, other, out PlayerRelationship pr)) {
					continue;
				}
				if (!pr.hasEmbassy) {
					TryMission(gameData, player, EspionageMission.EstablishEmbassy, other, null, GoldReserve);
					continue;
				}
				if (!pr.hasSpy) {
					TryMission(gameData, player, EspionageMission.PlantSpy, other, null, GoldReserve);
				}
				if (GameData.rng.Next(100) < TheftChancePercent) {
					TryMission(gameData, player, EspionageMission.StealTechnology, other, null, TheftReserve);
				}
			}
		}

		private static void TryMission(GameData gameData, Player player, EspionageMission mission, Player target, City city, int reserve) {
			if (Espionage.Unavailable(gameData, player, mission, target, city) != null) {
				return;
			}
			if (player.gold - Espionage.Cost(gameData, player, mission, target, city) < reserve) {
				return;
			}
			Espionage.Perform(gameData, player, mission, target, city);
		}
	}
}
