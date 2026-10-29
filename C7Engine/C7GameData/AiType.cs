namespace C7GameData;

// Which AI plays a computer civ, chosen per player when setting up a game.
// Standard is the usual AI; Advanced is the newer one (see
// Player.IsAdvancedAi). Standard comes first so it's the default, and saves
// from before this was chosen load with it.
public enum AiType {
	Standard,
	Advanced,
}
