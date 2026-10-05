using System;
using System.Collections.Generic;
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

		// Decompressed contents of recently read compressed BIQ files (e.g. the default conquests.biq, which is read
		// on every import), so they are only decompressed once. Entries are keyed by full path and invalidated when
		// the file's length or last write time changes. Callers always get their own copy of the bytes.
		private class DecompressedFile {
			public long Length;
			public DateTime LastWriteTimeUtc;
			public byte[] Data;
		}
		private const int MAX_CACHED_FILES = 4;
		private static readonly Dictionary<string, DecompressedFile> DecompressedFileCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly LinkedList<string> DecompressedFileCacheOrder = new(); // most recently used first

		public static byte[] ReadFile(string pathName) {
			bool cacheable = IsCacheableFile(pathName);
			string cacheKey = null;
			FileInfo fileInfo = null;

			if (cacheable) {
				fileInfo = new FileInfo(pathName);
				cacheKey = fileInfo.FullName;
				lock (DecompressedFileCache) {
					if (DecompressedFileCache.TryGetValue(cacheKey, out DecompressedFile cached)) {
						if (fileInfo.Exists && cached.Length == fileInfo.Length && cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc) {
							DecompressedFileCacheOrder.Remove(cacheKey);
							DecompressedFileCacheOrder.AddFirst(cacheKey);
							return (byte[])cached.Data.Clone();
						}
						DecompressedFileCache.Remove(cacheKey);
						DecompressedFileCacheOrder.Remove(cacheKey);
					}
				}
			}

			byte[] MyFileData = File.ReadAllBytes(pathName);
			if (MyFileData[0] == 0x00 && (MyFileData[1] == 0x04 || MyFileData[1] == 0x05 || MyFileData[1] == 0x06)) {
				byte[] decompressed = Decompress(MyFileData);
				if (cacheable && fileInfo.Exists) {
					AddToCache(cacheKey, new DecompressedFile() {
						// Stamp taken before reading the file, so if it changed in between the next read sees a mismatch
						Length = fileInfo.Length,
						LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
						Data = (byte[])decompressed.Clone(),
					});
				}
				return decompressed;
			}
			return MyFileData;
		}

		private static bool IsCacheableFile(string pathName) {
			string extension = Path.GetExtension(pathName);
			return extension.Equals(".biq", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".bic", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".bix", StringComparison.OrdinalIgnoreCase);
		}

		private static void AddToCache(string cacheKey, DecompressedFile entry) {
			lock (DecompressedFileCache) {
				if (DecompressedFileCache.ContainsKey(cacheKey)) {
					DecompressedFileCacheOrder.Remove(cacheKey);
				}
				DecompressedFileCache[cacheKey] = entry;
				DecompressedFileCacheOrder.AddFirst(cacheKey);
				while (DecompressedFileCacheOrder.Count > MAX_CACHED_FILES) {
					DecompressedFileCache.Remove(DecompressedFileCacheOrder.Last.Value);
					DecompressedFileCacheOrder.RemoveLast();
				}
			}
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
