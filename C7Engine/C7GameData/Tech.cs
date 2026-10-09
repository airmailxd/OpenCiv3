using System.Collections.Generic;
using C7GameData.Save;

namespace C7GameData {
	// The in-game representation of a tech that can be researched.
	public class Tech {
		public ID id;
		public string Name { get; set; }
		public string CivilopediaEntry { get; set; }
		public int Cost;
		public bool RequiredForEraAdvancement;
		public bool BonusTechToFirstCivThatResearches;
		public bool EnablesBridges;
		public bool DoublesWealthProduction;
		// Irrigation no longer needs fresh water (Electricity in Civ3).
		public bool EnablesIrrigationEverywhere;
		// Workers work twice as fast (Replaceable Parts in Civ3).
		public bool DoublesWorkerRate;

		// The civilopedia name of the era this tech is part of
		// (like ERA_Ancient_Times). This is what art lookups are based on.
		public string EraCivilopediaName { get; set; }

		// The path, like "Art\tech chooser\Icons\39-Mapmaking-small.pcx", of
		// the small icon for this tech.
		public string SmallIconPath;

		// The position of this tech within the tech advisor UI.
		public int X;
		public int Y;

		public List<Tech> Prerequisites = new();

		// The backing save tech, for serialization purposes. This should not be
		// made public - add accessors or fields for what you need.
		public SaveTech DataSource { private get; set; }

		public C7GameData.Save.SaveTech ToSaveTech() {
			return DataSource;
		}

		// An index from id to tech over one list of techs. Immutable; it is
		// replaced as a whole when a different list is looked up in, or when
		// the list has changed since it was built.
		//
		// Contract: the index notices any change made to the list itself
		// (through List's own methods), but not a change to a tech already
		// in it. Don't change the id or EnablesBridges of a tech once it is
		// in the game's list; replace the tech instead. As a safety net, a
		// lookup whose result no longer matches rebuilds the index once.
		private sealed class TechIndex {
			internal List<Tech> techs;
			internal int count;
			// An enumerator taken when the index was built. List enumerators
			// are documented to throw once the list has been modified, which
			// gives an O(1) check that the list is unchanged, including edits
			// that keep its size (like replacing an entry).
			internal List<Tech>.Enumerator version;
			internal Dictionary<ID, Tech> byId;
			internal Tech firstWithNullId;
			internal Tech bridgeTech;

			internal bool IsValidFor(List<Tech> list) {
				if (!ReferenceEquals(techs, list) || count != list.Count) {
					return false;
				}
				return CollectionVersion.Unchanged(version);
			}
		}

		private static TechIndex index;

		private static TechIndex IndexFor(List<Tech> techs, bool rebuild = false) {
			TechIndex result = index;
			if (!rebuild && result != null && result.IsValidFor(techs)) {
				return result;
			}
			result = new TechIndex { techs = techs, count = techs.Count, version = techs.GetEnumerator(), byId = new() };
			foreach (Tech t in techs) {
				// Keep the first of any duplicates, like List.Find does.
				if (t == null) {
					continue;
				}
				if (t.id is not null) {
					result.byId.TryAdd(t.id, t);
				} else {
					result.firstWithNullId ??= t;
				}
				if (result.bridgeTech == null && t.EnablesBridges) {
					result.bridgeTech = t;
				}
			}
			index = result;
			return result;
		}

		// The same as techs.Find(t => t.id == id), in O(1) for the list of
		// techs in the game (see the contract on TechIndex).
		public static Tech FindById(List<Tech> techs, ID id) {
			if (techs == null) {
				return null;
			}
			Tech tech = LookUpById(IndexFor(techs), id);
			if (tech != null && tech.id != id) {
				// A tech's id was changed in place: rebuild the index once.
				tech = LookUpById(IndexFor(techs, rebuild: true), id);
			}
			return tech;
		}

		private static Tech LookUpById(TechIndex techIndex, ID id) {
			if (id is null) {
				return techIndex.firstWithNullId;
			}
			return techIndex.byId.GetValueOrDefault(id);
		}

		// The same as techs.FirstOrDefault(t => t.EnablesBridges), in O(1)
		// (see the contract on TechIndex).
		public static Tech FindBridgeTech(List<Tech> techs) {
			Tech tech = IndexFor(techs).bridgeTech;
			if (tech != null && !tech.EnablesBridges) {
				// A tech's flag was changed in place: rebuild the index once.
				tech = IndexFor(techs, rebuild: true).bridgeTech;
			}
			return tech;
		}

		public void FillInPrereqs(List<SaveTech> saveTechs, List<Tech> techs) {
			SaveTech st = saveTechs.Find(st => st.id == this.id);

			foreach (ID prereq in st.Prerequisites) {
				this.Prerequisites.Add(techs.Find(t => t.id == prereq));
			}
		}
	}
}
