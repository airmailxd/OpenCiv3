using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConvertCiv3Media;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.PerfParsers;

public class MediaDecoderTests {
	private static string TempFile(string extension) => Path.Combine(Path.GetTempPath(), "c7-perf-parsers-" + Guid.NewGuid() + extension);

	private static byte[] Flatten(byte[,] array) {
		byte[] result = new byte[array.Length];
		Buffer.BlockCopy(array, 0, result, 0, result.Length);
		return result;
	}

	// Decodes the file both ways, and checks they produce the same pixels, or fail with the same exception
	private static void AssertPcxMatchesReference(byte[] file) {
		string path = TempFile(".pcx");
		try {
			File.WriteAllBytes(path, file);
			byte[] expected;
			int width, height;
			byte[,] palette;
			try {
				expected = ReferenceDecoders.DecodePcx(file, out width, out height, out palette);
			} catch (Exception e) {
				Exception actual = Assert.ThrowsAny<Exception>(() => new Pcx(path));
				Assert.Equal(e.GetType(), actual.GetType());
				return;
			}
			Pcx pcx = new(path);
			Assert.Equal(width, pcx.Width);
			Assert.Equal(height, pcx.Height);
			Assert.Equal(Flatten(palette), Flatten(pcx.Palette));
			Assert.Equal(expected, pcx.ColorIndices);
		} finally {
			File.Delete(path);
		}
	}

	// A PCX file with random run-length-encoded data. Runs may cross line ends, as they do in some files.
	private static byte[] RandomPcx(Random random, int width, int height, int bytesPerLine, int extraEncodedBytes = 0) {
		byte[] header = new byte[0x80];
		BitConverter.GetBytes((short)5).CopyTo(header, 4); // left
		BitConverter.GetBytes((short)7).CopyTo(header, 6); // top
		BitConverter.GetBytes((short)(5 + width - 1)).CopyTo(header, 8);
		BitConverter.GetBytes((short)(7 + height - 1)).CopyTo(header, 10);
		BitConverter.GetBytes((short)bytesPerLine).CopyTo(header, 0x42);
		List<byte> file = new(header);

		int encodedLength = bytesPerLine * height + extraEncodedBytes;
		int written = 0;
		while (written < encodedLength) {
			if (random.Next(3) == 0) {
				int run = Math.Min(random.Next(0, 64), Math.Max(1, encodedLength - written));
				file.Add((byte)(0xc0 | run));
				file.Add((byte)random.Next(256));
				written += run;
			} else {
				file.Add((byte)random.Next(0xc0)); // literal
				written++;
			}
		}
		byte[] palette = new byte[768];
		random.NextBytes(palette);
		file.AddRange(palette);
		return file.ToArray();
	}

	[Fact]
	public void PcxDecodesLikeReference() {
		Random random = new(5);
		for (int i = 0; i < 200; i++) {
			int width = random.Next(1, 70);
			int height = random.Next(1, 30);
			// Even widths, odd widths with a junk byte, and padding the original only partly skips
			int bytesPerLine = random.Next(4) switch {
				0 => width,
				1 => width + 1,
				2 => width + 2 + random.Next(3),
				_ => width + (width & 1),
			};
			AssertPcxMatchesReference(RandomPcx(random, width, height, bytesPerLine));
			// Runs past the end of the image (the original throws for these)
			AssertPcxMatchesReference(RandomPcx(random, width, height, bytesPerLine, random.Next(1, 100)));
		}
	}

	[SkippableFact]
	public void Civ3PcxFilesDecodeLikeReference() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string art = Path.Combine(Civ3Location.GetCiv3Path(), "Art");
		foreach (string file in Directory.EnumerateFiles(art, "*.pcx", SearchOption.AllDirectories).OrderBy(f => f).Where((f, i) => i % 40 == 0)) {
			AssertPcxMatchesReference(File.ReadAllBytes(file));
		}
	}

	// Builds a Civ3 style FLC: Civ3 header fields, a palette in the first frame, a run-length-encoded or uncompressed
	// first frame per animation and mostly delta frames after that, and a ring frame after each animation. Like Civ3
	// files, palette chunks may have no valid length, and frames may claim an extra, uninitialized, subchunk.
	private class FlicBuilder {
		private readonly Random random;
		private readonly int width, height;
		private readonly List<byte> file;

		public FlicBuilder(Random random, int width, int height, int animations, int framesPerAnimation, bool civ3Header) {
			this.random = random;
			this.width = width;
			this.height = height;
			byte[] header = new byte[128];
			BitConverter.GetBytes((ushort)0xaf12).CopyTo(header, 4);
			BitConverter.GetBytes((ushort)(animations * framesPerAnimation)).CopyTo(header, 6);
			BitConverter.GetBytes((ushort)width).CopyTo(header, 8);
			BitConverter.GetBytes((ushort)height).CopyTo(header, 10);
			BitConverter.GetBytes(66).CopyTo(header, 16);
			BitConverter.GetBytes(128).CopyTo(header, 80);
			if (civ3Header) {
				BitConverter.GetBytes((ushort)animations).CopyTo(header, 0x60);
				BitConverter.GetBytes((ushort)framesPerAnimation).CopyTo(header, 0x62);
			}
			BitConverter.GetBytes((ushort)3).CopyTo(header, 100);
			BitConverter.GetBytes((ushort)4).CopyTo(header, 102);
			BitConverter.GetBytes((ushort)240).CopyTo(header, 104);
			BitConverter.GetBytes((ushort)241).CopyTo(header, 106);
			BitConverter.GetBytes((ushort)500).CopyTo(header, 108);
			file = new List<byte>(header);

			for (int anim = 0; anim < animations; anim++) {
				for (int f = 0; f < framesPerAnimation; f++) {
					List<byte[]> subChunks = new();
					int kind = random.Next(12);
					if (f == 0 || kind < 2) {
						subChunks.Add(random.Next(3) == 0 ? Copy() : ByteRun());
					} else if (kind == 2) {
						// no image data: the same as the previous frame
					} else if (kind == 3) {
						subChunks.Add(SubChunk(13, new List<byte>())); // black
					} else {
						subChunks.Add(Delta());
					}
					if (random.Next(10) == 0) {
						subChunks.Add(SubChunk(18, new List<byte>(new byte[20]))); // postage stamp, ignored
					}
					bool garbage = random.Next(6) == 0;
					if (anim == 0 && f == 0) {
						// The palette comes last, as its length may be invalid
						subChunks.Add(Palette(garbageLength: random.Next(2) == 0));
					} else if (garbage) {
						subChunks.Add(new byte[] { 0xcd, 0xcd, 0xcd, 0xcd, 0xcd, 0xcd });
					}
					AddChunk(subChunks);
				}
				AddChunk(new List<byte[]>()); // ring frame
			}
		}

		public byte[] ToArray() => file.ToArray();

		private void AddChunk(List<byte[]> subChunks) {
			int length = 16 + subChunks.Sum(s => s.Length);
			file.AddRange(BitConverter.GetBytes(length));
			file.AddRange(BitConverter.GetBytes((ushort)0xf1fa));
			file.AddRange(BitConverter.GetBytes((ushort)subChunks.Count));
			file.AddRange(new byte[8]);
			foreach (byte[] subChunk in subChunks) file.AddRange(subChunk);
		}

		private static byte[] SubChunk(int type, List<byte> data) {
			List<byte> result = new();
			result.AddRange(BitConverter.GetBytes(6 + data.Count));
			result.AddRange(BitConverter.GetBytes((ushort)type));
			result.AddRange(data);
			return result.ToArray();
		}

		private byte[] Palette(bool garbageLength) {
			List<byte> data = new();
			if (random.Next(2) == 0) {
				data.AddRange(new byte[] { 1, 0, 0, 0 }); // one packet, no skip, copy count 0 (= 256)
				byte[] colors = new byte[768];
				random.NextBytes(colors);
				data.AddRange(colors);
			} else {
				// packets skipping and setting parts of the palette
				List<byte> packets = new();
				int count = 0;
				for (int color = 0; color < 250 && count < 5; count++) {
					int skip = random.Next(0, 20);
					int copy = random.Next(1, Math.Min(40, 256 - color - skip) + 1);
					packets.Add((byte)skip);
					packets.Add((byte)copy);
					byte[] colors = new byte[copy * 3];
					random.NextBytes(colors);
					packets.AddRange(colors);
					color += skip + copy;
				}
				data.AddRange(BitConverter.GetBytes((ushort)count));
				data.AddRange(packets);
			}
			byte[] chunk = SubChunk(4, data);
			if (garbageLength) {
				new byte[] { 0xcd, 0xcd, 0xcd, 0xcd }.CopyTo(chunk, 0);
			}
			return chunk;
		}

		private byte[] Copy() {
			byte[] pixels = new byte[width * height];
			random.NextBytes(pixels);
			return SubChunk(16, new List<byte>(pixels));
		}

		private byte[] ByteRun() {
			List<byte> data = new();
			for (int y = 0; y < height; y++) {
				data.Add(0); // obsolete packet count
				int x = 0;
				while (x < width) {
					// Runs may overshoot the end of the row except on the last row
					int maxRun = y == height - 1 ? width - x : Math.Min(127, width - x + 3);
					int count = random.Next(1, Math.Min(127, maxRun) + 1);
					if (random.Next(2) == 0) {
						data.Add((byte)(sbyte)-count);
						for (int i = 0; i < count; i++) data.Add((byte)random.Next(256));
					} else {
						data.Add((byte)count);
						data.Add((byte)random.Next(256));
					}
					x += count;
				}
			}
			return SubChunk(15, data);
		}

		private byte[] Delta() {
			List<byte> data = new();
			List<byte> lines = new();
			int lineCount = 0;
			for (int y = 0; y < height; y++) {
				if (random.Next(4) == 0 && y < height - 1) {
					// skip some lines
					int skip = random.Next(1, Math.Min(3, height - 1 - y) + 1);
					lines.AddRange(BitConverter.GetBytes((short)-skip));
					y += skip;
				}
				if (random.Next(5) == 0) {
					// set the last pixel of the line
					lines.AddRange(BitConverter.GetBytes(unchecked((short)(0x8000 | random.Next(256)))));
				}
				int packets = random.Next(0, 4);
				lines.AddRange(BitConverter.GetBytes((short)packets));
				int x = 0;
				for (int p = 0; p < packets; p++) {
					int skip = random.Next(0, Math.Max(1, Math.Min(255, width - x - 2)));
					x += skip;
					int maxWords = Math.Max(0, Math.Min(127, (width - x) / 2));
					int words = random.Next(-maxWords, maxWords + 1);
					lines.Add((byte)skip);
					lines.Add((byte)(sbyte)words);
					if (words > 0) {
						for (int i = 0; i < words * 2; i++) lines.Add((byte)random.Next(256));
						x += words * 2;
					} else {
						lines.Add((byte)random.Next(256));
						lines.Add((byte)random.Next(256));
						x += -words * 2;
					}
				}
				lineCount++;
			}
			data.AddRange(BitConverter.GetBytes((ushort)lineCount));
			data.AddRange(lines);
			return SubChunk(7, data);
		}
	}

	private static void AssertFlicMatchesReference(byte[] file) {
		string path = TempFile(".flc");
		try {
			File.WriteAllBytes(path, file);
			byte[,][] expected;
			byte[,] palette;
			try {
				expected = ReferenceDecoders.DecodeFlic(file, out palette);
			} catch (Exception e) {
				Exception actual = Assert.ThrowsAny<Exception>(() => new Flic(path));
				Assert.Equal(e.GetType(), actual.GetType());
				Assert.Equal(e.Message, actual.Message);
				return;
			}
			Flic flic = new(path);
			Assert.Equal(expected.GetLength(0), flic.Images.GetLength(0));
			Assert.Equal(expected.GetLength(1), flic.Images.GetLength(1));
			for (int anim = 0; anim < expected.GetLength(0); anim++) {
				for (int f = 0; f < expected.GetLength(1); f++) {
					Assert.Equal(expected[anim, f], flic.Images[anim, f]);
				}
			}
			Assert.Equal(Flatten(palette), Flatten(flic.Palette));

			FlicHeader header = Flic.ReadHeader(path);
			Assert.Equal(flic.Width, header.Width);
			Assert.Equal(flic.Height, header.Height);
			Assert.Equal(flic.NumAnimations, header.NumAnimations);
			Assert.Equal(flic.FramesPerAnimation, header.FramesPerAnimation);
			Assert.Equal(flic.AnimationSpeed, header.AnimationSpeed);
			Assert.Equal(flic.AnimationTime, header.AnimationTime);
			Assert.Equal(flic.OffsetLeft, header.OffsetLeft);
			Assert.Equal(flic.OffsetTop, header.OffsetTop);
			Assert.Equal(flic.OriginalWidth, header.OriginalWidth);
			Assert.Equal(flic.OriginalHeight, header.OriginalHeight);
		} finally {
			File.Delete(path);
		}
	}

	[Fact]
	public void FlicDecodesLikeReference() {
		Random random = new(6);
		for (int i = 0; i < 60; i++) {
			int width = random.Next(4, 90);
			int height = random.Next(1, 40);
			bool civ3Header = i % 5 != 0;
			AssertFlicMatchesReference(new FlicBuilder(random, width, height, civ3Header ? random.Next(1, 4) : 1, random.Next(1, 6), civ3Header).ToArray());
		}
	}

	[SkippableFact]
	public void Civ3FlicFilesDecodeLikeReference() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string art = Path.Combine(Civ3Location.GetCiv3Path(), "Art");
		foreach (string file in Directory.EnumerateFiles(art, "*.flc", SearchOption.AllDirectories).OrderBy(f => f).Where((f, i) => i % 60 == 0)) {
			AssertFlicMatchesReference(File.ReadAllBytes(file));
		}
	}

	// Files that were decoded wrongly: delta frames setting the last pixel of a line (which shifted the lines below it),
	// uncompressed (FLI_COPY) frames, palettes with several packets, and frames without image data
	private static readonly string[] FormerlyMisdecodedFlics = {
		"Art/Flics/dip_f_0.flc",
		"Art/Flics/dip_m_2.flc",
		"civ3PTW/Scenarios/Ancient Mediterranean/Art/Units/Fire Galley/FireGalleyAttackA.flc",
		"Art/Units/Tactical Nuke/Tact_AttackA.flc",
		"Conquests/Art/Flics/X2_William_Mid_fwrd.flc",
		"Conquests/Conquests/Sengoku/Art/Flics/oda.flc",
		"Art/Flics/Gh_01.flc",
		"civ3PTW/Art/Flics/x_Hannibal diplo mid fwrd.flc",
		"Art/Flics/Ce_01.flc",
	};

	[SkippableFact]
	public void FormerlyMisdecodedCiv3FlicsDecodeCorrectly() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		foreach (string name in FormerlyMisdecodedFlics) {
			string path = Path.Combine(Civ3Location.GetCiv3Path(), name);
			if (!File.Exists(path)) continue;
			byte[] file = File.ReadAllBytes(path);
			AssertFlicMatchesReference(file);

			Flic flic = new(path);
			byte[,][] images = ReferenceDecoders.DecodeFlic(file, out _, out byte[][] ringFrames);
			for (int anim = 0; anim < flic.NumAnimations; anim++) {
				// No frame is left blank
				for (int f = 0; f < flic.FramesPerAnimation; f++) {
					Assert.True(flic.Images[anim, f].Any(b => b != 0), $"{name}: frame {anim},{f} is blank");
				}
				// Each animation's ring frame turns its last frame back into the first, which only works out if every
				// delta frame was decoded correctly. (That isn't exact for a few animations in 25 of the 1760 Civ3 Flics,
				// including some of the Fire Galley's.)
				if (!name.Contains("Fire Galley"))
				Assert.True(ringFrames[anim].AsSpan().SequenceEqual(flic.Images[anim, 0]), $"{name}: animation {anim} doesn't loop back to its first frame");
			}
		}
	}

	[Fact]
	public void MalformedFlicsFailCleanly() {
		Random random = new(9);
		byte[] valid = new FlicBuilder(random, 20, 10, 2, 3, true).ToArray();
		string path = TempFile(".flc");
		try {
			for (int length = 0; length < valid.Length; length += 1 + length / 8) {
				File.WriteAllBytes(path, valid.AsSpan(0, length).ToArray());
				Exception e = Record.Exception(() => new Flic(path));
				Assert.True(e is InvalidDataException || e is IndexOutOfRangeException || e is ApplicationException, $"length {length}: {e}");
				e = Record.Exception(() => Flic.ReadHeader(path));
				Assert.True(e == null || length >= FlicHeader.Size || e is InvalidDataException, $"header, length {length}: {e}");
			}
		} finally {
			File.Delete(path);
		}
	}

	[SkippableFact]
	public void Civ3AmbFilesParse() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string path = Path.Combine(Civ3Location.GetCiv3Path(), "Art", "Units", "Worker", "WorkerRun.amb");
		Skip.IfNot(File.Exists(path), "No WorkerRun.amb");
		AmbData amb = new(path);
		Assert.Equal(amb.midiData.trackCount, amb.midiData.soundTracks.Length);
		Assert.All(amb.kmapChunks, k => Assert.All(k.items, item => Assert.EndsWith(".wav", item.wavFileName, StringComparison.OrdinalIgnoreCase)));
		Assert.NotEmpty(new Amb(path).soundEffects);
	}
}
