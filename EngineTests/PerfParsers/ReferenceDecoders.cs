using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Blast;

namespace EngineTests.PerfParsers;

// Verbatim (apart from naming and plumbing) copies of the original, unoptimized decoders. The optimized
// implementations must produce exactly the same output, so the tests compare against these. Where the original
// decoded something wrongly, the copy has the same correction as the real decoder, as noted on it.
public static class ReferenceDecoders {
	// Original QueryCiv3.Util.GetString
	public static string GetString(byte[] bytes) {
		string Out = QueryCiv3.Util.Civ3Encoding.GetString(bytes);
		Regex TrimAfterNull = new Regex(@"^[^\0]*");
		Match NoNullMatch = TrimAfterNull.Match(Out);
		return NoNullMatch.Value;
	}

	// Original ConvertCiv3Media.Pcx.Load, returning the color indices
	public static byte[] DecodePcx(byte[] PcxBytes, out int Width, out int Height, out byte[,] Palette) {
		int LeftMargin = BitConverter.ToInt16(PcxBytes, 4);
		int TopMargin = BitConverter.ToInt16(PcxBytes, 6);
		int RightMargin = BitConverter.ToInt16(PcxBytes, 8);
		int BottomMargin = BitConverter.ToInt16(PcxBytes, 10);
		int BytesPerLine = BitConverter.ToInt16(PcxBytes, 0x42);

		Width = RightMargin - LeftMargin + 1;
		Height = BottomMargin - TopMargin + 1;
		int PaletteOffset = PcxBytes.Length - 768;

		Palette = new byte[256, 3];
		Buffer.BlockCopy(PcxBytes, PaletteOffset, Palette, 0, 768);

		byte[] ColorIndices = new byte[Width * Height];

		bool JunkByte = BytesPerLine > Width;

		for (int ImgIdx = 0, PcxIdx = 0x80, RunLen = 0, LineIdx = 0; ImgIdx < Width * Height;) {
			if ((PcxBytes[PcxIdx] & 0xc0) == 0xc0) {
				RunLen = PcxBytes[PcxIdx] & 0x3f;
				PcxIdx++;
				for (int j = 0; j < RunLen; j++) {
					if (!(JunkByte && LineIdx % BytesPerLine == BytesPerLine - 1)) {
						ColorIndices[ImgIdx] = PcxBytes[PcxIdx];
						ImgIdx++;
					}
					LineIdx++;
				}
				PcxIdx++;
			} else {
				if (!(JunkByte && LineIdx % BytesPerLine == BytesPerLine - 1)) {
					ColorIndices[ImgIdx] = PcxBytes[PcxIdx];
					ImgIdx++;
				}
				PcxIdx++;
				LineIdx++;
			}
		}
		return ColorIndices;
	}

	// The original ConvertCiv3Media.Flic.Load, with these corrections:
	// - DELTA_FLC opcodes are told apart by their top two bits (0xC000 skip, 0x8000 last pixel), not 0xC00/0x800,
	//   and a line may have any number of them
	// - FLI_COPY (16) and BLACK (13) chunks are decoded, and palette chunks may have any number of skip/copy packets
	// - every frame after the first of an animation starts as a copy of the previous one, so frames without image data
	//   repeat it
	// - a subchunk whose length doesn't fit in its frame ends the frame; a palette chunk is still read from the rest of it
	// Each animation's ring frame, which turns its last frame back into its first, is decoded into ringFrames.
	public static byte[,][] DecodeFlic(byte[] FlicBytes, out byte[,] Palette) => DecodeFlic(FlicBytes, out Palette, out _);

	public static byte[,][] DecodeFlic(byte[] FlicBytes, out byte[,] Palette, out byte[][] ringFrames) {
		Palette = new byte[256, 3];
		int FileFormat = BitConverter.ToUInt16(FlicBytes, 4);
		if (FileFormat != 0xaf12) {
			throw new ApplicationException("Flic version # " + FileFormat.ToString("X4") + "does not match 0xaf12");
		}

		int NumFrames = BitConverter.ToUInt16(FlicBytes, 6);
		int Width = BitConverter.ToUInt16(FlicBytes, 8);
		int Height = BitConverter.ToUInt16(FlicBytes, 10);

		int NumAnimations = BitConverter.ToUInt16(FlicBytes, 0x60);
		int FramesPerAnimation = BitConverter.ToUInt16(FlicBytes, 0x62);
		if (NumAnimations == 0) {
			NumAnimations = 1;
			FramesPerAnimation = NumFrames;
		}

		byte[,][] Images = new byte[NumAnimations, FramesPerAnimation][];
		for (int i = 0; i < NumAnimations; i++) {
			for (int j = 0; j < FramesPerAnimation; j++) {
				Images[i, j] = new byte[Width * Height];
			}
		}
		ringFrames = new byte[NumAnimations][];

		int Offset = BitConverter.ToInt32(FlicBytes, 80);

		for (int anim = 0; anim < NumAnimations; anim++) {
			for (int f = 0; f <= FramesPerAnimation; f++) {
				byte[] image;
				if (f < FramesPerAnimation) {
					image = Images[anim, f];
				} else {
					image = new byte[Width * Height];
					ringFrames[anim] = image;
				}
				if (f > 0) {
					Array.Copy(Images[anim, f - 1], image, image.Length);
				}
				int ChunkLength = BitConverter.ToInt32(FlicBytes, Offset);
				int NumSubChunks = BitConverter.ToUInt16(FlicBytes, Offset + 6);
				int ChunkEnd = Offset + ChunkLength;

				for (int i = 0, SubOffset = Offset + 16; i < NumSubChunks; i++) {
					if (ChunkEnd - SubOffset < 6) {
						break;
					}
					int SubChunkLength = BitConverter.ToInt32(FlicBytes, SubOffset);
					int SubChunkType = BitConverter.ToUInt16(FlicBytes, SubOffset + 4);
					bool validLength = SubChunkLength >= 6 && SubChunkLength <= ChunkEnd - SubOffset;
					if (!validLength && SubChunkType != 4) {
						break;
					}
					switch (SubChunkType) {
						case 4:
							// (the palette of a ring frame isn't used)
							if (f == FramesPerAnimation) break;
							int NumPackets = BitConverter.ToUInt16(FlicBytes, SubOffset + 6);
							for (int packet = 0, color = 0, head = SubOffset + 8; packet < NumPackets; packet++) {
								color += FlicBytes[head];
								int CopyCount = FlicBytes[head + 1];
								head += 2;
								if (CopyCount == 0) CopyCount = 256;
								for (int c = 0; c < CopyCount; c++, color++) {
									Palette[color, 0] = FlicBytes[head++];
									Palette[color, 1] = FlicBytes[head++];
									Palette[color, 2] = FlicBytes[head++];
								}
							}
							break;
						case 13:
							Array.Clear(image);
							break;
						case 15:
							for (int y = 0, x = 0, head = SubOffset + 6; y < Height; y++, x = 0) {
								head++;
								for (; x < Width;) {
									int TypeSize = (sbyte)FlicBytes[head];
									if (TypeSize == 0) {
										throw new ApplicationException("TypeSize is 0");
									}
									head++;
									bool CopyMany = TypeSize < 0;
									for (int foo = 0; foo < Math.Abs(TypeSize); foo++) {
										image[y * Width + x] = FlicBytes[head];
										x++;
										if (CopyMany) {
											head++;
										}
									}
									if (!CopyMany) {
										head++;
									}
								}
							}
							break;
						case 16:
							for (int p = 0; p < Width * Height; p++) {
								image[p] = FlicBytes[SubOffset + 6 + p];
							}
							break;
						case 7:
							int NumLines = BitConverter.ToUInt16(FlicBytes, SubOffset + 6);
							for (int Line = 0, y = 0, head = SubOffset + 8; Line < NumLines; Line++) {
								int WordsPerLine = BitConverter.ToInt16(FlicBytes, head);
								head += 2;
								while ((WordsPerLine & 0xc000) != 0) {
									if ((WordsPerLine & 0xc000) == 0xc000) {
										y += Math.Abs(WordsPerLine);
									} else if ((WordsPerLine & 0xc000) == 0x8000) {
										image[Width * (y + 1) - 1] = (byte)(WordsPerLine & 0xff);
									} else {
										throw new ApplicationException("Undefined Flic delta opcode " + ((ushort)WordsPerLine).ToString("X4"));
									}
									WordsPerLine = BitConverter.ToInt16(FlicBytes, head);
									head += 2;
								}
								for (int packet = 0, x = 0; packet < WordsPerLine; packet++) {
									x += FlicBytes[head];
									head++;
									int NumWords = (sbyte)FlicBytes[head];
									bool Positive = NumWords > 0;
									head++;
									for (int ii = 0; ii < Math.Abs(NumWords); ii++) {
										image[Width * y + x] = FlicBytes[head];
										image[Width * y + x + 1] = FlicBytes[head + 1];
										if (Positive) { head += 2; }
										x += 2;
									}
									if (!Positive) { head += 2; }
								}
								y++;
							}
							break;
						default:
							break;
					}
					if (!validLength) {
						break;
					}
					SubOffset += SubChunkLength;
				}
				Offset += ChunkLength;
			}
		}
		return Images;
	}

	// Original Blast decoder (BlastDecoder, BitStream and InputBuffer), decoding bit by bit
	public class BlastDecoder {
		public const int MAX_WIN = 4096;
		private const int END_OF_STREAM = 519;
		private const int LITERAL_INDICATOR = 0;
		private static readonly short[] LENGTH_CODE_BASE = { 3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264 };
		private static readonly byte[] LENGTH_CODE_EXTRA = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

		private readonly InputBuffer _inputBuffer;
		private readonly BitStream _bitStream;
		private Stream _outputStream;
		private byte[] _outputBuffer = new byte[MAX_WIN * 2];
		private int _outputBufferPos = 0;

		public BlastDecoder(Stream inputStream, Stream outputStream) {
			this._inputBuffer = new InputBuffer(inputStream);
			this._bitStream = new BitStream(this._inputBuffer);
			this._outputStream = outputStream;
		}

		public static byte[] Decompress(byte[] compressedBytes) {
			MemoryStream DecompressedStream = new MemoryStream();
			BlastDecoder Decompressor = new BlastDecoder(new MemoryStream(compressedBytes, writable: false), DecompressedStream);
			Decompressor.Decompress();
			return DecompressedStream.ToArray();
		}

		public void Decompress() {
			do {
				DecompressStream();
			} while (_inputBuffer.IsInputRemaining());
		}

		private void DecompressStream() {
			bool codedLiteral = ReadCodedLiteralHeader();
			var readLiteral = codedLiteral ? (Func<byte>)ReadCodedLiteral : ReadUncodedLiteral;
			int dictSize = _bitStream.GetBits(8);
			if (dictSize < 4 || dictSize > 6) {
				throw new BlastException(BlastException.DictionarySizeMessage);
			}
			try {
				do {
					int nextCodeIndicator = _bitStream.GetBits(1);
					if (nextCodeIndicator == LITERAL_INDICATOR) {
						WriteBuffer(readLiteral());
					} else {
						var (length, distance) = ReadLengthDistance(dictSize);
						if (length == END_OF_STREAM) {
							break;
						}
						WriteWindowSegment(length, distance);
					}
				} while (true);
			} finally {
				FlushOutputBuffer();
				_bitStream.FlushBits();
			}
		}

		private byte ReadUncodedLiteral() {
			return (byte)_bitStream.GetBits(8);
		}
		private byte ReadCodedLiteral() {
			return (byte)Decode(HuffmanTable.LITERAL_CODE);
		}

		private bool ReadCodedLiteralHeader() {
			int codedLiteral = _bitStream.GetBits(8);
			if (codedLiteral > 1) {
				throw new BlastException(BlastException.LiteralFlagMessage);
			}
			return codedLiteral == 1;
		}

		private (int length, int distance) ReadLengthDistance(int dictSize) {
			int decodedLengthSymbol = Decode(HuffmanTable.LENGTH_CODE);
			int copyLength = LENGTH_CODE_BASE[decodedLengthSymbol] + _bitStream.GetBits(LENGTH_CODE_EXTRA[decodedLengthSymbol]);
			if (copyLength == END_OF_STREAM) {
				return (copyLength, 0);
			}
			int distanceAdditionalBits = copyLength == 2 ? 2 : dictSize;
			int copyDist = Decode(HuffmanTable.DISTANCE_CODE) << distanceAdditionalBits;
			copyDist += _bitStream.GetBits(distanceAdditionalBits);
			copyDist++;
			if (copyDist > _outputBufferPos) {
				throw new BlastException(BlastException.DistanceMessage);
			}
			return (copyLength, copyDist);
		}

		private void WriteWindowSegment(int copyLength, int copyDistance) {
			do {
				int fromIndex = _outputBufferPos - copyDistance;
				int copyCount = copyDistance;
				if (copyCount > copyLength) {
					copyCount = copyLength;
				}
				CopyBufferSection(fromIndex, copyCount);
				copyLength -= copyCount;
			} while (copyLength != 0);
		}

		private int Decode(HuffmanTable h) {
			int len = 1;
			int code = 0;
			int first = 0;
			int count;
			int index = 0;
			int bitbuf;
			int left;
			int next = 1;

			var bufferState = _bitStream.State;
			(bitbuf, left) = bufferState;

			while (true) {
				while (left-- > 0) {
					code |= (bitbuf & 1) ^ 1;
					bitbuf >>= 1;
					count = h.count[next++];
					if (code < first + count) {
						_bitStream.State = (bitbuf, ((bufferState.bufferCount - len) & 7));
						return h.symbol[index + (code - first)];
					}
					index += count;
					first += count;
					first <<= 1;
					code <<= 1;
					len++;
				}
				left = (HuffmanTable.MAX_BITS + 1) - len;
				if (left == 0)
					break;
				bitbuf = _inputBuffer.ConsumeByte();
				if (left > 8)
					left = 8;
			}
			return -9;
		}

		private void WriteBuffer(byte b) {
			EnsureBufferSpace(1);
			_outputBuffer[_outputBufferPos++] = b;
		}

		private void CopyBufferSection(int fromIndex, int copyCount) {
			fromIndex -= EnsureBufferSpace(copyCount);
			Buffer.BlockCopy(_outputBuffer, fromIndex, _outputBuffer, _outputBufferPos, copyCount);
			_outputBufferPos += copyCount;
		}

		private int EnsureBufferSpace(int required) {
			if (_outputBufferPos + required >= _outputBuffer.Length) {
				int startWindowOffset = _outputBufferPos - MAX_WIN;
				_outputStream.Write(_outputBuffer, 0, startWindowOffset);
				Buffer.BlockCopy(_outputBuffer, startWindowOffset, _outputBuffer, 0, MAX_WIN);
				_outputBufferPos = MAX_WIN;
				return startWindowOffset;
			}
			return 0;
		}

		private void FlushOutputBuffer() {
			if (_outputBufferPos > 0) {
				_outputStream.Write(_outputBuffer, 0, _outputBufferPos);
				_outputBufferPos = 0;
			}
		}

		private class BitStream {
			private readonly InputBuffer _inputBuffer;
			private int _bitBuffer = 0;
			private int _bitBufferCount = 0;

			public BitStream(InputBuffer inputBuffer) {
				_inputBuffer = inputBuffer;
			}

			public (int buffer, int bufferCount) State {
				get { return (_bitBuffer, _bitBufferCount); }
				set {
					_bitBuffer = value.buffer;
					_bitBufferCount = value.bufferCount;
				}
			}

			public int GetBits(int need) {
				int val = _bitBuffer;
				while (_bitBufferCount < need) {
					val |= ((int)_inputBuffer.ConsumeByte()) << _bitBufferCount;
					_bitBufferCount += 8;
				}
				_bitBuffer = val >> need;
				_bitBufferCount -= need;
				return val & ((1 << need) - 1);
			}

			public void FlushBits() {
				_bitBufferCount = 0;
			}
		}

		private class InputBuffer {
			private readonly Stream _inputStream;
			private readonly byte[] _inputBuffer = new byte[16384];
			private int _inputBufferPos = 0;
			private int _inputBufferRemaining = 0;

			public InputBuffer(Stream inputStream) {
				_inputStream = inputStream;
			}

			public byte ConsumeByte() {
				if (_inputBufferRemaining == 0) {
					DoReadBuffer();
					if (_inputBufferRemaining == 0) {
						throw new BlastException(BlastException.OutOfInputMessage);
					}
				}
				byte b = _inputBuffer[_inputBufferPos++];
				_inputBufferRemaining--;
				return b;
			}

			private void DoReadBuffer() {
				_inputBufferRemaining = _inputStream.Read(_inputBuffer, 0, _inputBuffer.Length);
				_inputBufferPos = 0;
			}

			public bool IsInputRemaining() {
				if (_inputBufferRemaining > 0) {
					return true;
				}
				DoReadBuffer();
				return _inputBufferRemaining > 0;
			}
		}
	}
}
