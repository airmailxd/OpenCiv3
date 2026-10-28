using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// MDJ (owner's choice, 2026-10-28): the rules of MPTournamentMDJ.biq, a
// multiplayer tournament scenario of Medieval Japan that ships with Civ3
// Complete (Conquests/Scenarios), built on the Sengoku conquest: its units,
// advances, civilizations, resources and buildings, on a generated map.
// Every civ starts knowing every advance, under Feudalism (the scenario's
// only government but Anarchy), which no one can leave, and a resource a
// civ has once had stays its own for the rest of the game, though it be cut
// off (see PersistentResources). Rising Tide is played under these rules
// when Civ3 is installed (owner's choice, 2026-10-28).
public partial class GameSetup {
	public const string MdjBiqName = "MPTournamentMDJ.biq";
	public const string MdjGovernment = "Feudalism";

	// Where MPTournamentMDJ.biq is in the Civ3 install, or null if it isn't
	// there.
	public static string MdjBiqPath(string civ3Path) {
		if (string.IsNullOrEmpty(civ3Path)) {
			return null;
		}
		string path = Path.Join(civ3Path, "Conquests", "Scenarios", MdjBiqName);
		return File.Exists(path) ? path : null;
	}

	// The MDJ rules, ready for Populate to make a map and players for, or
	// null without the BIQ. The scenario names its art folders (its search
	// folders: the Sengoku conquest's, and PTW's Medieval Japan extras)
	// relative to the Scenarios folder; its PediaIcons are the first of
	// them that has one.
	public static SaveGame LoadMdjRules(string civ3Path) {
		string biq = MdjBiqPath(civ3Path);
		if (biq == null) {
			return null;
		}
		string scenarios = Path.GetDirectoryName(biq);
		string defaultBiq = Path.Join(civ3Path, "Conquests", "conquests.biq");
		SaveGame save = ImportCiv3.ImportBiq(biq, defaultBiq, searchFolders => PediaIconsFor(civ3Path, scenarios, searchFolders));
		save.ScenarioName = Path.GetFileNameWithoutExtension(biq);
		SetUpMdjRules(save);
		return save;
	}

	// The PediaIcons file of the first search folder that has one, or
	// Conquests' own.
	internal static string PediaIconsFor(string civ3Path, string scenarios, string searchFolders) {
		foreach (string folder in (searchFolders ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
			string relative = folder.Replace('\\', Path.DirectorySeparatorChar);
			if (FindIgnoringCase(Path.GetFullPath(Path.Combine(scenarios, relative)), "Text", "PediaIcons.txt") is string path) {
				return path;
			}
		}
		return Path.Join(civ3Path, "Conquests", "Text", "PediaIcons.txt");
	}

	// The file under the folder by the given names, whatever their case
	// (Civ3's folders differ in case from the names BIQs give them), or
	// null.
	private static string FindIgnoringCase(string folder, params string[] names) {
		string at = folder;
		foreach (string name in names) {
			if (!Directory.Exists(at)) {
				return null;
			}
			string match = Directory.EnumerateFileSystemEntries(at).FirstOrDefault(e => string.Equals(Path.GetFileName(e), name, StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				return null;
			}
			at = match;
		}
		return File.Exists(at) ? at : null;
	}

	// The rules MDJ adds to the BIQ's.
	public static void SetUpMdjRules(SaveGame save) {
		save.Rules.AllTechsAtStart = true;
		save.Rules.OnlyGovernment = MdjGovernment;
		save.Rules.PersistentResources = true;
		save.Rules.GovernmentLocked = true;
	}

	// Every advance, and the era of the latest of them.
	private static void GiveAllTechs(SaveGame save, SavePlayer player) {
		player.knownTechs.UnionWith(save.Techs.Select(t => t.id));
		int era = save.Techs.Max(t => EraUtils.GetEraIndex(t.EraCivilopediaName));
		if (era > EraUtils.GetEraIndex(player.eraCivilopediaName)) {
			player.eraCivilopediaName = EraUtils.EraIndexToEra(era);
		}
	}

	// The government everyone starts in: the only one the rules allow, if
	// they allow only one, or the default.
	private ID StartingGovernment(SaveGame save) {
		if (save.Rules.OnlyGovernment is string only && save.Governments.Find(g => g.name == only) is Government government) {
			return government.id;
		}
		return worldCharacteristics.defaultGovernment.id;
	}
}
