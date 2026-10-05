using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using C7GameData;

namespace C7Engine.Network;

// Serializes the messages LAN machines exchange. Game objects (players,
// units, cities, tiles, techs, ...) are written as references and resolved
// against the receiving machine's game data, so a message must be read after
// the snapshot holding the objects it refers to.
public static class NetSerialization {
	private static readonly JsonSerializerOptions options = CreateOptions();

	public static byte[] Serialize(MessageToEngine msg) {
		return JsonSerializer.SerializeToUtf8Bytes(msg, options);
	}

	public static MessageToEngine DeserializeMessageToEngine(byte[] json) {
		return JsonSerializer.Deserialize<MessageToEngine>(json, options);
	}

	public static byte[] Serialize(MessageToUI msg) {
		return JsonSerializer.SerializeToUtf8Bytes(msg, options);
	}

	public static MessageToUI DeserializeMessageToUI(byte[] json) {
		return JsonSerializer.Deserialize<MessageToUI>(json, options);
	}

	// For the frames' other contents, like the lobby.
	public static byte[] SerializeData<T>(T value) {
		return JsonSerializer.SerializeToUtf8Bytes(value, options);
	}

	public static T DeserializeData<T>(byte[] json) {
		return JsonSerializer.Deserialize<T>(json, options);
	}

	private static JsonSerializerOptions CreateOptions() {
		DefaultJsonTypeInfoResolver resolver = new();
		resolver.Modifiers.Add(AddMessageSubtypes);

		return new JsonSerializerOptions {
			IncludeFields = true,
			TypeInfoResolver = resolver,
			Converters = {
				new IDJsonConverter(),
				new JsonStringEnumConverter(),
				new PlayerReferenceConverter(),
				new CityReferenceConverter(),
				new MapUnitReferenceConverter(),
				new TileReferenceConverter(),
				new TechReferenceConverter(),
				new GovernmentReferenceConverter(),
				new TerraformReferenceConverter(),
				new CivilizationReferenceConverter(),
				new VictoryReferenceConverter(),
				new TilePathConverter(),
			},
		};
	}

	// Lets the abstract message types be read back as whichever message was
	// written, tagged by class name.
	private static void AddMessageSubtypes(JsonTypeInfo typeInfo) {
		if (typeInfo.Type != typeof(MessageToEngine) && typeInfo.Type != typeof(MessageToUI)) {
			return;
		}

		JsonPolymorphismOptions polymorphism = new() {
			TypeDiscriminatorPropertyName = "$type",
			UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
		};
		foreach (Type type in MessageSubtypes(typeInfo.Type)) {
			polymorphism.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
		}
		typeInfo.PolymorphismOptions = polymorphism;
	}

	private static IEnumerable<Type> MessageSubtypes(Type baseType) {
		return baseType.Assembly.GetTypes()
			.Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t) && t != baseType)
			.OrderBy(t => t.Name);
	}

	private static GameData Game => EngineStorage.gameData;

	// Writes an object as its ID, and reads it back as the object with that ID
	// in this machine's game data, or null if there is none.
	private abstract class ReferenceConverter<T> : JsonConverter<T> where T : class {
		protected abstract string Key(T value);
		protected abstract T Find(string key);

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			return reader.TokenType == JsonTokenType.Null ? null : Find(reader.GetString());
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
			string key = value == null ? null : Key(value);
			if (key == null) {
				writer.WriteNullValue();
			} else {
				writer.WriteStringValue(key);
			}
		}
	}

	private class PlayerReferenceConverter : ReferenceConverter<Player> {
		protected override string Key(Player value) => value.id.ToString();
		protected override Player Find(string key) => Game.GetPlayer(ID.FromString(key));
	}

	// Finds the first item in a list with a key, in constant time while the
	// list doesn't change. The index is checked against the list on each use
	// and rebuilt when it's stale (such as when the game is replaced, or a
	// city is founded or destroyed), so it finds just what a search of the
	// list would.
	internal sealed class ListIndex<TKey, T> where T : class {
		private readonly Func<T, TKey> keyOf;
		private readonly object sync = new();
		private List<T> indexed;
		private Dictionary<TKey, (int position, T item)> index;

		public ListIndex(Func<T, TKey> keyOf) {
			this.keyOf = keyOf;
		}

		public T Find(List<T> list, TKey key) {
			if (list == null || key == null) {
				return null;
			}
			lock (sync) {
				if (TryFind(list, key, out T found)) {
					return found;
				}
				// The list has changed since it was indexed, or has nothing
				// with this key.
				Rebuild(list);
				return TryFind(list, key, out found) ? found : null;
			}
		}

		private bool TryFind(List<T> list, TKey key, out T found) {
			found = null;
			if (!ReferenceEquals(list, indexed) || !index.TryGetValue(key, out (int position, T item) entry)) {
				return false;
			}
			if (entry.position >= list.Count || !ReferenceEquals(list[entry.position], entry.item)
				|| !EqualityComparer<TKey>.Default.Equals(keyOf(entry.item), key)) {
				return false;
			}
			found = entry.item;
			return true;
		}

		private void Rebuild(List<T> list) {
			index = new Dictionary<TKey, (int, T)>(list.Count);
			for (int i = 0; i < list.Count; ++i) {
				T item = list[i];
				TKey key = item == null ? default : keyOf(item);
				if (key != null) {
					index.TryAdd(key, (i, item));
				}
			}
			indexed = list;
		}
	}

	private static readonly ListIndex<ID, City> citiesByID = new(c => c.id);
	private static readonly ListIndex<ID, Tech> techsByID = new(t => t.id);
	private static readonly ListIndex<ID, Government> governmentsByID = new(g => g.id);
	private static readonly ListIndex<ID, Terraform> terraformsByID = new(t => t.Id);
	private static readonly ListIndex<string, Civilization> civilizationsByName = new(c => c.name);

	private class CityReferenceConverter : ReferenceConverter<City> {
		protected override string Key(City value) => value.id.ToString();
		protected override City Find(string key) => citiesByID.Find(Game.cities, ID.FromString(key));
	}

	private class MapUnitReferenceConverter : ReferenceConverter<MapUnit> {
		protected override string Key(MapUnit value) => value == MapUnit.NONE ? null : value.id.ToString();
		protected override MapUnit Find(string key) => Game.GetUnit(ID.FromString(key));
	}

	// A tile is written as "x,y", formatted and parsed straight from the
	// JSON's bytes. Reading also takes [x, y].
	private class TileReferenceConverter : JsonConverter<Tile> {
		public override Tile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			int x, y;
			switch (reader.TokenType) {
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.StartArray:
					reader.Read();
					x = reader.GetInt32();
					reader.Read();
					y = reader.GetInt32();
					reader.Read();
					if (reader.TokenType != JsonTokenType.EndArray) {
						throw new JsonException("A tile's coordinates are two numbers");
					}
					break;
				case JsonTokenType.String:
					if (!TryParseCoordinates(ref reader, out x, out y)) {
						throw new JsonException($"\"{reader.GetString()}\" isn't a tile's coordinates");
					}
					break;
				default:
					throw new JsonException($"Expected a tile, got {reader.TokenType}");
			}
			return Game.map.tileAt(x, y);
		}

		private static bool TryParseCoordinates(ref Utf8JsonReader reader, out int x, out int y) {
			x = y = 0;
			ReadOnlySpan<byte> text = reader.HasValueSequence || reader.ValueIsEscaped
				? System.Text.Encoding.UTF8.GetBytes(reader.GetString())
				: reader.ValueSpan;
			int comma = text.IndexOf((byte)',');
			return comma >= 0
				&& Utf8Parser.TryParse(text[..comma], out x, out int xLength) && xLength == comma
				&& Utf8Parser.TryParse(text[(comma + 1)..], out y, out int yLength) && yLength == text.Length - comma - 1;
		}

		public override void Write(Utf8JsonWriter writer, Tile value, JsonSerializerOptions options) {
			if (value == null || value == Tile.NONE) {
				writer.WriteNullValue();
				return;
			}
			Span<byte> text = stackalloc byte[24];
			Utf8Formatter.TryFormat(value.XCoordinate, text, out int xLength);
			text[xLength] = (byte)',';
			Utf8Formatter.TryFormat(value.YCoordinate, text[(xLength + 1)..], out int yLength);
			writer.WriteStringValue(text[..(xLength + 1 + yLength)]);
		}
	}

	private class TechReferenceConverter : ReferenceConverter<Tech> {
		protected override string Key(Tech value) => value.id.ToString();
		protected override Tech Find(string key) => techsByID.Find(Game.techs, ID.FromString(key));
	}

	private class GovernmentReferenceConverter : ReferenceConverter<Government> {
		protected override string Key(Government value) => value.id.ToString();
		protected override Government Find(string key) => governmentsByID.Find(Game.governments, ID.FromString(key));
	}

	private class TerraformReferenceConverter : ReferenceConverter<Terraform> {
		protected override string Key(Terraform value) => value.Id.ToString();
		protected override Terraform Find(string key) => terraformsByID.Find(Game.Terraforms, ID.FromString(key));
	}

	private class CivilizationReferenceConverter : ReferenceConverter<Civilization> {
		protected override string Key(Civilization value) => value.name;
		protected override Civilization Find(string key) => civilizationsByName.Find(Game.civilizations, key);
	}

	private class VictoryReferenceConverter : ReferenceConverter<IVictory> {
		protected override string Key(IVictory value) => Game.victories.IndexOf(value).ToString();
		protected override IVictory Find(string key) {
			int index = int.Parse(key);
			return index >= 0 && index < Game.victories.Count ? Game.victories[index] : null;
		}
	}

	// A path is written as the tiles along it.
	private class TilePathConverter : JsonConverter<TilePath> {
		private readonly TileReferenceConverter tiles = new();

		public override TilePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if (reader.TokenType == JsonTokenType.Null) {
				return null;
			}
			Queue<Tile> path = new();
			reader.Read();
			while (reader.TokenType != JsonTokenType.EndArray) {
				Tile tile = tiles.Read(ref reader, typeof(Tile), options);
				if (tile == null) {
					throw new JsonException("Path contains a tile that isn't on the map");
				}
				path.Enqueue(tile);
				reader.Read();
			}
			return path.Count == 0 ? TilePath.NONE : new TilePath(path.Last(), path);
		}

		public override void Write(Utf8JsonWriter writer, TilePath value, JsonSerializerOptions options) {
			if (value == null) {
				writer.WriteNullValue();
				return;
			}
			writer.WriteStartArray();
			foreach (Tile tile in value.path) {
				tiles.Write(writer, tile, options);
			}
			writer.WriteEndArray();
		}
	}
}
