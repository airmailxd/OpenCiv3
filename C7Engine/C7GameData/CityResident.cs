namespace C7GameData {
	public class CityResident {
		public CitizenType citizenType;

		// Only relevant if citizenType.IsDefaultCitizen == true
		public Tile tileWorked = Tile.NONE;
		public Civilization nationality;
		public City city;

		// Only relevant if citizenType.IsDefaultCitizen == true
		public enum Mood {
			Happy,
			Content,
			Unhappy,
			// A resister in a conquered city: neither happy nor unhappy, and
			// left out of disorder (see City.StartResistance).
			Resisting
		};
		public Mood mood;
	}
}
