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
	public class BlastDecoder {
		public const int MAX_WIN = 4096;
		private const int END_OF_STREAM = 519;
		private const int LITERAL_INDICATOR = 0;

		// Size of the output buffer when decoding to a Stream. The last MAX_WIN bytes are kept as the sliding
		// window each time the buffer is flushed, so a larger buffer means fewer Stream writes and window shifts.
		private const int STREAM_OUTPUT_BUFFER_SIZE = 256 * 1024;
		private const int INPUT_BUFFER_SIZE = 16384;

		// The most bits a single literal or length/distance step can consume:
		// 1 indicator bit + 13 length code bits + 8 extra length bits + 13 distance code bits + 8 extra distance bits
		private const int MAX_BITS_PER_STEP = 43;

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

		// Number of bytes written by the current compressed stream, saturating at MAX_WIN. Distances can't
		// reach back past the start of the current stream.
		private int _streamOutputCount = 0;


		/// <summary>
		/// <para>Decompress input to output using the provided infun() and outfun() calls.
		/// On success, the return value of blast() is zero.  If there is an error in
		/// the source data, i.e. it is not in the proper format, then a negative value
		/// is returned.  If there is not enough input available or there is not enough
		/// output space, then a positive error is returned.</para>
		///
		/// <para>The input function is invoked: len = infun(how, &buf), where buf is set by
		/// infun() to point to the input buffer, and infun() returns the number of
		/// available bytes there.  If infun() returns zero, then blast() returns with
		/// an input error.  (blast() only asks for input if it needs it.)  inhow is for
		/// use by the application to pass an input descriptor to infun(), if desired.</para>
		///
		/// <para>The output function is invoked: err = outfun(how, buf, len), where the bytes
		/// to be written are buf[0..len-1].  If err is not zero, then blast() returns
		/// with an output error.  outfun() is always called with len &lt;= 4096.  outhow
		/// is for use by the application to pass an output descriptor to outfun(), if
		/// desired.</para>
		///
		/// <para>The return codes are:</para>
		///
		///   2:  ran out of input before completing decompression
		///   1:  output error before completing decompression
		///   0:  successful decompression
		///  -1:  literal flag not zero or one
		///  -2:  dictionary size not in 4..6
		///  -3:  distance is too far back
		///
		/// <para>At the bottom of blast.c is an example program that uses blast() that can be
		/// compiled to produce a command-line decompression filter by defining TEST.</para>
		/// </summary>
		public BlastDecoder(Stream inputStream, Stream outputStream) {
			this._inputStream = inputStream;
			this._input = new byte[INPUT_BUFFER_SIZE];

			this._outputStream = outputStream;
			this._outputBuffer = new byte[STREAM_OUTPUT_BUFFER_SIZE];
		}

		private BlastDecoder(byte[] input, int offset, int count, int initialOutputCapacity) {
			this._inputStream = null;
			this._input = input;
			this._inputPos = offset;
			this._inputEnd = offset + count;

			this._outputStream = null;
			this._outputBuffer = new byte[Math.Max(initialOutputCapacity, MAX_WIN)];
		}

		/// <summary>
		/// Decompress a whole PKWare Compression Library buffer straight into a byte array, avoiding the
		/// intermediate streams. Produces exactly the bytes <see cref="Decompress()"/> would write to its output stream.
		/// </summary>
		public static byte[] DecompressBytes(byte[] compressed) {
			return DecompressBytes(compressed, 0, compressed.Length);
		}

		/// <inheritdoc cref="DecompressBytes(byte[])"/>
		public static byte[] DecompressBytes(byte[] compressed, int offset, int count) {
			ArgumentNullException.ThrowIfNull(compressed);
			if (offset < 0 || count < 0 || offset > compressed.Length - count) {
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			// Civ3 files typically decompress to several times their compressed size; the buffer grows if needed
			long initialCapacity = Math.Min((long)count * 8, Array.MaxLength);
			BlastDecoder decoder = new BlastDecoder(compressed, offset, count, (int)initialCapacity);
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
			do {
				// some files are composed of multiple compressed streams
				DecompressStream();
			} while (IsInputRemaining());
		}

		private void DecompressStream() {
			// read header (start of compressed stream)
			bool codedLiteral = ReadCodedLiteralHeader();

			// log2(dictionary size) - 6
			int dictSize = GetBits(8);

			if (dictSize < 4 || dictSize > 6) {
				throw new BlastException(BlastException.DictionarySizeMessage);
			}

			_streamOutputCount = 0;

			ushort[] literalLookup = HuffmanTable.LITERAL_CODE.lookup;
			ushort[] lengthLookup = HuffmanTable.LENGTH_CODE.lookup;
			ushort[] distanceLookup = HuffmanTable.DISTANCE_CODE.lookup;

			// decode the compressed stream
			try {
				// decode literals and length/distance pairs
				do {
					// Make sure a whole step's worth of bits is buffered, if there is that much input left
					if (_bitBufferCount < MAX_BITS_PER_STEP) {
						FillBitBuffer();
					}

					// Bit indicates whether to read literal from stream or encoded length+distance pair
					int nextCodeIndicator = GetBits(1);

					if (nextCodeIndicator == LITERAL_INDICATOR) {
						// get literal and write it
						byte literal = codedLiteral ? (byte)Decode(literalLookup) : (byte)GetBits(8);
						if (_outputBufferPos == _outputBuffer.Length) {
							EnsureBufferSpace(1);
						}
						_outputBuffer[_outputBufferPos++] = literal;
						if (_streamOutputCount < MAX_WIN) {
							_streamOutputCount++;
						}
					} else {
						// get length/distance and write buffer segments from current window

						// decode length
						int decodedLengthSymbol = Decode(lengthLookup);
						int copyLength = LENGTH_CODE_BASE[decodedLengthSymbol] + GetBits(LENGTH_CODE_EXTRA[decodedLengthSymbol]);

						if (copyLength == END_OF_STREAM) // sentinel value
						{
							// no more for this stream,
							// stop and flush
							break;
						}

						// decode distance
						int distanceAdditionalBits = copyLength == 2 ? 2 : dictSize;
						int copyDist = Decode(distanceLookup) << distanceAdditionalBits;
						copyDist += GetBits(distanceAdditionalBits);
						copyDist++;

						// malformed input - you can't go back that far
						if (copyDist > _streamOutputCount) {
							throw new BlastException(BlastException.DistanceMessage);
						}

						WriteWindowSegment(copyLength, copyDist);
					}
				} while (true);
			} finally {
				// write remaining bytes
				if (_outputStream != null) {
					FlushOutputBuffer();
				}
				FlushBits();
			}
		}

		private bool ReadCodedLiteralHeader() {
			int codedLiteral = GetBits(8);
			if (codedLiteral > 1) {
				throw new BlastException(BlastException.LiteralFlagMessage);
			}

			return codedLiteral == 1;
		}

		private void WriteWindowSegment(int copyLength, int copyDistance) {
			// Copy copyLength bytes from copyDist bytes back.
			EnsureBufferSpace(copyLength);

			byte[] buffer = _outputBuffer;
			int toIndex = _outputBufferPos;
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

			_outputBufferPos += copyLength;
			_streamOutputCount = Math.Min(_streamOutputCount + copyLength, MAX_WIN);
		}

		#region Input bits

		/// <summary>
		/// Decode a Huffman code from the stream using the given direct lookup table (see
		/// <see cref="HuffmanTable.lookup"/>), returning the symbol. Behaves like blast.c's bit-by-bit decode():
		/// only the bits of the matched code are consumed, and input is only required up to the end of that code.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int Decode(ushort[] lookup) {
			if (_bitBufferCount < HuffmanTable.LOOKUP_BITS) {
				FillBitBuffer();
			}

			int entry = lookup[(int)_bitBuffer & HuffmanTable.LOOKUP_MASK];
			int length = entry & 0xf;

			if (length > _bitBufferCount) {
				// the code continues past the end of the input
				throw new BlastException(BlastException.OutOfInputMessage);
			}
			if (length == 0) {
				// Not reachable with the built-in tables, which are complete codes
				throw new BlastException("Invalid Huffman code");
			}

			_bitBuffer >>= length;
			_bitBufferCount -= length;

			return entry >> 4;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
		/// Discard the rest of the partially consumed byte at the end of a compressed stream. Any whole bytes that
		/// were read ahead stay buffered for the next stream.
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

		/// <summary>
		/// Check for presence of more input without consuming it.
		/// May refill the input buffer.
		/// </summary>
		private bool IsInputRemaining() {
			return _bitBufferCount > 0 || _inputPos < _inputEnd || ReadInput();
		}

		#endregion

		#region Output stream

		private void EnsureBufferSpace(int required) {
			// is there room in the buffer?
			if (_outputBufferPos + required <= _outputBuffer.Length) {
				return;
			}

			if (_outputStream == null) {
				// decoding to memory: grow the buffer, which holds all of the output
				long newSize = Math.Max((long)_outputBuffer.Length * 2, (long)_outputBufferPos + required);
				Array.Resize(ref _outputBuffer, (int)Math.Min(newSize, Array.MaxLength));
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
