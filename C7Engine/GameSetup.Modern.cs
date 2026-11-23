using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using C7GameData;
using C7GameData.Save;
using QueryCiv3;

namespace C7Engine;

// What Modern's rules (see GameSetup.BundledRules.cs) need over the import
// of Modern_2.4.biq: its buildings named as the mod names them. Done as the
// BIQ is imported (ImportBundledBiq), so the bundled file has them, and
// BundledScenariosTest can check it against the BIQ. The import itself
// keeps only the BIQ's playable civilizations (those with a Civ Tech but
// Vietnam; Rome, the Spectators and the others are left over in the BIQ),
// and resolves the buildings' references to each other by BIQ index.
//
// How Modern plays, from the mod's own Civilopedia and the League's notes:
// - Everyone starts in the Industrial Age under "!Switch Governments!", a
//   placeholder, and is to revolt on the first turn ("In modern, you need
//   to switch governments on the first turn of the game") for Equality,
//   Tradition, Freedom or Order. Nothing forces it: the placeholder is just
//   poor (rampant corruption, no buildings of its own). Starting in the
//   Industrial Age gives every advance of the earlier eras, which are the
//   four governments' (see ApplyScenarioStart), so all four are there from
//   the start. All civs are religious, so the anarchy is short.
// - A government's buildings and small wonders carry its initial (the
//   "O Barracks", "T Granary"), and the BIQ ties each to its government
//   with the building's required government, not with an advance.
// - Each of the 18 hidden "Civ Tech" advances is one civilization's own,
//   known from the start. Tradition's and Order's second wonders put a
//   building in every city or grant advances, which only a great wonder
//   does, so their second parts are great wonders, one copy for each Civ
//   Tech: each civilization builds its own, and no one else can take it.
public partial class GameSetup {
	// Modern's new units, by name without the mod's note in brackets ("Super
	// Trooper (+2 HP)"), and the art OpenCiv draws each with in standalone
	// mode. The mod draws them with stock art names (the Super Trooper is
	// "Samurai") under which it ships pictures of its own; OpenCiv's art for
	// those names is the stock unit's, so each has its own art instead:
	// C7/ModernArt/Art/Units/<art>, made by ArtGen/units (catalog/modern_mod_*.py),
	// and its Civilopedia icon Art/Civilopedia/Icons/Units/<art>_large.png
	// (ArtGen/civilopedia_icons/units_modern.py). Their Civilopedia entries
	// are still the stock units' (PRTO_Samurai).
	internal static readonly Dictionary<string, string> ModernUnitArt = new() {
		["Super Trooper"] = "Super Trooper",
		["Laser Tank"] = "Laser Tank",
		["Bradley"] = "Bradley",
		["Convoy"] = "Convoy",
		["Iron Dome"] = "Iron Dome",
		["Battalion"] = "Battalion",
		["Combat Engineer"] = "Combat Engineer",
		["RPG Trooper"] = "RPG Trooper",
		["Insurgent"] = "Insurgent",
		["Rebel"] = "Rebel",
		// Militant keeps the stock Knight look: its own art did not reach the bar (2026-11-23).
		["Militia"] = "Militia",
		["Skirmisher"] = "Skirmisher",
		["Pirate"] = "Pirate",
		["Troop Ship"] = "Troop Ship",
		["Fighter/Bomber"] = "Fighter Bomber",
	};

	// Points Modern's new units at their own art (ModernUnitArt), in
	// standalone mode, where that art is all there is; Civ3 mode keeps the
	// stock names, which Civ3's files have.
	internal static void UseModernUnitArt(SaveGame save) {
		foreach (SaveUnitPrototype unit in save.UnitPrototypes) {
			string name = Regex.Replace(unit.name ?? "", @"\s*\(.*\)$", "");
			if (!ModernUnitArt.TryGetValue(name, out string art)) {
				continue;
			}
			string stem = Regex.Replace(art, "[^A-Za-z0-9]+", "_");
			Art unitArt = unit.art;
			unitArt.mainArt = new MainArt { defaultName = art };
			unitArt.pediaArt = new PediaArt {
				large = $"Art/Civilopedia/Icons/Units/{stem}_large.png",
				small = $"Art/Civilopedia/Icons/Units/{stem}_small.png",
			};
			unit.art = unitArt;
		}
	}

	// The import tells apart buildings the BIQ names alike (see
	// ImportCiv3.MakeBuildingNamesUnique) by the government ("Barracks
	// (Freedom)") or a number ("O Experiments 15"). Named as the mod names
	// the others:
	// - a government's building starts with its initial ("F Barracks",
	//   "T Marketplace");
	// - a wonder's second part is named after its first and the civilization
	//   whose Civ Tech it needs ("T Service Part 2 (America)"), from the
	//   BIQ's civilizations: Vietnam's copies too, though Vietnam doesn't
	//   play.
	// The barracks Sun Tzu's gives every city on its continent, which needs
	// a wonder no one can build and no government, keeps the name
	// "Barracks".
	internal static void RenameModernBuildings(SaveGame save, BiqData biq) {
		Dictionary<string, string> renamed = new();
		HashSet<string> taken = save.Buildings.Select(b => b.name).ToHashSet();
		void Rename(SaveBuilding building, string name) {
			if (name == building.name || taken.Contains(name)) {
				return;
			}
			taken.Remove(building.name);
			taken.Add(name);
			renamed[building.name] = name;
			building.name = name;
		}

		// The civilization whose Civ Tech each hidden advance is, by the
		// BIQ's RACE list.
		Dictionary<ID, string> civTechs = new();
		foreach (QueryCiv3.Biq.RACE race in biq.Race ?? []) {
			foreach (int tech in new[] { race.FreeTech1, race.FreeTech2, race.FreeTech3, race.FreeTech4 }) {
				if (tech >= 0 && tech < save.Techs.Count) {
					civTechs.TryAdd(save.Techs[tech].id, race.Name);
				}
			}
		}

		// The second parts first: they need a government too.
		HashSet<ID> hidden = save.Techs.Where(t => t.EraCivilopediaName == EraUtils.HIDDEN_CVLPD).Select(t => t.id).ToHashSet();
		foreach (SaveBuilding part2 in save.Buildings.Where(b => b.requiredTech != null && hidden.Contains(b.requiredTech) && b.requiredBuilding != null)) {
			string part1 = Regex.Replace(part2.requiredBuilding, @"( Pt1)?( \(.*)?$", "");
			if (civTechs.TryGetValue(part2.requiredTech, out string civ)) {
				Rename(part2, $"{part1} Part 2 ({civ})");
			}
		}

		foreach (SaveBuilding building in save.Buildings.Where(b => b.requiredGovernment != null)) {
			Government government = save.Governments.Find(g => g.id == building.requiredGovernment);
			if (government == null || string.IsNullOrEmpty(government.name) || building.requiredTech != null && hidden.Contains(building.requiredTech)) {
				continue;
			}
			string initial = government.name[..1] + " ";
			if (building.name.StartsWith(initial)) {
				continue;
			}
			string bare = building.name.EndsWith($" ({government.name})") ? building.name[..^(government.name.Length + 3)] : building.name;
			Rename(building, initial + bare);
		}

		// A slip in the BIQ: "T Guard (+5 GPT, Marine/4t))".
		foreach (SaveBuilding building in save.Buildings.Where(b => b.name.EndsWith("))") && b.name.Count(ch => ch == ')') > b.name.Count(ch => ch == '('))) {
			Rename(building, building.name[..^1]);
		}

		// What referred to a renamed building by its old name, which was
		// its own (the import's names are unique).
		string Renamed(string name) => name != null && renamed.TryGetValue(name, out string now) ? now : name;
		foreach (SaveBuilding building in save.Buildings) {
			building.requiredBuilding = Renamed(building.requiredBuilding);
			building.doublesHappinessOf = Renamed(building.doublesHappinessOf);
			if (building.greatWonderProperties is SaveBuilding.GreatWonderProperties wonder) {
				wonder.buildingGainedInEveryCity = Renamed(wonder.buildingGainedInEveryCity);
				wonder.buildingGainedInEveryCityOnContinent = Renamed(wonder.buildingGainedInEveryCityOnContinent);
			}
		}
	}
}
