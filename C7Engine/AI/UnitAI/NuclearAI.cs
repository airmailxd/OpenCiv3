using System.Threading.Tasks;
using C7Engine.AI;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine {
	// A deliberately simple AI for nuclear weapons: while at war, fire at the
	// enemy city where a detonation does the most damage, as long as that is
	// worth a nuke and doesn't hit the AI's own cities and units or civs it
	// isn't at war with. Otherwise the nuke waits where it is.
	public static class NuclearAI {
		// The least damage (population plus enemy units) worth a nuke.
		public const int MinTargetValue = 8;

		public static Tile ChooseTarget(Player player, MapUnit nuke) {
			GameData gameData = EngineStorage.gameData;
			AIFogOfWar.Refresh(player);
			Tile best = null;
			int bestValue = MinTargetValue - 1;
			foreach (Player enemy in gameData.players) {
				if (enemy == player || enemy.defeated || enemy.isBarbarians || !AtWar(player, enemy)) {
					continue;
				}
				foreach (City city in enemy.cities) {
					// With the AI fog of war, only the cities we know of.
					if (!nuke.CanNukeTile(city.location) || !AIFogOfWar.KnowsTile(player, city.location)) {
						continue;
					}
					int value = TargetValue(player, city.location);
					if (value > bestValue) {
						bestValue = value;
						best = city.location;
					}
				}
			}
			return best;
		}

		// The damage a nuke over the tile would do to the player's enemies, or
		// -1 if it would also hit the player or anyone it isn't at war with,
		// whether their cities, their units or just their territory.
		internal static int TargetValue(Player player, Tile target) {
			int value = 0;
			foreach (Tile t in MapUnit.NuclearBlastArea(target)) {
				Player territoryOwner = t.OwningPlayer();
				if (territoryOwner != null && !IsEnemy(player, territoryOwner)) {
					return -1;
				}
				if (t.HasCity()) {
					if (!IsEnemy(player, t.cityAtTile.owner)) {
						return -1;
					}
					value += t.cityAtTile.residents.Count / 2;
				}
				// With the AI fog of war, the units we can see now.
				if (!AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				foreach (MapUnit unit in t.unitsOnTile) {
					if (!IsEnemy(player, unit.owner)) {
						return -1;
					}
					++value;
				}
			}
			return value;
		}

		private static bool IsEnemy(Player player, Player other) {
			return other != null && other != player && AtWar(player, other);
		}

		// Fires the nuke if there's a target worth it. Returns whether it did.
		public static async Task<bool> TryStrike(Player player, MapUnit nuke) {
			Tile target = ChooseTarget(player, nuke);
			if (target == null) {
				return false;
			}
			await nuke.NuclearStrike(target);
			return true;
		}
	}
}
