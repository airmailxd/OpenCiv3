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

		// The civ's flags and the plan add up: the flags are the civ's
		// standing personality, the plan the planner's call for now, and both
		// are small nudges on the same score. A plan can still outweigh the
		// flags (FocusPoints is three times FlagPoints).
		public static float ProductionAdjustment(Player player, IProducible option) {
			return PlanAdjustment(player, option) + FlagAdjustment(player, option);
		}

		private static float PlanAdjustment(Player player, IProducible option) {
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

		// Ozymandias measured the flags at no more than about 5% on unit
		// builds (compendium 08, section 3.2), so a flag is a third of what
		// a full plan focus is worth.
		public static float FlagPoints(Player player) => FocusPoints(player) / 3;

		// More flags than this and Civ3's AI ignores all of them (Ozymandias:
		// "more than 3 (maybe 4) confuses the AI"). We take 3. UNVERIFIED:
		// Predator145's second-hand limit of 6 (Alexmann, Firaxis).
		public const int MaxFlags = 3;

		// A build-often category adds FlagPoints to each option in it, a
		// build-never one takes them away; an option in several categories
		// gets the average, as with plan focus. Never is a strong nudge
		// down, not an exclusion: Soren Johnson advises rarely using the
		// Never flags, and the AI still has to build defenders and settlers
		// when it must. Advanced AI only, plan or no plan.
		public static float FlagAdjustment(Player player, IProducible option) {
			Civilization civ = player?.civilization;
			if (civ == null || !player.IsAdvancedAi || civ.buildFlagCount == 0 || civ.buildFlagCount > MaxFlags) {
				return 0;
			}
			float total = 0;
			int count = 0;
			foreach (PlanFocus f in Categories(option)) {
				if (civ.buildOften.Contains(f)) {
					++total;
				}
				if (civ.buildNever.Contains(f)) {
					--total;
				}
				++count;
			}
			return count == 0 ? 0 : total / count * FlagPoints(player);
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
