using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// War when it pays: the AI picks a weaker neighbour with cities worth
	// taking within reach and goes to war on it, through a war campaign on
	// it as prey (see PreyWar, WarCampaign: gather attackers and siege
	// engines at the nearest city, declare once the stack is ready, march,
	// bombard, take city after city, give up when it goes badly).
	//
	// Civ3's own plotting (StockWarPlotting.RollsToPlot) passes so seldom
	// that in headless games of the League's mods (MP Tournament, QC,
	// Modern, Future, MDJ) and stock rules, 64 to 90 turns each, nobody
	// declared war in 8 games of 9 (owner's request, 2026-11-22: "the AI
	// must fight"). Those mods are short, scored games (domination,
	// elimination at the turn limit), where a civ that only builds loses
	// to one that takes its neighbour's cities.
	//
	// When it goes to war (HEURISTIC, ours): it is not already at war with
	// a civ; it has cities enough and the expansion is over or the game is
	// far enough on (Ready); the victim has a city on our landmass that we
	// know, within MaxDistance of one of ours; and our military, by the
	// military advisor's measure, outweighs the victim's together with its
	// protectors' by RequiredRatio, less as the turns run out in a game
	// with a turn limit. A weaker civ keeps the peace. No new war in the
	// last MinTurnsLeft turns of a limited game: it couldn't be won.
	// The pick is kept (Victim) until the war ends or the odds turn.
	public static class OpportunityWar {
		// The switch that turns it off, for trials against it.
		public const string Off = "no-opportunity-war";

		public static bool On(Player player) {
			return player != null && !player.isHuman && !player.IsIndependent && !player.isBarbarians
				&& WarCampaign.Enabled(player) && !AiExperiments.Has(player, Off);
		}

		// HEURISTIC: our military power over the victim's (and its
		// protectors') needed for war, at the start of a game and once its
		// turns run out.
		internal const double RequiredRatio = 1.5;
		internal const double LateRequiredRatio = 1.15;
		// HEURISTIC: once the war is on, it is kept up while we stay this
		// much stronger (PeaceAI and the campaign's own gauges do the rest).
		internal const double KeepRatio = 0.9;
		// HEURISTIC: the farthest, in tiles from our nearest city, the
		// victim's nearest known city may be.
		internal const int MaxDistance = 14;
		// HEURISTIC: no new war this near the end of a limited game.
		internal const int MinTurnsLeft = 12;
		// HEURISTIC: the fewest cities to have before a war of choice.
		internal const int MinCities = 3;
		// HEURISTIC: in a game with no turn limit, the turn from which a civ
		// whose land hasn't run out may still go to war.
		internal const int UnlimitedReadyTurn = 50;
		// HEURISTIC: in a limited game, the share of its turns after which
		// a civ whose land hasn't run out may still go to war.
		internal const double LimitedReadyShare = 0.35;

		private sealed class Pick {
			public Player victim;
			public int turn;
		}

		private static readonly ConditionalWeakTable<Player, Pick> picks = new();

		// The turn limit of a game that ends at one, or null.
		public static int? TurnLimit(GameData gameData) {
			foreach (IVictory v in gameData.victories) {
				if (v is TimeLimitVictory t && t.TurnLimit > 0) {
					return t.TurnLimit;
				}
			}
			return null;
		}

		// How far through a limited game we are, from 0 to 1, or null.
		public static double? GameShare(GameData gameData) {
			int? limit = TurnLimit(gameData);
			return limit is int l ? Math.Clamp((double)gameData.turn / l, 0, 1) : null;
		}

		// The ratio needed for war now.
		internal static double Needed(GameData gameData) {
			double? share = GameShare(gameData);
			if (share is not double s) {
				return RequiredRatio;
			}
			// From LimitedReadyShare to the end, down to LateRequiredRatio.
			double t = Math.Clamp((s - LimitedReadyShare) / (1 - LimitedReadyShare), 0, 1);
			return RequiredRatio + (LateRequiredRatio - RequiredRatio) * t;
		}

		// Whether the civ is ready for a war of choice at all.
		internal static bool Ready(Player player, GameData gameData) {
			if (player.cities.Count < MinCities || player.government?.transitionType == true) {
				return false;
			}
			if (TurnLimit(gameData) is int limit && limit - gameData.turn < MinTurnsLeft) {
				return false;
			}
			City capital = player.cities.Find(c => c.IsCapital()) ?? player.cities[0];
			bool landLeft = ChooseProducible.NumberOfReachableOpenCitySpots(capital) > 0;
			if (!landLeft) {
				return true;
			}
			double? share = GameShare(gameData);
			return share is double s ? s >= LimitedReadyShare : gameData.turn >= UnlimitedReadyTurn;
		}

		// The victim's power with that of the civs bound to defend it.
		internal static double DefendingPower(Player player, Player victim, GameData gameData) {
			double power = victim.CalculateMilitaryStrength();
			foreach (Player p in gameData.players) {
				if (p == victim || p == player || p.IsIndependent || p.defeated) {
					continue;
				}
				if (Pacts.HasDeal(victim, p, DealSubType.MutualProtectionPact)
						|| (TryGetRelationship(victim, p, out PlayerRelationship r) && r.multiTurnDeals.Any(d => d.dealSubType == DealSubType.MilitaryAlliance))) {
					// Only part of a protector's army comes to the victim's
					// aid.
					power += p.CalculateMilitaryStrength() / 2;
				}
			}
			return power;
		}

		// Whether any civ is at war with the player (barbarians and
		// city-states left out).
		private static bool AtWarWithACiv(Player player, GameData gameData) {
			return gameData.players.Any(p => p != player && !p.IsIndependent && !p.defeated && AtWar(player, p));
		}

		// The nearest distance, in tiles, from a city of ours to a known
		// city of theirs on the same landmass, or int.MaxValue.
		internal static int Distance(Player player, Player victim) {
			int best = int.MaxValue;
			foreach (City theirs in victim.cities) {
				if (!AIFogOfWar.KnowsTile(player, theirs.location)) {
					continue;
				}
				foreach (City ours in player.cities) {
					if (ours.location.continent == theirs.location.continent) {
						best = Math.Min(best, ours.location.DistanceTo(theirs.location));
					}
				}
			}
			return best;
		}

		// How attractive a victim is: weaker, nearer, with more and bigger
		// cities, the better; 0 if not one at all.
		internal static double Score(Player player, Player victim, GameData gameData, double needed) {
			if (victim == player || victim.IsIndependent || victim.defeated || victim.cities.Count == 0
					|| !WarCampaign.CanDeclareWarOn(player, victim)
					|| Advanced.PlanEffects.Stance(player, victim) == RivalStance.Friend) {
				return 0;
			}
			int distance = Distance(player, victim);
			if (distance > MaxDistance) {
				return 0;
			}
			double ratio = player.CalculateMilitaryStrength() / Math.Max(1, DefendingPower(player, victim, gameData));
			if (ratio < needed) {
				return 0;
			}
			double citizens = victim.cities.Sum(c => c.residents.Count);
			return ratio * (victim.cities.Count + citizens / 2) / (1 + distance / 5.0);
		}

		// The civ the player has picked to go to war with, or null: kept
		// while it can still be fought and we stay strong enough, otherwise
		// picked again (at most once a turn).
		public static Player Victim(Player player, GameData gameData) {
			if (!On(player)) {
				return null;
			}
			if (picks.TryGetValue(player, out Pick pick) && pick.victim != null) {
				Player v = pick.victim;
				bool atWar = TryGetRelationship(player, v, out PlayerRelationship r) && r.AtWar();
				bool valid = !v.defeated && v.cities.Count > 0 && (atWar || WarCampaign.CanDeclareWarOn(player, v))
					&& player.CalculateMilitaryStrength() >= KeepRatio * DefendingPower(player, v, gameData);
				if (valid && (atWar || (Ready(player, gameData) && !AtWarWithACiv(player, gameData)))) {
					return v;
				}
				pick.victim = null;
			}
			if (picks.TryGetValue(player, out pick) && pick.turn == gameData.turn) {
				return null;
			}
			Pick next = picks.GetOrCreateValue(player);
			next.turn = gameData.turn;
			next.victim = null;
			if (!Ready(player, gameData) || AtWarWithACiv(player, gameData)) {
				return null;
			}
			double needed = Needed(gameData);
			double best = 0;
			foreach (Player p in gameData.players) {
				double s = Score(player, p, gameData, needed);
				if (s > best) {
					best = s;
					next.victim = p;
				}
			}
			return next.victim;
		}

		// HEURISTIC: the more a city wants an attacker for a war of
		// opportunity's stack still short of them, on top of a campaign's
		// own (ChooseProducible.WarCampaignUnitBonus, which only the cities
		// producing at least the average get): every city producing at
		// least ProductiveShare of the average. In headless games stacks
		// waited 15 turns and more for their last attackers.
		internal const float AttackerBonus = 15;
		internal const float ProductiveShare = .6f;

		public static float ProductionBonus(City city, Player player, UnitPrototype unit, float bestAttack) {
			if (!unit.IsLandUnit() || unit.isArmy || bestAttack <= 0 || unit.attack <= unit.defense || PreyWar.On(player)
					|| !On(player) || !WarCampaign.HasPreyCampaign(player) || !WarCampaign.WantsAttackers(player)) {
				return 0;
			}
			float average = (float)player.cities.Average(c => c.CurrentProductionYield().useful);
			if (city.CurrentProductionYield().useful < ProductiveShare * average) {
				return 0;
			}
			return AttackerBonus * unit.attack / bestAttack;
		}

		// HEURISTIC: turns a war of opportunity's stack gathers at peace
		// before it goes with two thirds of what it wants (a plan's prey
		// waits PreyWar.GoAfterTurns): short games can't wait long.
		public const int GoAfterTurns = 8;

		// For tests: forgets the picks.
		internal static void Forget(Player player) {
			picks.Remove(player);
		}

		// The fewest attackers the campaign gathers before declaring: fewer
		// for a small civ early on, so a short game's war starts in time.
		public static int MinAttackers(Player player) {
			return Math.Clamp(2 + player.cities.Count / 2, 4, PreyWar.MinAttackers);
		}
	}
}
