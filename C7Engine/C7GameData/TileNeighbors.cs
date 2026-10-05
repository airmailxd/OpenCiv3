using System;
using System.Collections;
using System.Collections.Generic;

namespace C7GameData;

/// <summary>
/// The neighbors of a tile, keyed by direction.
///
/// This replaces a Dictionary&lt;TileDirection, Tile&gt; per tile: neighbors
/// are read in the hottest loops (pathing, visibility, rendering), so they
/// are stored in a fixed array indexed by direction instead of being hashed.
///
/// It behaves like the dictionary it replaces:
/// - a direction may be absent (Tile.NONE and tiles whose neighbors haven't
///   been computed have none); the indexer throws KeyNotFoundException for
///   an absent direction and TryGetValue returns false. Off-map neighbors of
///   a computed tile are present and set to Tile.NONE.
/// - enumeration yields the directions in the order they were first added,
///   like a Dictionary with no removals.
/// Enumerating it, or its Keys/Values, doesn't allocate when done with
/// foreach on the concrete type.
/// </summary>
public sealed class TileNeighbors : IReadOnlyDictionary<TileDirection, Tile> {
	private const int DirectionCount = 8;

	// Indexed by (int)TileDirection; null means the direction is absent.
	private readonly Tile[] tiles = new Tile[DirectionCount];

	// Directions in the order they were added, to match Dictionary
	// enumeration order.
	private readonly TileDirection[] order = new TileDirection[DirectionCount];
	private int count;

	public TileNeighbors() { }

	public TileNeighbors(IEnumerable<KeyValuePair<TileDirection, Tile>> source) {
		foreach (KeyValuePair<TileDirection, Tile> pair in source) {
			this[pair.Key] = pair.Value;
		}
	}

	// Lets code that builds a Dictionary<TileDirection, Tile> keep assigning
	// it to Tile.neighbors.
	public static implicit operator TileNeighbors(Dictionary<TileDirection, Tile> source) {
		return source == null ? null : new TileNeighbors(source);
	}

	private static bool IsValidDirection(TileDirection direction) {
		return (uint)direction < DirectionCount;
	}

	public Tile this[TileDirection direction] {
		get {
			if (IsValidDirection(direction)) {
				Tile t = tiles[(int)direction];
				if (t != null) {
					return t;
				}
			}
			throw new KeyNotFoundException($"The given key '{direction}' was not present in the dictionary.");
		}
		set {
			if (!IsValidDirection(direction)) {
				throw new ArgumentOutOfRangeException(nameof(direction), $"{direction} is not a neighbor direction");
			}
			if (value == null) {
				// A Dictionary could hold a null value, but tiles never have
				// null neighbors; null is reserved to mean "absent" here.
				throw new ArgumentNullException(nameof(value), "Use Tile.NONE for a missing neighbor");
			}
			if (tiles[(int)direction] == null) {
				order[count++] = direction;
			}
			tiles[(int)direction] = value;
		}
	}

	// The neighbor in the given direction, or null if that direction is
	// absent. Cheaper than TryGetValue for hot code.
	public Tile Get(TileDirection direction) {
		return IsValidDirection(direction) ? tiles[(int)direction] : null;
	}

	public bool TryGetValue(TileDirection direction, out Tile value) {
		value = Get(direction);
		return value != null;
	}

	public bool ContainsKey(TileDirection direction) {
		return Get(direction) != null;
	}

	public bool ContainsValue(Tile value) {
		for (int i = 0; i < count; i++) {
			if (tiles[(int)order[i]] == value) {
				return true;
			}
		}
		return false;
	}

	public int Count => count;

	public KeyCollection Keys => new KeyCollection(this);

	public ValueCollection Values => new ValueCollection(this);

	IEnumerable<TileDirection> IReadOnlyDictionary<TileDirection, Tile>.Keys => Keys;

	IEnumerable<Tile> IReadOnlyDictionary<TileDirection, Tile>.Values => Values;

	public Enumerator GetEnumerator() => new Enumerator(this);

	IEnumerator<KeyValuePair<TileDirection, Tile>> IEnumerable<KeyValuePair<TileDirection, Tile>>.GetEnumerator() => GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public struct Enumerator : IEnumerator<KeyValuePair<TileDirection, Tile>> {
		private readonly TileNeighbors owner;
		private int index;

		internal Enumerator(TileNeighbors owner) {
			this.owner = owner;
			index = -1;
		}

		public KeyValuePair<TileDirection, Tile> Current {
			get {
				TileDirection direction = owner.order[index];
				return new KeyValuePair<TileDirection, Tile>(direction, owner.tiles[(int)direction]);
			}
		}

		object IEnumerator.Current => Current;

		public bool MoveNext() => ++index < owner.count;

		public void Reset() => index = -1;

		public void Dispose() { }
	}

	public readonly struct KeyCollection : IReadOnlyCollection<TileDirection> {
		private readonly TileNeighbors owner;

		internal KeyCollection(TileNeighbors owner) {
			this.owner = owner;
		}

		public int Count => owner.count;

		public KeyEnumerator GetEnumerator() => new KeyEnumerator(owner);

		IEnumerator<TileDirection> IEnumerable<TileDirection>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public struct KeyEnumerator : IEnumerator<TileDirection> {
		private readonly TileNeighbors owner;
		private int index;

		internal KeyEnumerator(TileNeighbors owner) {
			this.owner = owner;
			index = -1;
		}

		public TileDirection Current => owner.order[index];

		object IEnumerator.Current => Current;

		public bool MoveNext() => ++index < owner.count;

		public void Reset() => index = -1;

		public void Dispose() { }
	}

	public readonly struct ValueCollection : IReadOnlyCollection<Tile> {
		private readonly TileNeighbors owner;

		internal ValueCollection(TileNeighbors owner) {
			this.owner = owner;
		}

		public int Count => owner.count;

		public ValueEnumerator GetEnumerator() => new ValueEnumerator(owner);

		IEnumerator<Tile> IEnumerable<Tile>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public struct ValueEnumerator : IEnumerator<Tile> {
		private readonly TileNeighbors owner;
		private int index;

		internal ValueEnumerator(TileNeighbors owner) {
			this.owner = owner;
			index = -1;
		}

		public Tile Current => owner.tiles[(int)owner.order[index]];

		object IEnumerator.Current => Current;

		public bool MoveNext() => ++index < owner.count;

		public void Reset() => index = -1;

		public void Dispose() { }
	}
}
