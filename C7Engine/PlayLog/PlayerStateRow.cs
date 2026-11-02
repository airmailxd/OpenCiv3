using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine.PlayLog {
	// One civ's state at a turn, as a row of AiTrialTest's state log
	// (C7_TRIAL_STATE) and of a human's play log (see PlayRecorder), so the
	// analysis scripts (tools/advanced-ai-training) read both alike.
	public static class PlayerStateRow {
		// civs are the game's civs (not the independents), for atWarWith.
		// ai is the row's "ai" label: the player's AI type unless given.
		public static Dictionary<string, object> Build(GameData gameData, Player p, IEnumerable<Player> civs,
				object game, int seed, string run, int turn, bool trial, string ai = null) {
			bool dead = p.defeated;
			return new Dictionary<string, object> {
				["game"] = game,
				["seed"] = seed,
				["run"] = run,
				["turn"] = turn,
				["civ"] = p.civilization.name,
				["ai"] = ai ?? p.aiType.ToString(),
				["trial"] = trial,
				["defeated"] = dead,
				["cities"] = p.cities.Count,
				["citizens"] = p.cities.Sum(c => c.residents.Count),
				["units"] = p.units.Count,
				["military"] = p.units.Count(u => u.unitType.attack > 0 && !u.unitType.isWorker && !u.unitType.isSettler),
				["workers"] = p.units.Count(u => u.unitType.isWorker && !u.unitType.isSettler),
				["settlers"] = p.units.Count(u => u.unitType.isSettler),
				["ships"] = p.units.Count(u => u.unitType.IsSeaUnit()),
				["techs"] = p.knownTechs.Count,
				["government"] = p.government?.name,
				["gold"] = p.gold,
				["score"] = dead ? 0 : ScoreVictory.ComputeTurnScore(p, gameData),
				["culture"] = p.cities.Sum(c => c.GetCulture()),
				// Land tiles the civ has explored, and the open city sites
				// it could reach from its first city: the count the AI's
				// "room to expand" decisions use (ChooseProducible).
				["knownLand"] = p.tileKnowledge.knownTiles.Count(t => t.IsLand()),
				["openSites"] = dead || p.cities.Count == 0 ? 0 : ChooseProducible.NumberOfReachableOpenCitySpots(p.cities[0]),
				// An Advanced AI's current plan, as its planner explained it.
				["plan"] = p.IsAdvancedAi ? p.strategicPlan?.rationale : null,
				["atWarWith"] = civs.Where(o => o != p && !o.defeated && AtWarWith(p, o)).Select(o => o.civilization.name).ToList(),
			};
		}

		public static bool AtWarWith(Player a, Player b) => a.playerRelationships.TryGetValue(b.id, out PlayerRelationship r) && r.AtWar();
	}
}
