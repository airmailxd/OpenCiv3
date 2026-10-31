using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7Engine.AI.Advanced;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// On trial (switch "total-war", Advanced AI only): the owner's idea of
	// "declare war with a big stack and never give up until the civ is dead".
	// The civ picks one victim, builds a stack whose attack is a multiple of
	// the victim's defence near our border, declares, and takes city after
	// city (WarCampaign does the gathering, marching and sieging), refusing
	// every peace and proposing none until the victim is gone. Then it rests
	// and picks the next.
	//
	// The only ways out: we are losing cities of our own, or the war has gone
	// total-war-max-turns with no city taken for the last NoGainTurns turns.
	//
	// Knobs (AdvancedTuning): total-war-ratio, total-war-max-turns,
	// total-war-rest, total-war-victim (0 nearest, 1 weakest).
	public static class TotalWar {
		private static readonly ILogger log = Log.ForContext(typeof(TotalWar));

		public const string Switch = "total-war";
		public const string Ratio = "total-war-ratio";
		public const string MaxTurns = "total-war-max-turns";
		public const string Rest = "total-war-rest";
		public const string VictimChoice = "total-war-victim";

		// HEURISTIC: a war with no city taken for this long may be given up
		// once it has gone total-war-max-turns.
		private const int NoGainTurns = 30;
		// HEURISTIC: losing this many cities of our own during the war ends it.
		// On purpose, any loss counts, including a city the victim takes back:
		// the engine does not record who took a city, and a victim that wins
		// cities off us is not one we are beating.
		private const int CitiesLostToQuit = 2;
		// HEURISTIC: only victims whose cities lie this close to one of ours
		// count as near for the gathered stack's measure.
		private const int BorderRadius = 10;
		// HEURISTIC: the defence of a city of theirs we can't see into.
		private const double UnseenCityDefence = 6;

		private sealed class State {
			public Player victim;
			public int warStart;
			public int lastGainTurn;
			public int victimCities;
			public int mostCities;
			public int restUntil = -1;
		}

		private static readonly ConditionalWeakTable<Player, State> states = new();

		public static bool On(Player player) => player != null && AiExperiments.Has(player, Switch);

		// A rival we could take on: how far, how strong and how productive.
		public sealed record Candidate(Player rival, int distance, float strength, int production);

		// The victim among the candidates: only those we out-produce, the
		// nearest (ties: the weakest) or the weakest (ties: the nearest).
		// Null if none.
		public static Candidate Choose(IEnumerable<Candidate> candidates, int ourProduction, bool weakest) {
			IEnumerable<Candidate> able = candidates.Where(c => ourProduction >= c.production);
			return (weakest
				? able.OrderBy(c => c.strength).ThenBy(c => c.distance)
				: able.OrderBy(c => c.distance).ThenBy(c => c.strength)).FirstOrDefault();
		}

		// Whether we refuse peace with them: they are the victim of a war we
		// are fighting to the end.
		public static bool RefusesPeace(Player us, Player them) {
			return On(us) && states.TryGetValue(us, out State s) && s.victim == them && !them.defeated;
		}

		public static Player Victim(Player player) {
			return On(player) && states.TryGetValue(player, out State s) && s.victim != null && !s.victim.defeated ? s.victim : null;
		}

		// Whether the stack is still short of what the victim's defence
		// calls for, so that cities keep building attackers.
		public static bool WantsMoreStack(Player player, IEnumerable<MapUnit> stack, Tile staging) {
			Player victim = Victim(player);
			return victim != null && AttackOf(stack) < Ratio_(player) * DefenceNear(player, victim, staging);
		}

		// Whether the gathered attackers are enough to declare and march.
		public static bool StackReady(Player player, Player victim, IEnumerable<MapUnit> ready, Tile staging) {
			return AttackOf(ready) >= Ratio_(player) * DefenceNear(player, victim, staging);
		}

		private static double Ratio_(Player player) => AdvancedTuning.Get(player, Ratio);

		private static double AttackOf(IEnumerable<MapUnit> units) {
			return units.Where(WarCampaign.IsAttacker).Sum(u => (double)u.unitType.attack);
		}

		// The victim's defence on cities within BorderRadius of the staging
		// city: what we see of their defenders, and a guess for cities we
		// can't see into.
		internal static double DefenceNear(Player player, Player victim, Tile staging) {
			double total = 0;
			foreach (City city in victim.cities) {
				if (city.location.continent != staging.continent || city.location.DistanceTo(staging) > BorderRadius) {
					continue;
				}
				if (AIFogOfWar.SeesUnitsOn(player, city.location)) {
					total += city.location.unitsOnTile.Where(u => u.CanDefendOnLand()).Sum(u => (double)u.unitType.defense * u.hitPointsRemaining / Math.Max(1, u.maxHitPoints));
				} else {
					total += UnseenCityDefence;
				}
			}
			return Math.Max(UnseenCityDefence, total);
		}

		// Early in the civ's turn: ends a war that is over or given up, and
		// picks a victim when it is time for one. Returns the victim, or null.
		public static Player Update(Player player, GameData gameData) {
			if (!On(player)) {
				return null;
			}
			State s = states.GetOrCreateValue(player);
			if (s.victim != null) {
				bool done = false;
				if (s.victim.defeated || s.victim.cities.Count == 0) {
					log.Information("{Player} has destroyed {Victim}; resting {Rest} turns", player, s.victim, RestTurns(player));
					done = true;
				} else {
					if (s.victim.cities.Count < s.victimCities) {
						s.lastGainTurn = gameData.turn;
					}
					s.victimCities = s.victim.cities.Count;
					s.mostCities = Math.Max(s.mostCities, player.cities.Count);
					int turns = gameData.turn - s.warStart;
					if (s.mostCities - player.cities.Count >= CitiesLostToQuit) {
						log.Information("{Player} gives up total war on {Victim}: losing its own cities", player, s.victim);
						done = true;
					} else if (turns >= AdvancedTuning.GetInt(player, MaxTurns) && gameData.turn - s.lastGainTurn >= NoGainTurns) {
						log.Information("{Player} gives up total war on {Victim}: {Turns} turns, no city taken since turn {Gain}", player, s.victim, turns, s.lastGainTurn);
						done = true;
					}
				}
				if (done) {
					s.victim = null;
					s.restUntil = gameData.turn + RestTurns(player);
				}
			}
			if (s.victim == null && gameData.turn >= s.restUntil && player.cities.Count > 0) {
				PickVictim(player, gameData, s);
			}
			return s.victim;
		}

		private static int RestTurns(Player player) => AdvancedTuning.GetInt(player, Rest);

		private static void PickVictim(Player player, GameData gameData, State s) {
			int ours = Production(player);
			List<Candidate> candidates = [];
			foreach (Player p in gameData.players) {
				if (p == player || p.IsIndependent || p.defeated || p.cities.Count == 0 || !WarCampaign.CanFight(player, p)) {
					continue;
				}
				int distance = int.MaxValue;
				foreach (City theirs in p.cities) {
					if (!AIFogOfWar.KnowsTile(player, theirs.location)) {
						continue;
					}
					foreach (City mine in player.cities) {
						if (mine.location.continent == theirs.location.continent) {
							distance = Math.Min(distance, mine.location.DistanceTo(theirs.location));
						}
					}
				}
				if (distance != int.MaxValue) {
					candidates.Add(new Candidate(p, distance, p.CalculateMilitaryStrength(), Production(p)));
				}
			}
			bool weakest = AdvancedTuning.Get(player, VictimChoice) >= .5;
			Candidate pick = Choose(candidates, ours, weakest);
			if (pick == null) {
				return;
			}
			Begin(player, s, pick.rival, gameData.turn);
			log.Information("{Player} picks {Victim} for total war ({Choice}): {Distance} tiles away, strength {Their:0} against ours {Ours:0}, production {TheirProd} against ours {OurProd}",
				player, pick.rival, weakest ? "weakest" : "nearest", pick.distance, pick.strength, player.CalculateMilitaryStrength(), pick.production, ours);
		}

		internal static void Begin(Player player, Player victim, int turn) {
			Begin(player, states.GetOrCreateValue(player), victim, turn);
		}

		private static void Begin(Player player, State s, Player victim, int turn) {
			s.victim = victim;
			s.warStart = turn;
			s.lastGainTurn = turn;
			s.victimCities = victim.cities.Count;
			s.mostCities = player.cities.Count;
		}

		// What a civ can make in a turn, all its cities together.
		internal static int Production(Player p) {
			return p.cities.Sum(c => c.CurrentProductionYield().useful);
		}
	}
}
