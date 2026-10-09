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
		// the file's length or last write time changes.
		private class DecompressedFile {
			public long Length;
			public DateTime LastWriteTimeUtc;
			public byte[] Data;
		}
		private const int MAX_CACHED_FILES = 4;
		// Files written more recently than this aren't cached: a file replaced again within the resolution of the file
		// system's timestamps could otherwise keep the same length and last write time
		private static readonly TimeSpan MIN_CACHEABLE_FILE_AGE = TimeSpan.FromSeconds(2);
		private static readonly Dictionary<string, DecompressedFile> DecompressedFileCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly LinkedList<string> DecompressedFileCacheOrder = new(); // most recently used first

		/// <summary>
		/// Reads a Civ3 file, decompressing it if it is compressed (as BIQ and SAV files can be).
		/// The caller owns the returned array.
		/// </summary>
		public static byte[] ReadFile(string pathName) {
			return ReadFile(pathName, shared: false);
		}

		/// <summary>
		/// Like <see cref="ReadFile(string)"/>, but a decompressed BIQ may be returned straight from the cache
		/// instead of being copied, so the result must not be modified.
		/// </summary>
		public static ReadOnlyMemory<byte> ReadFileReadOnly(string pathName) {
			return ReadFileShared(pathName);
		}

		// The array may be shared with the cache and other callers, so it must not be modified
		internal static byte[] ReadFileShared(string pathName) {
			return ReadFile(pathName, shared: true);
		}

		private static byte[] ReadFile(string pathName, bool shared) {
			bool cacheable = IsCacheableFile(pathName);
			string cacheKey = null;
			FileInfo fileInfo = null;
			bool existedBefore = false;
			long lengthBefore = 0;
			DateTime lastWriteTimeBefore = default;

			if (cacheable) {
				// FileInfo caches what it reads the first time a property is used, so read the stamp now, before reading the file
				fileInfo = new FileInfo(pathName);
				fileInfo.Refresh();
				cacheKey = fileInfo.FullName;
				existedBefore = fileInfo.Exists;
				if (existedBefore) {
					lengthBefore = fileInfo.Length;
					lastWriteTimeBefore = fileInfo.LastWriteTimeUtc;
				}
				lock (DecompressedFileCache) {
					if (DecompressedFileCache.TryGetValue(cacheKey, out DecompressedFile cached)) {
						if (existedBefore && cached.Length == lengthBefore && cached.LastWriteTimeUtc == lastWriteTimeBefore) {
							DecompressedFileCacheOrder.Remove(cacheKey);
							DecompressedFileCacheOrder.AddFirst(cacheKey);
							return shared ? cached.Data : (byte[])cached.Data.Clone();
						}
						DecompressedFileCache.Remove(cacheKey);
						DecompressedFileCacheOrder.Remove(cacheKey);
					}
				}
			}

			byte[] MyFileData = File.ReadAllBytes(pathName);
			if (MyFileData.Length < 2) {
				throw new InvalidDataException($"'{pathName}' is too short ({MyFileData.Length} bytes) to be a Civ3 file.");
			}
			if (MyFileData[0] == 0x00 && (MyFileData[1] == 0x04 || MyFileData[1] == 0x05 || MyFileData[1] == 0x06)) {
				byte[] decompressed = Decompress(MyFileData);
				if (cacheable && existedBefore && MyFileData.Length == lengthBefore
						&& DateTime.UtcNow - lastWriteTimeBefore >= MIN_CACHEABLE_FILE_AGE) {
					// Only cache what was read if the file didn't change while it was being read
					fileInfo.Refresh();
					if (fileInfo.Exists && fileInfo.Length == lengthBefore && fileInfo.LastWriteTimeUtc == lastWriteTimeBefore) {
						AddToCache(cacheKey, new DecompressedFile() {
							Length = lengthBefore,
							LastWriteTimeUtc = lastWriteTimeBefore,
							Data = shared ? decompressed : (byte[])decompressed.Clone(),
						});
					}
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

		// The most a compressed Civ3 file may decompress to. Real BIQ and SAV files decompress to a few megabytes; the
		// limit stops a corrupt or hostile file from exhausting memory (BlastDecoder's own default is Array.MaxLength).
		public const int MAX_DECOMPRESSED_SIZE = 256 * 1024 * 1024;

		// Throws BlastException if the data is malformed or decompresses to more than MAX_DECOMPRESSED_SIZE bytes.
		public static byte[] Decompress(byte[] compressedBytes) {
			ArgumentNullException.ThrowIfNull(compressedBytes);
			return BlastDecoder.DecompressBytes(compressedBytes, 0, compressedBytes.Length, MAX_DECOMPRESSED_SIZE);
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
