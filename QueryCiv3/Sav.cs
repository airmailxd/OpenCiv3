using System;
using System.Collections.Generic;
using System.IO;
using QueryCiv3.Sav;

namespace QueryCiv3 {
	public unsafe class SavData {
		public BiqData Bic;
		public Civ3File Sav;

		// The read position while loading. Only valid during Load.
		public byte* scan;
		// The end of the data being loaded; reads never go past it
		private byte* end;

		public GAME Game;
		public WRLD Wrld;
		public TILE[] Tile;
		public CONT[] Cont;
		public LEAD[] Lead;
		public AIBS[] Aibs;
		public OUTP[] Outp;
		public VLOC[] Vloc;
		public RADT[] Radt;
		public CITY[] City;
		public PEER Peer;
		public PALV[] Palv;
		public DATE[] Date = new DATE[DATE_COUNT];
		public PLGI Plgi;
		public CNSL Cnsl;
		public TUTR Tutr;
		public FAXX Faxx;
		public HIST Hist;
		public UNIT[] Unit;
		public IDLS[] Idls;
		public CLNY[] Clny;

		public int[] CitiesPerContinent;
		// Bit k of element i means that civ k knows tech i.
		public IntBitmap[] KnownTechFlags;
		public int[] GreatWonderCityIDs;
		public bool[] GreatWondersBuilt;
		public int[] BuildingData1;
		public int[] BuildingData2;
		public int[] PrototypeStrategy1;
		public int[] PrototypeStrategy2;
		public int[] TechData;

		public int[] ResourceCounts;

		private int DateIndex = 0;
		private const int DATE_COUNT = 3;

		// LEAD section logic (blegh!):
		public LEAD_LEAD[][] ReputationRelationship;
		public LEAD_LEAD_Diplomacy[,][] LeadLeadDiplomacy;
		public short[][] LeadBldgCount;
		public short[][] LeadBldgInConstruction;
		public short[][] LeadBldgData;
		public int[][] LeadBldgSmallWonderCity;
		public bool[][] LeadBldgSmallWonderBuilt;
		public short[][] LeadPrtoCount;
		public short[][] LeadPrtoInConstruction;
		public short[][] LeadPrtoData;
		public short[][] LeadSpaceshipParts;
		public LEAD_GOOD_LEAD[,][] LeadGoodLead;
		public bool[][] LeadGoodAvailable;
		public int[][] LeadContCityCount;
		public int[][] LeadTechQueue;

		private const int LEAD_COUNT = 32; // should always be 32 in Conquests savs
		// The LEAD struct is read in these chunks, which must add up to its size (see BiqSectionSizeTests)
		internal const int LEAD_LEN_1 = 412;
		internal const int LEAD_LEN_2 = 2696;
		internal const int LEAD_LEN_3 = 108;
		internal const int LEAD_LEN_4 = 292;

		public RPLT[] Rplt;
		public RPLE[][] RpltRple;
		public string[][] RpltRpleDescription;

		public CTZN[][] CityCtzn;
		public CITY_Building[][] CityBuilding;

		private int CityIndex = 0;
		// HEURISTIC: a CITY header followed by this length starts a real city; the many other CITY headers (more than
		// there are cities, purpose unknown) are skipped. Found by inspection, not from a format description.
		private const int VALID_CITY_LENGTH = 136;
		// How often LoadSections' heuristics fired, for the debug log
		private int skippedCityHeaders, skippedGaps;

		// Not cached in a static field, so that it follows the logger the game configures
		private static Serilog.ILogger Log => Serilog.Log.ForContext<SavData>();
		private long ReadOffset => Sav.Length - (end - scan);
		// The CITY struct is read in these chunks, which must add up to its size (see BiqSectionSizeTests)
		internal const int CITY_LEN_1 = 556;
		internal const int CITY_LEN_2 = 12;
		internal const int CITY_LEN_3 = 140;

		public Turn[] HistTurn;
		public int[][] TurnCiv;
		public int[][] TurnPower;
		public int[][] TurnScore;
		public int[][] TurnCulture;
		public int[][] TurnVP;

		private const int BIQ_SECTION_START = 562;

		// The BIQ that the SAV file's own BIQ sections are loaded over, so that every Load starts from it
		private readonly byte[] baseBiqBytes;
		private bool loaded;

		public SavData(byte[] savBytes, byte[] biqBytes) {
			baseBiqBytes = biqBytes;
			Bic = new BiqData(biqBytes);
			Load(savBytes);
		}

		private static InvalidDataException Malformed(string problem) {
			return new InvalidDataException("Malformed SAV file: " + problem + ".");
		}

		// Returns the read position and moves it the given number of bytes ahead, throwing if that would go past the end of the data
		private byte* Take(long bytes, string what) {
			if (scan == null || end == null) {
				throw new InvalidOperationException("SAV data can only be read while loading.");
			}
			if (bytes < 0 || bytes > end - scan) {
				throw Malformed($"reading {bytes} bytes of {what} would go past the end of the file, {end - scan} bytes ahead");
			}
			byte* result = scan;
			scan += bytes;
			return result;
		}

		// Throws unless count values of T fit in the rest of the data, so that a corrupt count can't cause a huge allocation
		private int CheckCount<T>(long count, string what) where T : unmanaged {
			if (count < 0 || count > int.MaxValue || count * sizeof(T) > end - scan) {
				throw Malformed($"{count} {what} can't fit in the {end - scan} bytes left");
			}
			return (int)count;
		}

		private int ReadInt32(string what) {
			return *(int*)Take(sizeof(int), what);
		}

		public unsafe void Copy<T>(ref T data, int length = -1, int offset = 0) where T : unmanaged {
			if (length == -1) {
				length = sizeof(T);
			}
			if (length < 0 || offset < 0 || offset > sizeof(T) - length) {
				throw new ArgumentOutOfRangeException(nameof(length), $"Can't copy {length} bytes at offset {offset} into a {typeof(T).Name} of {sizeof(T)} bytes.");
			}

			byte* source = Take(length, typeof(T).Name);
			fixed (void* destPtr = &data) {
				Buffer.MemoryCopy(source, (byte*)destPtr + offset, length, length);
			}
		}

		public unsafe void CopyArray<T>(ref T[] data, int length) where T : unmanaged {
			CopyArray(ref data, (long)length);
		}

		private unsafe void CopyArray<T>(ref T[] data, long length) where T : unmanaged {
			int count = CheckCount<T>(length, typeof(T).Name + " values");
			long dataLength = (long)count * sizeof(T);
			byte* source = Take(dataLength, typeof(T).Name + " values");
			data = new T[count];

			fixed (void* destPtr = data) {
				Buffer.MemoryCopy(source, destPtr, dataLength, dataLength);
			}
		}

		// The length of a BIQ section that the SAV data depends on, which must have been loaded
		private static int SectionLength<T>(T[] section, string name) {
			if (section == null) {
				throw Malformed($"the BIQ data it is loaded with has no {name} section");
			}
			return section.Length;
		}

		// Clears everything a previous Load read, so that loading the same SavData again doesn't mix up the two files
		private void Reset() {
			Game = default; Wrld = default; Peer = default; Plgi = default; Cnsl = default; Tutr = default; Faxx = default; Hist = default;
			Tile = null; Cont = null; Lead = null; Aibs = null; Outp = null; Vloc = null; Radt = null; City = null; Palv = null;
			Unit = null; Idls = null; Clny = null;
			Date = new DATE[DATE_COUNT];
			DateIndex = 0;
			CitiesPerContinent = null; KnownTechFlags = null; GreatWonderCityIDs = null; GreatWondersBuilt = null;
			BuildingData1 = null; BuildingData2 = null; PrototypeStrategy1 = null; PrototypeStrategy2 = null; TechData = null;
			ResourceCounts = null;
			ReputationRelationship = null; LeadLeadDiplomacy = null; LeadBldgCount = null; LeadBldgInConstruction = null; LeadBldgData = null;
			LeadBldgSmallWonderCity = null; LeadBldgSmallWonderBuilt = null; LeadPrtoCount = null; LeadPrtoInConstruction = null;
			LeadPrtoData = null; LeadSpaceshipParts = null; LeadGoodLead = null; LeadGoodAvailable = null; LeadContCityCount = null;
			LeadTechQueue = null;
			Rplt = null; RpltRple = null; RpltRpleDescription = null;
			CityCtzn = null; CityBuilding = null;
			CityIndex = 0;
			skippedCityHeaders = 0; skippedGaps = 0;
			HistTurn = null; TurnCiv = null; TurnPower = null; TurnScore = null; TurnCulture = null; TurnVP = null;
			// The SAV's BIQ sections are loaded over those of the original BIQ, not over those of the previously loaded SAV
			if (baseBiqBytes != null) {
				Bic = new BiqData(baseBiqBytes);
			}
		}

		// Loads a SAV file. Throws InvalidDataException if the file is malformed; reads never go past the end of the data.
		// Loading again replaces everything the previous Load read.
		public unsafe void Load(byte[] savBytes) {
			ArgumentNullException.ThrowIfNull(savBytes);
			if (loaded) {
				Reset();
			}
			loaded = true;

			Sav = new Civ3File(savBytes);
			if (Sav.Length < BIQ_SECTION_START) {
				throw Malformed($"it is {Sav.Length} bytes long, too short for its {BIQ_SECTION_START} byte header");
			}
			// Load in any biq sections contained in Sav file, overwriting existing biq sections:
			int BiqSectionLength = Sav.ReadInt32(38);
			if (BiqSectionLength < 0 || BiqSectionLength > Sav.Length - BIQ_SECTION_START) {
				throw Malformed($"the length of its BIQ data, {BiqSectionLength}, doesn't fit in the file");
			}
			// Parse the embedded BIQ data in place
			Bic.Load(savBytes, BIQ_SECTION_START, BiqSectionLength);

			fixed (byte* bytePtr = savBytes) {
				scan = bytePtr + BIQ_SECTION_START + BiqSectionLength;
				end = bytePtr + savBytes.Length;
				try {
					LoadSections();
					Log.Debug("Loaded SAV: skipped {cityHeaders} CITY headers that aren't cities and {gaps} gaps between sections", skippedCityHeaders, skippedGaps);
				} finally {
					// Don't leave pointers into the no longer pinned array around
					scan = null;
					end = null;
				}
			}
		}

		private unsafe void LoadSections() {
			while (scan < end) {
				if (end - scan < sizeof(int)) {
					throw Malformed($"it ends with {end - scan} bytes that aren't a section");
				}
				int* header = (int*)scan;

				// Every header in civ 3 files is exactly 4-chars long, which means they can be represented as 32-bit integers instead of strings
				// Switching off of these hex values is substantially faster than string switching, but comes at the expense of readability
				switch (*header) {
					case 0x454d4147: // GAME
						Copy(ref Game);
						CopyArray(ref CitiesPerContinent, Game.NumberOfContinents);
						CopyArray(ref KnownTechFlags, SectionLength(Bic.Tech, "TECH"));
						CopyArray(ref GreatWonderCityIDs, SectionLength(Bic.Bldg, "BLDG"));
						CopyArray(ref GreatWondersBuilt, Bic.Bldg.Length);
						CopyArray(ref BuildingData1, Bic.Bldg.Length);
						CopyArray(ref BuildingData2, Bic.Bldg.Length);
						CopyArray(ref PrototypeStrategy1, SectionLength(Bic.Prto, "PRTO"));
						CopyArray(ref PrototypeStrategy2, Bic.Prto.Length);
						CopyArray(ref TechData, Bic.Tech.Length);

						// Instantiate City arrays after getting city count
						// Normally it'd be more straightforward to do this at the city case statement itself, but city sections are unique
						//   in that they can have many "dirty" sections which means the case statement might be encountered multiple times
						// (Every city takes up at least its 8 byte header)
						if (Game.NumberOfCities < 0 || Game.NumberOfCities > (end - scan) / 8) {
							throw Malformed($"{Game.NumberOfCities} cities can't fit in the {end - scan} bytes left");
						}
						City = new CITY[Game.NumberOfCities];
						CityCtzn = new CTZN[Game.NumberOfCities][];
						CityBuilding = new CITY_Building[Game.NumberOfCities][];
						CityIndex = 0;

						break;
					case 0x444c5257: // WRLD
						Copy(ref Wrld);
						break;
					case 0x454c4954: // TILE
						CopyArray(ref Tile, (long)Wrld.Width * Wrld.Height / 2);
						break;
					case 0x544e4f43: // CONT
						CopyArray(ref Cont, Game.NumberOfContinents);
						CopyArray(ref ResourceCounts, SectionLength(Bic.Good, "GOOD"));
						break;
					case 0x4441454c: // LEAD
						int bldgCount = SectionLength(Bic.Bldg, "BLDG");
						int prtoCount = SectionLength(Bic.Prto, "PRTO");
						int goodCount = SectionLength(Bic.Good, "GOOD");
						if (SectionLength(Bic.Rule, "RULE") == 0) {
							throw Malformed("the BIQ data it is loaded with has no RULE");
						}
						Lead = new LEAD[LEAD_COUNT];
						ReputationRelationship = new LEAD_LEAD[LEAD_COUNT][];
						LeadLeadDiplomacy = new LEAD_LEAD_Diplomacy[LEAD_COUNT, LEAD_COUNT][];
						LeadBldgCount = new short[LEAD_COUNT][];
						LeadBldgInConstruction = new short[LEAD_COUNT][];
						LeadBldgData = new short[LEAD_COUNT][];
						LeadBldgSmallWonderCity = new int[LEAD_COUNT][];
						LeadBldgSmallWonderBuilt = new bool[LEAD_COUNT][];
						LeadPrtoCount = new short[LEAD_COUNT][];
						LeadPrtoInConstruction = new short[LEAD_COUNT][];
						LeadPrtoData = new short[LEAD_COUNT][];
						LeadSpaceshipParts = new short[LEAD_COUNT][];
						LeadGoodLead = new LEAD_GOOD_LEAD[LEAD_COUNT, goodCount][];
						LeadGoodAvailable = new bool[LEAD_COUNT][];
						LeadContCityCount = new int[LEAD_COUNT][];
						LeadTechQueue = new int[LEAD_COUNT][];

						for (int i = 0; i < LEAD_COUNT; i++) {
							Copy(ref Lead[i], LEAD_LEN_1);
							CopyArray(ref ReputationRelationship[i], LEAD_COUNT);
							Copy(ref Lead[i], LEAD_LEN_2, LEAD_LEN_1);

							for (int j = 0; j < LEAD_COUNT; j++) {
								// The number of diplomacy entries, followed by the entries
								CopyArray(ref LeadLeadDiplomacy[i, j], ReadInt32("diplomacy count"));
							}

							if (Lead[i].RaceID != -1) { // if an actual leader
								CopyArray(ref LeadBldgCount[i], bldgCount);
								CopyArray(ref LeadBldgInConstruction[i], bldgCount);
								CopyArray(ref LeadBldgData[i], bldgCount);
								CopyArray(ref LeadBldgSmallWonderCity[i], bldgCount);
								CopyArray(ref LeadBldgSmallWonderBuilt[i], bldgCount);
								CopyArray(ref LeadPrtoCount[i], prtoCount);
								CopyArray(ref LeadPrtoInConstruction[i], prtoCount);
								CopyArray(ref LeadPrtoData[i], prtoCount);
								CopyArray(ref LeadSpaceshipParts[i], Bic.Rule[0].NumberOfSpaceshipParts);
								for (int j = 0; j < goodCount; j++) {
									CopyArray(ref LeadGoodLead[i, j], LEAD_COUNT);
								}
								CopyArray(ref LeadGoodAvailable[i], goodCount);
								Take(Wrld.ContinentCount * 16L, "continent data"); // 16 bytes of unknown data per continent
								CopyArray(ref LeadContCityCount[i], Wrld.ContinentCount);
							}

							Copy(ref Lead[i], LEAD_LEN_3, LEAD_LEN_1 + LEAD_LEN_2);
							CopyArray(ref LeadTechQueue[i], Lead[i].ScienceQueueSize);
							Copy(ref Lead[i], LEAD_LEN_4, LEAD_LEN_1 + LEAD_LEN_2 + LEAD_LEN_3);
						}
						break;
					case 0x534c5052: // RPLS
						// RPLS just consists of the 4-byte header and a 32-bit integer for the number of turns (RPLTs)
						// Because it's so simple, don't even both memory-copying into a struct for it and just get the RPLT length
						Take(4, "RPLS header");
						int rpltLength = CheckCount<RPLT>(ReadInt32("RPLS length"), "replay turns");

						Rplt = new RPLT[rpltLength];
						RpltRple = new RPLE[rpltLength][];
						RpltRpleDescription = new string[rpltLength][];
						for (int i = 0; i < rpltLength; i++) {
							Copy(ref Rplt[i]);
							int rpleLength = CheckCount<RPLE>(Rplt[i].EventCount, "replay events");

							RpltRple[i] = new RPLE[rpleLength];
							RpltRpleDescription[i] = new string[rpleLength];
							for (int j = 0; j < rpleLength; j++) {
								Copy(ref RpltRple[i][j]);
								// Retrieve null-terminated string, which must end before the data does:
								int stringLength = new ReadOnlySpan<byte>(scan, (int)Math.Min(end - scan, int.MaxValue)).IndexOf((byte)0);
								if (stringLength < 0) {
									throw Malformed("a replay event's description has no terminating null");
								}
								RpltRpleDescription[i][j] = Util.Civ3Encoding.GetString(scan, stringLength); // without the null character
								Take(stringLength + 1L, "replay event description");
							}
						}

						break;
					case 0x53424941: // AIBS
						CopyArray(ref Aibs, Game.NumberOfAirbases);
						break;
					case 0x5054554f: // OUTP
						CopyArray(ref Outp, Game.NumberOfOutposts);
						break;
					case 0x434f4c56: // VLOC
						CopyArray(ref Vloc, Game.NumberOfVPLocations);
						break;
					case 0x54444152: // RADT
						CopyArray(ref Radt, Game.NumberOfRadarTowers);
						break;
					case 0x59544943: // CITY
						// Sav files contain many "bad" City headers. In fact, there are more bad ones than valid ones
						// The purpose behind these headers is yet to be determined, but for now, they can be skipped
						if (SectionDataLength("CITY") == VALID_CITY_LENGTH) {
							if (City == null) {
								throw Malformed("a city comes before the GAME section");
							}
							if (CityIndex >= City.Length) {
								throw Malformed($"it has more than the {City.Length} cities its GAME section says it has");
							}
							Copy(ref City[CityIndex], CITY_LEN_1);
							CopyArray(ref CityCtzn[CityIndex], City[CityIndex].Popd.CitizenCount);
							Copy(ref City[CityIndex], CITY_LEN_2, CITY_LEN_1);
							CopyArray(ref CityBuilding[CityIndex], City[CityIndex].Binf.BuildingCount);
							Copy(ref City[CityIndex], CITY_LEN_3, CITY_LEN_1 + CITY_LEN_2);
							CityIndex++;
						} else {
							skippedCityHeaders++;
							SkipSection("CITY");
						}

						break;
					case 0x52454550: // PEER
						Copy(ref Peer);
						break;
					case 0x564c4150: // PALV
						CopyArray(ref Palv, LEAD_COUNT);
						break;
					case 0x45544144: // DATE
						if (DateIndex >= Date.Length) {
							throw Malformed($"it has more than {Date.Length} DATE sections");
						}
						Copy(ref Date[DateIndex++]);
						break;
					case 0x49474c50: // PLGI
						Copy(ref Plgi);
						break;
					case 0x4c534e43: // CNSL
						Copy(ref Cnsl);
						break;
					case 0x52545554: // TUTR
						Copy(ref Tutr);
						break;
					case 0x58584146: // FAXX
						Copy(ref Faxx);
						break;
					case 0x54534948: // HIST
						Copy(ref Hist);
						int turnCount = CheckCount<Turn>(Hist.TurnCount, "history turns");
						HistTurn = new Turn[turnCount];
						TurnCiv = new int[turnCount][];
						TurnPower = new int[turnCount][];
						TurnScore = new int[turnCount][];
						TurnCulture = new int[turnCount][];
						TurnVP = new int[turnCount][];

						// Histogram tracks Power, Score, Culture, and optionally Victory Points if that victory condition is enabled:
						for (int i = 0; i < turnCount; i++) {
							Copy(ref HistTurn[i]);
							int CivCount = HistTurn[i].RemainingCivs;
							CopyArray(ref TurnCiv[i], CivCount);
							CopyArray(ref TurnPower[i], CivCount);
							CopyArray(ref TurnScore[i], CivCount);
							CopyArray(ref TurnCulture[i], CivCount);
							if (Game.VictoryLocations) {
								CopyArray(ref TurnVP[i], CivCount);
							}
						}
						break;
					case 0x54494e55: // UNIT
						// Because most units have IDLS sections, it's easier to keep the array lengths the same and accept
						// that some indexes of Idls will be unused
						int unitCount = CheckCount<UNIT>(Game.NumberOfUnits, "units");
						Unit = new UNIT[unitCount];
						Idls = new IDLS[unitCount];

						for (int i = 0; i < unitCount; i++) {
							Copy(ref Unit[i]);
							if (Unit[i].HasIDLSSection) {
								Copy(ref Idls[i]);
							}
						}

						break;
					case 0x47505443: // CTPG
						// It's unclear what CTPG does, so for now, give it the invalid CITY treatment
						SkipSection("CTPG");
						break;
					case 0x594e4c43: // CLNY
						CopyArray(ref Clny, Game.NumberOfColonies);
						break;
					default:
						// There are 3 places in the Sav files where an inexplicable but consistent gap between sections exists
						// In any other case where a header isn't encounterd, we'll throw an error because something has gone wrong in the read
						// But for these 3, I guess just skip them for now...
						// Thoroughly magic: HEURISTIC look-ahead for the next known header (CNSL 8 bytes on, PALV 256 bytes
						// on, PEER 4 bytes on). The gaps' sizes are consistent, but what they hold is unknown, so they are
						// found by look-ahead rather than skipped by a fixed size after a particular section.
						// (Only look as far ahead as the data goes)
						long gapOffset = ReadOffset;
						int gap;
						if (scan + 3 * sizeof(int) <= end && header[2] == 0x4c534e43) {
							gap = 8;
						} else if (scan + 65 * sizeof(int) <= end && header[64] == 0x564c4150) {
							gap = 256;
						} else if (scan + 2 * sizeof(int) <= end && header[1] == 0x52454550) {
							gap = 4;
						} else {
							throw new InvalidDataException($"An error occured while parsing the SAV file because no header was found where one was expected (at {gapOffset}).");
						}
						scan += gap;
						skippedGaps++;
						Log.Verbose("Skipped a {gap} byte gap between SAV sections at {offset}", gap, gapOffset);
						break;
				}
			}
		}

		// The length of the section at the read position: the 32-bit value following its 4 byte header
		private int SectionDataLength(string name) {
			if (end - scan < 8) {
				throw Malformed($"a {name} section is cut off by the end of the file");
			}
			return *(int*)(scan + 4);
		}

		// Skips the section at the read position: its header, its length, and that many bytes
		private void SkipSection(string name) {
			int length = SectionDataLength(name);
			if (length < 0) {
				throw Malformed($"a {name} section has a negative length {length}");
			}
			Take(8L + length, name + " section");
		}
	}
}
