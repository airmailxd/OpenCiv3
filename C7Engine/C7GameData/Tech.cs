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
		// replaced as a whole when a different list is looked up in.
		private sealed class TechIndex {
			internal List<Tech> techs;
			internal int count;
			internal Dictionary<ID, Tech> byId;
			internal Tech firstWithNullId;
			internal Tech bridgeTech;
		}

		private static TechIndex index;

		private static TechIndex IndexFor(List<Tech> techs) {
			TechIndex result = index;
			if (result != null && ReferenceEquals(result.techs, techs) && result.count == techs.Count) {
				return result;
			}
			result = new TechIndex { techs = techs, count = techs.Count, byId = new() };
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
		// techs in the game.
		public static Tech FindById(List<Tech> techs, ID id) {
			if (techs == null) {
				return null;
			}
			// The index is checked against the list's identity and size. As
			// a last line of defence against the list being edited in place,
			// a hit must still have the right id, or we fall back to a scan.
			TechIndex techIndex = IndexFor(techs);
			if (id is null) {
				return techIndex.firstWithNullId;
			}
			if (!techIndex.byId.TryGetValue(id, out Tech tech)) {
				return null;
			}
			if (tech.id == id) {
				return tech;
			}
			return techs.Find(t => t?.id == id);
		}

		// The same as techs.FirstOrDefault(t => t.EnablesBridges).
		public static Tech FindBridgeTech(List<Tech> techs) {
			Tech tech = IndexFor(techs).bridgeTech;
			if (tech == null || tech.EnablesBridges) {
				return tech;
			}
			return techs.Find(t => t.EnablesBridges);
		}

		public void FillInPrereqs(List<SaveTech> saveTechs, List<Tech> techs) {
			SaveTech st = saveTechs.Find(st => st.id == this.id);

			foreach (ID prereq in st.Prerequisites) {
				this.Prerequisites.Add(techs.Find(t => t.id == prereq));
			}
		}
	}
}
