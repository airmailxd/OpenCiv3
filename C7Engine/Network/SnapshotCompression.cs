using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace C7Engine.Network;

// Compresses snapshots with zstd, whole or as a patch: the new snapshot
// compressed with the one before it as a prefix that matches can refer to,
// which is how zstd's --patch-from works. Most of a game is the same from
// one snapshot to the next, so a patch is usually a few hundred bytes where
// the whole game is tens or hundreds of kilobytes.
internal static class SnapshotCompression {
	// zstd's fastest level, which is still faster than gzip's fastest and
	// compresses a game a fifth smaller. Higher levels cost several times as
	// long for little more.
	private const int Level = 1;

	// The largest window zstd allows everywhere, which a patch's prefix and
	// the snapshot together have to fit in.
	private const int MaxWindowLog = 30;

	public static byte[] Compress(byte[] json) {
		using Compressor compressor = new(Level);
		return compressor.Wrap(json).ToArray();
	}

	// The snapshot compressed against the earlier one, or null if that
	// doesn't fit in maxBytes.
	public static unsafe byte[] CompressPatch(byte[] json, byte[] reference, int maxBytes) {
		long window = (long)json.Length + reference.Length;
		if (window > 1L << MaxWindowLog) {
			return null;
		}
		int windowLog = Math.Max(10, 64 - BitOperations.LeadingZeroCount((ulong)window - 1));
		byte[] patch = new byte[maxBytes];
		ZSTD_CCtx_s* context = Methods.ZSTD_createCCtx();
		try {
			Check(Methods.ZSTD_CCtx_setParameter(context, ZSTD_cParameter.ZSTD_c_compressionLevel, Level));
			Check(Methods.ZSTD_CCtx_setParameter(context, ZSTD_cParameter.ZSTD_c_windowLog, windowLog));
			// Long distance matching finds what's unchanged however far back
			// it is in the reference; without it, fast levels find little.
			Check(Methods.ZSTD_CCtx_setParameter(context, ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching, 1));
			nuint written;
			fixed (byte* src = json, prefix = reference, dst = patch) {
				Check(Methods.ZSTD_CCtx_refPrefix(context, prefix, (nuint)reference.Length));
				written = Methods.ZSTD_compress2(context, dst, (nuint)patch.Length, src, (nuint)json.Length);
			}
			if (Methods.ZSTD_isError(written)) {
				if (Methods.ZSTD_getErrorCode(written) == ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall) {
					return null;
				}
				Check(written);
			}
			return patch.AsSpan(0, (int)written).ToArray();
		} finally {
			Methods.ZSTD_freeCCtx(context);
		}
	}

	// Decompresses a snapshot, or a patch given the snapshot it was made
	// against. Anything that decompresses to more than maxBytes is treated as
	// broken, rather than read until memory runs out.
	public static byte[] Decompress(ReadOnlySpan<byte> compressed, int maxBytes, byte[] reference = null) {
		try {
			using Decompressor decompressor = new();
			decompressor.SetParameter(ZSTD_dParameter.ZSTD_d_windowLogMax, MaxWindowLog);
			if (reference != null) {
				// A dictionary that isn't one of zstd's own is taken as raw
				// content, just like a prefix.
				decompressor.LoadDictionary(reference);
			}
			// Ours say how large they are, but that's only a hint.
			ulong declared = Decompressor.GetDecompressedSize(compressed);
			MemoryStream json = new((int)Math.Min(declared, 16 * 1024 * 1024));
			byte[] buffer = new byte[81920];
			while (true) {
				OperationStatus status = decompressor.UnwrapStream(compressed, buffer, out int consumed, out int written);
				compressed = compressed[consumed..];
				if (json.Length + written > maxBytes) {
					throw new InvalidDataException($"The snapshot is larger than {maxBytes} bytes");
				}
				json.Write(buffer, 0, written);
				switch (status) {
					case OperationStatus.DestinationTooSmall:
						continue;
					case OperationStatus.Done when compressed.IsEmpty:
						return json.ToArray();
					case OperationStatus.Done:
						throw new InvalidDataException("The snapshot has more after its end");
					case OperationStatus.NeedMoreData:
						throw new InvalidDataException("The snapshot is cut short");
					default:
						throw new InvalidDataException("The snapshot can't be decompressed");
				}
			}
		} catch (ZstdException e) {
			throw new InvalidDataException($"The snapshot can't be decompressed: {e.Message}", e);
		}
	}

	private static void Check(nuint result) {
		if (Methods.ZSTD_isError(result)) {
			throw new InvalidOperationException($"zstd failed with {Methods.ZSTD_getErrorCode(result)}");
		}
	}
}
