namespace C7GameData {
	// A game recorded in a play log (see C7Engine.PlayLog): the name its
	// player gave it, and the folder that holds its saves, autosaves and log.
	// Saved with the game, so that loading it carries on the same log. Null
	// in games that were never recorded, and in saves made before it.
	public sealed class PlayLogInfo {
		// The game's name, as its player gave it (made fit for a folder).
		public string name;
		// The game's folder, in full.
		public string folder;
		// The log's file name, in the folder.
		public string logFile;

		public PlayLogInfo Copy() => new() { name = name, folder = folder, logFile = logFile };
	}
}
