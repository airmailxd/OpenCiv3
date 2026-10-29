using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.Advanced {
	// The written situation report the model reviewer gets (see
	// PolicyReview): what the civ knows, in Markdown, not the map. Lessons
	// taken from Vox Deorum and RuneBench:
	// - structured text, not pixels or raw tiles: models reason badly about
	//   coordinates, so the map is summed up as rivals and fronts;
	// - only what a human in the civ's seat would know: rivals it has met,
	//   and their strength in the military advisor's rough words.
	public static class PlanBriefing {
		public sealed class Result {
			public string text;
			// Rival names as written in the report, to their ids.
			public Dictionary<string, ID> rivals = new();
		}

		public static Result Situation(Player player, GameData gameData) {
			Result result = new();
			StringBuilder b = new();
			float ours = Math.Max(1f, player.CalculateMilitaryStrength());

			b.AppendLine($"# {player.civilization.name}, turn {gameData.turn}, {EraName(player)}");
			b.AppendLine();
			b.AppendLine("## Your civilization");
			b.AppendLine($"- Government: {player.government?.name ?? "anarchy"}");
			b.AppendLine($"- Cities: {player.cities.Count}, citizens: {player.cities.Sum(c => c.residents.Count)}");
			b.AppendLine($"- Techs known: {player.knownTechs.Count}; researching {gameData.techs.Find(t => t.id == player.currentlyResearchedTech)?.Name ?? "nothing"}");
			b.AppendLine($"- Treasury: {player.gold} gold");
			b.AppendLine($"- Land units: {player.units.Count(u => u.IsLandUnit() && StockWarPlotting.IsDefensive(u))} defensive, "
				+ $"{player.units.Count(u => u.IsLandUnit() && StockWarPlotting.IsOffensive(u))} offensive; "
				+ $"{player.units.Count(u => u.unitType.isWorker)} workers, {player.units.Count(u => u.unitType.isSettler)} settlers");
			int open = player.cities.Count == 0 ? 0 : player.cities.Max(c => ChooseProducible.NumberOfReachableOpenCitySpots(c));
			b.AppendLine($"- Open city sites your settlers can reach: {open}");
			int disorder = player.cities.Count(c => c.isInCivilDisorder);
			if (disorder > 0) {
				b.AppendLine($"- Cities in disorder: {disorder}");
			}
			if (player.strategicPlan != null) {
				b.AppendLine($"- The rules' current plan: {AdvancedAI.Describe(player.strategicPlan)}");
			}
			b.AppendLine();

			b.AppendLine("## Cities");
			foreach (City c in player.cities.OrderByDescending(c => c.residents.Count).Take(12)) {
				string producing = c.itemBeingProduced?.ToString() ?? "nothing";
				int guards = c.location.unitsOnTile.Count(u => u.CanDefendOnLand());
				b.AppendLine($"- {c.name}: size {c.residents.Count}, building {producing}, {guards} defenders{(c.IsCapital() ? ", capital" : "")}");
			}
			if (player.cities.Count > 12) {
				b.AppendLine($"- ... and {player.cities.Count - 12} smaller cities");
			}
			b.AppendLine();

			b.AppendLine("## Rivals you have met");
			List<Player> met = Met(player, gameData);
			if (met.Count == 0) {
				b.AppendLine("- none yet");
			}
			foreach (Player p in met) {
				string name = p.civilization.name;
				result.rivals[name] = p.id;
				string relation = AtWar(player, p) ? $"AT WAR ({OutlookWords(PeaceAI.Assess(player, p))})" : "at peace";
				bool sameLand = p.cities.Any(c => player.cities.Any(o => o.location.continent == c.location.continent));
				b.AppendLine($"- {name}: {relation}; military {StrengthWords(p.CalculateMilitaryStrength() / ours)} than yours; "
					+ $"{p.cities.Count} cities; {Attitudes.Level(gameData, player, p)} toward them; "
					+ $"{(sameLand ? "shares your continent" : "overseas")}; {p.knownTechs.Count} techs");
			}
			result.text = b.ToString();
			return result;
		}

		public static List<Player> Met(Player player, GameData gameData) =>
			gameData.players.Where(p => p != player && !p.IsIndependent && !p.defeated
				&& player.playerRelationships.ContainsKey(p.id)).ToList();

		public static string EraName(Player player) => player.EraIndex() switch {
			<= 0 => "Ancient Times",
			1 => "Middle Ages",
			2 => "Industrial Age",
			_ => "Modern Era",
		};

		// The military advisor's kind of words, not exact numbers.
		public static string StrengthWords(float ratio) =>
			ratio >= 2f ? "much stronger" : ratio >= 1.25f ? "stronger" : ratio > .8f ? "about as strong" : ratio > .5f ? "weaker" : "much weaker";

		private static string OutlookWords(PeaceAI.WarOutlook o) => o switch {
			PeaceAI.WarOutlook.Dominating => "you are dominating",
			PeaceAI.WarOutlook.Winning => "you are winning",
			PeaceAI.WarOutlook.Even => "even",
			PeaceAI.WarOutlook.Losing => "you are losing",
			_ => "you are losing badly",
		};
	}
}
