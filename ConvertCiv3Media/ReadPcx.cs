using System;
using System.IO;

namespace ConvertCiv3Media {
	public class Pcx {

		public byte[,] Palette = new byte[256, 3];
		public byte[] ColorIndices;
		public int Width = 0;
		public int Height = 0;

		// constructors
		public Pcx() { }
		public Pcx(string path) {
			this.Load(path);
		}

		public byte ColorIndexAt(int x, int y) {
			int pixel = y * Width + x;
			return ColorIndices[pixel];
		}

		// not a generalized pcx reader
		// assumes 8-bit image with 256-color 8-bit rgb palette
		public void Load(string path) {
			byte[] PcxBytes = File.ReadAllBytes(path);

			// Read info from PCX header
			int LeftMargin = BitConverter.ToInt16(PcxBytes, 4);
			int TopMargin = BitConverter.ToInt16(PcxBytes, 6);
			int RightMargin = BitConverter.ToInt16(PcxBytes, 8);
			int BottomMargin = BitConverter.ToInt16(PcxBytes, 10);
			// assuming 1 color plane
			// this is always even, so last byte may be junk if image width is odd
			int BytesPerLine = BitConverter.ToInt16(PcxBytes, 0x42);

			this.Width = RightMargin - LeftMargin + 1;
			this.Height = BottomMargin - TopMargin + 1;
			// Palette is 256*3 bytes at end of file
			int PaletteOffset = PcxBytes.Length - 768;

			// Populate color palette
			Buffer.BlockCopy(PcxBytes, PaletteOffset, Palette, 0, 768);

			int ImageLength = Width * Height;
			byte[] Pixels = ColorIndices = new byte[ImageLength];

			// Encoding always have even number of bytes per line; if image width is odd, there is a junk byte in every row
			bool JunkByte = BytesPerLine > Width;
			// Column within the encoded line; only needed (and only tracked) when there is a junk byte to skip
			int LineCol = 0;

			// Loop to decode run-length-encoded image data which begins at file offset 0x80
			for (int ImgIdx = 0, PcxIdx = 0x80; ImgIdx < ImageLength;) {
				byte Code = PcxBytes[PcxIdx];
				// if two most significant bits are 11
				if ((Code & 0xc0) == 0xc0) {
					// then it & 0x3f is the run length of the following byte
					int RunLen = Code & 0x3f;
					PcxIdx++;
					if (!JunkByte) {
						ImgIdx = FillRun(Pixels, ImgIdx, RunLen, PcxBytes, PcxIdx);
					} else {
						// Repeat the pixel in the image RunLen times, except where the run covers the junk byte at the end of a line
						while (RunLen > 0) {
							int Segment = Math.Min(RunLen, BytesPerLine - LineCol);
							bool EndsLine = LineCol + Segment == BytesPerLine;
							ImgIdx = FillRun(Pixels, ImgIdx, EndsLine ? Segment - 1 : Segment, PcxBytes, PcxIdx);
							LineCol = EndsLine ? 0 : LineCol + Segment;
							RunLen -= Segment;
						}
					}
					PcxIdx++;
				} else {
					// Literal pixel. Copy it together with any literals following it, up to the end of the image,
					// the end of the data, or the junk byte at the end of the line.
					int MaxLiterals = Math.Min(ImageLength - ImgIdx, PcxBytes.Length - PcxIdx);
					if (JunkByte) {
						MaxLiterals = Math.Min(MaxLiterals, BytesPerLine - 1 - LineCol);
					}
					if (MaxLiterals > 0) {
						ReadOnlySpan<byte> Literals = new ReadOnlySpan<byte>(PcxBytes, PcxIdx, MaxLiterals);
						int Count = Literals.IndexOfAnyInRange((byte)0xc0, (byte)0xff);
						if (Count < 0) {
							Count = MaxLiterals;
						}
						Literals.Slice(0, Count).CopyTo(Pixels.AsSpan(ImgIdx));
						ImgIdx += Count;
						PcxIdx += Count;
						if (JunkByte) {
							LineCol += Count;
						}
					} else {
						// The junk byte at the end of the line, which isn't part of the image
						LineCol = 0;
						PcxIdx++;
					}
				}
			}
		}

		// Write Count copies of PcxBytes[PcxIdx] at Pixels[ImgIdx], returning the new image index. Like writing them
		// one at a time, this throws IndexOutOfRangeException if they don't all fit (after writing the ones that do).
		private static int FillRun(byte[] Pixels, int ImgIdx, int Count, byte[] PcxBytes, int PcxIdx) {
			if (Count <= 0) {
				return ImgIdx;
			}
			byte Value = PcxBytes[PcxIdx];
			int Fits = Math.Min(Count, Pixels.Length - ImgIdx);
			Pixels.AsSpan(ImgIdx, Fits).Fill(Value);
			if (Fits < Count) {
				throw new IndexOutOfRangeException();
			}
			return ImgIdx + Count;
		}
	}
}
