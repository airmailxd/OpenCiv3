using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C7GameData {

	// The sections of the Civilopedia, in the order of its main menu.
	public enum CivilopediaSection {
		Main,
		Civilizations,
		Advances,
		GreatWonders,
		SmallWonders,
		Improvements,
		Governments,
		Units,
		WorkerActions,
		Terrain,
		Resources,
		GameConcepts,
		Index,
	}

	// One page of the Civilopedia: the article for something in the game's
	// rules (a unit, an advance, ...) or for a game concept.
	public class CivilopediaEntry {
		public string Key;
		public string Name;
		public CivilopediaSection Section;

		// The article from Civilopedia.txt, or null if the file doesn't have
		// one (e.g. for a unit only a scenario has).
		public CivilopediaArticle Article;

		// What the entry is about; at most one of these is set.
		public Tech Tech;
		public UnitPrototype Unit;
		public Building Building;
		public Government Government;
		public TerrainType Terrain;
		public Resource Resource;
		public Terraform Terraform;
		public Civilization Civilization;

		public override string ToString() => $"{Section} {Name} ({Key})";
	}

	// The Civilopedia's entries for a game: everything in its rules, plus the
	// game concepts and the other articles of Civilopedia.txt, sorted into
	// the sections of the Civilopedia.
	public class CivilopediaCatalog {
		public static readonly CivilopediaSection[] MenuSections = {
			CivilopediaSection.Civilizations,
			CivilopediaSection.Advances,
			CivilopediaSection.GreatWonders,
			CivilopediaSection.SmallWonders,
			CivilopediaSection.Improvements,
			CivilopediaSection.Governments,
			CivilopediaSection.Units,
			CivilopediaSection.WorkerActions,
			CivilopediaSection.Terrain,
			CivilopediaSection.Resources,
			CivilopediaSection.GameConcepts,
			CivilopediaSection.Index,
		};

		// The names the Civilopedia's main menu gives its sections.
		public static string SectionName(CivilopediaSection section) => section switch {
			CivilopediaSection.Main => "Main",
			CivilopediaSection.Civilizations => "Tribes",
			CivilopediaSection.Advances => "Civilization Advances",
			CivilopediaSection.GreatWonders => "Great Wonders",
			CivilopediaSection.SmallWonders => "Small Wonders",
			CivilopediaSection.Improvements => "City Improvements",
			CivilopediaSection.Governments => "Governments",
			CivilopediaSection.Units => "Units",
			CivilopediaSection.WorkerActions => "Worker Actions",
			CivilopediaSection.Terrain => "Terrain",
			CivilopediaSection.Resources => "Resources",
			CivilopediaSection.GameConcepts => "Game Concepts",
			CivilopediaSection.Index => "Index",
			_ => section.ToString(),
		};

		public readonly CivilopediaText Text;

		private readonly Dictionary<string, CivilopediaEntry> byKey = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<object, CivilopediaEntry> bySubject = new(ReferenceEqualityComparer.Instance);
		private readonly Dictionary<CivilopediaSection, List<CivilopediaEntry>> bySection = new();
		private List<CivilopediaEntry> all = new();

		// The game's rules, for working out what leads to what.
		private readonly List<Tech> techs = new();
		private readonly List<UnitPrototype> units = new();
		private readonly List<Building> buildings = new();
		private readonly List<Government> governments = new();
		private readonly List<Resource> resources = new();
		private readonly List<Terraform> terraforms = new();
		private readonly List<TerrainType> terrains = new();
		private readonly List<TerrainImprovement> improvements = new();

		private CivilopediaCatalog(CivilopediaText text) {
			Text = text ?? new CivilopediaText();
			foreach (CivilopediaSection section in Enum.GetValues<CivilopediaSection>()) {
				bySection[section] = new List<CivilopediaEntry>();
			}
		}

		public static CivilopediaCatalog Build(GameData gameData, CivilopediaText text) {
			CivilopediaCatalog catalog = new(text);
			catalog.AddRules(gameData);
			catalog.AddOtherArticles();
			catalog.Sort();
			return catalog;
		}

		// The entry with this key, or null.
		public CivilopediaEntry Get(string key) {
			return key == null ? null : byKey.GetValueOrDefault(key.Trim());
		}

		// The entry for a tech, unit, building, government, terrain type,
		// resource, worker job or civilization of the game, or null.
		public CivilopediaEntry For(object subject) {
			return subject == null ? null : bySubject.GetValueOrDefault(subject);
		}

		// The entries of a section in alphabetical order. The index has every
		// entry.
		public IReadOnlyList<CivilopediaEntry> In(CivilopediaSection section) {
			return section == CivilopediaSection.Index ? all : bySection[section];
		}

		public IReadOnlyList<CivilopediaEntry> All => all;

		// The advances that need this one.
		public List<Tech> LeadsTo(Tech tech) {
			return techs.Where(t => t.Prerequisites.Contains(tech)).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
		}

		// What an advance makes possible: units, buildings, governments,
		// worker jobs, and the resources it reveals.
		public List<CivilopediaEntry> AllowedBy(Tech tech) {
			List<CivilopediaEntry> result = new();
			void Add(object subject) {
				CivilopediaEntry entry = For(subject);
				if (entry != null && !result.Contains(entry)) {
					result.Add(entry);
				}
			}
			foreach (UnitPrototype unit in units.Where(u => u.requiredTech == tech)) {
				Add(unit);
			}
			foreach (Building building in buildings.Where(b => b.requiredTech == tech)) {
				Add(building);
			}
			foreach (Government government in governments.Where(g => g.prerequisiteTech != null && g.prerequisiteTech == tech.id)) {
				Add(government);
			}
			foreach (Terraform terraform in terraforms.Where(t => t.RequiredTech != null && t.RequiredTech == tech.id)) {
				Add(terraform);
			}
			foreach (Resource resource in resources.Where(r => r.Prerequisite != null && r.Prerequisite == tech.id)) {
				Add(resource);
			}
			return result;
		}

		// The buildings an advance makes obsolete.
		public List<CivilopediaEntry> ObsoletedBy(Tech tech) {
			return buildings.Where(b => b.renderedObsoleteBy == tech).Select(For).Where(e => e != null).ToList();
		}

		public Tech TechById(ID id) {
			return id == null ? null : techs.Find(t => t.id == id);
		}

		// The units that can do a worker job.
		public List<UnitPrototype> UnitsThatCanDo(Terraform terraform) {
			return units.Where(u => u.terraformActions.Contains(terraform)).ToList();
		}

		// The terrain types a resource can appear on.
		public List<TerrainType> TerrainsWith(Resource resource) {
			return terrains.Where(t => t.allowedResources.Contains(resource.Key)).ToList();
		}

		public List<Resource> ResourcesOn(TerrainType terrain) {
			return resources.Where(r => terrain.allowedResources.Contains(r.Key)).ToList();
		}

		// What an improvement, like "mine", adds to a terrain type's yield.
		public int ImprovementBonus(TerrainType terrain, string improvementKey, Tile.YieldType yield) {
			TerrainImprovement improvement = improvements.Find(i => i.key == improvementKey);
			return improvement?.GetYieldBonus(terrain, yield) ?? 0;
		}

		// The units only one civilization can build, which replace a normal
		// unit for it.
		public List<UnitPrototype> UniqueUnitsOf(Civilization civ) {
			return units.Where(u => u.IsProducibleBy(civ) && u.producibleBy.Count == 1).ToList();
		}

		// The units that upgrade to this one.
		public List<UnitPrototype> UpgradesFrom(UnitPrototype unit) {
			return units.Where(u => u.upgradesTo.Contains(unit)).ToList();
		}

		// "Bronze_Working" -> "Bronze Working", for articles without a name.
		public static string NameFromKey(string key) {
			int underscore = key.IndexOf('_');
			string name = underscore >= 0 ? key.Substring(underscore + 1) : key;
			return name.Replace('_', ' ').Trim();
		}

		// A key made from a name, like PRTO_Jaguar_Warrior, for rules that
		// don't give one; Civ3's own keys are made this way.
		public static string KeyFromName(string prefix, string name) {
			StringBuilder key = new(prefix);
			foreach (char c in name.Trim()) {
				key.Append(c == ' ' || c == '-' ? '_' : c);
			}
			return key.ToString();
		}

		private void AddRules(GameData gameData) {
			techs.AddRange(gameData.techs.Where(t => t != null));
			units.AddRange(gameData.unitPrototypes.Where(u => u != null));
			buildings.AddRange(gameData.Buildings.Where(b => b != null));
			governments.AddRange(gameData.governments.Where(g => g != null));
			resources.AddRange(gameData.Resources.Where(r => r != null && r != Resource.NONE));
			terraforms.AddRange(gameData.Terraforms.Where(t => t != null));
			terrains.AddRange(gameData.terrainTypes.Where(t => t != null && t != TerrainType.NONE));
			improvements.AddRange(gameData.terrainImprovements.Where(i => i != null));

			foreach (Civilization civ in gameData.civilizations.Where(c => c != null && !c.IsIndependent)) {
				string key = civ.civilopediaEntry ?? FindArticleKey("RACE_", civ.noun, civ.name, civ.adjective);
				Add(new CivilopediaEntry { Key = key, Name = civ.noun ?? civ.name, Section = CivilopediaSection.Civilizations, Civilization = civ }, civ);
			}
			foreach (Tech tech in techs) {
				// Techs of era -1 are hidden ones Civ3 doesn't show.
				if (tech.EraCivilopediaName == EraUtils.HIDDEN_CVLPD) {
					continue;
				}
				Add(new CivilopediaEntry { Key = tech.CivilopediaEntry ?? KeyFromName("TECH_", tech.Name), Name = tech.Name, Section = CivilopediaSection.Advances, Tech = tech }, tech);
			}
			foreach (Building building in buildings) {
				CivilopediaSection section = building.IsGreatWonder() ? CivilopediaSection.GreatWonders
					: building.isSmallWonder ? CivilopediaSection.SmallWonders
					: CivilopediaSection.Improvements;
				Add(new CivilopediaEntry { Key = building.civilopediaEntry ?? KeyFromName("BLDG_", building.name), Name = building.name, Section = section, Building = building }, building);
			}
			foreach (Inflow inflow in gameData.Inflows.Where(i => i != null)) {
				// Civ3's Wealth is a building; C7 makes it an inflow.
				string key = KeyFromName("BLDG_", inflow.name);
				if (Text.Has(key)) {
					Add(new CivilopediaEntry { Key = key, Name = inflow.name, Section = CivilopediaSection.Improvements }, inflow);
				}
			}
			foreach (Government government in governments) {
				Add(new CivilopediaEntry { Key = government.civilopediaEntry ?? KeyFromName("GOVT_", government.name), Name = government.name, Section = CivilopediaSection.Governments, Government = government }, government);
			}
			foreach (UnitPrototype unit in units) {
				Add(new CivilopediaEntry { Key = unit.civilopediaEntry ?? KeyFromName("PRTO_", unit.name), Name = unit.name, Section = CivilopediaSection.Units, Unit = unit }, unit);
			}
			foreach (Terraform terraform in terraforms) {
				Add(new CivilopediaEntry { Key = terraform.CivilopediaEntry ?? KeyFromName("TFRM_", terraform.Name), Name = terraform.Name, Section = CivilopediaSection.WorkerActions, Terraform = terraform }, terraform);
			}
			foreach (TerrainType terrain in terrains) {
				Add(new CivilopediaEntry { Key = KeyFromName("TERR_", terrain.DisplayName), Name = terrain.DisplayName, Section = CivilopediaSection.Terrain, Terrain = terrain }, terrain);
			}
			foreach (Resource resource in resources) {
				Add(new CivilopediaEntry { Key = resource.CivilopediaEntry ?? KeyFromName("GOOD_", resource.Name), Name = resource.Name, Section = CivilopediaSection.Resources, Resource = resource }, resource);
			}
		}

		// The articles of Civilopedia.txt that the rules have nothing for: the
		// game concepts, and terrain like rivers and worker jobs like building
		// colonies.
		private void AddOtherArticles() {
			foreach (string key in Text.GameConceptKeys) {
				CivilopediaArticle article = Text.Get(key);
				if (article != null) {
					Add(new CivilopediaEntry { Key = article.Key.Trim(), Name = article.Name ?? NameFromKey(key), Section = CivilopediaSection.GameConcepts }, null);
				}
			}
			foreach (CivilopediaArticle article in Text.Articles) {
				string key = article.Key.Trim();
				if (byKey.ContainsKey(key) || article.Lines.Count == 0) {
					continue;
				}
				CivilopediaSection? section = key.Split('_')[0].ToUpperInvariant() switch {
					"TERR" => CivilopediaSection.Terrain,
					"TFRM" => CivilopediaSection.WorkerActions,
					"GCON" => CivilopediaSection.GameConcepts,
					_ => null,
				};
				if (section is CivilopediaSection s) {
					Add(new CivilopediaEntry { Key = key, Name = article.Name ?? NameFromKey(key), Section = s }, null);
				}
			}
		}

		private void Add(CivilopediaEntry entry, object subject) {
			if (entry.Key == null) {
				entry.Key = KeyFromName(entry.Section.ToString().ToUpperInvariant() + "_", entry.Name ?? "");
			}
			entry.Article = Text.Get(entry.Key);
			if (byKey.ContainsKey(entry.Key)) {
				// Two things with the same key (e.g. units of the same name)
				// share the first one's page.
				if (subject != null) {
					bySubject.TryAdd(subject, byKey[entry.Key]);
				}
				return;
			}
			byKey[entry.Key] = entry;
			if (subject != null) {
				bySubject.TryAdd(subject, entry);
			}
			bySection[entry.Section].Add(entry);
		}

		private void Sort() {
			foreach (List<CivilopediaEntry> entries in bySection.Values) {
				entries.Sort(CompareByName);
			}
			all = byKey.Values.ToList();
			all.Sort(CompareByName);
		}

		private static int CompareByName(CivilopediaEntry a, CivilopediaEntry b) {
			int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			return byName != 0 ? byName : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
		}

		// The key of the first article with one of these names after the
		// prefix, as Civ3's RACE keys use a civilization's noun or name.
		private string FindArticleKey(string prefix, params string[] names) {
			foreach (string name in names) {
				if (string.IsNullOrWhiteSpace(name)) {
					continue;
				}
				string key = KeyFromName(prefix, name);
				if (Text.Has(key)) {
					return Text.Get(key).Key.Trim();
				}
			}
			return null;
		}
	}
}
