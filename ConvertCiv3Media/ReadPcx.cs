using System;
using System.IO;

namespace ConvertCiv3Media {
	public class Pcx {

		public byte[,] Palette = new byte[256, 3];
		public byte[] ColorIndices;
		public int Width = 0;
		public int Height = 0;

		private const int HEADER_SIZE = 0x80;
		// The 256 color palette at the end of the file, preceded by a 0x0C marker byte
		private const int PALETTE_SIZE = 768;
		private const byte PALETTE_MARKER = 0x0c;

		// constructors
		public Pcx() { }
		public Pcx(string path) {
			this.Load(path);
		}

		public byte ColorIndexAt(int x, int y) {
			int pixel = y * Width + x;
			return ColorIndices[pixel];
		}

		// Not a generalized pcx reader: only reads 8-bit images with a single color plane and a 256 color palette, like
		// Civ3's. Throws NotSupportedException for other kinds of PCX files and InvalidDataException for malformed ones.
		public void Load(string path) {
			byte[] PcxBytes = File.ReadAllBytes(path);

			if (PcxBytes.Length < HEADER_SIZE + 1 + PALETTE_SIZE || PcxBytes[0] != 0x0a || PcxBytes[2] != 1) {
				throw new InvalidDataException($"Not a run-length encoded PCX file: {path}");
			}
			// Read info from PCX header
			int BitsPerPixel = PcxBytes[3];
			int LeftMargin = BitConverter.ToInt16(PcxBytes, 4);
			int TopMargin = BitConverter.ToInt16(PcxBytes, 6);
			int RightMargin = BitConverter.ToInt16(PcxBytes, 8);
			int BottomMargin = BitConverter.ToInt16(PcxBytes, 10);
			int Planes = PcxBytes[0x41];
			// Each line is encoded as this many bytes, which is always even, so there are padding bytes at the end of
			// each line which aren't part of the image if the image width is odd
			int BytesPerLine = BitConverter.ToInt16(PcxBytes, 0x42);
			if (BitsPerPixel != 8 || Planes != 1) {
				throw new NotSupportedException($"Only 8-bit PCX files with a palette are supported, but this has {BitsPerPixel} bits per pixel in {Planes} planes: {path}");
			}

			int width = RightMargin - LeftMargin + 1;
			int height = BottomMargin - TopMargin + 1;
			if (width <= 0 || height <= 0 || BytesPerLine < width || (long)width * height > Array.MaxLength) {
				throw new InvalidDataException($"PCX file has an invalid size of {width}x{height} with {BytesPerLine} bytes per line: {path}");
			}
			// Palette is 256*3 bytes at end of file, after the marker
			int PaletteOffset = PcxBytes.Length - PALETTE_SIZE;
			if (PcxBytes[PaletteOffset - 1] != PALETTE_MARKER) {
				throw new InvalidDataException($"PCX file has no 256 color palette: {path}");
			}
			this.Width = width;
			this.Height = height;

			// Populate color palette
			Buffer.BlockCopy(PcxBytes, PaletteOffset, Palette, 0, PALETTE_SIZE);

			byte[] Pixels = ColorIndices = new byte[width * height];

			// The run-length encoded lines, padding included, are between the header and the palette. Runs can continue
			// from one line to the next. The padding of the last line isn't needed.
			int DataEnd = PaletteOffset - 1;
			long EncodedLength = (long)(height - 1) * BytesPerLine + width;
			long Position = 0; // in the decoded lines
			for (int PcxIdx = HEADER_SIZE; Position < EncodedLength;) {
				if (PcxIdx >= DataEnd) {
					throw new InvalidDataException($"PCX image data ends before the image does: {path}");
				}
				byte Code = PcxBytes[PcxIdx];
				// if two most significant bits are 11
				if ((Code & 0xc0) == 0xc0) {
					// then it & 0x3f is the run length of the following byte
					if (PcxIdx + 1 >= DataEnd) {
						throw new InvalidDataException($"PCX image data ends before the image does: {path}");
					}
					Position = Fill(Pixels, Position, Code & 0x3f, PcxBytes[PcxIdx + 1], width, BytesPerLine, EncodedLength);
					PcxIdx += 2;
					continue;
				}
				int Column = (int)(Position % BytesPerLine);
				if (Column >= width) {
					// A literal padding byte, which isn't part of the image
					PcxIdx++;
					Position++;
					continue;
				}
				// Literal pixel. Copy it together with any literals following it, up to the end of the line's pixels or
				// the end of the data.
				ReadOnlySpan<byte> Literals = new ReadOnlySpan<byte>(PcxBytes, PcxIdx, Math.Min(width - Column, DataEnd - PcxIdx));
				int Count = Literals.IndexOfAnyInRange((byte)0xc0, (byte)0xff);
				if (Count < 0) {
					Count = Literals.Length;
				}
				Literals.Slice(0, Count).CopyTo(Pixels.AsSpan((int)(Position / BytesPerLine) * width + Column));
				PcxIdx += Count;
				Position += Count;
			}
		}

		// Writes Count copies of Value at Position in the decoded lines, leaving out the padding at the end of each line
		// and anything past the end of the image, and returns the position after them
		private static long Fill(byte[] Pixels, long Position, int Count, byte Value, int Width, int BytesPerLine, long EncodedLength) {
			while (Count > 0 && Position < EncodedLength) {
				int Column = (int)(Position % BytesPerLine);
				int Segment = Math.Min(Count, BytesPerLine - Column);
				int Visible = Math.Clamp(Width - Column, 0, Segment);
				if (Visible > 0) {
					Pixels.AsSpan((int)(Position / BytesPerLine) * Width + Column, Visible).Fill(Value);
				}
				Position += Segment;
				Count -= Segment;
			}
			return Position;
		}
	}
}
