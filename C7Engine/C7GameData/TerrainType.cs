namespace C7GameData {
	using QueryCiv3;
	using QueryCiv3.Biq;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json.Serialization;

	public class TerrainType {
		//The "key" is a language-independent name for the terrain.  Civ3 relies on their index in the list to know
		//what they are; we'll use the key.  This allows adding custom terrain types in the future, including having
		//different custom terrains in Mod A than Mod B, while still allowing internationalized versions of their
		//names that don't break the scenario.  E.g. "ocean"
		public string Key {
			get => key;
			set {
				key = value;
				UpdateKeyFlags();
			}
		}
		private string key = "";
		//The name is the display name.  E.g. "Ocean" in English scenarios, "Hochsee" in German scenarios.
		public string DisplayName { get; set; } = "";
		public int baseFoodProduction { get; set; }
		public int baseShieldProduction { get; set; }
		public int baseCommerceProduction { get; set; }
		public int movementCost { get; set; }
		public bool allowCities { get; set; } = true;
		public bool impassable { get; set; }
		public StrengthBonus defenseBonus;
		public HashSet<string> allowedResources = new();
		public int height = -1;

		// These enum and field are kept for compatibility with CIV3 saves.
		public enum Civ3FoliageAction {
			None,
			ClearForest,
			ClearWetlands,
			PlantForest
		}
		public Civ3FoliageAction allowedFoliageAction = Civ3FoliageAction.None;

		//some stuff about graphics would probably make sense, too

		// Checks of the key are made in very hot code (pathing, visibility,
		// yields), so they are computed once whenever the key is set rather
		// than comparing strings on every call. Key is the only input and it
		// can only change through its setter, so these are always current,
		// however the terrain was constructed or deserialized.
		[JsonIgnore] public bool IsDesert { get; private set; }
		[JsonIgnore] public bool IsPlains { get; private set; }
		[JsonIgnore] public bool IsGrassland { get; private set; }
		[JsonIgnore] public bool IsTundra { get; private set; }
		[JsonIgnore] public bool IsFloodPlain { get; private set; }
		[JsonIgnore] public bool IsHills { get; private set; }
		[JsonIgnore] public bool IsMountains { get; private set; }
		[JsonIgnore] public bool IsForest { get; private set; }
		[JsonIgnore] public bool IsJungle { get; private set; }
		[JsonIgnore] public bool IsMarsh { get; private set; }
		[JsonIgnore] public bool IsVolcano { get; private set; }
		[JsonIgnore] public bool IsCoast { get; private set; }
		[JsonIgnore] public bool IsSea { get; private set; }
		[JsonIgnore] public bool IsOcean { get; private set; }
		// Coast, sea or ocean.
		[JsonIgnore] public bool IsWater { get; private set; }
		// Mountains, hills or volcano.
		[JsonIgnore] public bool IsHilly { get; private set; }

		private void UpdateKeyFlags() {
			string k = key;
			IsDesert = k == "desert";
			IsPlains = k == "plains";
			IsGrassland = k == "grassland";
			IsTundra = k == "tundra";
			IsFloodPlain = k == "flood plain";
			IsHills = k == "hills";
			IsMountains = k == "mountains";
			IsForest = k == "forest";
			IsJungle = k == "jungle";
			IsMarsh = k == "marsh";
			IsVolcano = k == "volcano";
			IsCoast = k == "coast";
			IsSea = k == "sea";
			IsOcean = k == "ocean";
			IsWater = IsCoast || IsSea || IsOcean;
			IsHilly = IsMountains || IsHills || IsVolcano;
		}

		public bool isHilly() {
			return IsHilly;
		}

		public bool isVolcano() {
			return IsVolcano;
		}

		public bool isWater() {
			return IsWater;
		}

		public bool isCoast() {
			return IsCoast;
		}

		public bool isSea() {
			return IsSea;
		}

		public override string ToString() {
			return DisplayName + "(" + baseFoodProduction + ", " + baseShieldProduction + ", " + baseCommerceProduction + ")";
		}

		public static TerrainType NONE = new TerrainType();


		public static TerrainType ImportFromCiv3(int civ3Index, TERR civ3Terrain) {
			TerrainType c7Terrain = new() {
				Key = civTerrainKeyLookup[civ3Index],
				DisplayName = civ3Terrain.Name,
				baseFoodProduction = civ3Terrain.Food,
				baseShieldProduction = civ3Terrain.Shields,
				baseCommerceProduction = civ3Terrain.Commerce,
				movementCost = civ3Terrain.MovementCost,
				allowCities = civ3Terrain.AllowCities != 0,
				impassable = civ3Terrain.Impassable != 0,
				defenseBonus = new StrengthBonus {
					description = civ3Terrain.Name,
					amount = civ3Terrain.DefenseBonus / 100.0
				},
				allowedFoliageAction = LoadFoliageAction(civ3Terrain),
			};

			if (c7Terrain.Key == "mountains" || c7Terrain.Key == "volcano") {
				c7Terrain.height = 4;
			} else if (c7Terrain.Key == "hills") {
				c7Terrain.height = 2;
			} else if (c7Terrain.Key == "ocean" || c7Terrain.Key == "sea" || c7Terrain.Key == "coast" || c7Terrain.Key == "marsh") {
				c7Terrain.height = -2;
			} else if (c7Terrain.Key == "forest" || c7Terrain.Key == "jungle") {
				c7Terrain.height = 1;
			} else {
				c7Terrain.height = 0;
			}

			return c7Terrain;
		}

		private static Civ3FoliageAction LoadFoliageAction(TERR civ3Terrain) {
			if (civ3Terrain.CanChopForest) return Civ3FoliageAction.ClearForest;
			if (civ3Terrain.CanClearWetlands) return Civ3FoliageAction.ClearWetlands;
			if (civ3Terrain.CanPlantForest) return Civ3FoliageAction.PlantForest;

			return Civ3FoliageAction.None;
		}

		//This only works for Conquests due to the new terrains being added in the middle of the list.
		private static Dictionary<int, string> civTerrainKeyLookup = new Dictionary<int, string>() {
			{ 0,  "desert"},
			{ 1,  "plains"},
			{ 2,  "grassland"},
			{ 3,  "tundra"},
			{ 4,  "flood plain"},
			{ 5,  "hills"},
			{ 6,  "mountains"},
			{ 7,  "forest"},
			{ 8,  "jungle"},
			{ 9,  "marsh"},
			{ 10, "volcano"},
			{ 11, "coast"},
			{ 12, "sea"},
			{ 13, "ocean"}
		};
	}
}
