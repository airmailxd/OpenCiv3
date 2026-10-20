using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// Civ3's own rules for when an AI at peace begins to plot a war, and on
	// whom, as Flintlock read them from the C3C 1.22 executable ("All About
	// AI Aggression Levels and War Plotting", 27 Jun 2026,
	// https://forums.civfanatics.com/threads/all-about-ai-aggression-levels-and-war-plotting.703785/;
	// compendium 08, section 3.4.1, and 06, section 6.13), which replace
	// WarCampaign.MaybeStartCampaign's choice to start a war in peacetime:
	// - The per-turn rolls that must all pass before the AI plots a war at
	//   all (RollsToPlot), in place of waiting until it had no wide-open
	//   land left to settle.
	// - Which civ it plots against, by Civ3's target score (Excluded,
	//   TargetScore), in place of the weakest neighbour 1.3 times weaker
	//   than us.
	// The campaign's own rules (a city it can reach and take, and the
	// strength to declare) still apply.
	//
	// Paired AI trials (8 games of 200 turns, score per civ against the
	// same civs without it): the rolls alone +32 +- 44 on Continents and
	// +76 +- 34 on Pangaea, with 0.7 and 1.0 fewer wars declared; the target
	// score alone +124 +- 67 and +87 +- 109; both together +42 +- 48 and
	// +128 +- 56, with 1.6 fewer cities lost on Pangaea. Adopted.
	public static class StockWarPlotting {
		// Living civs, strongest first. "The power values used here are the
		// same shown on the F8 histograph screen"; UNVERIFIED: the military
		// advisor's strength (Player.CalculateMilitaryStrength) stands in
		// for the histograph's power.
		private static List<Player> ByPower(GameData gameData) {
			return gameData.players.Where(p => !p.isBarbarians && !p.defeated)
				.OrderByDescending(p => p.CalculateMilitaryStrength()).ToList();
		}

		// "PowerRank = 31 if it's the strongest civ in the game, 30 if it's
		// second, and so forth."
		public static int PowerRank(GameData gameData, Player player) {
			int index = ByPower(gameData).IndexOf(player);
			return index < 0 ? 0 : 31 - index;
		}

		private static int DifficultyIndex(GameData gameData) {
			return Math.Max(0, gameData.difficulties.FindIndex(d => d.Name == gameData.gameDifficulty?.Name));
		}

		// "level = civ.AggressionLevel + GameDifficulty // 2 -
		// DifficultyLevelCount // 6" and "return max(-2, min(level, 2))",
		// with "GameDifficulty = 0 for Chieftain". UNVERIFIED: the game's
		// overall AI aggression setting is taken to be Normal, which
		// changes nothing.
		public static int EffectiveAggressionLevel(GameData gameData, Player player) {
			int level = (player.civilization?.aggressionLevel ?? 0) + DifficultyIndex(gameData) / 2
				- Math.Max(1, gameData.difficulties.Count) / 6;
			return Math.Clamp(level, -2, 2);
		}

		// UNVERIFIED: the units "with the offensive or defensive AI
		// strategy" are taken to be the land units that can attack and are
		// stronger in attack, and those that can defend and are at least as
		// strong in defence.
		private static bool IsOffensive(MapUnit u) {
			return u.IsLandUnit() && !u.IsArmy() && !u.IsLeader() && !u.unitType.isSettler && !u.unitType.isWorker
				&& u.unitType.attack > u.unitType.defense && u.CanAttack();
		}

		private static bool IsDefensive(MapUnit u) {
			return u.IsLandUnit() && !u.IsArmy() && !u.IsLeader() && !u.unitType.isSettler && !u.unitType.isWorker
				&& u.unitType.defense > 0 && u.unitType.defense >= u.unitType.attack && u.CanDefendOnLand();
		}

		private static bool OneIn(int n) => n <= 1 || GameData.rng.Next(n) == 0;

		// Whether the AI begins to plot a war this turn: "Each AI player has
		// a chance at the start of each turn to begin plotting war", and
		// every roll must pass.
		public static bool RollsToPlot(GameData gameData, Player player) {
			// "Have a capital city"
			City capital = player.cities.Find(c => c.IsCapital());
			if (capital == null) {
				return false;
			}
			int cities = player.cities.Count;
			// "Roll one in (32 - PowerRank)"
			if (!OneIn(32 - PowerRank(gameData, player))) {
				return false;
			}
			// "If the game is set to the lowest difficulty, roll one in 32"
			if (DifficultyIndex(gameData) == 0 && !OneIn(32)) {
				return false;
			}
			// "Roll one in (12 + -2 * EffectiveAggressionLevel)"
			if (!OneIn(12 - 2 * EffectiveAggressionLevel(gameData, player))) {
				return false;
			}
			// "If already at war, roll one in 16"
			if (gameData.players.Any(p => p != player && !p.isBarbarians && !p.defeated && AtWar(player, p)) && !OneIn(16)) {
				return false;
			}
			// "If in anarchy, roll one in 8"
			if (player.government?.transitionType == true && !OneIn(8)) {
				return false;
			}
			// "If have fewer than 2 * OCN cities and there are any open city
			// spots on the capital's continent, roll one in 2". UNVERIFIED:
			// the open spots are those the capital's settlers could reach.
			if (cities < 2 * player.GetAdjustedOptimalCityNumber(gameData)
					&& ChooseProducible.NumberOfReachableOpenCitySpots(capital) > 0 && !OneIn(2)) {
				return false;
			}
			// "If in a government that experiences war weariness, roll one in 2"
			if (player.government != null && player.government.warWeariness > 0 && !OneIn(2)) {
				return false;
			}
			// "If do not have at least one defensive unit per city and one
			// offensive unit per two cities, roll one in 2", and likewise for
			// two defensive units per city and one offensive per two cities,
			// and for two defensive units and one offensive unit per city.
			int defensive = player.units.Count(IsDefensive);
			int offensive = player.units.Count(IsOffensive);
			if (!(defensive >= cities && 2 * offensive >= cities) && !OneIn(2)) {
				return false;
			}
			if (!(defensive >= 2 * cities && 2 * offensive >= cities) && !OneIn(2)) {
				return false;
			}
			if (!(defensive >= 2 * cities && offensive >= cities) && !OneIn(2)) {
				return false;
			}
			// "If treasury is too low, roll one in 2 (too low here means < 16
			// per city" (the extra for Wall Street type small wonders is
			// left out: UNVERIFIED how much it is).
			if (player.gold < 16 * cities && !OneIn(2)) {
				return false;
			}
			return true;
		}

		private static bool HumanRankedFirst(List<Player> byPower, Player target) {
			return target.isHuman && byPower.Count > 0 && byPower[0] == target;
		}

		private static int CommonEnemies(GameData gameData, Player a, Player b) {
			return gameData.players.Count(p => p != a && p != b && !p.isBarbarians && !p.defeated
				&& AtWar(a, p) && AtWar(b, p));
		}

		private static int MilitaryAlliances(Player a, Player b) {
			return TryGetRelationship(a, b, out PlayerRelationship ab)
				? ab.multiTurnDeals.Count(d => d.dealSubType == DealSubType.MilitaryAlliance) : 0;
		}

		// Whether the target is left out this turn. "Other players with whom
		// the AI has a mutual protection pact are excluded", and "Lastly the
		// AI randomly excludes some players": it rolls "not one in X", with
		// "X = 1 to start", "X += 2 if the AI has a peace treaty with the
		// target", "X += 2 for each common enemy the AI and the target have"
		// and "X += 2 for each military alliance the AI and the target
		// have"; Flintlock's post #8: "This check is reversed from all the
		// others", so the target stays in only on the one in X. "The AI will
		// skip this check if the target is a human player who is ranked
		// first in power." Left out (UNVERIFIED, his belief only): a cooldown
		// "within 8 turns of signing peace".
		public static bool Excluded(GameData gameData, Player ai, Player target) {
			if (Pacts.HasDeal(ai, target, DealSubType.MutualProtectionPact)) {
				return true;
			}
			if (HumanRankedFirst(ByPower(gameData), target)) {
				return false;
			}
			int x = 1;
			if (!AtWar(ai, target)) {
				x += 2;
			}
			x += 2 * CommonEnemies(gameData, ai, target);
			x += 2 * MilitaryAlliances(ai, target);
			return !OneIn(x);
		}

		private static bool HasPeaceOrPact(Player a, Player b) {
			return TryGetRelationship(a, b, out PlayerRelationship ab) && ab.multiTurnDeals.Any(
				d => d.dealSubType is DealSubType.Peace or DealSubType.MutualProtectionPact);
		}

		// The target score: "Higher values mean more attractive targets".
		// Left out: past war damage ("DamageBalance"), which the engine
		// doesn't keep.
		public static int TargetScore(GameData gameData, Player ai, Player target) {
			List<Player> byPower = ByPower(gameData);
			int powerRank = byPower.IndexOf(target) < 0 ? 0 : 31 - byPower.IndexOf(target);
			// "To start, X = 2 * (PlayerCount - 32 + PowerRank)." UNVERIFIED:
			// PlayerCount counts the living civs, barbarians left out.
			int x = 2 * (byPower.Count - 32 + powerRank);
			// "If the AI has a capital, X = X + 64 // NearestCityDistance"
			City capital = ai.cities.Find(c => c.IsCapital());
			if (capital != null && target.cities.Count > 0) {
				int distance = target.cities.Min(c => capital.location.DistanceTo(c.location));
				x += 64 / Math.Max(1, distance);
			}
			// "DiploEffect = 8 if furious toward the target, 4 if annoyed, 2
			// if cautious, 1 if polite, and 0 if gracious."
			x += Attitudes.Level(gameData, ai, target) switch {
				AttitudeLevel.Furious => 8,
				AttitudeLevel.Annoyed => 4,
				AttitudeLevel.Cautious => 2,
				AttitudeLevel.Polite => 1,
				_ => 0,
			};
			// "X = X + 4 if LandUnitsPerCity is 0, + 2 if it's 1, + 1 if
			// it's 2, and + 0 if it's 3 or more."
			int perCity = target.units.Count(u => IsOffensive(u) || IsDefensive(u)) / Math.Max(1, target.cities.Count);
			x += perCity switch { 0 => 4, 1 => 2, 2 => 1, _ => 0 };
			// "If target is in anarchy, X = X + 1"
			if (target.government?.transitionType == true) {
				x += 1;
			}
			// "If the target is a human player ranked top in power and twice
			// as powerful as the second-ranked player, X = 2 * X"
			if (HumanRankedFirst(byPower, target) && byPower.Count > 1
					&& target.CalculateMilitaryStrength() >= 2 * byPower[1].CalculateMilitaryStrength()) {
				x *= 2;
			}
			// "If the AI has a resource trade with the target, X = 3 * X // 4"
			if (TryGetRelationship(ai, target, out PlayerRelationship ours)
					&& ours.multiTurnDeals.Any(d => d.dealSubType is DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn)) {
				x = 3 * x / 4;
			}
			// "For each common enemy that the AI and the target share, X = 2
			// * X // 3"
			for (int i = CommonEnemies(gameData, ai, target); i > 0; --i) {
				x = 2 * x / 3;
			}
			// "If the AI and the target do not share a continent, X = X //
			// 2"; "Two players are considered to share a continent if they
			// both have at least one city on it."
			HashSet<int> ourLand = ai.cities.Select(c => c.location.continent).ToHashSet();
			if (!target.cities.Any(c => ourLand.Contains(c.location.continent))) {
				x /= 2;
			}
			// "If the target has a peace treaty or mutual protection pact
			// with any player, X = X // 2."
			if (gameData.players.Any(p => p != target && !p.isBarbarians && !p.defeated && HasPeaceOrPact(target, p))) {
				x /= 2;
			}
			// "If the AI has a peace treaty or mutual protection pact with
			// the target, X = X // 2."
			if (HasPeaceOrPact(ai, target)) {
				x /= 2;
			}
			// "X = X // (AllianceCount + 1)". UNVERIFIED: the target's
			// military alliances.
			int alliances = target.playerRelationships.Values.Sum(r => r.multiTurnDeals.Count(d => d.dealSubType == DealSubType.MilitaryAlliance));
			x /= alliances + 1;
			return x;
		}
	}
}
