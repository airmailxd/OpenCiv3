using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blast;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.PerfParsers;

// Builds PKWare DCL ("blast") compressed streams for tests, using the same Huffman codes the decoder uses
public class BlastTestEncoder {
	private static readonly short[] LENGTH_CODE_BASE = { 3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264 };
	private static readonly byte[] LENGTH_CODE_EXTRA = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };
	private static readonly (int bits, int length)[] LiteralCodes = Codes(HuffmanTable.LITERAL_CODE, 256);
	private static readonly (int bits, int length)[] LengthCodes = Codes(HuffmanTable.LENGTH_CODE, 16);
	private static readonly (int bits, int length)[] DistanceCodes = Codes(HuffmanTable.DISTANCE_CODE, 64);

	// The stream bits (first bit in bit 0) and length of each symbol's code
	private static (int bits, int length)[] Codes(HuffmanTable table, int symbolCount) {
		var codes = new (int bits, int length)[symbolCount];
		for (int bits = 0; bits < table.lookup.Length; bits++) {
			int entry = table.lookup[bits];
			int length = entry & 0xf;
			if (length > 0 && bits < (1 << length)) {
				codes[entry >> 4] = (bits, length);
			}
		}
		return codes;
	}

	private readonly List<byte> output = new();
	private ulong bitBuffer;
	private int bitCount;
	private bool codedLiterals;
	private int dictSize;

	public byte[] ToArray() => output.ToArray();

	private void WriteBits(int value, int count) {
		bitBuffer |= (ulong)(uint)value << bitCount;
		bitCount += count;
		while (bitCount >= 8) {
			output.Add((byte)bitBuffer);
			bitBuffer >>= 8;
			bitCount -= 8;
		}
	}

	private void WriteCode((int bits, int length) code) => WriteBits(code.bits, code.length);

	public void BeginStream(bool codedLiterals, int dictSize) {
		this.codedLiterals = codedLiterals;
		this.dictSize = dictSize;
		WriteBits(codedLiterals ? 1 : 0, 8);
		WriteBits(dictSize, 8);
	}

	public void Literal(byte value) {
		WriteBits(0, 1);
		if (codedLiterals) {
			WriteCode(LiteralCodes[value]);
		} else {
			WriteBits(value, 8);
		}
	}

	private void Length(int length) {
		WriteBits(1, 1);
		for (int symbol = 0; symbol < 16; symbol++) {
			int extra = length - LENGTH_CODE_BASE[symbol];
			if (extra >= 0 && extra < (1 << LENGTH_CODE_EXTRA[symbol])) {
				WriteCode(LengthCodes[symbol]);
				WriteBits(extra, LENGTH_CODE_EXTRA[symbol]);
				return;
			}
		}
		throw new ArgumentOutOfRangeException(nameof(length));
	}

	public static int MaxDistance(int length, int dictSize) => 64 << (length == 2 ? 2 : dictSize);

	public void Match(int length, int distance) {
		Length(length);
		int extraBits = length == 2 ? 2 : dictSize;
		int d = distance - 1;
		WriteCode(DistanceCodes[d >> extraBits]);
		WriteBits(d & ((1 << extraBits) - 1), extraBits);
	}

	public void EndStream() {
		Length(519);
		if (bitCount > 0) {
			WriteBits(0, 8 - bitCount);
		}
	}

	// Writes a random stream with literals, short and long matches, overlapping copies and single byte runs,
	// returning the data it decompresses to
	public byte[] RandomStream(Random random, bool codedLiterals, int dictSize, int targetLength) {
		BeginStream(codedLiterals, dictSize);
		List<byte> data = new();
		while (data.Count < targetLength) {
			int choice = random.Next(10);
			if (data.Count == 0 || choice < 4) {
				// mostly a small alphabet, like real data
				byte value = random.Next(4) == 0 ? (byte)random.Next(256) : (byte)(0x40 + random.Next(8));
				Literal(value);
				data.Add(value);
				continue;
			}
			int length = choice switch {
				4 or 5 => random.Next(2, 10),
				6 => random.Next(2, 64),
				7 => random.Next(64, 519),
				_ => random.Next(2, 519),
			};
			int maxDistance = Math.Min(MaxDistance(length, dictSize), data.Count);
			int distance = random.Next(4) switch {
				0 => 1, // run of a single byte
				1 => random.Next(1, Math.Min(maxDistance, Math.Max(1, length - 1)) + 1), // overlapping copy
				_ => random.Next(1, maxDistance + 1),
			};
			Match(length, distance);
			int from = data.Count - distance;
			for (int i = 0; i < length; i++) {
				data.Add(data[from + i]);
			}
		}
		EndStream();
		return data.ToArray();
	}
}

// Stream that hands out at most a few bytes per Read call, to exercise input buffer refills
public class TrickleStream : MemoryStream {
	private readonly int maxRead;
	public TrickleStream(byte[] data, int maxRead) : base(data, writable: false) { this.maxRead = maxRead; }
	public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, maxRead));
}

public class BlastDecoderTests {
	private static byte[] DecompressWithStreams(byte[] compressed, Stream input = null) {
		MemoryStream output = new();
		new BlastDecoder(input ?? new MemoryStream(compressed, writable: false), output).Decompress();
		return output.ToArray();
	}

	// Runs a decode, returning its output or the message of the BlastException it throws, plus whatever it wrote
	// to the output stream before failing
	private static string Outcome(Action<MemoryStream> decode) {
		MemoryStream output = new();
		try {
			decode(output);
			return "ok " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(output.ToArray()));
		} catch (BlastException e) {
			return "error " + e.Message + " after " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(output.ToArray()));
		}
	}

	private static string OutcomeOfArrayDecode(byte[] compressed) {
		try {
			return "ok " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(BlastDecoder.DecompressBytes(compressed)));
		} catch (BlastException e) {
			return "error " + e.Message;
		}
	}

	[Fact]
	public void LookupTablesMatchBitByBitDecoding() {
		foreach (HuffmanTable table in new[] { HuffmanTable.LITERAL_CODE, HuffmanTable.LENGTH_CODE, HuffmanTable.DISTANCE_CODE }) {
			for (int bits = 0; bits < table.lookup.Length; bits++) {
				int symbol = table.DecodeBitByBit(bits, out int length);
				// The PKWare code tables are complete, so every bit pattern decodes to some symbol
				Assert.True(symbol >= 0);
				Assert.Equal((symbol << 4) | length, table.lookup[bits]);
			}
		}
	}

	[Theory]
	[InlineData(false, 4)]
	[InlineData(false, 5)]
	[InlineData(false, 6)]
	[InlineData(true, 4)]
	[InlineData(true, 5)]
	[InlineData(true, 6)]
	public void RandomStreamsDecodeLikeReference(bool codedLiterals, int dictSize) {
		Random random = new(dictSize * 2 + (codedLiterals ? 1 : 0));
		foreach (int targetLength in new[] { 0, 1, 100, 5000, 70_000, 600_000 }) {
			BlastTestEncoder encoder = new();
			byte[] expected = encoder.RandomStream(random, codedLiterals, dictSize, targetLength);
			byte[] compressed = encoder.ToArray();

			Assert.Equal(expected, ReferenceDecoders.BlastDecoder.Decompress(compressed));
			Assert.Equal(expected, BlastDecoder.DecompressBytes(compressed));
			Assert.Equal(expected, Util.Decompress(compressed));
			Assert.Equal(expected, DecompressWithStreams(compressed));
			Assert.Equal(expected, DecompressWithStreams(compressed, new TrickleStream(compressed, 3)));

			// Decompressing part of a larger array
			byte[] padded = new byte[compressed.Length + 10];
			compressed.CopyTo(padded, 7);
			Assert.Equal(expected, BlastDecoder.DecompressBytes(padded, 7, compressed.Length));
		}
	}

	[Fact]
	public void BytesAfterTheEndOfTheStreamAreIgnored() {
		Random random = new(42);
		BlastTestEncoder encoder = new();
		byte[] expected = encoder.RandomStream(random, true, 6, 300_000);
		byte[] single = encoder.ToArray();
		// Another stream, and garbage, after the first are ignored, as blast.c does
		encoder.RandomStream(random, false, 4, 10);
		foreach (byte[] trailing in new[] { encoder.ToArray().Skip(single.Length).ToArray(), new byte[] { 0 }, new byte[] { 0, 4 }, new byte[] { 7, 7, 7 } }) {
			byte[] compressed = single.Concat(trailing).ToArray();
			Assert.Equal(expected, ReferenceDecoders.BlastDecoder.Decompress(compressed));
			Assert.Equal(expected, BlastDecoder.DecompressBytes(compressed));
			Assert.Equal(expected, DecompressWithStreams(compressed, new TrickleStream(compressed, 5)));
		}
	}

	[Fact]
	public void OutputCanBeLimited() {
		Random random = new(43);
		BlastTestEncoder encoder = new();
		byte[] expected = encoder.RandomStream(random, true, 5, 100_000);
		byte[] compressed = encoder.ToArray();
		Assert.Equal(expected, BlastDecoder.DecompressBytes(compressed, 0, compressed.Length, expected.Length));
		BlastException e = Assert.Throws<BlastException>(() => BlastDecoder.DecompressBytes(compressed, 0, compressed.Length, expected.Length - 1));
		Assert.Equal(BlastException.OutputTooLargeMessage, e.Message);
		Assert.Throws<BlastException>(() => BlastDecoder.DecompressBytes(compressed, 0, compressed.Length, 0));
	}

	[Fact]
	public void StreamsMustNotBeNull() {
		Assert.Throws<ArgumentNullException>(() => new BlastDecoder(null, new MemoryStream()));
		Assert.Throws<ArgumentNullException>(() => new BlastDecoder(new MemoryStream(), null));
		Assert.Throws<ArgumentNullException>(() => BlastDecoder.DecompressBytes(null));
	}

	// Throws an IOException on the first write, and something else after that
	private class FailingStream : MemoryStream {
		private int writes;
		public override void Write(byte[] buffer, int offset, int count) {
			if (writes++ == 0) throw new IOException("first write");
			throw new InvalidOperationException("later write");
		}
	}

	[Fact]
	public void OutputErrorsAreNotMasked() {
		Random random = new(44);
		BlastTestEncoder encoder = new();
		encoder.RandomStream(random, true, 5, 600_000); // more than the output buffer holds
		byte[] compressed = encoder.ToArray();
		IOException e = Assert.Throws<IOException>(() => new BlastDecoder(new MemoryStream(compressed), new FailingStream()).Decompress());
		Assert.Equal("first write", e.Message);

		// An error in the data isn't hidden by one writing the output decoded before it
		byte[] truncated = compressed.AsSpan(0, 1000).ToArray();
		BlastException blastException = Assert.Throws<BlastException>(() => new BlastDecoder(new MemoryStream(truncated), new FailingStream()).Decompress());
		Assert.Equal(BlastException.OutOfInputMessage, blastException.Message);
	}

	[Fact]
	public void LongRunsDecodeLikeReference() {
		BlastTestEncoder encoder = new();
		encoder.BeginStream(false, 6);
		List<byte> expected = new();
		encoder.Literal(7);
		expected.Add(7);
		for (int i = 0; i < 2000; i++) {
			encoder.Match(518, 1);
			expected.AddRange(Enumerable.Repeat((byte)7, 518));
		}
		encoder.Literal(1);
		encoder.Literal(2);
		encoder.Literal(3);
		expected.AddRange(new byte[] { 1, 2, 3 });
		for (int i = 0; i < 1000; i++) {
			encoder.Match(517, 3);
			for (int j = 0; j < 517; j++) {
				expected.Add(expected[expected.Count - 3]);
			}
		}
		encoder.EndStream();
		byte[] compressed = encoder.ToArray();

		Assert.Equal(expected.ToArray(), ReferenceDecoders.BlastDecoder.Decompress(compressed));
		Assert.Equal(expected.ToArray(), BlastDecoder.DecompressBytes(compressed));
		Assert.Equal(expected.ToArray(), DecompressWithStreams(compressed));
	}

	[Fact]
	public void MalformedInputFailsLikeReference() {
		Random random = new(7);
		List<byte[]> inputs = new() {
			new byte[0],
			new byte[] { 2, 4 }, // bad literal flag
			new byte[] { 0, 7 }, // bad dictionary size
			new byte[] { 1 },
		};

		// Truncated streams
		BlastTestEncoder encoder = new();
		encoder.RandomStream(random, true, 5, 20_000);
		byte[] compressed = encoder.ToArray();
		for (int length = 0; length < compressed.Length; length += 1 + length / 4) {
			inputs.Add(compressed.AsSpan(0, length).ToArray());
		}

		// A match before any output, and one reaching back before the start of the second stream
		encoder = new BlastTestEncoder();
		encoder.BeginStream(false, 4);
		encoder.Match(5, 1);
		encoder.EndStream();
		inputs.Add(encoder.ToArray());
		encoder = new BlastTestEncoder();
		encoder.RandomStream(random, false, 4, 100);
		encoder.BeginStream(false, 4);
		encoder.Literal(1);
		encoder.Match(5, 2);
		encoder.EndStream();
		inputs.Add(encoder.ToArray());

		// Random garbage after a valid header
		for (int i = 0; i < 200; i++) {
			byte[] garbage = new byte[random.Next(3, 400)];
			random.NextBytes(garbage);
			garbage[0] = (byte)random.Next(2);
			garbage[1] = (byte)random.Next(4, 7);
			inputs.Add(garbage);
		}

		foreach (byte[] input in inputs) {
			string expected = Outcome(output => new ReferenceDecoders.BlastDecoder(new MemoryStream(input), output).Decompress());
			Assert.Equal(expected, Outcome(output => new BlastDecoder(new MemoryStream(input), output).Decompress()));
			Assert.Equal(expected, Outcome(output => new BlastDecoder(new TrickleStream(input, 2), output).Decompress()));
			Assert.Equal(expected.Split(" after ")[0], OutcomeOfArrayDecode(input));
		}
	}

	[SkippableFact]
	public void Civ3FilesDecompressLikeReference() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		string conquests = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests");
		List<string> files = new() { Path.Combine(conquests, "conquests.biq") };
		files.AddRange(Civ3TestData.EnumerateFiles(Path.Combine(conquests, "Saves"), "*.sav").Take(8));

		int compressedFiles = 0;
		foreach (string file in files) {
			byte[] compressed = File.ReadAllBytes(file);
			if (compressed.Length < 2 || compressed[0] != 0 || compressed[1] < 4 || compressed[1] > 6) {
				continue; // not compressed
			}
			++compressedFiles;
			Assert.Equal(ReferenceDecoders.BlastDecoder.Decompress(compressed), Util.Decompress(compressed));
		}
		// Civ3 ships conquests.biq compressed, so something was compared.
		Assert.True(compressedFiles > 0, $"None of the {files.Count} Civ3 files checked was compressed");
	}
}
