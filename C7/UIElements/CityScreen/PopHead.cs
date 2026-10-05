using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7GameData;

// A utility class for rendering pop heads.
public class PopHead {
	public record struct TextureKey {
		public CityResident cityResident;
		public int eraNum;
	}

	public const int HEAD_SIZE = 48;

	// A head's picture only depends on the citizen type, mood and era, so
	// cache it by those. The cache is held per citizen type, so it goes away
	// with the game the type belongs to.
	private static readonly ConditionalWeakTable<CitizenType, Dictionary<(CityResident.Mood, int), ImageTexture>> cache = new();

	public static ImageTexture GetTexture(CityResident cityResident, int eraNum) {
		if (cityResident.citizenType == null) {
			return LoadTexture(cityResident, eraNum);
		}
		Dictionary<(CityResident.Mood, int), ImageTexture> textures = cache.GetOrCreateValue(cityResident.citizenType);
		var key = (cityResident.mood, eraNum);
		if (!textures.TryGetValue(key, out ImageTexture texture) || !GodotObject.IsInstanceValid(texture)) {
			texture = LoadTexture(cityResident, eraNum);
			textures[key] = texture;
		}
		return texture;
	}

	private static ImageTexture LoadTexture(CityResident cityResident, int eraNum) {
		return TextureLoader.Load("popheads", new TextureKey() { cityResident = cityResident, eraNum = eraNum });
	}
}
