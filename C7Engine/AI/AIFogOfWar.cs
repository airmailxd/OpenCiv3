using C7GameData;

namespace C7Engine.AI {
	// The "AI Fog of War" testing option (Settings, Developer). Off, the AI
	// plans with the whole map in view, as it always has. On, it plans only
	// with what it knows, as a human must: tiles it has explored, and units
	// on tiles it can see now. It is set from the setting when a game starts
	// or loads (on a LAN host, the host's setting), and isn't saved.
	//
	// This is a switch for testing the AI, not a Civ3 rule.
	public static class AIFogOfWar {
		public static bool Enabled => EngineStorage.aiFogOfWar;

		// Whether the player's AI may plan with this tile: always without
		// the fog, otherwise once the player has explored it. Humans aren't
		// planned for.
		public static bool KnowsTile(Player player, Tile tile) {
			if (!Enabled || player == null || player.isHuman) {
				return true;
			}
			return tile != null && tile != Tile.NONE && player.tileKnowledge.isTileKnown(tile);
		}

		// Whether the player's AI may plan with the units on this tile:
		// always without the fog, otherwise only while one of its units or
		// cities can see the tile. Call Refresh first, once per decision.
		public static bool SeesUnitsOn(Player player, Tile tile) {
			if (!Enabled || player == null || player.isHuman) {
				return true;
			}
			return player.tileKnowledge.isActiveTile(tile);
		}

		// Brings what the player can see up to date, with the fog on.
		public static void Refresh(Player player) {
			if (Enabled && player != null && !player.isHuman) {
				player.tileKnowledge.RecomputeActiveTiles();
			}
		}
	}
}
