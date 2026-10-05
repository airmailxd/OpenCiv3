using System;
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

	private class CityReferenceConverter : ReferenceConverter<City> {
		protected override string Key(City value) => value.id.ToString();
		protected override City Find(string key) {
			ID id = ID.FromString(key);
			return Game.cities.Find(c => c.id == id);
		}
	}

	private class MapUnitReferenceConverter : ReferenceConverter<MapUnit> {
		protected override string Key(MapUnit value) => value == MapUnit.NONE ? null : value.id.ToString();
		protected override MapUnit Find(string key) => Game.GetUnit(ID.FromString(key));
	}

	private class TileReferenceConverter : ReferenceConverter<Tile> {
		protected override string Key(Tile value) => value == Tile.NONE ? null : $"{value.XCoordinate},{value.YCoordinate}";
		protected override Tile Find(string key) {
			string[] coordinates = key.Split(',');
			return Game.map.tileAt(int.Parse(coordinates[0]), int.Parse(coordinates[1]));
		}
	}

	private class TechReferenceConverter : ReferenceConverter<Tech> {
		protected override string Key(Tech value) => value.id.ToString();
		protected override Tech Find(string key) => Game.GetTech(ID.FromString(key));
	}

	private class GovernmentReferenceConverter : ReferenceConverter<Government> {
		protected override string Key(Government value) => value.id.ToString();
		protected override Government Find(string key) {
			ID id = ID.FromString(key);
			return Game.governments.Find(g => g.id == id);
		}
	}

	private class TerraformReferenceConverter : ReferenceConverter<Terraform> {
		protected override string Key(Terraform value) => value.Id.ToString();
		protected override Terraform Find(string key) {
			ID id = ID.FromString(key);
			return Game.Terraforms.Find(t => t.Id == id);
		}
	}

	private class CivilizationReferenceConverter : ReferenceConverter<Civilization> {
		protected override string Key(Civilization value) => value.name;
		protected override Civilization Find(string key) => Game.civilizations.Find(c => c.name == key);
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
