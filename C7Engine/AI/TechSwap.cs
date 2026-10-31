using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.AI {
	// Trial switch "tech-swap" (Suede, doc/suede-benchmarks.md idea 2): he
	// meets the civs early and swaps techs that are dear but of little use to
	// him (Alphabet, Writing) for the ones he lacks. With the switch on, an
	// AI swaps one tech for one with every other AI it knows, at once on
	// meeting someone or learning a tech. It asks first for the techs on its
	// way out of Despotism, then the most valuable to it, and pays with the
	// tech least valuable to itself.
	//
	// Civ3 rule (doc/civ3-compendium/06, section 6.8, AiTechPricing): each
	// side prices a tech at what it costs its buyer, and an AI takes a deal
	// if what it gets is worth the full value of what it gives; the side
	// with the cheaper tech adds the gold between. Both AIs check that
	// through WouldAcceptDealFrom, as for any tech deal.
	public static class TechSwap {
		public const string Switch = "tech-swap";

		// Most swaps a civ makes with one other in a turn.
		private const int MostSwapsPerPair = 3;

		public static bool On(Player us, Player them) {
			return AiExperiments.Has(us, Switch) && !us.isHuman && !them.isHuman;
		}

		private class Seen {
			public int techs = -1;
			public HashSet<ID> met = new();
		}
		private static readonly ConditionalWeakTable<Player, Seen> seen = new();

		// Whether the civ has met someone or learned a tech since it last
		// asked.
		public static bool WantsToSwapNow(Player us) {
			if (!AiExperiments.Has(us, Switch)) {
				return false;
			}
			Seen s = seen.GetOrCreateValue(us);
			bool changed = s.techs != us.knownTechs.Count;
			s.techs = us.knownTechs.Count;
			foreach (ID id in us.playerRelationships.Keys) {
				changed |= s.met.Add(id);
			}
			return changed;
		}

		// Swaps techs with them while there are swaps both accept, returning
		// how many were made.
		public static int Swap(GameData gD, Player us, Player them) {
			int made = 0;
			while (made < MostSwapsPerPair && SwapOne(gD, us, them)) {
				++made;
			}
			return made;
		}

		private static bool SwapOne(GameData gD, Player us, Player them) {
			List<Tech> wanted = them.GetTechsTradableTo(us, gD.techs);
			List<Tech> baits = us.GetTechsTradableTo(them, gD.techs);
			if (wanted.Count == 0 || baits.Count == 0) {
				return false;
			}
			HashSet<Tech> route = GovernmentAI.RoutePath(us, gD);
			int ourBeakers = us.BeakersPerTurn();
			// Techs on the government route first, then the most valuable.
			IEnumerable<Tech> asks = wanted
				.OrderByDescending(t => route.Contains(t))
				.ThenByDescending(t => DevelopmentAI.TechValue(gD, us, t, ourBeakers));
			// The tech least valuable to us goes first, the dearest to them
			// among equals.
			List<Tech> offers = baits
				.OrderBy(t => DevelopmentAI.TechValue(gD, us, t, ourBeakers))
				.ThenByDescending(t => gD.TechCostFor(t, them))
				.ToList();
			foreach (Tech ask in asks) {
				foreach (Tech bait in offers) {
					TradeOffer weGive = new() { techs = { bait } };
					TradeOffer weWant = new() { techs = { ask } };
					// Each side prices a tech at its buyer's cost, so the two
					// rarely match; the side whose tech is worth less adds
					// gold, if it has it.
					int gap = weWant.GoldEquivalentFor(gD, us, counterpart: them) - weGive.GoldEquivalentFor(gD, us, given: true, counterpart: them);
					if (gap > 0 && gap <= us.gold) {
						weGive.gold = gap;
					} else if (gap < 0 && -gap <= them.gold) {
						weWant.gold = -gap;
					} else if (gap != 0) {
						continue;
					}
					if (them.WouldAcceptDealFrom(gD, us, weGive, weWant)
							&& us.WouldAcceptDealFrom(gD, them, weWant, weGive)
							&& us.ExecuteDeal(gD, them, weWant, weGive)) {
						// Counted as a tech bought by each, for the trial measures.
						AiExperiments.onTechsBought?.Invoke(us, 1);
						AiExperiments.onTechsBought?.Invoke(them, 1);
						return true;
					}
				}
			}
			return false;
		}
	}
}
