using System;
using System.IO;
using Blast;
using System.Text;

namespace QueryCiv3 {
	public struct ByteBitmap {
		private byte Flags;
		public bool this[int i] { get => ((Flags >> i) & 1) == 1; }
	}

	public struct IntBitmap {
		private int Flags;
		public bool this[int i] { get => ((Flags >> i) & 1) == 1; }
	}

	public class Util {
		// Encoding code page ID; 1252 is Civ3 encoding for US language version
		public static Encoding Civ3Encoding;

		static Util() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Civ3Encoding = Encoding.GetEncoding(1252);
		}

		public static byte[] ReadFile(string pathName) {
			byte[] MyFileData = File.ReadAllBytes(pathName);
			if (MyFileData[0] == 0x00 && (MyFileData[1] == 0x04 || MyFileData[1] == 0x05 || MyFileData[1] == 0x06)) {
				return Decompress(MyFileData);
			}
			return MyFileData;
		}

		public static byte[] Decompress(byte[] compressedBytes) {
			return BlastDecoder.DecompressBytes(compressedBytes);
		}

		// Decodes the bytes up to (not including) the first null byte. Civ3Encoding is a single-byte code page in which
		// only 0x00 decodes to '\0', so this matches decoding all the bytes and trimming at the first '\0'.
		public static string GetString(ReadOnlySpan<byte> bytes) {
			int nullIndex = bytes.IndexOf((byte)0);
			if (nullIndex >= 0) {
				bytes = bytes.Slice(0, nullIndex);
			}
			return Civ3Encoding.GetString(bytes);
		}

		public static string GetString(byte[] bytes) {
			ArgumentNullException.ThrowIfNull(bytes);
			return GetString(new ReadOnlySpan<byte>(bytes));
		}

		public static unsafe string GetString<T>(ref T structData, int start, int length) where T : unmanaged {
			fixed (void* dataPtr = &structData) {
				return GetString(new ReadOnlySpan<byte>(((byte*)dataPtr) + start, length));
			}
		}

		public static bool GetFlag(byte flags, int index) {
			return ((flags >> index) & 1) == 1;
		}
	}
}
