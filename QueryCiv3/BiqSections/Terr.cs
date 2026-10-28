using System;
using System.Runtime.InteropServices;

namespace QueryCiv3.Biq {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public unsafe struct TERR {
		public int Length;
		public int NumPossibleResources;
		/*
            In the TERR section of Civ 3 BIQ files, there is a bit array for possible resources
            One bit for each resource (as defined in GOOD), round up to the nearest byte for space
            Because this is a dynamic-length section, it is stored separately from this struct
        */

		private fixed byte Text[64];
		public string Name { get => Util.GetString(ref this, 8, 32); }
		public string CivilopediaEntry { get => Util.GetString(ref this, 40, 32); }

		public int IrrigationBonus;
		public int MiningBonus;
		public int RoadBonus;
		public int DefenseBonus;
		public int MovementCost;
		public int Food;
		public int Shields;
		public int Commerce;
		// Which worker job (TFRM) can be performed on this terrain type
		private int WorkerJobAllowed;
		// The worker job index, or -1 for none.
		public readonly int WorkerJob { get => WorkerJobAllowed; }
		public readonly bool CanPlantForest { get => WorkerJobAllowed == 5; }
		public readonly bool CanChopForest { get => WorkerJobAllowed == 6; }
		public readonly bool CanClearWetlands { get => WorkerJobAllowed == 7; }
		// Which Terrain this Terrain becomes if affected by pollution.  -1 = not affected.  14 = Base Terrain Type (probably 12 in Vanilla/PTW)
		public int PollutionEffect;
		public byte AllowCities;
		public byte AllowColonies;
		public byte Impassable;
		public byte ImpassableByWheeled;
		public byte AllowAirfields;
		public byte AllowForts;
		public byte AllowOutposts;
		public byte AllowRadarTowers;
		private int Unknown;
		public byte LandmarkEnabled;
		public int LandmarkFood;
		public int LandmarkShields;
		public int LandmarkCommerce;
		public int LandmarkIrrigationBonus;
		public int LandmarkMiningBonus;
		public int LandmarkRoadBonus;
		public int LandmarkMovementBonus;
		public int LandmarkDefensiveBonus;

		private fixed byte Text2[64];
		public string LandmarkName { get => Util.GetString(ref this, 157, 32); }
		public string LandmarkCivilopediaEntry { get => Util.GetString(ref this, 189, 32); }

		private int Unknown2;
		// Only the low bits are meaningful: conquests.biq has 0xCC filler
		// above them (e.g. 0xCCCCCCCC for Flood Plain, 0xCCCCCCC0 for
		// Grassland). Quintillus's editor reads "causes disease" and
		// "disease can be cured" from them
		// (https://github.com/instafluff0/C3X_Editor/blob/main/docs/biq/sections/TERR.md).
		// Which bit is which is inferred from conquests.biq: 0x4 is set for
		// exactly Flood Plain, Jungle and Marsh, the terrains the Conquests
		// Civilopedia (GCON_Disease) says cause disease, and 0x8 for Flood
		// Plain alone, the only one Sanitation cures (TECH_Sanitation:
		// "Disease from Floodplains is eliminated").
		public int TerrainFlags;
		public readonly bool CausesDisease { get => (TerrainFlags & 0x4) != 0; }
		public readonly bool DiseaseCanBeCured { get => (TerrainFlags & 0x8) != 0; }
		public int DiseaseStrength;
	}
}
