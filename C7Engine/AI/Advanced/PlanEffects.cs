using System;
using System.Collections.Generic;
using C7GameData;

namespace C7Engine.AI.Advanced {
	// How an Advanced AI's plan (see StrategicPlan) steers the classical AI.
	// Each effect is a small nudge on a choice the AI already makes:
	// production scores, war plotting's aggression level, and its choice of
	// target. With no plan, an expired one or a Standard AI, every effect is
	// nothing, so the Standard AI is untouched.
	public static class PlanEffects {
		// The production score a focus of 100 adds (and 0 takes away) for
		// each option in its category. HEURISTIC: about what the best attack
		// or defence on offer adds to a unit's score (ChooseProducible's
		// attack and defense weights are 10 to 15), so a strong focus tips
		// close choices without overriding the AI's needs (an unguarded
		// city still builds a defender first).
		public static float FocusPoints(Player player) => (float)AdvancedTuning.Get(player, "FocusPoints");

		// The plan steering this player now, or null.
		public static StrategicPlan Active(Player player) {
			if (player == null || !player.IsAdvancedAi) {
				return null;
			}
			StrategicPlan plan = player.strategicPlan;
			int turn = EngineStorage.gameData?.turn ?? 0;
			return plan != null && plan.ActiveOn(turn) ? plan : null;
		}

		public static float ProductionAdjustment(Player player, IProducible option) {
			StrategicPlan plan = Active(player);
			if (plan == null) {
				return 0;
			}
			float total = 0;
			int count = 0;
			foreach (PlanFocus f in Categories(option)) {
				total += (plan.Focus(f) - StrategicPlan.NeutralFocus) / (float)StrategicPlan.NeutralFocus;
				++count;
			}
			return count == 0 ? 0 : total / count * FocusPoints(player);
		}

		public static int AggressionShift(Player player) => Active(player)?.aggressionShift ?? 0;

		public static RivalStance Stance(Player player, Player rival) =>
			Active(player)?.StanceToward(rival?.id) ?? RivalStance.Neutral;

		// The categories an option counts in. Like Civ3, one option can
		// count in several: the AI "preprocesses every unit/building to put
		// them into multiple categories" (Soren Johnson; compendium 08,
		// section 3.2).
		public static IEnumerable<PlanFocus> Categories(IProducible option) {
			if (option is UnitPrototype unit) {
				return UnitCategories(unit);
			}
			if (option is Building building) {
				return BuildingCategories(building);
			}
			return [];
		}

		private static IEnumerable<PlanFocus> UnitCategories(UnitPrototype unit) {
			if (unit.isSettler || unit.actions.Contains(UnitAction.BuildCity)) {
				yield return PlanFocus.Settlers;
				yield break;
			}
			if (unit.isWorker) {
				yield return PlanFocus.Workers;
				yield break;
			}
			if (unit.categories.Contains("Sea")) {
				yield return PlanFocus.Naval;
				yield break;
			}
			if (unit.categories.Contains("Air")) {
				yield return PlanFocus.Air;
				yield break;
			}
			if (unit.IsLandUnit() && unit.bombard > 0 && unit.attack == 0) {
				yield return PlanFocus.Artillery;
				yield break;
			}
			if (StockWarPlotting.IsOffensive(unit)) {
				yield return PlanFocus.Offense;
			} else if (StockWarPlotting.IsDefensive(unit)) {
				yield return PlanFocus.Defense;
			}
		}

		// HEURISTIC: by what the building does, after Predator145's list of
		// which buildings each category covers (compendium 08, section 3.2);
		// a wall counts as defence, which Civ3's categories leave out.
		private static IEnumerable<PlanFocus> BuildingCategories(Building b) {
			if (b.allowsCitySize2 || b.allowsCitySize3 || b.doublesCityGrowthRate) {
				yield return PlanFocus.Growth;
			}
			if (b.productionBonusPercent > 0) {
				yield return PlanFocus.Production;
			}
			if (b.contentFacesInCity > 0 || b.contentFacesAllCities > 0 || b.increasesLuxury || b.increasesLuxuryTrade) {
				yield return PlanFocus.Happiness;
			}
			if (b.increasesResearch || b.doublesResearch) {
				yield return PlanFocus.Science;
			}
			if (b.increasesTax || b.treasuryEarnsInterest || b.goldPerTurn > 0) {
				yield return PlanFocus.Wealth;
			}
			if (b.increasesTax || b.allowsWaterTrade || b.allowsAirTrade || b.reducesCorruption) {
				yield return PlanFocus.Trade;
			}
			if (b.culturePerTurn > 0) {
				yield return PlanFocus.Culture;
			}
			if (b.providesWalls || b.bombardDefense > 0) {
				yield return PlanFocus.Defense;
			}
			if (b.providesVeteranGroundUnits) {
				yield return PlanFocus.Offense;
			}
			if (b.providesVeteranSeaUnits) {
				yield return PlanFocus.Naval;
			}
			if (b.providesVeteranAirUnits) {
				yield return PlanFocus.Air;
			}
		}
	}
}
