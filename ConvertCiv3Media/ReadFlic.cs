using System;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace ConvertCiv3Media {
	// Under construction
	// Not intended to be a generalized/universal Flic reader
	// [BROKEN LINK] Implementing from description at https://www.drdobbs.com/windows/the-flic-file-format/184408954
	//
	// The link above is broken at the time of writing this 28/10/2025
	// The link below probably is the same article as above, on another site
	// https://jacobfilipp.com/DrDobbs/articles/DDJ/1993/9303/9303a/9303a.htm#00af_0005
	public class Flic {
		private static ILogger log = Log.ForContext<Flic>();
		// Images is an array of animations,images, each of which is a byte array of palette indexes
		public byte[,][] Images;
		// All animations/images have same palette, height, and width
		// Palette is 256 colors in red, green, blue order
		public byte[,] Palette = new byte[256,3];
		public int OriginalWidth = 0;
		public int OriginalHeight = 0;
		public int Width = 0;
		public int Height = 0;
		public int OffsetLeft = 0;
		public int OffsetTop = 0;
		public int NumAnimations = 0;
		public int FramesPerAnimation = 0;
		public int AnimationTime = 0;
		public int AnimationSpeed = 0;

		private string path;

		// constructors
		public Flic() { }
		public Flic(string path) {
			this.path = path;
			this.Load(path);
		}

		// Frame subchunk types; see the FLC format description linked above
		private const int COLOR_256 = 4;
		private const int DELTA_FLC = 7;
		private const int BLACK = 13;
		private const int BYTE_RUN = 15;
		private const int FLI_COPY = 16;
		private const int PSTAMP = 18;
		private const int CHUNK_HEADER_SIZE = 16;
		private const int SUBCHUNK_HEADER_SIZE = 6;

		public void Load(string path) {
			byte[] FlicBytes = File.ReadAllBytes(path);

			FlicHeader header = FlicHeader.Parse(FlicBytes);

			this.Width = header.Width;
			this.Height = header.Height;

			this.AnimationSpeed = header.AnimationSpeed;

			this.OffsetLeft = header.OffsetLeft;
			this.OffsetTop = header.OffsetTop;

			this.OriginalWidth = header.OriginalWidth;
			this.OriginalHeight = header.OriginalHeight;

			this.AnimationTime = header.AnimationTime;

			this.NumAnimations = header.NumAnimations;
			this.FramesPerAnimation = header.FramesPerAnimation;

			int width = this.Width;
			int height = this.Height;
			// The header's counts and sizes are checked before anything is
			// allocated from them: a corrupt header could otherwise ask for
			// tens of gigabytes.
			int ImageLength = CheckedImageLength(header, FlicBytes.Length, path);

			// The frames themselves are allocated as they are decoded, so a
			// file that ends early fails before allocating all of them.
			this.Images = new byte[NumAnimations, this.FramesPerAnimation][];

			// technically should be UInt32 I think
			// frame 1 chunk offset
			int Offset = header.FirstFrameOffset;

			// Animations loop
			for (int anim = 0; anim < NumAnimations; anim++) {
				// Flic frames loop
				for (int f = 0; f < this.FramesPerAnimation; f++) {
					byte[] frame = new byte[ImageLength];
					this.Images[anim, f] = frame;
					// A frame only describes how it differs from the previous one: a delta changes some of its pixels, and a
					// frame without any image data (e.g. the second frame of some scenario leaderheads) is the same.
					// The first frame of each animation is drawn from scratch; in Civ3 files it is always a full frame.
					if (f > 0) {
						Array.Copy(this.Images[anim, f - 1], frame, frame.Length);
					}

					// Frame chunk headers should be 0xF1FA
					int ChunkLength = ReadInt32(FlicBytes, Offset);
					int NumSubChunks = ReadUInt16(FlicBytes, Offset + 6);
					if (ChunkLength < CHUNK_HEADER_SIZE || ChunkLength > FlicBytes.Length - Offset) {
						throw new InvalidDataException($"Flic frame chunk at {Offset} has an invalid length {ChunkLength}");
					}
					int ChunkEnd = Offset + ChunkLength;

					// Subchunk loop
					for (int i = 0, SubOffset = Offset + CHUNK_HEADER_SIZE; i < NumSubChunks; i++) {
						if (ChunkEnd - SubOffset < SUBCHUNK_HEADER_SIZE) {
							log.Debug("Ignoring the rest of the Flic frame at {offset} in {path}: it has fewer subchunks than it says", Offset, path);
							break;
						}
						int SubChunkLength = ReadInt32(FlicBytes, SubOffset);
						int SubChunkType = ReadUInt16(FlicBytes, SubOffset + 4);
						bool validLength = SubChunkLength >= SUBCHUNK_HEADER_SIZE && SubChunkLength <= ChunkEnd - SubOffset;
						if (!validLength) {
							// The palette chunks of Civ3 Flics don't have their length filled in (it is uninitialized memory,
							// 0xCDCDCDCD), and many unit Flics count one more subchunk than their frames have, which is also
							// uninitialized. A palette is decoded from the rest of the frame; either way, nothing in the frame
							// after the bad subchunk can be found.
							if (SubChunkType != COLOR_256) {
								log.Debug("Ignoring the rest of the Flic frame at {offset} in {path}: its subchunk at {subOffset} has an invalid length {length}", Offset, path, SubOffset, SubChunkLength);
								break;
							}
							SubChunkLength = ChunkEnd - SubOffset;
						}
						// The subchunk's data, which decoding never reads beyond
						ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(FlicBytes, SubOffset + SUBCHUNK_HEADER_SIZE, SubChunkLength - SUBCHUNK_HEADER_SIZE);
						switch (SubChunkType) {
							case COLOR_256:
								DecodeColor256(data, this.Palette);
								break;
							case BYTE_RUN:
								DecodeByteRun(data, frame, width, height);
								break;
							case DELTA_FLC:
								DecodeDeltaFlc(data, frame, width, height);
								break;
							case FLI_COPY:
								// The whole frame, uncompressed
								if (data.Length < ImageLength) {
									throw new InvalidDataException($"Flic FLI_COPY chunk has {data.Length} bytes, too few for a {width}x{height} frame");
								}
								data.Slice(0, ImageLength).CopyTo(frame);
								break;
							case BLACK:
								Array.Clear(frame);
								break;
							case PSTAMP:
								// A thumbnail of the animation, which isn't needed
								log.Debug("Skipping postage stamp chunk in Flic {path}", path);
								break;
							default:
								log.Warning("Flic subchunk type {type} not recognized in {path}", SubChunkType, path);
								break;
						}
						if (!validLength) {
							break;
						}
						SubOffset += SubChunkLength;
					}
					Offset = ChunkEnd;
				}
				// Skip the ring frame (which loops back to the animation's first
				// frame) between animations. Nothing is read after the last
				// animation, so files without a final ring frame (as leaderheads
				// may be) still load.
				if (anim + 1 < NumAnimations) {
					int RingChunkLength = ReadInt32(FlicBytes, Offset);
					if (RingChunkLength < CHUNK_HEADER_SIZE || RingChunkLength > FlicBytes.Length - Offset) {
						throw new InvalidDataException($"Flic ring frame chunk at {Offset} has an invalid length {RingChunkLength}");
					}
					Offset += RingChunkLength;
				}
			}
		}

		// The largest Flic frame and the most decoded pixels (over all frames)
		// that Load accepts. Civ3's largest Flics (leaderheads, 240x240 units)
		// are far below these.
		private const int MAX_DIMENSION = 4096;
		private const long MAX_TOTAL_PIXELS = 512L * 1024 * 1024;

		// Width * height, after checking that the header's frame counts and
		// sizes could belong to a file of fileLength bytes and that decoding
		// every frame takes a sane amount of memory.
		private static int CheckedImageLength(FlicHeader header, int fileLength, string path) {
			if (header.Width > MAX_DIMENSION || header.Height > MAX_DIMENSION) {
				throw new InvalidDataException($"Flic {path} is {header.Width}x{header.Height}, larger than the supported {MAX_DIMENSION}x{MAX_DIMENSION}");
			}
			long imageLength = (long)header.Width * header.Height;
			long frames = (long)header.NumAnimations * header.FramesPerAnimation;
			// Every frame has a chunk header in the file.
			if (frames * CHUNK_HEADER_SIZE > fileLength) {
				throw new InvalidDataException($"Flic {path} claims {header.NumAnimations} animations of {header.FramesPerAnimation} frames, too many for its {fileLength} bytes");
			}
			if (frames * imageLength > MAX_TOTAL_PIXELS) {
				throw new InvalidDataException($"Flic {path} would decode to {frames * imageLength} bytes of frames, more than the supported {MAX_TOTAL_PIXELS}");
			}
			return (int)imageLength;
		}

		private static int ReadInt32(byte[] bytes, int offset) {
			if (offset < 0 || offset > bytes.Length - 4) {
				throw new InvalidDataException($"Flic data at {offset} is past the end of the file");
			}
			return BitConverter.ToInt32(bytes, offset);
		}

		private static int ReadUInt16(byte[] bytes, int offset) {
			if (offset < 0 || offset > bytes.Length - 2) {
				throw new InvalidDataException($"Flic data at {offset} is past the end of the file");
			}
			return BitConverter.ToUInt16(bytes, offset);
		}

		private static int ReadUInt16(ReadOnlySpan<byte> data, ref int head) {
			if (head > data.Length - 2) {
				throw new IndexOutOfRangeException();
			}
			int value = data[head] | (data[head + 1] << 8);
			head += 2;
			return value;
		}

		// COLOR_256 palette chunk: a number of packets, each of which skips a number of colors and then sets a number of
		// them (0 meaning all 256) to the red, green, blue triplets that follow
		private static void DecodeColor256(ReadOnlySpan<byte> data, byte[,] palette) {
			int head = 0;
			int NumPackets = ReadUInt16(data, ref head);
			for (int packet = 0, color = 0; packet < NumPackets; packet++) {
				if (head > data.Length - 2) {
					throw new IndexOutOfRangeException();
				}
				color += data[head];
				int CopyCount = data[head + 1];
				head += 2;
				if (CopyCount == 0) {
					CopyCount = 256;
				}
				if (color + CopyCount > 256) {
					throw new InvalidDataException($"Flic palette chunk sets colors past the end of the palette ({color} + {CopyCount})");
				}
				if (head > data.Length - CopyCount * 3) {
					throw new IndexOutOfRangeException();
				}
				// red, green, blue triplets, in the same order as Palette[p, 0..2]
				data.Slice(head, CopyCount * 3).CopyTo(MemoryMarshal.CreateSpan(ref palette[color, 0], CopyCount * 3));
				head += CopyCount * 3;
				color += CopyCount;
			}
		}

		// BYTE_RUN run-length-encoded full frame
		private static void DecodeByteRun(ReadOnlySpan<byte> data, byte[] frame, int width, int height) {
			for (int y = 0, head = 0; y < height; y++) {
				// first byte of row is obsolete
				head++;
				int rowStart = y * width;
				for (int x = 0; x < width;) {
					int TypeSize = (sbyte)data[head];
					// TypeSize == 0 makes no sense, something is wrong
					if (TypeSize == 0) {
						throw new ApplicationException("TypeSize is 0");
					}
					head++;
					// If TypeSize is negative, copy abs(TypeSize) following bytes
					// If TypeSize is positive, repeat the next byte TypeSize times
					if (TypeSize < 0) {
						int count = -TypeSize;
						CopyBytes(data, head, frame, rowStart + x, count);
						head += count;
						x += count;
					} else {
						FillBytes(frame, rowStart + x, TypeSize, data[head]);
						x += TypeSize;
						// We were repeating a byte and are still pointing at it; advance head
						head++;
					}
				}
			}
		}

		// DELTA_FLC (word oriented delta) frame: the number of lines that have packets, then for each of those lines a
		// sequence of opcode words, told apart by their top two bits:
		//   11: skip -opcode lines
		//   10: set the last pixel of the line to the low byte (for odd widths)
		//   00: the number of packets in the line, which follow; this ends the line
		private static void DecodeDeltaFlc(ReadOnlySpan<byte> data, byte[] frame, int width, int height) {
			int head = 0;
			int NumLines = ReadUInt16(data, ref head);
			for (int Line = 0, y = 0; Line < NumLines; Line++) {
				int PacketCount;
				while (true) {
					int opcode = ReadUInt16(data, ref head);
					if ((opcode & 0xc000) == 0xc000) {
						y -= (short)opcode;
					} else if ((opcode & 0xc000) == 0x8000) {
						if (y >= height) {
							throw new IndexOutOfRangeException();
						}
						frame[width * (y + 1) - 1] = (byte)(opcode & 0xff);
					} else if ((opcode & 0xc000) == 0x4000) {
						throw new ApplicationException("Undefined Flic delta opcode " + opcode.ToString("X4"));
					} else {
						PacketCount = opcode;
						break;
					}
				}
				int rowStart = width * y;
				// Loop over the packets for this line
				for (int packet = 0, x = 0; packet < PacketCount; packet++) {
					if (head > data.Length - 2) {
						throw new IndexOutOfRangeException();
					}
					// least significant byte of word (first byte) is columns to skip
					x += data[head];
					// most significant byte of word (second byte) is number of words in the packet
					int NumWords = (sbyte)data[head + 1];
					head += 2;
					if (NumWords > 0) {
						// If NumWords is positive, copy NumWords following words to image
						int count = NumWords * 2;
						CopyBytes(data, head, frame, rowStart + x, count);
						head += count;
						x += count;
					} else {
						// If NumWords is negative, repeat the next word abs(NumWords) times
						// (a zero count still skips over the word)
						int count = -NumWords;
						if (head > data.Length - 2) {
							throw new IndexOutOfRangeException();
						}
						if (count > 0) {
							FillWords(frame, rowStart + x, count, data[head], data[head + 1]);
							x += count * 2;
						}
						head += 2;
					}
				}
				y++;
			}
		}

		private static void CopyBytes(ReadOnlySpan<byte> source, int sourceIndex, byte[] destination, int destinationIndex, int count) {
			if (sourceIndex < 0 || (long)sourceIndex + count > source.Length) {
				throw new IndexOutOfRangeException();
			}
			CheckRange(destination, destinationIndex, count);
			source.Slice(sourceIndex, count).CopyTo(destination.AsSpan(destinationIndex, count));
		}

		// Bounds checks for the block operations below. Like the per-byte indexing they replace, these throw
		// IndexOutOfRangeException for data that runs past the end of the file or frame.
		private static void CheckRange(byte[] array, int start, int count) {
			if (start < 0 || (long)start + count > array.Length) {
				throw new IndexOutOfRangeException();
			}
		}

		private static void FillBytes(byte[] destination, int destinationIndex, int count, byte value) {
			CheckRange(destination, destinationIndex, count);
			destination.AsSpan(destinationIndex, count).Fill(value);
		}

		private static void FillWords(byte[] destination, int destinationIndex, int count, byte low, byte high) {
			CheckRange(destination, destinationIndex, count * 2);
			Span<byte> words = destination.AsSpan(destinationIndex, count * 2);
			if (low == high) {
				words.Fill(low);
			} else if (BitConverter.IsLittleEndian) {
				MemoryMarshal.Cast<byte, ushort>(words).Fill((ushort)(low | (high << 8)));
			} else {
				for (int i = 0; i < words.Length; i += 2) {
					words[i] = low;
					words[i + 1] = high;
				}
			}
		}

		/// <summary>
		/// Reads just the header of a Flic file (dimensions, frame counts, speed and the offset of the first frame)
		/// without reading or decoding any frames. The values match the corresponding fields of a <see cref="Flic"/>
		/// loaded from the same file, and it fails in the same way for a bad header: InvalidDataException if the file
		/// is too short to contain one, ApplicationException if it isn't a Flic file.
		/// </summary>
		// NOTE: C7's Util.LoadFlicHeader duplicates this (with a cache); it could call this instead.
		public static FlicHeader ReadHeader(string path) {
			byte[] headerBytes = new byte[FlicHeader.Size];
			int read = 0;
			using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1)) {
				read = stream.ReadAtLeast(headerBytes, headerBytes.Length, throwOnEndOfStream: false);
			}
			// A short file leaves headerBytes short too, so it is rejected just like Load rejects it
			return FlicHeader.Parse(read < headerBytes.Length ? headerBytes.AsSpan(0, read).ToArray() : headerBytes);
		}

		public override string ToString() {
			return "FLIC " + this.path;
		}
	}

	/// <summary>
	/// The header fields of a (Civ3) Flic file, see <see cref="Flic.ReadHeader"/>.
	/// </summary>
	public struct FlicHeader {
		// Number of bytes at the start of the file that the header fields are read from
		public const int Size = 110;

		// Number of frames according to the standard Flic header
		public int NumFrames;
		public int Width;
		public int Height;
		public int AnimationSpeed;
		// Offset of the first frame chunk in the file
		public int FirstFrameOffset;
		public int OffsetLeft;
		public int OffsetTop;
		public int OriginalWidth;
		public int OriginalHeight;
		public int AnimationTime;
		// Civ3-specific values. Leaderheads don't have them, in which case these describe a regular Flic:
		// a single animation of NumFrames frames
		public int NumAnimations;
		public int FramesPerAnimation;

		/// <summary>
		/// Parses the header at the start of FlicBytes, which must have at least <see cref="Size"/> bytes.
		/// Throws InvalidDataException if there are fewer, and ApplicationException if it isn't a Flic file.
		/// </summary>
		public static FlicHeader Parse(byte[] FlicBytes) {
			ArgumentNullException.ThrowIfNull(FlicBytes);
			if (FlicBytes.Length < Size) {
				throw new InvalidDataException($"Flic file is too short ({FlicBytes.Length} bytes) to contain a header");
			}
			int FileFormat = BitConverter.ToUInt16(FlicBytes, 4);
			// Should be 0xAF12
			if (FileFormat != 0xaf12) {
				throw new ApplicationException("Flic version # " + FileFormat.ToString("X4") + "does not match 0xaf12");
			}

			FlicHeader header = new FlicHeader() {
				NumFrames = BitConverter.ToUInt16(FlicBytes, 6),
				Width = BitConverter.ToUInt16(FlicBytes, 8),
				Height = BitConverter.ToUInt16(FlicBytes, 10),
				AnimationSpeed = BitConverter.ToInt32(FlicBytes, 16),
				OffsetLeft = BitConverter.ToUInt16(FlicBytes, 100),
				OffsetTop = BitConverter.ToUInt16(FlicBytes, 102),
				// Disclaimer! I don't know if the width & height order is correct here
				// but since the game always assumes a 240x240 size, maybe it doesn't matter
				OriginalWidth = BitConverter.ToUInt16(FlicBytes, 104),
				OriginalHeight = BitConverter.ToUInt16(FlicBytes, 106),
				AnimationTime = BitConverter.ToUInt16(FlicBytes, 108),
				// Civ3-specific values
				NumAnimations = BitConverter.ToUInt16(FlicBytes, 0x60),
				// but every animation has a ring frame, so there are this many frames plus one for each
				FramesPerAnimation = BitConverter.ToUInt16(FlicBytes, 0x62),
				// technically should be UInt32 I think
				FirstFrameOffset = BitConverter.ToInt32(FlicBytes, 80),
			};
			// Leaderheads don't have the above values, so revert to act like a regular Flic.
			// (Flic.Load only skips ring frames between animations, so a single
			// animation never needs one.)
			if (header.NumAnimations == 0) {
				header.NumAnimations = 1;
				header.FramesPerAnimation = header.NumFrames;
			}
			return header;
		}
	}
}
