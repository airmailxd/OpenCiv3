using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using EngineTests.Utils;
using QueryCiv3;
using QueryCiv3.Biq;
using Xunit;

namespace EngineTests.PerfParsers;

public class QueryCiv3ParsingTests {
	[Fact]
	public void GetStringMatchesRegexImplementation() {
		Random random = new(1);
		List<byte[]> inputs = new() {
			new byte[0],
			new byte[] { 0 },
			new byte[] { 0, 65, 66 },
			new byte[] { 65, 66, 67 },
			new byte[] { 65, 0, 66, 0 },
			new byte[] { 10, 13, 65, 10, 0, 66 }, // newlines
		};
		// every byte value, without and with a null in the middle
		byte[] all = Enumerable.Range(1, 255).Select(i => (byte)i).ToArray();
		inputs.Add(all);
		inputs.Add(all.Take(100).Append((byte)0).Concat(all).ToArray());
		for (int i = 0; i < 500; i++) {
			byte[] bytes = new byte[random.Next(0, 300)];
			random.NextBytes(bytes);
			if (random.Next(2) == 0) {
				// fewer nulls
				for (int j = 0; j < bytes.Length; j++) {
					if (bytes[j] == 0) bytes[j] = 1;
				}
				if (bytes.Length > 0 && random.Next(2) == 0) bytes[random.Next(bytes.Length)] = 0;
			}
			inputs.Add(bytes);
		}

		foreach (byte[] bytes in inputs) {
			Assert.Equal(ReferenceDecoders.GetString(bytes), Util.GetString(bytes));
		}
	}

	[Fact]
	public void GetStringFromStructReadsTheGivenRange() {
		byte[] bytes = new byte[520];
		Util.Civ3Encoding.GetBytes("Café").CopyTo(bytes, 0);
		bytes.AsSpan(4, 256).Fill((byte)'x'); // no terminator within the first string
		Util.Civ3Encoding.GetBytes("second").CopyTo(bytes, 260);
		RACE_ERAS eras = MemoryMarshal.Read<RACE_ERAS>(bytes);
		Assert.Equal("Café" + new string('x', 256), eras.ForwardFilename);
		Assert.Equal("second", eras.ReverseFilename);

		RACE_City city = MemoryMarshal.Read<RACE_City>(Util.Civ3Encoding.GetBytes("Abcdefghijklmnopqrstuvwx"));
		Assert.Equal("Abcdefghijklmnopqrstuvwx", city.Name);
	}

	// Original Civ3File section lookups, over the original section scan
	private static bool ReferenceSectionExists(Civ3Section[] sections, string name) => sections.Any(s => s.Name == name);
	private static int ReferenceSectionOffset(Civ3Section[] sections, string name, int nth) {
		int n = 0;
		foreach (Civ3Section section in sections) {
			if (section.Name == name && ++n >= nth) {
				return section.Offset + name.Length;
			}
		}
		throw new ArgumentException();
	}

	private static byte[] RandomFileData(Random random, int length, string header) {
		byte[] data = new byte[length];
		random.NextBytes(data);
		// Plenty of runs of bytes in the 0x20-0x5A section name range
		string[] words = { "BLDG", "WCHR", "CITY", "GAME", "XBLDG", "AB", "TILEX", "  " };
		for (int i = 0; i < length / 20; i++) {
			string word = words[random.Next(words.Length)];
			int at = random.Next(Math.Max(1, length - word.Length));
			for (int j = 0; j < word.Length && at + j < length; j++) data[at + j] = (byte)word[j];
		}
		for (int j = 0; j < header.Length; j++) data[j] = (byte)header[j];
		return data;
	}

	[Fact]
	public void SectionLookupsMatchLinearScan() {
		Random random = new(2);
		for (int i = 0; i < 50; i++) {
			byte[] data = RandomFileData(random, random.Next(4, 3000), i % 2 == 0 ? "BICX" : "CIV3");
			Civ3File file = new(data);
			Civ3Section[] sections = file.Sections;
			foreach (string name in new[] { "BLDG", "WCHR", "CITY", "GAME", "TILE", "NONE", "AB" }) {
				Assert.Equal(ReferenceSectionExists(sections, name), file.SectionExists(name));
				for (int nth = -1; nth < 6; nth++) {
					int expected;
					try {
						expected = ReferenceSectionOffset(sections, name, nth);
					} catch (ArgumentException) {
						Assert.Throws<ArgumentException>(() => file.SectionOffset(name, nth));
						continue;
					}
					Assert.Equal(expected, file.SectionOffset(name, nth));
				}
			}
			Assert.False(file.SectionExists(null));
		}
	}

	[Fact]
	public void EmbeddedFileReadsLikeCopiedFile() {
		Random random = new(3);
		for (int i = 0; i < 20; i++) {
			byte[] data = RandomFileData(random, random.Next(40, 2000), "BICQ");
			byte[] container = new byte[data.Length + 100];
			random.NextBytes(container);
			int start = random.Next(100);
			data.CopyTo(container, start);

			Civ3File copied = new(data);
			Civ3File embedded = new(container, start, data.Length);

			Assert.Equal(copied.Length, embedded.Length);
			Assert.Equal(copied.IsBicFile, embedded.IsBicFile);
			Assert.Equal(copied.IsGameFile, embedded.IsGameFile);
			Assert.Equal(copied.Civ3Version.FileTypeName, embedded.Civ3Version.FileTypeName);
			Assert.Equal(copied.Civ3Version.MajorVersion, embedded.Civ3Version.MajorVersion);
			Assert.Equal(copied.Sections.Select(s => (s.Name, s.Offset)), embedded.Sections.Select(s => (s.Name, s.Offset)));
			for (int offset = -2; offset < data.Length + 10; offset += 7) {
				foreach (int length in new[] { 0, 1, 4, 30, 500 }) {
					if (offset >= 0 || length == 0) {
						Assert.Equal(copied.GetBytes(offset, length), embedded.GetBytes(offset, length));
						Assert.Equal(copied.GetString(offset, length), embedded.GetString(offset, length));
					}
				}
				if (offset >= 0 && offset + 4 <= data.Length) {
					Assert.Equal(copied.ReadInt32(offset), embedded.ReadInt32(offset));
					Assert.Equal(copied.ReadInt16(offset), embedded.ReadInt16(offset));
					Assert.Equal(copied.ReadByte(offset), embedded.ReadByte(offset));
				}
			}
			// Reads past the end of the embedded data must fail rather than read the container
			Assert.ThrowsAny<Exception>(() => embedded.ReadInt32(data.Length - 2));
			Assert.ThrowsAny<Exception>(() => embedded.ReadByte(data.Length));
		}
		Assert.Throws<InvalidDataException>(() => new Civ3File(new byte[] { 0x42, 0x49, 0x43 }));
		Assert.Throws<InvalidDataException>(() => new Civ3File(new byte[10], 8, 2));
	}

	private static string TempFile(string extension) => Path.Combine(Path.GetTempPath(), "c7-perf-parsers-" + Guid.NewGuid() + extension);

	[Fact]
	public void ReadFileCachesDecompressedBiqsSafely() {
		Random random = new(4);
		BlastTestEncoder encoder = new();
		byte[] expected = encoder.RandomStream(random, false, 4, 50_000);
		string path = TempFile(".biq");
		try {
			File.WriteAllBytes(path, encoder.ToArray());

			byte[] first = Util.ReadFile(path);
			Assert.Equal(expected, first);
			// Callers own the returned array; changing it must not affect later reads
			Array.Clear(first);
			byte[] second = Util.ReadFile(path);
			Assert.Equal(expected, second);
			Assert.NotSame(first, second);

			// A changed file is decompressed again
			encoder = new BlastTestEncoder();
			byte[] changed = encoder.RandomStream(random, false, 6, 1000);
			File.WriteAllBytes(path, encoder.ToArray());
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
			Assert.Equal(changed, Util.ReadFile(path));

			// Uncompressed files are returned as they are
			byte[] plain = Enumerable.Range(0, 100).Select(i => (byte)(i + 1)).ToArray();
			File.WriteAllBytes(path, plain);
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(2));
			Assert.Equal(plain, Util.ReadFile(path));
		} finally {
			File.Delete(path);
		}
	}

	private static byte[] Bytes<T>(T[] array) where T : unmanaged => array == null ? null : MemoryMarshal.AsBytes(array.AsSpan()).ToArray();

	private static void AssertSameBiqData(BiqData expected, BiqData actual) {
		Assert.Equal(expected.Title, actual.Title);
		Assert.Equal(expected.Description, actual.Description);
		Assert.Equal(Bytes(expected.Bldg), Bytes(actual.Bldg));
		Assert.Equal(Bytes(expected.Race), Bytes(actual.Race));
		Assert.Equal(Bytes(expected.Rule), Bytes(actual.Rule));
		Assert.Equal(Bytes(expected.Prto), Bytes(actual.Prto));
		Assert.Equal(Bytes(expected.Tech), Bytes(actual.Tech));
		Assert.Equal(Bytes(expected.Terr), Bytes(actual.Terr));
		Assert.Equal(Bytes(expected.Govt), Bytes(actual.Govt));
		Assert.Equal(Bytes(expected.Good), Bytes(actual.Good));
		Assert.Equal(Bytes(expected.Tile), Bytes(actual.Tile));
		Assert.Equal(expected.Bldg.Select(b => b.Name), actual.Bldg.Select(b => b.Name));
		Assert.Equal(expected.RaceCityName.SelectMany(r => r.Select(c => c.Name)), actual.RaceCityName.SelectMany(r => r.Select(c => c.Name)));
		Assert.Equal(expected.HasCustomRules, actual.HasCustomRules);
		Assert.Equal(expected.HasCustomMap, actual.HasCustomMap);
	}

	[SkippableFact]
	public void EmbeddedBiqParsesLikeCopiedBiq() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		byte[] biq = Util.ReadFile(PathUtils.defaultBicPath);
		byte[] container = new byte[biq.Length + 1000];
		biq.CopyTo(container, 562);

		AssertSameBiqData(new BiqData(biq), new BiqData(container, 562, biq.Length));
		// and through the cache
		AssertSameBiqData(new BiqData(biq), BiqData.LoadFile(PathUtils.defaultBicPath));
	}
}
