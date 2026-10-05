using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7GameData;
using Godot;

// Caches leader head textures, so screens showing them don't run the texture
// mapping again each time they are opened. The cache is held per player or
// civilization object, so it goes away with the game (or setup screen) they
// belong to.
public static class LeaderHeadTextures {
	private static readonly ConditionalWeakTable<object, Dictionary<int, ImageTexture>> cache = new();

	// A player's leader head changes with their era.
	public static ImageTexture Get(Player player) {
		return Get(player, player.EraIndex());
	}

	public static ImageTexture Get(Civilization civilization) {
		return Get(civilization, 0);
	}

	private static ImageTexture Get(object playerOrCivilization, int eraIndex) {
		Dictionary<int, ImageTexture> byEra = cache.GetOrCreateValue(playerOrCivilization);
		if (!byEra.TryGetValue(eraIndex, out ImageTexture texture) || !GodotObject.IsInstanceValid(texture)) {
			texture = TextureLoader.Load("leader_heads", playerOrCivilization);
			byEra[eraIndex] = texture;
		}
		return texture;
	}
}
