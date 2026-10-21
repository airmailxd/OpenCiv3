using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData.Save;

namespace C7GameData {
	// Civ3's City View: the picture of a city with its buildings, wonders,
	// walls and houses, opened from the city screen's View button (the eye of
	// art/City Screen/xandview.pcx, which Civ3 FUN_00418160 puts at (871, 22)
	// and FUN_0041c870 answers, command 0x21f, by calling FUN_00407e80).
	//
	// This works out what Civ3's cityview module (Civ3Conquests.exe 1.22,
	// 0x4076f0-0x417ae0) draws for a city, back to front, as pieces cut from
	// the PCX files under Art/City View; the UI only draws them. The tables
	// below are Civ3's own, read from the exe's data: where each cell of the
	// city's ground is on the 1024x768 picture, which building or kind of
	// ground each cell holds for a town, a city and a metropolis, and so on.
	//
	// Civ3 names a building by its art index, the order of the improvements
	// in Play the World's rules (FUN_004076f0 gives each one's Civilopedia
	// key, like BLDG_Temple for 3), and finds the rules' building by that key.
	// Its tables hold these art indexes; Conquests reads them as indexes into
	// its own, differently ordered, list of improvements, which puts some
	// wonders in each other's places. Here a table entry is always an art
	// index, which is what the tables were made for.
	public static class CityView {
		public const string ART = "Art/City View/";

		// The land the city stands on, which picks the background and the
		// colors of most pieces. In Civ3's order (FUN_004163e0: desert is D,
		// plains P, tundra T and every other terrain, hills too, G).
		public enum Terrain { Grassland, Desert, Tundra, Plains }

		private static readonly string[] TerrainLetters = ["G", "D", "T", "P"];
		// The culture groups' art, by culture group index (FUN_00409630).
		private static readonly string[] Cultures = ["AMR", "EUR", "ROM", "MEA", "FEA"];
		// The eras' art, by era (FUN_004091c0, FUN_0040f750).
		private static readonly string[] Eras = ["ANC", "REN", "IND", "MOD"];

		// A piece of the picture: the part of a file to draw (all of it when
		// cropWidth is 0) and where its top left corner goes. A shadow is
		// drawn as a darkening, the file's grey levels saying how dark. art
		// is the art index of the building the piece shows, or -1.
		public readonly record struct Sprite(string path, int cropX, int cropY, int cropWidth, int cropHeight,
				int x, int y, bool isShadow, int art) {
			public bool WholeFile => cropWidth == 0;
		}

		// What the picture depends on.
		public class Input {
			// The owner's era, 0 to 3, and culture group, 0 to 4 (-1 for none,
			// which has no houses, palace or walls).
			public int era;
			public int cultureGroup = -1;
			public Terrain terrain;
			// Civ3's water code (see WaterCode).
			public int waterCode;
			// Whether a tile of the city's 21 has a river, and, when the city
			// sits by a river on flood plains, whether it is also by water.
			public bool riverInRadius;
			public bool floodPlainCityByWater;
			public int population;
			// The rules' largest towns and cities (Civ3's two city size levels).
			public int maxTownSize;
			public int maxCitySize;
			// The city's map coordinates, which Civ3 seeds the picture's
			// random choices with, so a city always looks the same.
			public int mapX;
			public int mapY;
			// How far the owner's palace is built in the Palace View, 0 to 3
			// (Civ3 FUN_0058a3d0). 0 shows the first palace.
			public int palaceStage;
			// Whether the city is a town with a building that defends
			// against bombardment (Civ3 FUN_004c8270), and whether it has
			// city walls that defend (art 7, with a bombard defense and a
			// defense bonus).
			public bool townHasBombardDefense;
			public bool wallsDefend;
			// The art indexes of the buildings the city has, of those the
			// rules have, and of those that must be near a river.
			public readonly HashSet<int> builtArt = [];
			public readonly HashSet<int> knownArt = [];
			public readonly HashSet<int> nearRiverArt = [];
		}

		// The Civilopedia key of each art index (Civ3 FUN_004076f0), null for
		// the ones it has none for. 78 is the palace's second piece.
		private static readonly string[] ArtKeys = [
			"BLDG_Palace", "BLDG_Barracks", "BLDG_Granary", "BLDG_Temple", "BLDG_Marketplace",
			"BLDG_Library", "BLDG_Courthouse", "BLDG_Walls", "BLDG_Aqueduct", "BLDG_Bank",
			"BLDG_Cathedral", "BLDG_University", "BLDG_Colosseum", "BLDG_Factory", "BLDG_Manufacturing_Plant",
			"BLDG_Recycling_Center", "BLDG_Coal_Plant", "BLDG_Hydro_Plant", "BLDG_Nuclear_Plant", "BLDG_Hospital",
			"BLDG_Research_Lab", "BLDG_Mass_Transit_System", "BLDG_SAM_Missile_Battery", "BLDG_Coastal_Fortress", "BLDG_Solar_Plant",
			"BLDG_Harbor", "BLDG_Offshore_Platform", "BLDG_Airport", "BLDG_Police_Station", null,
			"BLDG_Pyramids", "BLDG_Hanging_Gardens", "BLDG_Colossus", "BLDG_Lighthouse", "BLDG_Great_Library",
			"BLDG_Oracle", "BLDG_Great_Wall", "BLDG_Art_of_War", "BLDG_Cistine_Chapel", "BLDG_Circumnavigation",
			"BLDG_Solar_System", "BLDG_Great_Playhouse", "BLDG_Inventor's_Workshop", "BLDG_Grand_Cathedral", "BLDG_Great_University",
			"BLDG_Trading_Company", "BLDG_Universal_Suffrage", "BLDG_Hoover_Dam", "BLDG_Theory_of_Evolution", "BLDG_United_Nations",
			"BLDG_Manhattan_Project", "BLDG_Cure_for_Cancer", "BLDG_Longevity", "BLDG_SETI_Program", "BLDG_Epic",
			"BLDG_Great_Ironworks", "BLDG_Forbidden_Palace", "BLDG_Military_Academy", "BLDG_Pentagon", "BLDG_Wall_Street",
			"BLDG_Apollo_Project", "BLDG_SDI", "BLDG_Intelligence_Center", "BLDG_Battlefield_Medicine", null,
			null, null, null, null, null,
			null, null, null, null, "BLDG_Internet",
			"BLDG_Civil_Defense", "BLDG_Stock_Exchange", "BLDG_Commercial_Dock", "BLDG_Palace",
		];

		// Conquests' keys for two of them differ from the ones Civ3 looks
		// for (its rules say BLDG_Lab and BLDG_Sistine_Chapel), so Civ3
		// never finds them; these are taken too, so the art is shown.
		private static readonly Dictionary<string, int> ArtKeyAliases = new() {
			{ "BLDG_Lab", 20 },
			{ "BLDG_Sistine_Chapel", 38 },
		};

		public const int PALACE = 0, WALLS = 7, AQUEDUCT = 8, COASTAL_FORTRESS = 23, HARBOR = 25,
			LONGEVITY = 52, UNITED_NATIONS = 49, WALL_STREET = 59, PALACE_SECOND_PIECE = 78;
		public const int ART_COUNT = 79;

		// The Civilopedia key Civ3 gives an art index, or null.
		public static string ArtKey(int art) => art >= 0 && art < ART_COUNT ? ArtKeys[art] : null;

		// The art index of the building with a Civilopedia key, or -1.
		public static int ArtOf(string civilopediaEntry) {
			if (civilopediaEntry == null) {
				return -1;
			}
			int art = Array.IndexOf(ArtKeys, civilopediaEntry);
			if (art >= 0) {
				return art;
			}
			return ArtKeyAliases.TryGetValue(civilopediaEntry, out int alias) ? alias : -1;
		}

		// Where each cell's top left corner is on the picture, by row and
		// column (Civ3's tables at 0x69f28c and 0x69f67c, 14 rows of 18). The
		// streets run along the cells' corners, the same points.
		private static readonly int[] CellX = [
			-141, -102, -63, -24, 15, 54, 93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522,
			-102, -63, -24, 15, 54, 93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561,
			-63, -24, 15, 54, 93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600,
			-24, 15, 54, 93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639,
			15, 54, 93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678,
			54, 93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717,
			93, 132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756,
			132, 171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795,
			171, 210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795, 834,
			210, 249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795, 834, 873,
			249, 288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795, 834, 873, 912,
			288, 327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795, 834, 873, 912, 951,
			327, 366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795, 834, 873, 912, 951, 990,
			366, 405, 444, 483, 522, 561, 600, 639, 678, 717, 756, 795, 834, 873, 912, 951, 990, 1029,
		];
		private static readonly int[] CellY = [
			366, 351, 336, 321, 306, 291, 276, 261, 246, 231, 216, 201, 186, 171, 156, 141, 126, 111,
			382, 367, 352, 337, 322, 307, 292, 277, 262, 247, 232, 217, 202, 187, 172, 157, 142, 127,
			398, 383, 368, 353, 338, 323, 308, 293, 278, 263, 248, 233, 218, 203, 188, 173, 158, 143,
			414, 399, 384, 369, 354, 339, 324, 309, 294, 279, 264, 249, 234, 219, 204, 189, 174, 159,
			430, 415, 400, 385, 370, 355, 340, 325, 310, 295, 280, 265, 250, 235, 220, 205, 190, 175,
			446, 431, 416, 401, 386, 371, 356, 341, 326, 311, 296, 281, 266, 251, 236, 221, 206, 191,
			462, 447, 432, 417, 402, 387, 372, 357, 342, 327, 312, 297, 282, 267, 252, 237, 222, 207,
			478, 463, 448, 433, 418, 403, 388, 373, 358, 343, 328, 313, 298, 283, 268, 253, 238, 223,
			494, 479, 464, 449, 434, 419, 404, 389, 374, 359, 344, 329, 314, 299, 284, 269, 254, 239,
			510, 495, 480, 465, 450, 435, 420, 405, 390, 375, 360, 345, 330, 315, 300, 285, 270, 255,
			526, 511, 496, 481, 466, 451, 436, 421, 406, 391, 376, 361, 346, 331, 316, 301, 286, 271,
			542, 527, 512, 497, 482, 467, 452, 437, 422, 407, 392, 377, 362, 347, 332, 317, 302, 287,
			558, 543, 528, 513, 498, 483, 468, 453, 438, 423, 408, 393, 378, 363, 348, 333, 318, 303,
			574, 559, 544, 529, 514, 499, 484, 469, 454, 439, 424, 409, 394, 439, 364, 349, 334, 319,
		];

		// What each of the 13x17 cells holds, for a town, a city and a
		// metropolis (Civ3's tables at 0x6a0154), followed by the table that
		// comes after them in Civ3's data (FootprintOf's), which Civ3 reads
		// past the end of the metropolis's: an art index, or -8 for nothing,
		// -7 for a cell a building next to it covers, -6 for a house, -5, -4
		// and -3 for the main street (-4 its square), -10 and -9 for the tall
		// houses of the industrial and modern eras.
		private static readonly int[] Layouts = [
			-8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, 56, -7, 3, 74, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, 12, -7, -7, -7, -6, 2, -6, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, 8, -7, -7, 0, -7, -7, -6, 1, -7, -8, -8, -8, -8, -8,
			-8, -8, -8, 41, -7, 48, -6, 78, 37, 50, -6, 55, -6, -8, -8, -8, -8,
			-8, -8, -8, -7, -7, 76, -6, -4, 43, -7, -6, -7, 21, -6, -8, -8, -8,
			-8, -8, -8, -6, 5, 38, -7, -5, -7, -7, 25, 39, 59, -10, -8, -8, -8,
			-8, -8, -8, 4, -7, -6, 34, -5, 6, -6, -8, -8, -7, -9, -8, -8, -8,
			-8, -8, -8, -6, -6, 54, -7, -5, 9, -6, -8, 49, -10, -8, -8, -8, -8,
			-8, -8, -8, -8, -6, -6, -7, -3, -6, -6, -8, -7, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, 44, -7, 75, -7, 28, -6, -8, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -6, -7, -7, -7, -7, -6, 57, -7, -8, -8, -8, -8, -8,
			-8, -8, -6, -6, -8, -8, 56, -7, 3, 74, -8, -8, 42, -7, -8, -8, -8,
			-8, -8, 10, -8, 12, -7, -7, -7, -6, 2, -6, -8, -7, -7, -8, -8, -8,
			-8, -8, -6, 8, -7, -7, 0, -7, -7, -6, 1, -7, -8, -6, -8, -8, -8,
			-8, 11, -7, 41, -7, 48, -6, 78, 37, 50, -6, 55, -6, -8, -6, -8, -8,
			-8, -7, -7, -7, -7, 76, -6, -4, 43, -7, -6, -7, 21, -6, -10, -8, -8,
			-8, -8, -6, -6, 5, 38, -7, -5, -7, -7, 25, 39, 59, -10, -9, -8, -8,
			-8, -8, -8, 4, -7, -6, 34, -5, 6, -6, -8, -8, -7, -9, -8, -8, -8,
			-8, -8, -8, -6, -6, 54, -7, -5, 9, -6, -8, 49, -10, -8, -8, -8, -8,
			-8, -8, -8, -8, -6, -6, -7, -3, -6, -6, -8, -7, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -8, -8, -6, 66, 21, -6, 19, -6, -8, -8, -8, -8, -8,
			-8, -8, -8, -8, -6, 51, -6, -6, -6, -6, -6, 63, -6, -8, -8, -8, -8,
			-8, -8, -8, -8, -6, 44, -7, 75, -7, 28, -6, 22, 65, 62, -7, -8, -8,
			-8, -6, -6, -6, -6, -7, -7, -7, -7, -6, 57, -7, 46, -6, 20, -6, -8,
			70, 15, -6, -6, -8, -8, 56, -7, 3, 74, -8, -8, 42, -7, -6, -6, -8,
			-6, -6, 10, -8, 12, -7, -7, -7, -6, 2, 67, -8, -7, -7, 16, 64, -8,
			-8, -6, -6, 8, -7, -7, 0, -7, -7, -6, 1, -7, -8, -6, 14, 13, -8,
			69, 11, -7, 41, -7, 48, -6, 78, 37, 50, -6, 55, -6, -8, -6, -10, -8,
			21, -7, -7, -7, -7, 76, -6, -4, 43, -7, 68, -7, 21, -6, -10, -9, -9,
			-6, -6, 72, -6, 5, 38, -7, -5, -7, -7, 25, 39, 59, -10, -9, -10, -10,
			-8, -8, 73, 4, -7, -6, 34, -5, 6, -6, -8, -8, -7, -9, -8, -8, -8,
			-8, -8, -8, -6, 71, 54, -7, -5, 9, -6, -8, 49, -10, -8, -8, -8, -8,
			-8, -8, -8, -8, -6, -6, -7, -3, -6, -6, -8, -7, -8, -8, -8, -8, -8,
			2, 1, 0, 0, 1, 0, 0, -1, -1, 0, 0, 4, 4, 0, 0, 0, 0, -1, -1, 0,
			0, 0, 0, -1, -1, -1, -1, -1, 0, -1, -1, -1, -1, -1, 5, -1, -1, 0, 1, 0,
			0, 4, 4, 4, 4, -1, 0, -1, 0, 3, 0, 0, 0, 3, 0, 3, 4, 1, -1, 3,
			-1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 4, 0,
		];
		private const int LAYOUT_SIZE = 13 * 17;

		// The shape of each art index's building, which the houses of a
		// building the city doesn't have take (Civ3's table at 0x6a0bb0): 0
		// one cell, 1 two cells along a row, 2 three, 3 two along a column,
		// 4 two by two, 5 three along a column; -1 none.
		private static int FootprintOf(int art) => Layouts[3 * LAYOUT_SIZE + art];

		// Which of the three passes draws each cell (Civ3's table at
		// 0x69fde0); a building in a cell marked 3 is drawn in the second.
		private static readonly int[] PassOfCell = [
			2, 2, 2, 2, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			2, 2, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			2, 2, 2, 0, 3, 3, 1, 1, 1, 1, 3, 0, 0, 0, 0, 0, 0,
			2, 2, 2, 3, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0, 0,
			2, 2, 2, 3, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0, 0,
			2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 0, 0, 0,
			2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0,
			2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0,
			2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0,
			2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0,
			2, 2, 2, 3, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
		];

		// Where the buildings drawn whole are (Civ3 FUN_00413440).
		private static readonly Dictionary<int, (int x, int y)> FixedPositions = new() {
			{ 8, (0, 292) }, { 17, (602, 563) }, { 18, (621, 209) }, { 24, (373, 130) }, { 25, (641, 433) },
			{ 26, (905, 453) }, { 27, (0, 183) }, { 30, (106, 36) }, { 31, (501, 516) }, { 32, (597, 269) },
			{ 33, (808, 204) }, { 35, (190, 583) }, { 36, (0, 0) }, { 40, (192, 215) }, { 45, (673, 581) },
			{ 47, (543, 696) }, { 53, (588, 70) }, { 58, (332, 212) }, { 60, (389, 100) }, { 61, (617, 38) },
			{ 77, (815, 305) },
		};
		// The ones drawn before the city's cells and after them (Civ3
		// FUN_00412210).
		private static readonly int[] FixedBefore = [36, 53, 30, 27, 40, 58, 60, 24, 33, 18, 77];
		private static readonly int[] FixedAfter = [26, 32, 31, 45, 17, 47, 35, 61];

		// Civ3's random numbers (FUN_006263f0, FUN_00626440).
		private struct Lcg {
			private uint state;

			public Lcg(int seed) {
				state = unchecked((uint)seed);
			}

			public double Next() {
				state = unchecked(state * 0x41c64e6d + 0x3039);
				return ((state >> 16) & 0x7fff) / 32768.0;
			}

			public int NextInt(int n) => (int)(Next() * n);
		}

		// Civ3's water code for a city (FUN_004163e0): 1 for a river among
		// its 21 tiles, 8 for that and water among them but not next to it, 2,
		// 4 or 6 for 1-2, 3-4 or 5 or more of the 9 tiles at its center
		// being water, 9 for a desert or flood plain city by a river on
		// flood plains, else 0; one more when the city has a harbor (a
		// building allowing water trade that isn't obsolete, FUN_004b5010).
		public static int WaterCode(int waterTilesAtCenter, bool riverInRadius, bool floodPlainRiver,
				bool waterInRadius, bool hasHarbor) {
			int code;
			if (riverInRadius && floodPlainRiver) {
				code = 9;
			} else if (waterTilesAtCenter >= 5) {
				code = 6;
			} else if (waterTilesAtCenter >= 3) {
				code = 4;
			} else if (waterTilesAtCenter >= 1) {
				code = 2;
			} else if (riverInRadius) {
				code = waterInRadius ? 8 : 1;
			} else {
				code = 0;
			}
			return hasHarbor ? code + 1 : code;
		}

		// What the picture of a city depends on.
		public static Input InputFor(GameData gameData, City city) {
			Player owner = city.owner;
			Tile tile = city.location;
			string terrainKey = tile.baseTerrainType?.Key;
			Input input = new() {
				era = Math.Clamp(owner?.EraIndex() ?? 0, 0, 3),
				cultureGroup = owner?.civilization?.cultureGroup?.index ?? -1,
				terrain = terrainKey switch {
					"desert" => Terrain.Desert,
					"plains" => Terrain.Plains,
					"tundra" => Terrain.Tundra,
					_ => Terrain.Grassland,
				},
				population = city.residents.Count,
				maxTownSize = gameData.rules.MaximumLevel1CitySize,
				maxCitySize = gameData.rules.MaximumLevel2CitySize,
				mapX = tile.XCoordinate,
				mapY = tile.YCoordinate,
			};

			foreach (Building building in gameData.Buildings) {
				int art = ArtOf(building.civilopediaEntry);
				if (art < 0) {
					continue;
				}
				input.knownArt.Add(art);
				if (art == PALACE) {
					input.knownArt.Add(PALACE_SECOND_PIECE);
				}
				if (building.ToSaveBuilding().flags.Contains(SaveBuilding.Flag.MustBeNearRiver)) {
					input.nearRiverArt.Add(art);
				}
				if (art == WALLS) {
					input.wallsDefend = building.bombardDefense > 0 && building.combatDefenseBonus != null;
				}
			}

			List<Building> built = city.GetBuildings().Select(cb => cb.building).ToList();
			bool Obsolete(Building b) => b.renderedObsoleteBy != null && owner != null
				&& owner.knownTechs.Contains(b.renderedObsoleteBy.id);
			foreach (Building building in built) {
				int art = ArtOf(building.civilopediaEntry);
				if (art >= 0) {
					input.builtArt.Add(art);
				}
			}
			bool town = input.population <= input.maxTownSize && input.population <= input.maxCitySize;
			input.townHasBombardDefense = town && built.Any(b => b.bombardDefense > 0 && !Obsolete(b));

			List<Tile> center = tile.GetTilesWithinRankDistance(1);
			List<Tile> radius = tile.GetTilesWithinRankDistance(2);
			int waterAtCenter = center.Count(t => t.IsWater());
			input.riverInRadius = radius.Any(t => t.BordersRiver());
			bool floodPlainRiver = (terrainKey == "desert" || terrainKey == "flood plain")
				&& radius.Any(t => t.BordersRiver() && t.baseTerrainType?.Key == "flood plain");
			input.floodPlainCityByWater = waterAtCenter > 0;
			input.waterCode = WaterCode(waterAtCenter, input.riverInRadius, floodPlainRiver,
				radius.Any(t => t.IsWater()), built.Any(b => b.allowsWaterTrade && !Obsolete(b)));
			return input;
		}

		// Whether the picture shows a building of the city (Civ3
		// FUN_00407c40): one it has, and, if it must be near a river, only
		// when the city is by water. The palace's second piece goes with it.
		public static bool Shows(Input input, int art) {
			if (art == PALACE_SECOND_PIECE) {
				art = PALACE;
			}
			return input.builtArt.Contains(art) && input.knownArt.Contains(art)
				&& (!input.nearRiverArt.Contains(art) || input.waterCode != 0);
		}

		// Which of the three layouts the picture uses (Civ3 FUN_00416910):
		// the one for the city's size, or a bigger one if a building the city
		// has that the metropolis has room for has none there.
		public static int SizeIndex(Input input) {
			int size = input.population > input.maxCitySize ? 2 : input.population > input.maxTownSize ? 1 : 0;
			for (; size < 2; size++) {
				bool missing = false;
				for (int art = 0; art < ART_COUNT && !missing; art++) {
					if (Shows(input, art) && InLayout(2, art, LAYOUT_SIZE) && !InLayout(size, art, 14 * 17 + 1)) {
						missing = true;
					}
				}
				if (!missing) {
					break;
				}
			}
			return size;
		}

		// Whether an art index is in a layout. Civ3 looks at a little more
		// than the layout, 14 rows of 18, into the next one.
		private static bool InLayout(int size, int art, int length) {
			for (int i = 0; i < length; i++) {
				if (LayoutAt(size * LAYOUT_SIZE + i) == art) {
					return true;
				}
			}
			return false;
		}

		private static int LayoutAt(int index) => index >= 0 && index < Layouts.Length ? Layouts[index] : -8;

		// The pieces of the picture of a city, back to front.
		public static List<Sprite> Compose(Input input) => new Composer(input).Compose();

		private sealed class Composer {
			private readonly Input input;
			private readonly int size;
			private readonly int era;
			private readonly List<Sprite> sprites = [];
			// The street corners (14 rows of 18): -1 for none, 0 for a corner
			// a street may go through, 1 for one it does.
			private readonly int[] streets = new int[14 * 18];
			// How many tall houses have been drawn.
			private int highHouses;
			private int skyscrapers;

			public Composer(Input input) {
				this.input = input;
				size = SizeIndex(input);
				era = Math.Clamp(input.era, 0, 3);
			}

			private string T => TerrainLetters[(int)input.terrain];
			private string Culture => input.cultureGroup >= 0 && input.cultureGroup < Cultures.Length ? Cultures[input.cultureGroup] : null;
			private int Seed => input.mapX * input.mapY;

			private int Cell(int row, int column) => LayoutAt(CellIndex(row, column));
			private int CellIndex(int row, int column) => column + (row + size * 13) * 17;
			private bool Shows(int art) => CityView.Shows(input, art);
			private bool Known(int art) => art == PALACE_SECOND_PIECE || input.knownArt.Contains(art);

			private int Street(int index) => index >= 0 && index < streets.Length ? streets[index] : -1;

			// The random numbers of one cell (Civ3 FUN_00415570, FUN_004137f0).
			private Lcg CellRandom(int row, int column) {
				Lcg random = new(unchecked((17 * row + column + 1) * Seed * 0x130d907a));
				random.Next();
				random.Next();
				return random;
			}

			// The cells in the order Civ3 goes through them, back to front:
			// along the diagonals from the top right corner.
			private static IEnumerable<(int row, int column)> Cells() {
				for (int diagonal = 0; diagonal < 29; diagonal++) {
					int k = Math.Min(diagonal, 16);
					int row = diagonal - k;
					int column = 16 - k;
					while (row < 13 && k >= 0) {
						yield return (row, column);
						row++;
						k--;
						column++;
					}
				}
			}

			public List<Sprite> Compose() {
				AddBackground();
				AddWater();
				PlanStreets();
				AddStreets();
				AddMainStreet();
				AddBuildings();
				return sprites;
			}

			private void Add(string path, int x, int y, int art = -1) {
				sprites.Add(new Sprite(path, 0, 0, 0, 0, x, y, false, art));
			}

			private void Add(string path, (int x, int y, int w, int h) crop, int x, int y, bool shadow = false, int art = -1) {
				sprites.Add(new Sprite(path, crop.x, crop.y, crop.w, crop.h, x, y, shadow, art));
			}

			private void AddShadow(string path, int x, int y) {
				if (path != null) {
					sprites.Add(new Sprite(path, 0, 0, 0, 0, x, y, true, -1));
				}
			}

			// Civ3 FUN_00408d20.
			private void AddBackground() {
				Add(ART + "Backgrounds/" + T + "-" + new[] { "SML", "MED", "LRG" }[size] + ".pcx", 0, 0);
			}

			// The rivers, coasts and harbor (Civ3 FUN_004082b0, FUN_00408b00).
			private void AddWater() {
				int code = input.waterCode;
				if (code < 1) {
					return;
				}
				string water = ART + "Backgrounds/" + T + "-H2O-";
				string riverFront = water + (code == 9 || code == 10 ? "RiverFore-FP.pcx" : "RiverFore.pcx");
				Add(riverFront, 207, 456);
				if (code == 1 || code == 8) {
					Add(water + "RiverBack.pcx", 863, 141);
					if (code == 8) {
						Add(water + "IslandLeft.pcx", 0, 504);
					}
					return;
				}
				if (code == 9 || code == 10) {
					Add(water + "RiverBack.pcx", 863, 141);
					Add(riverFront, 207, 456);
					if (input.floodPlainCityByWater) {
						Add(water + "CoastRight.pcx", 603, 0);
					}
				} else {
					Add(water + "CoastRight.pcx", 603, 0);
					if (!input.riverInRadius) {
						Add(water + "NOTtheRiver.pcx", 207, 574);
					}
				}
				if (code > 3 && code < 9) {
					Add(water + "CoastBack.pcx", 0, 0);
				}
				if (code > 5 && code < 9) {
					Add(water + "IslandLeft.pcx", 0, 504);
				}
				if (code == 3 || code == 5 || code == 7 || code == 10) {
					Add(water + "Harbor.pcx", 646, 457);
				}
			}

			// Where the streets go (Civ3 FUN_00408d20, FUN_00414210): the
			// corners of the houses' cells may take one, the main street's are
			// streets, and each building, and most houses, get a street to the
			// main one.
			private void PlanStreets() {
				Array.Fill(streets, -1);
				for (int row = 0; row < 13; row++) {
					for (int column = 0; column < 17; column++) {
						int cell = Cell(row, column);
						if (cell != -6 && cell != -7 && cell != -10 && cell != -9) {
							continue;
						}
						int corner = row * 18 + column;
						bool evenRow = row % 2 == 0, evenColumn = column % 2 == 0;
						if (evenRow || evenColumn) {
							streets[corner] = 0;
						}
						if (!evenRow || evenColumn) {
							streets[corner + 18] = 0;
						}
						if (evenRow || !evenColumn) {
							streets[corner + 1] = 0;
						}
						if (!evenRow || !evenColumn) {
							streets[corner + 19] = 0;
						}
					}
				}
				for (int row = 0; row < 13; row++) {
					for (int column = 0; column < 17; column++) {
						int cell = Cell(row, column);
						if (cell != -5 && cell != -4) {
							continue;
						}
						int corner = row * 18 + column;
						if (row % 2 == 0 || column % 2 == 1) {
							streets[corner] = 1;
							streets[corner + 1] = 1;
						} else {
							streets[corner] = 0;
							streets[corner + 1] = -1;
						}
					}
				}

				Lcg random = new(unchecked(90210 * Seed));
				random.Next();
				random.Next();
				foreach ((int row, int column) in Cells()) {
					float chance = (float)random.Next();
					int cell = Cell(row, column);
					if (cell < 0) {
						if (cell == -6 || cell == -10 || cell == -9) {
							Houses(row, column, 0, chance, true);
						}
					} else if (cell != AQUEDUCT && cell != HARBOR) {
						if (Shows(cell)) {
							StreetsFrom(FootprintOfCell(cell), row, column);
						} else {
							Houses(row, column, FootprintOfCell(cell), chance, true);
						}
					}
				}
			}

			// The footprint Civ3 gives a cell's building (FUN_00407a60): -7,
			// one cell, for one the rules lack and for the palace's second
			// piece, which Conquests' rules don't have at that index.
			private int FootprintOfCell(int art) =>
				art != PALACE_SECOND_PIECE && input.knownArt.Contains(art) ? FootprintOf(art) : -7;

			// The corners of a building's cells, by footprint, each with the
			// cell Civ3 checks is on the ground (FUN_00414590).
			private static readonly (int row, int column, int cellRow, int cellColumn)[][] FootprintCorners = [
				[(0, 0, 0, 0), (1, 0, 1, 0), (0, 1, 0, 1), (1, 1, 1, 1)],
				[(0, 0, 0, 0), (1, 0, 1, 0), (0, 1, 0, 1), (1, 1, 1, 1), (0, 2, 0, 2), (1, 2, 1, 2)],
				[(0, 0, 0, 0), (0, 1, 0, 1), (1, 0, 1, 0), (1, 1, 1, 1), (0, 2, 0, 2), (1, 2, 1, 2), (0, 3, 0, 3), (1, 3, 0, 3)],
				[(0, 0, 0, 0), (1, 0, 1, 0), (0, 1, 0, 1), (1, 1, 1, 1), (2, 0, 2, 0), (2, 1, 2, 1)],
				[(0, 0, 0, 0), (1, 0, 1, 0), (2, 0, 2, 0), (0, 1, 0, 1), (0, 2, 0, 2), (1, 2, 1, 2), (2, 2, 2, 2), (2, 1, 2, 1)],
				[(0, 0, 0, 0), (0, 1, 0, 1), (1, 0, 1, 0), (1, 1, 1, 1), (2, 0, 2, 0), (2, 1, 2, 1), (3, 0, 3, 0), (3, 1, 3, 1)],
			];

			// Streets from the corners of a building's cells to the main
			// street (Civ3 FUN_00414590).
			private void StreetsFrom(int footprint, int row, int column) {
				if (footprint < 0 || footprint >= FootprintCorners.Length) {
					return;
				}
				foreach ((int dr, int dc, int cr, int cc) in FootprintCorners[footprint]) {
					int r = row + dr, c = column + dc;
					if (c > 17 || Street(r * 18 + c) == -1 || LayoutAt(CellIndex(row + cr, column + cc)) == -8) {
						continue;
					}
					(int targetRow, int targetColumn) = NearestMainStreetCorner(r, c);
					FindStreet(r, c, targetRow, targetColumn, -1, -1, false, false, 0);
				}
			}

			// Civ3 FUN_00415c50.
			private (int row, int column) NearestMainStreetCorner(int fromRow, int fromColumn) {
				int best = 0xfffffff;
				(int, int) result = (fromRow, fromColumn);
				for (int row = 0; row < 13; row++) {
					for (int column = 0; column < 17; column++) {
						int cell = Cell(row, column);
						if (cell != -5 && cell != -4) {
							continue;
						}
						int dr = fromRow - row;
						if (row % 2 == 0 || column % 2 == 1) {
							int dc = fromColumn - column - 1;
							if (dr * dr + dc * dc < best) {
								best = dr * dr + dc * dc;
								result = (row, column + 1);
							}
						}
						if (row % 2 == 0 || column % 2 == 0) {
							int dc = fromColumn - column;
							if (dr * dr + dc * dc < best) {
								best = dr * dr + dc * dc;
								result = (row, column);
							}
						}
					}
				}
				return result;
			}

			// Civ3's street finder (FUN_00415d30): a search from corner to
			// corner towards the target, along the corners that can take a
			// street, marking the way it finds. The depth limit is C7's.
			private bool FindStreet(int row, int column, int targetRow, int targetColumn, int fromRow, int fromColumn,
					bool rowReached, bool columnReached, int depth) {
				if (row < 0 || row > 13 || column < 0 || column > 17 || depth > 400) {
					return false;
				}
				int corner = row * 18 + column;
				if (streets[corner] == -1) {
					return false;
				}
				if (row == targetRow && column == targetColumn) {
					streets[corner] = 1;
					return true;
				}
				int dr = targetRow < row ? -1 : 1;
				int dc = targetColumn < column ? -1 : 1;
				if (targetRow == fromRow) {
					rowReached = true;
				}
				if (targetColumn == fromColumn) {
					columnReached = true;
				}
				bool first = true, second = true, third = true, fourth = true;
				(int, int)[] moves;
				if (((targetRow - row) * dr < (targetColumn - column) * dc && targetRow != row) || targetColumn == column) {
					if (rowReached && column != targetColumn) {
						first = false;
						third = false;
					}
					if (columnReached && row != targetRow) {
						second = false;
						fourth = false;
					}
					moves = [(row + dr, column), (row, column + dc), (row - dr, column), (row, column - dc)];
				} else {
					if (rowReached && column != targetColumn) {
						second = false;
						fourth = false;
					}
					if (columnReached && row != targetRow) {
						first = false;
						third = false;
					}
					moves = [(row, column + dc), (row + dr, column), (row, column - dc), (row - dr, column)];
				}
				bool[] allowed = [first, second, third, fourth];
				for (int i = 0; i < 4; i++) {
					(int r, int c) = moves[i];
					if (allowed[i] && (r != fromRow || c != fromColumn)
							&& FindStreet(r, c, targetRow, targetColumn, row, column, rowReached, columnReached, depth + 1)) {
						streets[corner] = 1;
						return true;
					}
				}
				return false;
			}

			// Whether a house goes in a cell (Civ3 FUN_00416840), given a
			// random chance: rarely at the middle of the city, mostly towards
			// its edge, never past it, always next to the tall houses (a chance
			// of 2).
			private bool HouseHere(int row, int column, float chance) {
				if (chance == 2f) {
					return true;
				}
				double falloff = size == 0 ? 0.33f : 0.12f;
				int distance = Math.Abs(column - 7) + Math.Abs(row - 9);
				double left = 1.0 - distance * falloff;
				double factor = left > 0 ? 1.0 / left : 0;
				return factor * chance * 10.0 > 9.0;
			}

			// The houses of a cell, and of the other cells of a building the
			// city doesn't have; when planning, the streets to them instead
			// (Civ3 FUN_004137f0).
			private void Houses(int row, int column, int footprint, float chance, bool planning) {
				int index = CellIndex(row, column);
				int cell = LayoutAt(index);
				if (cell == -5 || cell == -4 || cell == -3) {
					return;
				}
				bool tall = cell == -10 || cell == -9;
				if (era > 1 && planning && tall) {
					StreetsFrom(0, row, column);
				}
				if (era > 1 && !tall) {
					foreach (int offset in new[] { 17, 1, 18, 34, 2, 35, 19, 36 }) {
						int next = LayoutAt(index + offset);
						if (next == -10 || next == -9) {
							chance = 2f;
						}
					}
				}
				if (HouseHere(row, column, chance)) {
					House(row, column, planning);
				}
				if (footprint <= 0) {
					return;
				}
				(int, int)[] others = footprint switch {
					1 => [(row, column + 1)],
					2 => [(row, column + 1), (row, column + 2)],
					3 => [(row + 1, column)],
					4 => [(row, column + 1), (row + 1, column), (row + 1, column + 1)],
					5 when column >= 2 => [(row + 1, column), (row + 2, column)],
					_ => [],
				};
				Lcg random = CellRandom(row, column);
				foreach ((int r, int c) in others) {
					if (HouseHere(r, c, (float)random.Next())) {
						House(r, c, planning);
					}
				}
			}

			private void House(int row, int column, bool planning) {
				if (planning) {
					Lcg random = CellRandom(row, column);
					if (random.NextInt(12) < 8) {
						StreetsFrom(0, row, column);
					}
					return;
				}
				if (Culture == null) {
					return;
				}
				int house = CellRandom(row, column).NextInt(12);
				(int, int, int, int) crop = (1 + 76 * house, era * 109 + 1, 75, 108);
				(int x, int y) = CellPosition(row, column);
				Add(ART + "Miscellaneous/Filler-" + Culture + "_Sh.pcx", crop, x, y, shadow: true);
				Add(ART + "Miscellaneous/Filler-" + Culture + ".pcx", crop, x, y);
			}

			private static (int x, int y) CellPosition(int row, int column) =>
				(CellX[row * 18 + column], CellY[row * 18 + column]);

			// The streets between the corners (Civ3 FUN_00413ff0), each piece
			// joining a corner to those next to it on a street.
			private void AddStreets() {
				string roads = ART + "Miscellaneous/ROADS-" + Eras[era] + ".pcx";
				for (int row = 0; row < 14; row++) {
					for (int column = 0; column < 18; column++) {
						int corner = row * 18 + column;
						if (streets[corner] == -1 || streets[corner] == 0) {
							continue;
						}
						int joins = 0;
						if (Street(corner + 1) == 1) {
							joins |= 1;
						}
						if (Street(corner + 18) == 1) {
							joins |= 2;
						}
						if (column > 0 && Street(corner - 1) == 1) {
							joins |= 4;
						}
						if (row > 0 && Street(corner - 18) == 1) {
							joins |= 8;
						}
						int index = CellIndex(row, column);
						joins = LeaveOutMainStreet(joins, LayoutAt(index));
						if (column > 0) {
							joins = LeaveOutMainStreet(joins, LayoutAt(index - 1));
						}
						if (column == 17) {
							joins &= ~0xa;
						}
						if (joins != 0) {
							Add(roads, RoadPiece(joins - 1), CellX[corner], CellY[corner] - 15);
						}
					}
				}
			}

			private static int LeaveOutMainStreet(int joins, int cell) => cell switch {
				-5 or -3 => joins & ~0xa,
				-4 => joins & ~0x2,
				_ => joins,
			};

			// A piece of ROADS-<era>.pcx: two rows of 9 for each terrain.
			private (int, int, int, int) RoadPiece(int piece) {
				int top = new[] { 1, 219, 437, 655 }[(int)input.terrain] + piece / 9 * 109;
				return (1 + 76 * (piece % 9), top, 75, 108);
			}

			// The main street's paving (Civ3 FUN_004120f0).
			private void AddMainStreet() {
				string roads = ART + "Miscellaneous/ROADS-" + Eras[era] + ".pcx";
				for (int row = 0; row < 13; row++) {
					for (int column = 16; column >= 0; column--) {
						int piece = Cell(row, column) switch { -5 => 15, -4 => 16, -3 => 17, _ => -1 };
						if (piece >= 0) {
							(int x, int y) = CellPosition(row, column);
							Add(roads, RoadPiece(piece), x, y);
						}
					}
				}
			}

			// The buildings, houses and walls (Civ3 FUN_00412210).
			private void AddBuildings() {
				foreach (int art in FixedBefore) {
					AddFixed(art);
				}
				Lcg random = new(unchecked(90210 * Seed));
				random.Next();
				random.Next();
				for (int pass = 0; pass < 3; pass++) {
					if (pass == 1) {
						AddBackWalls();
					} else if (pass == 2) {
						AddFrontWalls();
					}
					foreach ((int row, int column) in Cells()) {
						int cell = Cell(row, column);
						int cellPass = PassOfCell[row * 17 + column];
						if (cellPass != pass && !(cell >= 0 && cellPass == 3 && pass == 1 && Shows(cell))) {
							continue;
						}
						AddCell(row, column, cell, (float)random.Next());
					}
				}
				foreach (int art in FixedAfter) {
					AddFixed(art);
				}
			}

			private void AddCell(int row, int column, int cell, float chance) {
				switch (cell) {
					case -4:
						// The main square's statue.
						if (Shows(LONGEVITY)) {
							AddBuildingAt(row, column, LONGEVITY);
						}
						return;
					case -6:
						Houses(row, column, 0, chance, false);
						return;
					case -10:
					case -9:
						AddTallHouse(row, column, cell);
						return;
					case -7:
						// Wall Street is drawn again over the cell in front.
						if (row > 1 && LayoutAt(CellIndex(row - 1, column)) == WALL_STREET && Shows(WALL_STREET)) {
							AddBuildingAt(row - 1, column, WALL_STREET);
						}
						return;
				}
				if (cell < 0) {
					return;
				}
				if (cell == AQUEDUCT || cell == HARBOR) {
					AddFixed(cell);
				} else if (Known(cell) && Shows(cell)) {
					AddBuildingAt(row, column, cell);
				} else {
					Houses(row, column, FootprintOfCell(cell), chance, false);
				}
				// The United Nations covers the harbor, which is drawn again.
				if (cell == UNITED_NATIONS && Shows(UNITED_NATIONS) && Shows(HARBOR)) {
					AddFixed(HARBOR);
				}
			}

			// The tall houses of the industrial and modern eras (Civ3
			// FUN_00415570). Civ3 takes each one's look from the city's data;
			// here they're picked at random for the city.
			private void AddTallHouse(int row, int column, int cell) {
				(int x, int y) = CellPosition(row, column);
				Lcg random = new(unchecked((highHouses + 2 * skyscrapers + 1) * Seed * 0x130d907a));
				random.Next();
				int look = random.NextInt(8);
				if ((cell == -10 && era == 3) || (cell == -9 && era == 2)) {
					highHouses++;
					(int, int, int, int) crop = (1 + 76 * look, 1, 75, 216);
					string file = ART + "Miscellaneous/Filler-HIGH-" + Eras[era];
					Add(file + "_Sh.pcx", crop, x, y - 109, shadow: true);
					Add(file + ".pcx", crop, x, y - 109);
				} else if (cell == -9 && era == 3) {
					skyscrapers++;
					(int, int, int, int) crop = (1 + 76 * look, 1, 75, 324);
					string file = ART + "Miscellaneous/Filler-SKY-MOD";
					Add(file + "_Sh.pcx", crop, x, y - 218, shadow: true);
					Add(file + ".pcx", crop, x, y - 218);
				}
			}

			private void AddBuildingAt(int row, int column, int art) {
				(string file, string shadow, (int, int, int, int) crop)? piece = CellArt(art);
				if (piece == null) {
					return;
				}
				(int x, int y) = CellPosition(row, column);
				if (piece.Value.shadow != null) {
					Add(piece.Value.shadow, piece.Value.crop, x, y, shadow: true);
				}
				Add(piece.Value.file, piece.Value.crop, x, y, art: art);
			}

			// The buildings of IMP-Ages1.pcx, a row each.
			private static readonly int[] Ages1Rows = [15, 13, 2, 6, 5, 9, 19, 28, 16, 14, 21, 20, 22];

			// Where a building drawn in a cell is cut from (Civ3
			// FUN_00409630, FUN_0040d6a0): sheets with a column for each era
			// or culture group, or a single picture.
			private (string file, string shadow, (int, int, int, int) crop)? CellArt(int art) {
				string buildings = ART + "Buildings/";
				(string, string, (int, int, int, int)) Sheet(string name, int x, int y, int w, int h) =>
					(buildings + name + ".pcx", buildings + name + "_Sh.pcx", (x, y, w, h));
				int byEra75 = era * 76 + 1, byEra112 = era * 115 + 1, byEra150 = era * 154 + 1;
				int culture = input.cultureGroup;
				int ages1Row = Array.IndexOf(Ages1Rows, art);
				if (ages1Row >= 0) {
					return Sheet("IMP-Ages1", byEra75, 1 + 109 * ages1Row, 75, 108);
				}
				switch (art) {
					case 54: return Sheet("SWON-Monuments1", 1, 1, 75, 108);
					case 50: return Sheet("SWON-Monuments1", 77, 1, 75, 108);
					case 63: return Sheet("SWON-Monuments1", 153, 1, 75, 108);
					case 37: return Sheet("WON-Monuments1", 1, 1, 75, 108);
					case 52: return Sheet("WON-Monuments1", 77, 1, 75, 108);
					case 48: return Sheet("WON-Monuments1", 153, 1, 75, 108);
					case 46: return Sheet("WON-Monuments1", 229, 1, 75, 108);
					case 51: return Sheet("WON-Monuments1", 305, 1, 75, 108);
					case 1: return Sheet("IMP-Ages2-UP", byEra112, 1, 112, 108);
					case 4: return Sheet("IMP-Ages2-UP", byEra112, 110, 112, 108);
					case 55: return Sheet("SWON-Ages2-DOWN", byEra112, 1, 112, 124);
					case 59: return Sheet("SWON-Ages2-DOWN", byEra112, 126, 112, 124);
					case 62: return Sheet("SWON-Ages2-UP", byEra112, 1, 112, 108);
					case 57: return Sheet("SWON-Ages2-UP", byEra112, 110, 112, 108);
					case 34: return Sheet("WON-Ages3", 1, 1, 150, 140);
					case 12: return Sheet("IMP-Ages4", byEra150, 1, 150, 124);
					case 11: return Sheet("IMP-Ages4", byEra150, 126, 150, 124);
					case 44: return Sheet("WON-Ages4", byEra150, 1, 150, 124);
					case 42: return Sheet("WON-Ages4", byEra150, 126, 150, 124);
					case 74: return (buildings + "x_internet.pcx", buildings + "x_internet_shadow.pcx", (0, 0, 75, 108));
					case 75: return (buildings + "x_civildefence.pcx", buildings + "x_civildefence_shadow.pcx", (0, 0, 153, 124));
					case 76: return (buildings + "x_stockexchange.pcx", buildings + "x_stockexchange_shadow.pcx", (0, 0, 75, 108));
				}
				if (culture < 0 || culture >= Cultures.Length) {
					return null;
				}
				int byCulture75 = culture * 76 + 1, byCulture112 = culture * 115 + 1, byCulture150 = culture * 154 + 1;
				switch (art) {
					case 3: return Sheet("IMP-Cultures1", byCulture75, 1, 75, 108);
					case 10: return Sheet("IMP-Cultures1", byCulture75, 110, 75, 108);
					case 39: return Sheet("WON-Cultures1", byCulture75, 1, 75, 108);
					case 38: return Sheet("WON-Cultures2-UP", byCulture112, 1, 112, 108);
					case 49: return Sheet("WON-Cultures2-DOWN", byCulture112, 1, 112, 124);
					case 56: return Sheet("SWON-Cultures4", byCulture150, 1, 150, 124);
					case 43: return Sheet("WON-Cultures4", byCulture150, 1, 150, 124);
					case 41: return Sheet("WON-Cultures4", byCulture150, 126, 150, 124);
					case PALACE:
					case PALACE_SECOND_PIECE: {
							// The first palace, or the culture's palace as far as
							// it's built (Civ3 FUN_0040d6a0).
							int stage = Math.Clamp(input.palaceStage, 0, 3);
							string name = stage < 1 ? "Imp-Palace-CAVE" : "Imp-Palace-" + Cultures[culture];
							return art == PALACE
								? Sheet(name, stage < 1 ? 1 : (stage - 1) * 154 + 1, 1, 150, 108)
								: Sheet(name, stage < 1 ? 1 : (stage - 1) * 76 + 1, 110, 75, 108);
						}
				}
				return null;
			}

			// A building drawn whole where Civ3 puts it (FUN_00413440), with
			// its shadow, if the city has it.
			private void AddFixed(int art) {
				if (!Shows(art) || !FixedPositions.TryGetValue(art, out (int x, int y) at)) {
					return;
				}
				string buildings = ART + "Buildings/";
				string file, shadow;
				switch (art) {
					case AQUEDUCT: {
							string age = era < 3 ? "ANC" : "MOD";
							file = "IMP-Aqueduct-" + age + "-" + T;
							shadow = "IMP-Aqueduct-" + age + "_Sh";
							break;
						}
					case 27: {
							string age = era > 2 ? "MOD" : "IND";
							file = "IMP-Airport-" + age + "-" + T;
							shadow = "IMP-Airport-" + age + "_Sh";
							break;
						}
					case HARBOR:
						file = "IMP-Harbor-" + Eras[era];
						shadow = file + "_Sh";
						break;
					case 77:
						Add(buildings + "x_commdock_shadow.pcx", (0, 0, 153, 124), at.x, at.y, shadow: true);
						Add(buildings + "x_commdock.pcx", (0, 0, 153, 124), at.x, at.y, art: art);
						return;
					default: {
							(string name, bool byTerrain, bool hasShadow) = art switch {
								17 => ("IMP-PlantHydro", true, true),
								18 => ("IMP-PlantNuke", true, true),
								24 => ("IMP-PlantSolar", true, true),
								26 => ("IMP-Offshore", false, true),
								30 => ("WON-Pyramid", true, true),
								31 => ("WON-Gardens", true, true),
								32 => ("WON-Colossus", true, true),
								33 => ("WON-LHAlex", false, true),
								35 => ("WON-Oracle", true, true),
								36 => ("WON-GreatWall", true, false),
								40 => ("WON-SolarSys", true, true),
								45 => ("WON-TradingCo", true, true),
								47 => ("WON-Dam", true, false),
								53 => ("WON-SETI", true, false),
								58 => ("SWON-Pentagon", true, true),
								60 => ("WON-Apollo", true, true),
								_ => ("SWON-SDI", false, true),
							};
							file = byTerrain ? name + "-" + T : name;
							shadow = hasShadow ? name + "_Sh" : null;
							break;
						}
				}
				AddShadow(shadow == null ? null : buildings + shadow + ".pcx", at.x, at.y);
				Add(buildings + file + ".pcx", at.x, at.y, art);
			}

			// Whether the city has walls in the picture, and which: a town
			// with a building that defends against bombardment gets the small
			// ones, a city with walls the others (Civ3 FUN_00412e90).
			private bool TownWalls => input.townHasBombardDefense
				&& input.population <= input.maxTownSize && input.population <= input.maxCitySize && size < 1;

			private string Walls => ART + "City Walls/";

			// The walls behind the city's buildings (Civ3 FUN_00412e90).
			private void AddBackWalls() {
				string culture = Culture;
				bool town = TownWalls;
				if (culture == null || (!town && !(Shows(WALLS) && input.wallsDefend))) {
					return;
				}
				AddShadow(Walls + (Shows(COASTAL_FORTRESS) ? "Wall0-Fort_Sh.pcx" : "Wall0-NOFort_Sh.pcx"), 799, 365);
				AddShadow(Walls + (Shows(HARBOR) ? "Wall" + culture + "0-Harb_Sh.pcx" : "Wall0-NOHarb_Sh.pcx"), 698, 443);
				AddShadow(Walls + (town ? "Wall0SML_Sh.pcx" : "Wall0SMLold_Sh.pcx"), 154, 336);
				Add(Walls + "Wall" + culture + (town ? "1-SML.pcx" : "1-SMLold.pcx"), 194, 319);
			}

			// The coastal fortress, the walls in front of the city's buildings
			// and the trees before them (Civ3 FUN_00412210, FUN_00413160).
			private void AddFrontWalls() {
				string culture = Culture;
				if (culture == null) {
					return;
				}
				if (Shows(COASTAL_FORTRESS)) {
					AddShadow(Walls + "Wall0-Fort_Sh.pcx", 799, 365);
					Add(Walls + "Wall" + culture + "2-Fort.pcx", 799, 365, COASTAL_FORTRESS);
				}
				bool town = TownWalls;
				if (!town && !(Shows(WALLS) && input.wallsDefend)) {
					return;
				}
				if (!Shows(COASTAL_FORTRESS)) {
					Add(Walls + "Wall" + culture + "2-NOFort.pcx", 799, 365);
				}
				Add(Walls + "Wall" + culture + (Shows(HARBOR) ? "3-Harb.pcx" : "3-NOHarb.pcx"), 698, 443);
				// Civ3 draws the town's front wall from 4-SMLold and the
				// city's from 4-SML, the other way around from the back walls.
				Add(Walls + "Wall" + culture + (town ? "4-SMLold.pcx" : "4-SML.pcx"), 176, 465);
				AddShadow(Walls + "Wall5SML-" + T + "Tree_Sh.pcx", 191, 491);
				Add(Walls + "Wall5SML-" + T + "Tree.pcx", 191, 491);
			}
		}
	}
}
