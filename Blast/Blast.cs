/*
Translation to managed C# of blast.c/h, published by Mark Adler. The copyright notice
for the C implementation is included below. This implementation varies from the C
original.

Any part of this implementation that is not covered by the original
notice is Copyright (c) 2012 James Telfer, and is released under the Apache 2.0 license:
see http://www.apache.org/licenses/LICENSE-2.0.html.

It should be noted that this algorithm was originally implemented by
PKWare, and while there was no reference to their implementation
there may be portions that come under patents originating from that
company.

The license terms do not and cannot cover any part of this work that
is covered by patent claims of any other entity.


https://github.com/madler/zlib/blob/master/contrib/blast/
blast.c

Copyright (C) 2003 Mark Adler
version 1.1, 16 Feb 2003

This software is provided 'as-is', without any express or implied
warranty.  In no event will the author be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
    claim that you wrote the original software. If you use this software
    in a product, an acknowledgment in the product documentation would be
    appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
    misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.

Mark Adler    madler@alumni.caltech.edu
 */
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Blast {
	/// <summary>
	/// Decompresses a PKWare Data Compression Library ("implode", decoded by blast.c) stream. Like blast.c, only one
	/// compressed stream is decoded; any input after its end code is ignored.
	/// Malformed input throws a <see cref="BlastException"/>.
	/// </summary>
	public class BlastDecoder {
		public const int MAX_WIN = 4096;
		private const int END_OF_STREAM = 519;
		private const int LITERAL_INDICATOR = 0;

		// Size of the output buffer when decoding to a Stream. The last MAX_WIN bytes are kept as the sliding
		// window each time the buffer is flushed, so a larger buffer means fewer Stream writes and window shifts.
		private const int STREAM_OUTPUT_BUFFER_SIZE = 256 * 1024;
		private const int INPUT_BUFFER_SIZE = 16384;

		// Initial size of the output buffer when decoding to memory: a few times the compressed size (Civ3 files
		// typically decompress to around 10 times their size), within these bounds. It grows as needed.
		private const int MIN_INITIAL_OUTPUT_SIZE = 64 * 1024;
		private const int MAX_INITIAL_OUTPUT_SIZE = 4 * 1024 * 1024;

		// The most bits a single literal or length/distance step can consume: 1 indicator bit + a 13 bit literal
		// code, or 1 indicator bit + 7 length code bits + 8 extra length bits + 8 distance code bits + 6 extra distance bits
		private const int MAX_BITS_PER_STEP = 30;

		/// <summary>
		/// base for length codes
		/// </summary>
		private static readonly short[] LENGTH_CODE_BASE = { 3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264 };

		/// <summary>
		/// extra bits for length codes
		/// </summary>
		private static readonly byte[] LENGTH_CODE_EXTRA = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };


		//
		// state variables
		//

		// Input: either the whole compressed array (_inputStream == null) or a buffer refilled from _inputStream
		private readonly Stream _inputStream;
		private byte[] _input;
		private int _inputPos;
		private int _inputEnd;

		// Bit accumulator; bits are consumed from the least significant end. Whole bytes may be read ahead of
		// what has been consumed, see FlushBits.
		private ulong _bitBuffer;
		private int _bitBufferCount;

		// Output: the buffer doubles as the sliding window. When _outputStream is null the buffer simply grows
		// and holds the whole decompressed output; otherwise it is flushed to the stream as it fills up.
		private readonly Stream _outputStream;
		private byte[] _outputBuffer;
		private int _outputBufferPos = 0; // index of next write location in _outputBuffer[]
										  // The most output to decode to memory
		private readonly long _maxOutputLength = Array.MaxLength;

		/// <summary>
		/// Creates a decoder that reads compressed data from <paramref name="inputStream"/> and writes the
		/// decompressed data to <paramref name="outputStream"/> when <see cref="Decompress()"/> is called.
		/// </summary>
		public BlastDecoder(Stream inputStream, Stream outputStream) {
			ArgumentNullException.ThrowIfNull(inputStream);
			ArgumentNullException.ThrowIfNull(outputStream);
			this._inputStream = inputStream;
			this._input = new byte[INPUT_BUFFER_SIZE];

			this._outputStream = outputStream;
			this._outputBuffer = new byte[STREAM_OUTPUT_BUFFER_SIZE];
		}

		private BlastDecoder(byte[] input, int offset, int count, int initialOutputCapacity, long maxOutputLength) {
			this._inputStream = null;
			this._input = input;
			this._inputPos = offset;
			this._inputEnd = offset + count;

			this._outputStream = null;
			this._maxOutputLength = maxOutputLength;
			this._outputBuffer = new byte[Math.Max(initialOutputCapacity, MAX_WIN)];
		}

		/// <summary>
		/// Decompress a whole PKWare Compression Library buffer straight into a byte array, avoiding the
		/// intermediate streams. Produces exactly the bytes <see cref="Decompress()"/> would write to its output stream.
		/// </summary>
		public static byte[] DecompressBytes(byte[] compressed) {
			ArgumentNullException.ThrowIfNull(compressed);
			return DecompressBytes(compressed, 0, compressed.Length);
		}

		/// <inheritdoc cref="DecompressBytes(byte[])"/>
		public static byte[] DecompressBytes(byte[] compressed, int offset, int count) {
			return DecompressBytes(compressed, offset, count, Array.MaxLength);
		}

		/// <summary>
		/// Like <see cref="DecompressBytes(byte[], int, int)"/>, but throws a <see cref="BlastException"/> if the
		/// output would be longer than <paramref name="maxOutputLength"/> bytes.
		/// </summary>
		public static byte[] DecompressBytes(byte[] compressed, int offset, int count, int maxOutputLength) {
			ArgumentNullException.ThrowIfNull(compressed);
			if (offset < 0 || count < 0 || offset > compressed.Length - count) {
				throw new ArgumentOutOfRangeException(nameof(count));
			}
			ArgumentOutOfRangeException.ThrowIfNegative(maxOutputLength);

			long initialCapacity = Math.Clamp((long)count * 10, MIN_INITIAL_OUTPUT_SIZE, MAX_INITIAL_OUTPUT_SIZE);
			initialCapacity = Math.Min(initialCapacity, Math.Max(maxOutputLength, MAX_WIN));
			BlastDecoder decoder = new BlastDecoder(compressed, offset, count, (int)initialCapacity, maxOutputLength);
			decoder.Decompress();

			byte[] output = decoder._outputBuffer;
			if (output.Length != decoder._outputBufferPos) {
				output = output.AsSpan(0, decoder._outputBufferPos).ToArray();
			}
			return output;
		}

		/// <summary>
		/// Decode PKWare Compression Library stream.
		/// </summary>
		public void Decompress() {
			try {
				DecompressStream();
			} catch (BlastException) {
				// Like blast.c, which writes output as it goes, write what was decoded before the error. A failure
				// to write it mustn't hide the error in the data.
				if (_outputStream != null) {
					try {
						FlushOutputBuffer();
					} catch (Exception) {
					}
				}
				throw;
			}
			// write remaining bytes
			if (_outputStream != null) {
				FlushOutputBuffer();
			}
			FlushBits();
		}

		private void DecompressStream() {
			// read header (start of compressed stream)
			FillBitBuffer();
			bool codedLiteral = ReadCodedLiteralHeader();

			// log2(dictionary size) - 6
			int dictSize = GetBits(8);

			if (dictSize < 4 || dictSize > 6) {
				throw new BlastException(BlastException.DictionarySizeMessage);
			}

			// Number of bytes written by this stream, saturating at MAX_WIN. Distances can't reach back past its start.
			int streamOutputCount = 0;

			HuffmanTable literalCode = HuffmanTable.LITERAL_CODE;
			HuffmanTable lengthCode = HuffmanTable.LENGTH_CODE;
			HuffmanTable distanceCode = HuffmanTable.DISTANCE_CODE;

			// The hot state is kept in locals, and only synced with the fields around the (rare) refills and flushes
			ulong bitBuffer = _bitBuffer;
			int bitCount = _bitBufferCount;
			byte[] output = _outputBuffer;
			int outputPos = _outputBufferPos;

			// decode literals and length/distance pairs
			try {
				while (true) {
					// Make sure a whole step's worth of bits is buffered, if there is that much input left. Within a step, running
					// out of bits means running out of input.
					if (bitCount < MAX_BITS_PER_STEP) {
						_bitBuffer = bitBuffer;
						_bitBufferCount = bitCount;
						FillBitBuffer();
						bitBuffer = _bitBuffer;
						bitCount = _bitBufferCount;
						if (bitCount == 0) {
							throw new BlastException(BlastException.OutOfInputMessage);
						}
					}

					// Bit indicates whether to read literal from stream or encoded length+distance pair
					int nextCodeIndicator = (int)bitBuffer & 1;
					bitBuffer >>= 1;
					bitCount--;

					if (nextCodeIndicator == LITERAL_INDICATOR) {
						// get literal and write it
						int literal;
						if (codedLiteral) {
							literal = Decode(literalCode, ref bitBuffer, ref bitCount);
						} else {
							if (bitCount < 8) {
								throw new BlastException(BlastException.OutOfInputMessage);
							}
							literal = (int)bitBuffer & 0xff;
							bitBuffer >>= 8;
							bitCount -= 8;
						}
						if (outputPos == output.Length) {
							_outputBufferPos = outputPos;
							EnsureBufferSpace(1);
							output = _outputBuffer;
							outputPos = _outputBufferPos;
						}
						output[outputPos++] = (byte)literal;
						if (streamOutputCount < MAX_WIN) {
							streamOutputCount++;
						}
						continue;
					}

					// get length/distance and write buffer segments from current window

					// decode length
					int decodedLengthSymbol = Decode(lengthCode, ref bitBuffer, ref bitCount);
					int lengthExtraBits = LENGTH_CODE_EXTRA[decodedLengthSymbol];
					if (bitCount < lengthExtraBits) {
						throw new BlastException(BlastException.OutOfInputMessage);
					}
					int copyLength = LENGTH_CODE_BASE[decodedLengthSymbol] + ((int)bitBuffer & ((1 << lengthExtraBits) - 1));
					bitBuffer >>= lengthExtraBits;
					bitCount -= lengthExtraBits;

					if (copyLength == END_OF_STREAM) // sentinel value
					{
						// no more for this stream
						break;
					}

					// decode distance
					int distanceAdditionalBits = copyLength == 2 ? 2 : dictSize;
					int copyDist = Decode(distanceCode, ref bitBuffer, ref bitCount) << distanceAdditionalBits;
					if (bitCount < distanceAdditionalBits) {
						throw new BlastException(BlastException.OutOfInputMessage);
					}
					copyDist += (int)bitBuffer & ((1 << distanceAdditionalBits) - 1);
					bitBuffer >>= distanceAdditionalBits;
					bitCount -= distanceAdditionalBits;
					copyDist++;

					// malformed input - you can't go back that far
					if (copyDist > streamOutputCount) {
						throw new BlastException(BlastException.DistanceMessage);
					}

					// Copy copyLength bytes from copyDist bytes back.
					if (copyLength > output.Length - outputPos) {
						_outputBufferPos = outputPos;
						EnsureBufferSpace(copyLength);
						output = _outputBuffer;
						outputPos = _outputBufferPos;
					}
					CopyFromWindow(output, outputPos, copyLength, copyDist);
					outputPos += copyLength;
					streamOutputCount = Math.Min(streamOutputCount + copyLength, MAX_WIN);
				}
			} finally {
				// (also when the data turns out to be malformed, so what was decoded before can be written)
				_bitBuffer = bitBuffer;
				_bitBufferCount = bitCount;
				_outputBufferPos = outputPos;
			}
		}

		private bool ReadCodedLiteralHeader() {
			int codedLiteral = GetBits(8);
			if (codedLiteral > 1) {
				throw new BlastException(BlastException.LiteralFlagMessage);
			}

			return codedLiteral == 1;
		}

		// Copies copyLength bytes from copyDistance bytes back to buffer[toIndex], which has room for them
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void CopyFromWindow(byte[] buffer, int toIndex, int copyLength, int copyDistance) {
			int fromIndex = toIndex - copyDistance;

			if (copyDistance >= copyLength) {
				// No overlap between source and destination
				Buffer.BlockCopy(buffer, fromIndex, buffer, toIndex, copyLength);
			} else if (copyDistance == 1) {
				// Run of a single repeated byte
				buffer.AsSpan(toIndex, copyLength).Fill(buffer[fromIndex]);
			} else {
				// If copyLength is greater than copyDist, the copyDist bytes repeat up to a count of copyLength.
				// Everything from fromIndex up to the write position is already a whole number of repetitions,
				// so it can be copied as one non-overlapping chunk, which doubles the available pattern each time.
				int remaining = copyLength;
				while (remaining > 0) {
					int chunk = Math.Min(toIndex - fromIndex, remaining);
					Buffer.BlockCopy(buffer, fromIndex, buffer, toIndex, chunk);
					toIndex += chunk;
					remaining -= chunk;
				}
			}
		}

		#region Input bits

		/// <summary>
		/// Decode a Huffman code from the bits using the table's direct lookup table (see
		/// <see cref="HuffmanTable.lookup"/>), returning the symbol. Behaves like blast.c's bit-by-bit decode():
		/// only the bits of the matched code are consumed, and input is only required up to the end of that code.
		/// The caller has buffered as many bits as the input has, up to a whole step's worth.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Decode(HuffmanTable table, ref ulong bitBuffer, ref int bitCount) {
			int entry = table.lookup[(int)bitBuffer & table.lookupMask];
			int length = entry & 0xf;

			if (length > bitCount) {
				// the code continues past the end of the input
				throw new BlastException(BlastException.OutOfInputMessage);
			}
			if (length == 0) {
				// Not reachable with the built-in tables, which are complete codes
				throw new BlastException("Invalid Huffman code");
			}

			bitBuffer >>= length;
			bitCount -= length;

			return entry >> 4;
		}

		private int GetBits(int need) {
			if (_bitBufferCount < need) {
				FillBitBuffer();

				if (_bitBufferCount < need) {
					throw new BlastException(BlastException.OutOfInputMessage);
				}
			}

			int val = (int)_bitBuffer & ((1 << need) - 1);
			_bitBuffer >>= need;
			_bitBufferCount -= need;

			return val;
		}

		/// <summary>
		/// Top up the bit buffer with whole bytes, as far as the input allows.
		/// </summary>
		private void FillBitBuffer() {
			while (_bitBufferCount <= 56) {
				if (_inputPos == _inputEnd && !ReadInput()) {
					return;
				}
				_bitBuffer |= (ulong)_input[_inputPos++] << _bitBufferCount;
				_bitBufferCount += 8;
			}
		}

		/// <summary>
		/// Discard the rest of the partially consumed byte at the end of the compressed stream.
		/// </summary>
		private void FlushBits() {
			int partialBits = _bitBufferCount & 7;
			_bitBuffer >>= partialBits;
			_bitBufferCount -= partialBits;
		}

		private bool ReadInput() {
			if (_inputStream == null) {
				return false;
			}

			_inputEnd = _inputStream.Read(_input, 0, _input.Length);
			_inputPos = 0;
			return _inputEnd > 0;
		}

		#endregion

		#region Output stream

		private void EnsureBufferSpace(int required) {
			// is there room in the buffer?
			long needed = (long)_outputBufferPos + required;
			if (needed <= _outputBuffer.Length) {
				return;
			}

			if (_outputStream == null) {
				// decoding to memory: grow the buffer, which holds all of the output
				if (needed > _maxOutputLength) {
					throw new BlastException(BlastException.OutputTooLargeMessage);
				}
				long newSize = Math.Min(Math.Max((long)_outputBuffer.Length * 2, needed), _maxOutputLength);
				Array.Resize(ref _outputBuffer, (int)newSize);
				return;
			}

			// flush everything except the window that later copies may still refer back to
			int startWindowOffset = _outputBufferPos - MAX_WIN;

			FlushOutputBufferSection(startWindowOffset); // only flush the section that's not part of the window

			// position the stream further back
			Buffer.BlockCopy(_outputBuffer, startWindowOffset, _outputBuffer, 0, MAX_WIN);
			_outputBufferPos = MAX_WIN;
		}

		private void FlushOutputBufferSection(int count) {
			_outputStream.Write(_outputBuffer, 0, count);
		}

		private void FlushOutputBuffer() {
			if (_outputBufferPos > 0) {
				FlushOutputBufferSection(_outputBufferPos);
				_outputBufferPos = 0;
			}
		}

		#endregion
	}
}
