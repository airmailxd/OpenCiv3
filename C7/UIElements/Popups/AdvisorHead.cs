using Godot;
using System;
using System.Collections.Generic;

public class AdvisorHead {
	public enum Mood {
		Happy,
		Angry,
		Sad,
		Surprised
	};

	public enum Advisor {
		Domestic,
		Trade,
		Military,
		Foreign,
		Culture,
		Science
	};

	// This is a named struct to make the advisor_heads.lua file easier to use.
	public record struct AdvisorGraphicsDetails(
		Advisor advisor,
		Mood mood,
		int eraIndex
	);

	// The images already loaded, for the game they were loaded in (a new game
	// may use different textures).
	private static readonly Dictionary<AdvisorGraphicsDetails, ImageTexture> cache = new();
	private static C7GameData.GameData cacheGameData;

	public static ImageTexture GetPopupImage(Advisor advisor, Mood mood, int eraIndex) {
		if (!ReferenceEquals(cacheGameData, C7Engine.EngineStorage.gameData)) {
			cache.Clear();
			cacheGameData = C7Engine.EngineStorage.gameData;
		}
		AdvisorGraphicsDetails details = new() {
			advisor = advisor,
			mood = mood,
			eraIndex = eraIndex,
		};
		if (!cache.TryGetValue(details, out ImageTexture texture) || !GodotObject.IsInstanceValid(texture)) {
			texture = TextureLoader.Load("advisor_heads", details);
			cache[details] = texture;
		}
		return texture;
	}
}
