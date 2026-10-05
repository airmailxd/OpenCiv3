using System.Collections.Generic;
using C7Engine;
using C7GameData;
using System.Linq;
using Godot;

namespace C7.Textures;

public static class PlayerTextureUtil {
	// <colorIndex, shaderMaterial>
	private static Dictionary<int, ShaderMaterial> materialCache = [];
	// <playerId, colorIndex>
	private static Dictionary<ID, int> colorsInUseCache = [];

	/// <summary>
	/// Returns the color index this player will use for the game, for their border, uniform, etc
	/// </summary>
	/// <param name="player"></param>
	/// <returns>the color index number</returns>
	public static int GetPlayerColor(this Player player) {
		if (playerColorCache.TryGetValue(player, out int colorIndex)) {
			return colorIndex;
		}

		if (!colorsInUseCache.TryGetValue(player.id, out colorIndex)) {
			InitializeCivColors(EngineStorage.gameData);
			colorIndex = colorsInUseCache[player.id];
		}

		// Player objects are replaced when a LAN client gets a new snapshot of
		// the game, so don't let the old ones pile up.
		if (playerColorCache.Count >= MaxPlayerColorCacheSize) {
			playerColorCache.Clear();
		}
		playerColorCache[player] = colorIndex;
		return colorIndex;
	}

	// The colors of players that have been looked up, by Player object. This
	// spares looking them up by ID every time, since that needs the ID hashed.
	// It's only filled in once a player's color is settled.
	private static readonly Dictionary<Player, int> playerColorCache = new(ReferenceEqualityComparer.Instance);
	private const int MaxPlayerColorCacheSize = 256;

	// If the player's primary color is in use, fall back to their secondary color.
	// This may already be in use, but that's ok; we don't have a third
	// color for each civ to fall back to.
	//
	// If this runs for a second time from InitializeCivColors()
	// we want to return if possible a color that is not already in use.
	// Consider a game where the Netherlands and England are in play.
	// Netherlands' color indexes are 2, 18
	// England's color indexes    are 2, 2
	// If the player is playing as England, we will pick 2 either way.
	// Then, Netherlands will pick 18, because 2 is in use.
	// But if the player is playing as the Netherlands, we will pick 2, because the player's civ is first on the list.
	// Then, when England's turn comes, we try the primary 2, it's in use, so we pick the secondary 2.
	// After all player are assigned at least one color, we check for duplicates in the colorsInUseCache values.
	// Netherlands is the first player we check, we find that their color, 2, is a duplicate, 
	// so we assign the secondary color 18 to the Netherlands, and England keep their 2.
	private static int LoadCivColor(Player player) {
		if (colorsInUseCache.TryGetValue(player.id, out int colorIndex))
			return colorIndex;

		if (colorsInUseCache.ContainsValue(player.primaryColorIndex)) {
			colorsInUseCache.Add(player.id, player.secondaryColorIndex);
			return player.secondaryColorIndex;
		}

		colorsInUseCache.Add(player.id, player.primaryColorIndex);
		return player.primaryColorIndex;
	}

	// This will only run the first time we ask for any civ's color, for all the civs
	private static void InitializeCivColors(GameData gameData) {
		// Colors may be reassigned below.
		playerColorCache.Clear();

		// a first basic run to assign the colors
		foreach (var player in gameData.players) {
			LoadCivColor(player);
		}

		// a second run to avoid duplicate colors if possible
		foreach (var player in gameData.players) {
			int playerColor = colorsInUseCache[player.id];
			if (colorsInUseCache.Values.Count(p => p == playerColor) > 1) {
				colorsInUseCache.Remove(player.id);
				LoadCivColor(player);
			}
		}
	}

	private static readonly StringName TintColorParameter = "tintColor";

	// Returns the material that tints units with a civ color, shared by all units with that color.
	public static ShaderMaterial GetShaderMaterialForUnit(int civIndex) {
		if (materialCache.TryGetValue(civIndex, out ShaderMaterial material)) {
			return material;
		}
		material = new();
		material.Shader = GD.Load<Shader>("res://UnitTint.gdshader");
		Color civColor = TextureLoader.LoadColor(civIndex);
		material.SetShaderParameter(TintColorParameter, new Vector3(civColor.R, civColor.G, civColor.B));
		materialCache[civIndex] = material;
		return material;
	}

	public static void ClearCache() {
		materialCache.Clear();
		colorsInUseCache.Clear();
		playerColorCache.Clear();
	}
}
