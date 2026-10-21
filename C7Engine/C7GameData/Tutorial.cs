using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using C7Engine;
using Serilog;

namespace C7GameData {

	// Civ3's tutorial (the `tutorial` module of Civ3Conquests.exe 1.22,
	// 0x5bf440-0x5c1e10): with Tutorial Mode on, the domestic advisor pops up
	// with a tip from text\tutorial.txt the first time something happens, such
	// as a worker standing where it could irrigate. Each tip is shown once per
	// game; Civ3 counts them in the tutorial object it saves with the game
	// (FUN_0059d690 reads it back and turns the tutorial on again, and the main
	// menu turns it off, at 0x55ab57), so this is saved with the game too.
	public class TutorialState {
		// Whether the tutorial is on in this game (Civ3's 0xcbecbc, set once
		// the tutorial text is loaded).
		public bool enabled;

		// The keys of the tips already shown, like TUT_FIRST_MOVE (Civ3 keeps
		// a counter for each).
		public HashSet<string> shown = new();

		public bool HasShown(string key) => shown.Contains(key);
	}

	// A tip to show: its key in tutorial.txt and the values of its numbered
	// variables ($CITY0 is variables[0], $UNIT1 variables[1] and so on).
	public record TutorialTip(string Key, IReadOnlyList<string> Variables) {
		public TutorialTip(string key, params string[] variables) : this(key, (IReadOnlyList<string>)variables) { }
	}

	// When the tips come up, worked out as Civ3 does from the game.
	public static class Tutorial {
		public const string FirstMessage = "TUT_FIRST_MESSAGE";
		public const string FirstMove = "TUT_FIRST_MOVE";
		public const string Irrigate = "TUT_IRRIGATE";
		public const string Mine = "TUT_MINE";
		public const string Road = "TUT_ROAD";
		public const string StrategicResource = "TUT_STRATEGIC_RESOURCE";
		public const string StrategicLuxury = "TUT_STRATEGIC_LUXURY";
		public const string ConnectCities = "TUT_CONNECT_CITIES";
		public const string FirstMilitaryUnit = "TUT_FIRST_MILITARY_UNIT";
		public const string OneCity = "TUT_ONE_CITY";
		public const string CivilDisorder = "TUT_CIVIL_DISORDER";
		public const string FirstCityManagement = "TUT_FIRST_CITY_MANAGEMENT";
		public const string ProductionButton = "TUT_PRODUCTION_BUTTON";
		public const string Shields = "TUT_SHIELDS";
		public const string Citizens = "TUT_CITIZENS";

		// The words of tutorial.txt's VARIABLES section, in their order there
		// (Civ3 FUN_005bf440 reads four lines into the tutorial object).
		public enum Word { River, Lake, Hill, Mountain }

		// The tip for the unit the player has just been given (Civ3
		// FUN_005bf6b0, which the human's turn loop, FUN_004e8540, calls each
		// time it has picked the unit to move). At most one tip comes up at a
		// time, and it's marked shown. words are the VARIABLES of
		// tutorial.txt. Returns null when there's nothing to say.
		public static TutorialTip ForActiveUnit(GameData gameData, MapUnit unit, IReadOnlyList<string> words) {
			TutorialState state = gameData.tutorial;
			if (unit == null || unit == MapUnit.NONE || unit.owner == null || unit.location == null || unit.location == Tile.NONE) {
				return null;
			}
			// Civ3 tells the settler and worker apart by their place in the
			// rules: the unit types numbered 0 and 1 (the Settler and the
			// Worker in Civ3's rules).
			int type = gameData.unitPrototypes.IndexOf(unit.unitType);
			TutorialTip tip;
			if (type == 0) {
				tip = Once(state, FirstMessage, () => new TutorialTip(FirstMessage));
			} else if (type == 1) {
				tip = ForWorker(gameData, unit, words);
			} else {
				// Civ3 FUN_005bf6b0 (unit types' attack is at +0x60). Civ3
				// doesn't set the text's $CITY0 for this tip, so it shows
				// whatever city the last text named; the unit's nearest city
				// is named here.
				tip = unit.unitType.attack != 0
					? Once(state, FirstMilitaryUnit, () => new TutorialTip(FirstMilitaryUnit, NearestCity(unit)?.name ?? ""))
					: null;
				tip ??= Once(state, OneCity, () => {
					// "Player has only one city, and it is size 4+"
					// (tutorial.txt), the city nearest the unit (Civ3
					// FUN_005c0020).
					if (unit.owner.cities.Count != 1) {
						return null;
					}
					City city = NearestCity(unit);
					return city != null && city.residents.Count >= 4 ? new TutorialTip(OneCity, city.name) : null;
				});
			}
			if (tip != null) {
				state.shown.Add(tip.Key);
			}
			return tip;
		}

		private static TutorialTip ForWorker(GameData gameData, MapUnit worker, IReadOnlyList<string> words) {
			TutorialState state = gameData.tutorial;
			if (!state.HasShown(FirstMove)) {
				return new TutorialTip(FirstMove);
			}
			Tile tile = worker.location;

			// Civ3 FUN_005bfa80: where the worker could irrigate, with a city
			// on its landmass.
			TutorialTip tip = Once(state, Irrigate, () => {
				City city = CanDo(gameData, worker, C7Action.UnitIrrigate) ? NearestCity(worker) : null;
				// Civ3 also names the fresh water: "lake" if the tile or one
				// next to it is water in a body of fewer than 21 tiles (Civ3
				// FUN_00603750), here an inland, fresh-water one, and "river"
				// otherwise. The text no longer uses it.
				return city == null ? null
					: new TutorialTip(Irrigate, city.name, WordOf(words, tile.NeighborsFreshWater() ? Word.Lake : Word.River));
			});
			// Civ3 FUN_005bfbe0: where it could mine a hill (terrain 5) or a
			// mountain (terrain 6), with a city on its landmass.
			tip ??= Once(state, Mine, () => {
				if (!CanDo(gameData, worker, C7Action.UnitBuildMine)) {
					return null;
				}
				TerrainType terrain = tile.overlayTerrainType;
				Word? word = terrain.IsMountains ? Word.Mountain : terrain.IsHills ? Word.Hill : null;
				City city = word == null ? null : NearestCity(worker);
				return city == null ? null : new TutorialTip(Mine, WordOf(words, word.Value), city.name);
			});
			// Civ3 FUN_005bfd90: where it could build a road, not in a city
			// (FUN_00579c40, FUN_005f9f10), with a city on its landmass.
			tip ??= Once(state, Road, () => {
				City city = CanDo(gameData, worker, C7Action.UnitBuildRoad) ? NearestCity(worker) : null;
				return city == null ? null : new TutorialTip(Road, city.name);
			});
			// Civ3 FUN_005c07a0 and FUN_005c0620: a strategic resource the
			// player can see on or next to the worker, and the first unit
			// that needs it. Without such a unit the tip waits.
			tip ??= Once(state, StrategicResource, () => {
				Resource resource = ResourceNear(worker, ResourceCategory.STRATEGIC);
				UnitPrototype needing = resource == null ? null
					: gameData.unitPrototypes.FirstOrDefault(p => p.requiredResources.Contains(resource));
				return needing == null ? null : new TutorialTip(StrategicResource, resource.Name, needing.name);
			});
			// Civ3 FUN_005bff00 and FUN_005c08e0: a luxury on or next to the
			// worker, with a city on its landmass.
			tip ??= Once(state, StrategicLuxury, () => {
				Resource luxury = ResourceNear(worker, ResourceCategory.LUXURY);
				City city = luxury == null ? null : NearestCity(worker);
				return city == null ? null : new TutorialTip(StrategicLuxury, luxury.Name, city.name);
			});
			// Civ3 FUN_005bf6b0: once the player has more than one city.
			tip ??= Once(state, ConnectCities, () => worker.owner.cities.Count > 1 ? new TutorialTip(ConnectCities) : null);
			return tip;
		}

		// The tip for a city of the player's in civil disorder, the first
		// time one is (Civ3 FUN_005c0140, called from the city's disorder
		// check, FUN_004c5600, when its unhappy citizens outnumber its happy
		// ones). Asked at the start of the player's turn, after the turn's
		// disorder check.
		public static TutorialTip ForCivilDisorder(GameData gameData, Player player) {
			TutorialState state = gameData.tutorial;
			if (player == null || state.HasShown(CivilDisorder)) {
				return null;
			}
			City city = player.cities.FirstOrDefault(c => c.isInCivilDisorder);
			if (city == null) {
				return null;
			}
			state.shown.Add(CivilDisorder);
			return new TutorialTip(CivilDisorder, city.name);
		}

		// The tips for the city screen, the first time it's opened: the four
		// come one after another (Civ3 FUN_005c01d0, called as the city
		// screen opens, FUN_00422bf0). Empty once they've been shown.
		public static List<TutorialTip> ForCityScreen(GameData gameData) {
			TutorialState state = gameData.tutorial;
			if (state.HasShown(FirstCityManagement)) {
				return [];
			}
			List<TutorialTip> tips = [
				new(FirstCityManagement),
				new(ProductionButton),
				new(Shields),
				new(Citizens),
			];
			foreach (TutorialTip tip in tips) {
				state.shown.Add(tip.Key);
			}
			return tips;
		}

		// The tip made by make, if the one of that key hasn't been shown.
		private static TutorialTip Once(TutorialState state, string key, Func<TutorialTip> make) {
			return state.HasShown(key) ? null : make();
		}

		private static bool CanDo(GameData gameData, MapUnit worker, string action) {
			Terraform terraform = gameData.Terraforms.FirstOrDefault(t => t?.UIAction == action);
			return terraform != null && worker.CanPerformTerraformAction(terraform);
		}

		// The nearest of the unit owner's cities on the unit's landmass (Civ3
		// FUN_005799c0, given the tile's continent), the first of them on a
		// tie, or null.
		private static City NearestCity(MapUnit unit) {
			Tile tile = unit.location;
			City nearest = null;
			int best = int.MaxValue;
			foreach (City city in unit.owner.cities) {
				if (city.location == null || city.location.continent != tile.continent) {
					continue;
				}
				int distance = Civ3Distance(tile, city.location);
				if (distance < best) {
					best = distance;
					nearest = city;
				}
			}
			return nearest;
		}

		// FUN_005799c0's measure of distance: the larger of the (wrapped) x
		// and y differences, less half of how far their mean exceeds the
		// smaller, rounded up.
		private static int Civ3Distance(Tile a, Tile b) {
			int dx = Math.Abs(a.map.CalculateXDelta(a.XCoordinate, b.XCoordinate));
			int dy = Math.Abs(a.map.CalculateYDelta(a.YCoordinate, b.YCoordinate));
			int larger = Math.Max(dx, dy);
			int smaller = Math.Min(dx, dy);
			return larger - ((dx + dy) / 2 - smaller + 1) / 2;
		}

		// The first resource of the category (Civ3's resource type 1 for a
		// luxury, 2 for a strategic one, FUN_005f2f80 and FUN_005f2f60) on
		// the unit's tile or next to it whose tech the owner knows (Civ3
		// scans the tile and the eight around it, FUN_005f66a0).
		private static Resource ResourceNear(MapUnit unit, ResourceCategory category) {
			IEnumerable<Tile> tiles = new[] { unit.location }.Concat(unit.location.neighbors.Values);
			foreach (Tile tile in tiles) {
				Resource resource = tile?.Resource;
				if (resource == null || resource == Resource.NONE || resource.Category != category) {
					continue;
				}
				if (resource.Prerequisite == null || unit.owner.HasTech(resource.Prerequisite)) {
					return resource;
				}
			}
			return null;
		}

		private static string WordOf(IReadOnlyList<string> words, Word word) {
			return words != null && (int)word < words.Count ? words[(int)word] : "";
		}
	}

	// One tip of tutorial.txt.
	public class TutorialEntry {
		public string Key;
		// The #ADVISOR line's advisor and mood, like "Domestic" and "happy".
		public string Advisor = "Domestic";
		public string Mood = "happy";
		// The lines, read as the Civilopedia's are: each ^ starts a line,
		// and lines without one continue the line before. The markup
		// ({bold}, [bold], $LINK<text=key>) and variables ($CITY0) are kept.
		public List<string> Lines = new();
	}

	// Civ3's text\tutorial.txt: the tips by key, and the words of its
	// VARIABLES section (Civ3 FUN_005bf440).
	public class TutorialText {
		private static readonly ILogger log = Log.ForContext<TutorialText>();

		private readonly Dictionary<string, TutorialEntry> entries = new(StringComparer.OrdinalIgnoreCase);

		public readonly List<string> Variables = new();

		public int Count => entries.Count;

		public TutorialEntry Get(string key) => key == null ? null : entries.GetValueOrDefault(key.Trim());

		// Reads a tutorial.txt, which is Windows-1252 text. A missing or
		// unreadable file gives no tips rather than an error.
		public static TutorialText Load(string path) {
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
				log.Warning("No tutorial text at {path}", path);
				return new TutorialText();
			}
			try {
				return Parse(File.ReadAllLines(path, QueryCiv3.Util.Civ3Encoding));
			} catch (Exception e) {
				log.Error(e, "Could not read the tutorial text at {path}", path);
				return new TutorialText();
			}
		}

		public static TutorialText Parse(string text) {
			return Parse(text.Replace("\r\n", "\n").Split('\n'));
		}

		public static TutorialText Parse(IEnumerable<string> fileLines) {
			TutorialText result = new();
			TutorialEntry entry = null;
			bool inVariables = false;

			foreach (string rawLine in fileLines) {
				string line = rawLine.TrimEnd('\r', '\n');
				if (line.StartsWith('#')) {
					string directive = line.Substring(1).Trim();
					string word = directive.Split(' ', 2)[0];
					switch (word.ToUpperInvariant()) {
						case "ADVISOR":
							if (entry != null) {
								string[] parts = directive.Split(' ', StringSplitOptions.RemoveEmptyEntries);
								entry.Advisor = parts.Length > 1 ? parts[1] : entry.Advisor;
								entry.Mood = parts.Length > 2 ? parts[2] : entry.Mood;
							}
							continue;
						case "TEXT":
						case "X":
						case "Y":
							continue;
						case "COMMENT":
						case "":
							entry = null;
							inVariables = false;
							continue;
						case "VARIABLES":
							entry = null;
							inVariables = true;
							continue;
					}
					inVariables = false;
					entry = new TutorialEntry { Key = directive };
					result.entries[directive] = entry;
					continue;
				}

				if (inVariables) {
					if (line.Trim().Length > 0) {
						result.Variables.Add(line.Trim());
					}
					continue;
				}
				if (entry == null || line.Trim().Length == 0) {
					continue;
				}
				List<string> lines = entry.Lines;
				if (line.StartsWith('^')) {
					lines.Add(line.Substring(1).TrimEnd());
				} else if (lines.Count == 0) {
					lines.Add(line.Trim());
				} else {
					string previous = lines[^1];
					lines[^1] = previous.Length == 0 ? line.Trim() : previous.TrimEnd() + " " + line.Trim();
				}
			}

			foreach (TutorialEntry e in result.entries.Values) {
				while (e.Lines.Count > 0 && e.Lines[^1].Trim().Length == 0) {
					e.Lines.RemoveAt(e.Lines.Count - 1);
				}
			}
			return result;
		}

		// A variable is a name ending in the number of the value it takes,
		// like $CITY0 or $HILL_OR_MTN0 (Civ3 FUN_0063e390 sets value 0, 1...).
		private static readonly Regex VariablePattern = new(@"\$[A-Za-z_]+(\d)", RegexOptions.Compiled);

		// The tip's lines with its variables filled in, its markup kept; or
		// null if tutorial.txt has no such tip.
		public List<string> LinesOf(TutorialTip tip) {
			TutorialEntry entry = Get(tip.Key);
			if (entry == null) {
				return null;
			}
			return entry.Lines.ConvertAll(line => VariablePattern.Replace(line, match => {
				int index = match.Groups[1].Value[0] - '0';
				return index < tip.Variables.Count ? tip.Variables[index] ?? "" : "";
			}));
		}

		// The tip as plain text: its lines without markup, one to a line.
		public string PlainTextOf(TutorialTip tip) {
			List<string> lines = LinesOf(tip);
			return lines == null ? null : string.Join("\n", lines.Select(CivilopediaMarkup.PlainText));
		}
	}
}
