using System;

namespace C7GameData {
	// The game's random number generator. Unlike System.Random, its whole
	// state is one number, so a save can hold it and a loaded game carries on
	// with the same sequence of random numbers it would have had.
	//
	// It is SplitMix64: each number is the state, advanced by a fixed step,
	// put through a mixing function.
	public class GameRandom : Random {
		private const ulong Step = 0x9E3779B97F4A7C15;

		// Everything the generator needs to carry on where it left off.
		public ulong State;

		public GameRandom(int seed) : this(InitialState(seed, 0)) { }

		public GameRandom(ulong state) {
			State = state;
		}

		// The state a game with the given seed starts the given turn with,
		// for saves made before the state was saved.
		public static ulong InitialState(int seed, int turn) {
			return Mix(((ulong)(uint)seed << 32) | (uint)turn);
		}

		private static ulong Mix(ulong z) {
			z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
			z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
			return z ^ (z >> 31);
		}

		public ulong NextUInt64() {
			State += Step;
			return Mix(State);
		}

		// A number from 0 up to but not including bound.
		private ulong NextBelow(ulong bound) {
			if (bound <= 1) {
				return 0;
			}
			// The high half of a 64 by 64 bit product is evenly spread over
			// [0, bound), up to a bias of bound / 2^64, which is negligible.
			return Math.BigMul(NextUInt64(), bound, out _);
		}

		protected override double Sample() {
			// 53 random bits, the precision of a double.
			return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
		}

		public override double NextDouble() => Sample();

		public override float NextSingle() {
			return (NextUInt64() >> 40) * (1.0f / (1U << 24));
		}

		public override int Next() {
			// System.Random.Next never returns int.MaxValue.
			return (int)NextBelow(int.MaxValue);
		}

		public override int Next(int maxValue) {
			ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
			return (int)NextBelow((ulong)maxValue);
		}

		public override int Next(int minValue, int maxValue) {
			ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
			return (int)(minValue + (long)NextBelow((ulong)((long)maxValue - minValue)));
		}

		public override long NextInt64() {
			return (long)NextBelow(long.MaxValue);
		}

		public override long NextInt64(long maxValue) {
			ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
			return (long)NextBelow((ulong)maxValue);
		}

		public override long NextInt64(long minValue, long maxValue) {
			ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
			return minValue + (long)NextBelow((ulong)(maxValue - minValue));
		}

		public override void NextBytes(byte[] buffer) {
			ArgumentNullException.ThrowIfNull(buffer);
			NextBytes(buffer.AsSpan());
		}

		public override void NextBytes(Span<byte> buffer) {
			for (int i = 0; i < buffer.Length; i += 8) {
				ulong bits = NextUInt64();
				for (int j = i; j < Math.Min(i + 8, buffer.Length); ++j) {
					buffer[j] = (byte)bits;
					bits >>= 8;
				}
			}
		}
	}
}
