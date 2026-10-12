using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace C7GameData {

	// The description of one unit art's animations that's saved with our own
	// unit art (C7/ModernArt/Art/Units/<art>/<art>.json, written by
	// ArtGen/units/metadata.py). In standalone mode, where there are no Civ3
	// files, the game reads it in place of the art's .INI and the headers of
	// its .flc files, and draws the unit from the sheets next to it (see
	// ModernGraphics in the C7 project).
	//
	//   {
	//     "actions": {"DEFAULT": "WarriorDefault", "RUN": "WarriorRun", ...},
	//     "flics": {
	//       "WarriorDefault": {"size": [w, h], "directions": 8, "frames": 10,
	//                          "frameMs": 66, "offset": [left, top], "canvas": [240, 240]},
	//       ...
	//     }
	//   }
	//
	// Like an INI, "actions" names the flic of each action (the sheets are
	// <flic>.png and <flic>_tint.png). Each flic gives what the game reads
	// from a flic's header: the size of its frames, its number of directions
	// (rows, in Civ3's order SW, S, SE, E, NE, N, NW, W) and of frames per
	// direction, the time per frame and where the frame sits on the canvas
	// that units are placed with.
	public sealed class UnitArtMetadata {
		public sealed class Flic {
			public int Width { get; init; }
			public int Height { get; init; }
			public int Directions { get; init; }
			public int Frames { get; init; }
			public int FrameMs { get; init; }
			public int OffsetLeft { get; init; }
			public int OffsetTop { get; init; }
			public int CanvasWidth { get; init; }
			public int CanvasHeight { get; init; }

			// How long the whole animation lasts, in milliseconds.
			public int DurationMs => FrameMs * Frames;
		}

		public const string DefaultAction = "DEFAULT";

		// The flic name by action, actions ignoring case.
		private readonly Dictionary<string, string> actions = new(StringComparer.OrdinalIgnoreCase);
		// The flics by name, ignoring case like file names.
		private readonly Dictionary<string, Flic> flics = new(StringComparer.OrdinalIgnoreCase);

		public IReadOnlyDictionary<string, string> Actions => actions;
		public IReadOnlyDictionary<string, Flic> Flics => flics;

		private UnitArtMetadata() { }

		public static UnitArtMetadata Load(string path) {
			return Parse(File.ReadAllText(path), path);
		}

		// Parses the metadata; `source` names it in errors.
		public static UnitArtMetadata Parse(string json, string source = "unit art metadata") {
			UnitArtMetadata result = new();
			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				foreach (JsonProperty flic in root.GetProperty("flics").EnumerateObject()) {
					JsonElement f = flic.Value;
					JsonElement size = f.GetProperty("size"), offset = f.GetProperty("offset"), canvas = f.GetProperty("canvas");
					result.flics[flic.Name] = new Flic {
						Width = size[0].GetInt32(),
						Height = size[1].GetInt32(),
						Directions = f.GetProperty("directions").GetInt32(),
						Frames = f.GetProperty("frames").GetInt32(),
						FrameMs = f.GetProperty("frameMs").GetInt32(),
						OffsetLeft = offset[0].GetInt32(),
						OffsetTop = offset[1].GetInt32(),
						CanvasWidth = canvas[0].GetInt32(),
						CanvasHeight = canvas[1].GetInt32(),
					};
				}
				foreach (JsonProperty action in root.GetProperty("actions").EnumerateObject()) {
					string flicName = action.Value.GetString();
					if (string.IsNullOrEmpty(flicName)) {
						continue;
					}
					if (!result.flics.ContainsKey(flicName)) {
						throw new FormatException($"action {action.Name} names the flic {flicName}, which isn't described");
					}
					result.actions[action.Name] = flicName;
				}
			} catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or FormatException) {
				throw new FormatException($"Invalid unit art metadata in {source}: {e.Message}", e);
			}
			if (!result.actions.ContainsKey(DefaultAction)) {
				throw new FormatException($"Invalid unit art metadata in {source}: no {DefaultAction} animation");
			}
			foreach ((string name, Flic flic) in result.flics) {
				if (flic.Width <= 0 || flic.Height <= 0 || flic.Directions <= 0 || flic.Frames <= 0) {
					throw new FormatException($"Invalid unit art metadata in {source}: flic {name} has no frames");
				}
			}
			return result;
		}

		// The flic an action names, or null if it has none of its own.
		public string FlicNameOf(string action) {
			return actions.TryGetValue(action, out string name) ? name : null;
		}

		// The flic an action is drawn with: its own, or else the DEFAULT one,
		// as the game does with a Civ3 INI.
		public string FlicNameOrDefault(string action) {
			return FlicNameOf(action) ?? actions[DefaultAction];
		}

		public Flic GetFlic(string name) {
			return flics[name];
		}

		// The column of a sheet with `columns` columns that shows frame
		// `frame` of an animation of `frames` frames. A sheet may have fewer
		// columns than the animation has frames: a still has one, which
		// stands for every frame, and a move cycle or victory has every
		// other frame, each column standing for the frames up to the next
		// column's.
		public static int SheetColumn(int frame, int frames, int columns) {
			if (columns >= frames || frames <= 0) {
				return frame;
			}
			if (columns <= 1) {
				return 0;
			}
			return Math.Clamp(frame * columns / frames, 0, columns - 1);
		}
	}
}
