using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.AI;
using C7Engine.AI.Advanced;
using C7GameData;

namespace C7Engine.PlayLog {
	// What the Advanced AI would have chosen at a human's decision (the play
	// log's "shadow"; see PlayRecorder): research, a city's production, a
	// revolution and the government after it, the tax rates and a city's
	// site. Each is asked through ShadowAdvanced, which leaves the game as it
	// was. The AI's own dice are left out (see ShadowAdvanced), so it is the
	// choice the AI makes most often.
	internal static class ShadowDecisions {
		// How many of the AI's best options a row lists.
		private const int Top = 3;

		// Adds the AI's choice to the message's row, before the message is
		// handled. Returns what to add once it has been (the human's choice
		// as the game took it), or null if the row is complete now.
		internal static Action<Dictionary<string, object>> Shadow(GameData gameData, Player player, ShadowAdvanced.State state,
				MessageToEngine msg, Dictionary<string, object> row) {
			switch (msg) {
				case MsgChooseProduction m: {
					City city = gameData.GetCity(m.cityID);
					if (city == null || city.owner != player) {
						return null;
					}
					row["shadow"] = Production(gameData, player, state, city, m.producibleName);
					return r => r["now"] = PlayRecorder.Text(city.itemBeingProduced);
				}
				case MsgChooseResearch m: {
					if (m.tech == null) {
						return null;
					}
					List<(Tech tech, double value)> values = ResearchValues(gameData, player, state);
					return r => r["shadow"] = Research(gameData, player, values);
				}
				case StartGovernmentTransitionMsg: {
					row["shadow"] = ShadowAdvanced.Evaluate(gameData, player, state, () => {
						Government target = player.government.transitionType ? null : GovernmentAI.RevolutionTarget(player, gameData);
						return new Dictionary<string, object> {
							["wouldRevolt"] = target != null,
							["target"] = target?.name,
							["best"] = GovernmentAI.BestGovernment(player, gameData, GovernmentAI.WarPressure(player, gameData))?.name,
							["current"] = player.government?.name,
						};
					});
					return r => r["anarchyUntilTurn"] = player.inAnarchyUntilTurn;
				}
				case SelectGovernmentMsg m: {
					string pick = ShadowAdvanced.Evaluate(gameData, player, state,
						() => GovernmentAI.BestGovernment(player, gameData, GovernmentAI.WarPressure(player, gameData))?.name);
					row["shadow"] = new Dictionary<string, object> {
						["pick"] = pick,
						["agree"] = pick != null && pick == m.government?.name,
					};
					return null;
				}
				case MsgChangeSliders: {
					Dictionary<string, object> rates = Sliders(gameData, player, state);
					return r => {
						Dictionary<string, object> after = PlayRecorder.Rates(player);
						r["after"] = after;
						r["shadow"] = new Dictionary<string, object> {
							["rates"] = rates,
							["agree"] = rates.All(kv => Equals(after[kv.Key], kv.Value)),
						};
					};
				}
				case MsgBuildCity m: {
					if (m.unit == null || m.unit.owner != player || m.unit.location == null || m.unit.location == Tile.NONE) {
						return null;
					}
					row["shadow"] = Site(gameData, player, state, m.unit.location);
					return null;
				}
				default:
					return null;
			}
		}

		// The AI's scores for what the city could build, and where the
		// human's choice ranks among them.
		private static Dictionary<string, object> Production(GameData gameData, Player player, ShadowAdvanced.State state, City city, string human) {
			List<(IProducible option, float score)> scored = ShadowAdvanced.Evaluate(gameData, player, state,
				() => ChooseProducible.ScoredOptions(city, player));
			// Highest first; of equal scores the first, as ChooseProducible.Choose takes.
			List<(IProducible option, float score)> ranked = scored.OrderByDescending(s => s.score).ToList();
			int rank = ranked.FindIndex(s => s.option.name == human);
			string pick = ranked.Count > 0 ? ranked[0].option.name : null;
			return new Dictionary<string, object> {
				["pick"] = pick,
				["agree"] = pick != null && pick == human,
				["humanRank"] = rank < 0 ? null : (object)(rank + 1),
				["humanScore"] = rank < 0 ? null : Score(ranked[rank].score),
				["options"] = ranked.Count,
				["top"] = ranked.Take(Top).Select(s => new Dictionary<string, object> {
					["item"] = s.option.name,
					["score"] = Score(s.score),
				}).ToList(),
			};
		}

		// A score, or "ruled out" for ChooseProducible's int.MinValue (an
		// option it never takes, such as a settler from a city too small).
		private static object Score(float score) => score <= int.MinValue / 2f ? "ruled out" : Math.Round(score, 1);

		// The worth the AI puts on each tech the player could research now
		// (DevelopmentAI.TechValue; its pick is a roll weighted by them).
		private static List<(Tech tech, double value)> ResearchValues(GameData gameData, Player player, ShadowAdvanced.State state) {
			return ShadowAdvanced.Evaluate(gameData, player, state, () => {
				int beakers = player.BeakersPerTurn();
				return ((IEnumerable<Tech>)player.GetAvailableTechsToResearch(gameData.techs))
					.Select(t => (t, DevelopmentAI.TechValue(gameData, player, t, beakers))).ToList();
			});
		}

		private static Dictionary<string, object> Research(GameData gameData, Player player, List<(Tech tech, double value)> values) {
			double total = values.Sum(v => Math.Max(0, v.value));
			List<(Tech tech, double value)> ranked = values.OrderByDescending(v => v.value).ToList();
			Tech human = gameData.GetTech(player.currentlyResearchedTech);
			int rank = ranked.FindIndex(v => v.tech == human);
			string pick = ranked.Count > 0 ? ranked[0].tech.Name : null;
			return new Dictionary<string, object> {
				["pick"] = pick,
				["agree"] = pick != null && pick == human?.Name,
				["human"] = human?.Name,
				["humanRank"] = rank < 0 ? null : (object)(rank + 1),
				// The chance the AI's roll would have picked the human's tech.
				["humanShare"] = rank < 0 || total <= 0 ? null : (object)Math.Round(Math.Max(0, ranked[rank].value) / total, 3),
				["options"] = ranked.Count,
				["top"] = ranked.Take(Top).Select(v => new Dictionary<string, object> {
					["tech"] = v.tech.Name,
					["value"] = Math.Round(v.value, 1),
					["share"] = total <= 0 ? 0 : Math.Round(Math.Max(0, v.value) / total, 3),
				}).ToList(),
			};
		}

		// The rates the AI would set (PlayerAI.AdjustSliders), with the
		// citizens where the human has them. The AI moves the rates and
		// works out the cities' moods as it goes; both are put back.
		private static Dictionary<string, object> Sliders(GameData gameData, Player player, ShadowAdvanced.State state) {
			int tax = player.taxRate, science = player.scienceRate, luxury = player.luxuryRate;
			List<(CityResident resident, CityResident.Mood mood)> moods = player.cities
				.SelectMany(c => c.residents).Select(r => (r, r.mood)).ToList();
			try {
				return ShadowAdvanced.Evaluate(gameData, player, state, () => {
					PlayerAI.AdjustSliders(player, reassignCitizens: false);
					return PlayRecorder.Rates(player);
				});
			} finally {
				player.taxRate = tax;
				player.scienceRate = science;
				player.luxuryRate = luxury;
				foreach ((CityResident resident, CityResident.Mood mood) in moods) {
					resident.mood = mood;
				}
			}
		}

		// How the AI scores the tile the human founds a city on, against the
		// other sites it knows of (SettlerLocationAI), and where it would
		// send this settler.
		private static Dictionary<string, object> Site(GameData gameData, Player player, ShadowAdvanced.State state, Tile tile) {
			(Dictionary<Tile, float> scores, Tile aiPick) = ShadowAdvanced.Evaluate(gameData, player, state, () => (
				SettlerLocationAI.GetScoredSettlerCandidates(tile, player),
				SettlerLocationAI.FindSettlerLocation(tile, player)));
			List<KeyValuePair<Tile, float>> ranked = scores.OrderByDescending(s => s.Value).ToList();
			int rank = ranked.FindIndex(s => s.Key == tile);
			Dictionary<string, object> Site(Tile t) => new() {
				["at"] = PlayRecorder.TileText(t),
				["score"] = scores.TryGetValue(t, out float s) ? (object)Math.Round(s, 1) : null,
				["distance"] = tile.DistanceTo(t),
			};
			return new Dictionary<string, object> {
				["siteScore"] = rank < 0 ? null : (object)Math.Round(ranked[rank].Value, 1),
				["siteRank"] = rank < 0 ? null : (object)(rank + 1),
				["candidates"] = ranked.Count,
				["bestScore"] = ranked.Count > 0 ? (object)Math.Round(ranked[0].Value, 1) : null,
				["top"] = ranked.Take(Top).Select(s => Site(s.Key)).ToList(),
				["aiPick"] = aiPick == null || aiPick == Tile.NONE ? null : Site(aiPick),
			};
		}
	}
}
