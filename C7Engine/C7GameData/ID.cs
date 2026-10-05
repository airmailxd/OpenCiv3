using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using C7GameData.Save;

namespace C7GameData {
	public interface IHasID {
		ID id { get; }
	}

	public class ID : IEquatable<ID> {
		// at runtime, any ID parsed from a string as "<key>-none" will be
		// compared to other IDs by -0xC7C7
		private static readonly int magicNoneIdNumber = -0xC7C7;

		private readonly string key;

		private readonly int n;

		// IDs are immutable and used as keys in many hot dictionaries/sets, so
		// the hash is computed once instead of formatting a string per lookup.
		private readonly int hashCode;

		public override string ToString() {
			return n != magicNoneIdNumber ? $"{key}-{n}" : $"{key}-none";
		}

		internal ID(string key, int n) {
			this.key = key;
			this.n = n;
			this.hashCode = HashCode.Combine(key, n);
		}

		public static ID None(string key) {
			return new ID(key, magicNoneIdNumber);
		}

		// Parses the ToString() form, "<key>-<n>" or "<key>-none". Malformed
		// input throws a FormatException.
		public static ID FromString(string str) {
			if (str == null) {
				throw new FormatException("ID string cannot be null");
			}
			// To handle units like "Man-O-War" we need to only look after the final
			// dash to get the id.
			int lastDash = str.LastIndexOf('-');
			string key = lastDash >= 0 ? str.Substring(0, lastDash) : "";
			string number = lastDash >= 0 ? str.Substring(lastDash + 1) : str;
			if (number == "none") {
				return None(key);
			}
			if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) {
				throw new FormatException($"Invalid ID \"{str}\": \"{number}\" is not a number");
			}
			if (n < 0) {
				throw new FormatException($"ID cannot have a negative number, got {n}");
			}
			return new ID(key, n);
		}

		public override bool Equals(object obj) {
			return Equals(obj as ID);
		}

		public bool Equals(ID other) {
			if (other is null) {
				return false;
			}
			return ReferenceEquals(this, other) || (other.hashCode == hashCode && other.n == n && other.key == key);
		}

		// Consistent with Equals: equal (key, n) pairs give equal hashes. Note
		// string hashes are randomized per process in .NET, so nothing can
		// depend on the hash value across runs anyway.
		public override int GetHashCode() => hashCode;

		public static bool operator ==(ID lhs, ID rhs) {
			if (lhs is null && rhs is null) {
				return true; // null == null, no footguns
			}
			if (lhs is null || rhs is null) {
				return false;
			}
			return lhs.n == rhs.n && lhs.key == rhs.key;
		}

		public static bool operator !=(ID lhs, ID rhs) {
			return !(lhs == rhs);
		}

		public class Factory {
			private Dictionary<string, int> keyCounter;

			public Factory() {
				keyCounter = new Dictionary<string, int>();
			}

			// When creating an ID Factory for a loaded save game, 
			// this constructor finds the largest n-value for each ID key.
			// ie. loading a save with units ["warrior-1", "warrior-3", "worker-2"]
			//     should result in an id factory with
			// keyCounter = {
			//     "warrior": 4,
			//     "worker": 3,
			// }
			public Factory(SaveGame save) {
				IEnumerable<IHasID> entities = new List<IEnumerable<IHasID>>() {save.Units, save.Cities}
												.SelectMany(list=> list); // flattening the list

				keyCounter = entities
					.GroupBy(entity => entity.id.key)
					.ToDictionary(
						group => group.Key,
						group => group.Select(entity => entity.id.n).Max() + 1
					);
			}

			public ID CreateID(string key) {
				int n = keyCounter.GetValueOrDefault(key, 1);
				keyCounter[key] = n + 1;
				return new ID(key, n);
			}
		}
	}

	public class IDJsonConverter : JsonConverter<ID> {
		public override void Write(Utf8JsonWriter writer, ID id, JsonSerializerOptions options) => writer.WriteStringValue(id.ToString());

		public override ID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ID.FromString(reader.GetString());
	}
}
