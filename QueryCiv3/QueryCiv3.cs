using System;
using System.IO;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace QueryCiv3 {
	public enum FileVersion {
		Vanilla,
		PlayTheWorld,
		Conquests,
		// Not a BIQ format QueryCiv3 recognizes, e.g. a SAV file or a BIQ with an unknown header
		Unknown,
	}

	public class Civ3Version {
		public FileVersion FileVersion;
		public string FileTypeName;
		public short MagicNumber; // UNVERIFIED: not sure what it is, or even if it is in anything except .sav files
		public int MajorVersion;
		public int MinorVersion;
	}

	public class Civ3Section {
		public string Name;
		public int Offset;
	}
	public class Civ3File {
		protected internal byte[] FileData;
		// This file's data is FileData[DataStart .. DataStart + Length). DataStart is non-zero when the file is
		// embedded in a larger buffer, e.g. the BIQ data inside a SAV file, which is read in place instead of copied.
		protected internal int DataStart;
		private readonly int DataLength;
		private Civ3Section[] sections;
		private Dictionary<string, List<int>> sectionOffsetsByName;
		// Every run of 4+ bytes in the range 0x20-0x5A, which includes the section headers but also lots of junk,
		// so it is only computed if something asks for it
		public Civ3Section[] Sections {
			get {
				if (sections == null) {
					sections = PopulateSections(new ReadOnlySpan<byte>(FileData, DataStart, DataLength));
				}
				return sections;
			}
			protected set {
				sections = value;
				sectionOffsetsByName = null;
			}
		}
		public bool IsGameFile { get; protected set; }
		public bool IsBicFile { get; protected set; }
		public Civ3Version Civ3Version { get; protected set; }
		public int Length => DataLength;
		public Civ3File(byte[] fileBytes) : this(fileBytes, 0, fileBytes.Length) { }
		public Civ3File(byte[] fileBytes, int start, int length) {
			if (start < 0 || length < 0 || start > fileBytes.Length - length) {
				throw new ArgumentOutOfRangeException(nameof(length));
			}
			this.FileData = fileBytes;
			this.DataStart = start;
			this.DataLength = length;
			if (length < 4) {
				throw new InvalidDataException($"The file is {length} bytes long, too short to be a Civ3 file.");
			}
			byte[] Civ3Bytes = new byte[]{0x43, 0x49, 0x56, 0x33}; // CIV3
			byte[] BicBytes = new byte[]{0x42, 0x49, 0x43}; // BIC
			IsGameFile = true;
			IsBicFile = true;
			for (int i = 0; i < 4; i++) {
				if (FileData[start + i] != Civ3Bytes[i]) {
					IsGameFile = false;
				}
				if (i < 3 && FileData[start + i] != BicBytes[i]) {
					IsBicFile = false;
				}
			}

			this.Civ3Version = PopulateCiv3Version();
		}

		private bool IsWholeArray => DataStart == 0 && DataLength == FileData.Length;

		private Civ3Version PopulateCiv3Version() {
			string header = "";
			short mag = -1;
			int maj = -1;
			int min = -1;

			// Files too short to have a version leave it at -1
			if (IsGameFile) {
				header = GetString(0, 4);
				if (Length >= 14) {
					mag = ReadInt16(4);
					maj = ReadInt32(6);
					min = ReadInt32(10);
				}
			} else if (IsBicFile) {
				header = GetString(0, 4);
				mag = -1;
				if (Length >= 32) {
					maj = ReadInt32(24);
					min = ReadInt32(28);
				}
			}

			return new Civ3Version() {
				FileTypeName = header,
				FileVersion = GetGameVersion(header, maj),
				MagicNumber = mag,
				MajorVersion = maj,
				MinorVersion = min,
			};
		}

		// The BIQ format version of a BIQ file. The header of vanilla BIQ files is "BIC " (with a trailing space); anything
		// other than a BIQ header, including the "CIV3" header of SAV files, is Unknown.
		internal static FileVersion GetGameVersion(string input, int majorVersion) {
			string type = input?.Trim() ?? "";
			if ((type == "BICX" || type == "BICQ") && majorVersion >= 12) return FileVersion.Conquests;
			if (type == "BICX") return FileVersion.PlayTheWorld;
			if (type == "BIC") return FileVersion.Vanilla;
			return FileVersion.Unknown;
		}
		private Dictionary<string, List<int>> SectionOffsetsByName {
			get {
				if (sectionOffsetsByName == null) {
					Dictionary<string, List<int>> offsets = new Dictionary<string, List<int>>();
					foreach (Civ3Section section in Sections) {
						if (section.Name == null) continue;
						if (!offsets.TryGetValue(section.Name, out List<int> list)) {
							list = new List<int>();
							offsets.Add(section.Name, list);
						}
						list.Add(section.Offset);
					}
					sectionOffsetsByName = offsets;
				}
				return sectionOffsetsByName;
			}
		}
		public Boolean SectionExists(string sectionName) {
			return sectionName != null && SectionOffsetsByName.ContainsKey(sectionName);
		}
		protected internal Civ3Section[] PopulateSections(byte[] Data) {
			return PopulateSections(new ReadOnlySpan<byte>(Data));
		}
		private static Civ3Section[] PopulateSections(ReadOnlySpan<byte> Data) {
			int Count = 0;
			int Offset = 0;
			List<Civ3Section> MySectionList = new List<Civ3Section>();
			for (int i = 0; i < Data.Length; i++) {
				if (Data[i] < 0x20 || Data[i] > 0x5a) {
					Count = 0;
				} else {
					if (Count == 0) {
						Offset = i;
					}
					Count++;
				}
				if (Count > 3) {
					Count = 0;
					Civ3Section Section = new Civ3Section();
					Section.Offset = Offset;
					Section.Name = System.Text.Encoding.ASCII.GetString(Data.Slice(Offset, 4));
					MySectionList.Add(Section);
				}
			}
			// TODO: Filter junk and dirty data from array (e.g. stray CITYs, non-headers, and such)
			return MySectionList.ToArray();
		}
		public int SectionOffset(string name, int nth) {
			if (name != null && SectionOffsetsByName.TryGetValue(name, out List<int> offsets)) {
				// nth is 1-based; anything less than 1 means the first one
				int index = Math.Max(nth, 1) - 1;
				if (index < offsets.Count) {
					return offsets[index] + name.Length;
				}
			}
			// TODO: Add name and nth to message
			throw new ArgumentException($"Unable to find section '{name}' nth {nth}");
		}
		// Civ3 files are little endian. These throw ArgumentOutOfRangeException if the value isn't entirely inside the file.
		// NOTE: Cast result as (uint) if unsigned desired
		public int ReadInt32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(GetCheckedSpan(offset, sizeof(int)));
		// NOTE: Cast result as (ushort) if unsigned desired
		public short ReadInt16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(GetCheckedSpan(offset, sizeof(short)));
		private ReadOnlySpan<byte> GetCheckedSpan(int offset, int length) {
			if (offset < 0 || offset > DataLength - length) {
				throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Reading {length} bytes at this offset goes past the end of the {DataLength} byte file.");
			}
			return new ReadOnlySpan<byte>(FileData, DataStart + offset, length);
		}
		// NOTE: Cast result as (sbyte) if signed desired
		public byte ReadByte(int offset) {
			if ((uint)offset >= (uint)DataLength) {
				throw new IndexOutOfRangeException();
			}
			return this.FileData[DataStart + offset];
		}
		// The bytes in [offset, offset + length), clamped to the end of the file
		private ReadOnlySpan<byte> GetSpan(int offset, int length) {
			if (offset > Length) return ReadOnlySpan<byte>.Empty;
			long clampedLength = Math.Min((long)offset + length, Length) - offset; // long, so offset + length can't overflow
			if (clampedLength <= 0) return ReadOnlySpan<byte>.Empty;
			if (offset < 0) throw new IndexOutOfRangeException();
			return new ReadOnlySpan<byte>(FileData, DataStart + offset, (int)clampedLength);
		}
		public byte[] GetBytes(int offset, int length) {
			return GetSpan(offset, length).ToArray();
		}
		// NOTE: Tried to parameterize encoding with default of Civ3StringEncoding, but default must be compile-time constant
		public string GetString(int offset, int length) {
			return Util.GetString(GetSpan(offset, length));
		}
	}
}
