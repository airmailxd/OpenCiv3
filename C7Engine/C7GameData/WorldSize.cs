namespace C7GameData {
	public class WorldSize {
		public string name;

		public int width;
		public int height;

		public int optimalNumberOfCities;
		public int techRate;

		public int distanceBetweenCivs;
		public int numberOfCivs;

		public bool isDefault;

		// A copy, to change without changing the world size it came from
		// (which the game's list of world sizes may share).
		public WorldSize Clone() {
			return (WorldSize)MemberwiseClone();
		}

		public static WorldSize Generic() {
			return new WorldSize() {
				name = "Default",
				width = 100,
				height = 100,
				numberOfCivs = 8,
				distanceBetweenCivs = 12,
				techRate = 240,
				optimalNumberOfCities = 20,
				isDefault = true
			};
		}
	}
}
