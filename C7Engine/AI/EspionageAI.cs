using System.Linq;
using C7Engine;
using C7GameData;

namespace C7Engine.AI {
	// A simple espionage policy for the AI: establish embassies with every
	// civ it has met, plant spies once it has the Intelligence Agency, and
	// now and then steal technology from civs it is at war with. It always
	// keeps a reserve of gold. More hostile missions (sabotage, inciting
	// revolts) are left to human players for now.
	public static class EspionageAI {
		private const int GoldReserve = 100;
		// Theft is only tried when the treasury is comfortably above the
		// reserve, and only some turns.
		private const int TheftReserve = 300;
		private const int TheftChancePercent = 25;

		public static void PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.isBarbarians || player.cities.Count == 0) {
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
				if (pr.AtWar() && GameData.rng.Next(100) < TheftChancePercent) {
					// With the AI fog of war, only a city we know of.
					City target = other.cities.Where(c => AIFogOfWar.KnowsTile(player, c.location))
						.OrderByDescending(c => c.IsCapital()).FirstOrDefault();
					if (target != null) {
						TryMission(gameData, player, EspionageMission.StealTechnology, other, target, TheftReserve);
					}
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
