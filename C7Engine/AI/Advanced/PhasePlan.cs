using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.Advanced {
	// On trial (switch "phase-plan", owner's idea, ZUR-122): the Advanced AI
	// plays a script. Expand to a few cities toward the nearest rival on
	// its landmass, with a road to them; muster an army of attackers in
	// the capital and its best producing cities, with Barracks; make the
	// first war on the weakest rival it can march on (an ordinary
	// WarCampaign, the archer rush's stack and march, see ArcherRush);
	// consolidate by filling the gaps; then muster again. The phase is
	// saved with the AI's state (GamePhaseState) and sets the rules'
	// plan (see RulePlanner). Losing a war puts the civ in Consolidate,
	// and the rules defend as ever.
	public static class PhasePlan {
		public const string Switch = "phase-plan";

		// HEURISTIC: the army size's cap unless the knob is higher, and the
		// most attackers a script war gathers (as WarCampaign.MaxAttackers).
		public const int ArmyCap = 12;
		public const int MostAttackers = 16;
		// HEURISTIC: attackers per defender seen in the target city.
		public const int AttackersPerDefender = 3;
		// HEURISTIC: how often, in turns, the phases look for open city
		// sites (the search is dear).
		public const int SiteCheckTurns = 5;
		// HEURISTIC: how much a city site nearer the rival than our nearest
		// city is worth in Expand, at the rival's own doorstep; the size of
		// SettlerLocationAI.TightSiteBonus.
		public const float TowardRivalBonus = 40;
		// HEURISTIC: the muster cities besides the capital: one, or two
		// from this many cities.
		public const int TwoMusterCitiesFrom = 4;

		// Lets AiTrialTest log the phase changes. Null outside a trial.
		internal static Action<Player, GamePhase?, GamePhase> onPhase;

		public static bool On(Player player) {
			return player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);
		}

		public static GamePhase? PhaseOf(Player player) {
			return On(player) ? player.gamePhase?.phase : null;
		}

		public static bool Expanding(Player player) => PhaseOf(player) == GamePhase.Expand;

		// Whether the civ is gathering its army (Barracks, attackers).
		public static bool Mustering(Player player) => PhaseOf(player) == GamePhase.Muster;

		public static int Cities(Player player) => Math.Clamp(AdvancedTuning.GetInt(player, "PhaseCities"), 3, 5);

		public static int ConsolidateTurns(Player player) => Math.Max(1, AdvancedTuning.GetInt(player, "PhaseConsolidateTurns"));

		// The attackers to gather: the knob, one more for each difficulty
		// level above Regent (to the cap, or the knob if that is higher),
		// and at least 3 for each defender known in the target city.
		public static int ArmySize(Player player, GameData gameData, int defenders) {
			int knob = AdvancedTuning.GetInt(player, "PhaseArmy");
			int levels = Math.Max(0, gameData.difficulties.IndexOf(gameData.gameDifficulty) - gameData.difficulties.IndexOf(gameData.AiRulesDifficulty()));
			int size = Math.Min(knob + levels, Math.Max(ArmyCap, knob));
			return Math.Min(Math.Max(size, AttackersPerDefender * defenders), MostAttackers);
		}

		// The healthy attackers left after which the war goes on: a third
		// of the army.
		public static int AttackersToContinue(Player player, GameData gameData) {
			return (ArmySize(player, gameData, 0) + 2) / 3;
		}

		// The cities that build Barracks and the army: the capital and the
		// ones with the most shields left after corruption, on the
		// capital's landmass (one more from TwoMusterCitiesFrom cities).
		public static List<City> ChooseMusterCities(Player player) {
			City capital = player.cities.Find(c => c.IsCapital()) ?? player.cities.OrderBy(c => c.id.ToString()).FirstOrDefault();
			if (capital == null) {
				return [];
			}
			List<City> result = [capital];
			int more = player.cities.Count >= TwoMusterCitiesFrom ? 2 : 1;
			result.AddRange(player.cities.Where(c => c != capital && c.location.continent == capital.location.continent)
				.OrderByDescending(c => c.CurrentProductionYield().useful).ThenBy(c => c.id.ToString()).Take(more));
			return result;
		}

		public static bool IsMusterCity(Player player, City city) {
			return On(player) && player.gamePhase?.musterCities.Contains(city.id.ToString()) == true;
		}

		// Whether the city builds Barracks for the script.
		public static bool WantsBarracks(Player player, City city) {
			return PhaseOf(player) is GamePhase.Muster or GamePhase.War && IsMusterCity(player, city);
		}

		// The rival the road and the settlers head for: the nearest city
		// of one we know, on a landmass of ours, and our city nearest to it.
		public static (City ours, City theirs)? NearestRival(Player player) {
			(City ours, City theirs)? best = null;
			int bestDistance = int.MaxValue;
			foreach (Player p in EngineStorage.gameData.players) {
				if (p == player || p.IsIndependent || p.defeated) {
					continue;
				}
				foreach (City theirs in p.cities) {
					if (!AIFogOfWar.KnowsTile(player, theirs.location)) {
						continue;
					}
					foreach (City ours in player.cities) {
						int d = ours.location.continent == theirs.location.continent ? ours.location.DistanceTo(theirs.location) : int.MaxValue;
						if (d < bestDistance) {
							bestDistance = d;
							best = (ours, theirs);
						}
					}
				}
			}
			return best;
		}

		// The settler-site bonus of a tile in Expand: up to TowardRivalBonus
		// the nearer it is to the rival's city than our nearest city.
		public static float SiteBonus(Tile site, (City ours, City theirs) rival) {
			if (site.continent != rival.theirs.location.continent) {
				return 0;
			}
			int start = rival.ours.location.DistanceTo(rival.theirs.location);
			int gain = start - site.DistanceTo(rival.theirs.location);
			return start <= 0 || gain <= 0 ? 0 : TowardRivalBonus * Math.Min(1f, (float)gain / start);
		}

		// Whether the script has a rival to muster against.
		public static bool HasTarget(Player player, GameData gameData) {
			return WarCampaign.PickScriptTarget(player, gameData, out _) != null;
		}

		// Moves the script on at the start of the turn; true if the phase
		// changed.
		public static bool Update(Player player, GameData gameData) {
			if (!On(player) || player.cities.Count == 0) {
				return false;
			}
			GamePhaseState state = player.gamePhase;
			if (state == null) {
				Enter(player, gameData, GamePhase.Expand);
				return true;
			}
			GamePhase next = NextPhase(player, gameData, state);
			if (next == state.phase) {
				return false;
			}
			Enter(player, gameData, next);
			return true;
		}

		private static GamePhase NextPhase(Player player, GameData gameData, GamePhaseState state) {
			bool sites = gameData.turn % SiteCheckTurns == 0;
			// Losing a war: no more mustering until it is over.
			bool losing = Losing(player, gameData);
			switch (state.phase) {
				case GamePhase.Expand:
					if (player.cities.Count >= Cities(player) || (sites && player.cities.Count >= 2 && OpenSites(player) == 0)) {
						return !losing && HasTarget(player, gameData) ? GamePhase.Muster : GamePhase.Consolidate;
					}
					break;
				case GamePhase.Muster:
					if (losing) {
						return GamePhase.Consolidate;
					}
					if (WarCampaign.PhaseCampaignMarching(player)) {
						return GamePhase.War;
					}
					break;
				case GamePhase.War:
					if (losing || !WarCampaign.HasPhaseCampaign(player)) {
						return GamePhase.Consolidate;
					}
					break;
				default:
					if (!losing && (gameData.turn - state.startTurn >= ConsolidateTurns(player) || (sites && OpenSites(player) == 0))
							&& HasTarget(player, gameData)) {
						return GamePhase.Muster;
					}
					break;
			}
			return state.phase;
		}

		private static int OpenSites(Player player) {
			return player.cities.Max(c => ChooseProducible.NumberOfReachableOpenCitySpots(c));
		}

		// Whether the war that goes worst for the civ is a lost one (the
		// rules' own test, see RulePlanner).
		public static bool Losing(Player player, GameData gameData) {
			List<Player> enemies = gameData.players.Where(p => p != player && !p.IsIndependent && !p.defeated
				&& player.playerRelationships.ContainsKey(p.id) && AtWar(player, p)).ToList();
			return enemies.Count > 0 && enemies.Min(e => PeaceAI.Assess(player, e)) <= PeaceAI.WarOutlook.Losing;
		}

		private static void Enter(Player player, GameData gameData, GamePhase phase) {
			GamePhase? from = player.gamePhase?.phase;
			GamePhaseState state = player.gamePhase ??= new GamePhaseState();
			state.phase = phase;
			state.startTurn = gameData.turn;
			if (phase == GamePhase.Muster) {
				state.musterCities = ChooseMusterCities(player).Select(c => c.id.ToString()).ToList();
			}
			if (phase == GamePhase.War && state.firstWarTurn < 0) {
				state.firstWarTurn = gameData.turn;
			}
			onPhase?.Invoke(player, from, phase);
		}

		// The phase's say in the plan, when no war is on (a war is the
		// rules' own): what to build, and who to muster against.
		public static void ApplyFocus(Player player, StrategicPlan plan, List<string> why) {
			GamePhase? phase = PhaseOf(player);
			switch (phase) {
				case GamePhase.Expand:
					plan.focus[PlanFocus.Settlers] = 90;
					plan.focus[PlanFocus.Workers] = 65;
					plan.focus[PlanFocus.Growth] = 60;
					why.Add("phase Expand: cities toward the nearest rival, with a road");
					break;
				case GamePhase.Muster:
					// The muster cities build the army (see ChooseProducible);
					// the others grow and keep working the land.
					plan.focus[PlanFocus.Settlers] = 45;
					plan.focus[PlanFocus.Workers] = 60;
					plan.focus[PlanFocus.Growth] = 60;
					plan.focus[PlanFocus.Production] = 55;
					if (WarCampaign.TryGetCampaign(player, out Player enemy, out _, out _, out _)) {
						plan.stances[enemy.id.ToString()] = RivalStance.Target;
					}
					why.Add("phase Muster: Barracks and attackers in the muster cities");
					break;
				case GamePhase.Consolidate:
					plan.focus[PlanFocus.Settlers] = 80;
					plan.focus[PlanFocus.Workers] = 70;
					plan.focus[PlanFocus.Production] = 60;
					plan.focus[PlanFocus.Growth] = 60;
					plan.focus[PlanFocus.Science] = 55;
					why.Add("phase Consolidate: fill the gaps, build up");
					break;
			}
		}
	}
}
