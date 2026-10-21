using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7Engine.Pathing;

namespace C7GameData {

	// Works out from the game what an advisor's speech is chosen from (see
	// AdvisorSpeech). Each part says which Civ3 function it stands in for;
	// where OpenCiv3 doesn't keep what Civ3 looks at, the nearest thing it
	// does keep is used, and the comment says so.
	public static class AdvisorFactsGatherer {
		// The facts the advisor's speech needs. selectedForeignCiv is the
		// leader picked on the foreign advisor's screen, and militaryRival the
		// civ picked to compare armies with on the military advisor's, each
		// null if none.
		public static AdvisorFacts Gather(GameData gameData, Player player, AdvisorKind advisor,
				Player selectedForeignCiv = null, bool foreignTradeView = false, Player militaryRival = null) {
			AdvisorFacts facts = new() {
				LocalHour = DateTime.Now.Hour,
				LeaderTitle = player.civilization?.leaderTitle ?? "",
				OurCivName = player.civilization?.name ?? "",
				OtherCivNames = gameData.GetKnownRivals(player).Select(r => r.civilization?.name ?? "").ToList(),
			};
			switch (advisor) {
				case AdvisorKind.Domestic:
					GatherDomestic(gameData, player, facts);
					break;
				case AdvisorKind.Trade:
					GatherTrade(gameData, player, facts);
					break;
				case AdvisorKind.Military:
					GatherMilitary(gameData, player, militaryRival, facts);
					break;
				case AdvisorKind.Foreign:
					GatherForeign(gameData, player, selectedForeignCiv, foreignTradeView, facts);
					break;
				case AdvisorKind.Culture:
					GatherCulture(gameData, player, facts);
					break;
				case AdvisorKind.Science:
					GatherScience(gameData, player, facts);
					break;
			}
			return facts;
		}

		// The titles the player's ruler may go by under their government, by
		// the leader's gender, the male one where there's no female one
		// (Civ3 FUN_00544d70). With none imported, the government's name.
		public static List<string> RulerTitles(Player player) {
			Government government = player.government;
			if (government == null) {
				return [];
			}
			List<string> male = government.maleRulerTitles ?? [];
			List<string> female = government.femaleRulerTitles ?? [];
			bool isFemale = player.civilization?.leaderGender == Gender.Female;
			List<string> titles = [];
			for (int i = 0; i < male.Count; i++) {
				string title = isFemale && i < female.Count && !string.IsNullOrEmpty(female[i]) ? female[i] : male[i];
				if (!string.IsNullOrEmpty(title)) {
					titles.Add(title);
				}
			}
			if (titles.Count == 0 && !string.IsNullOrEmpty(government.name)) {
				titles.Add(government.name);
			}
			return titles;
		}

		private static void GatherDomestic(GameData gameData, Player player, AdvisorFacts facts) {
			facts.RulerTitles = RulerTitles(player);
			foreach (City city in player.cities) {
				// Civ3 FUN_004b8d50: the improvement a city at its size limit
				// needs, an aqueduct up to the second limit and a hospital
				// past it.
				GrowthBlocker blocker = GrowthBlocker.None;
				if (!city.CanGrowPopulationByOne(gameData)) {
					blocker = city.residents.Count >= gameData.rules.MaximumLevel2CitySize ? GrowthBlocker.Hospital : GrowthBlocker.Aqueduct;
				}
				facts.Cities.Add(new CityAdviceFacts(city.name, city.FoodGrowthPerTurn(),
					city.foodStored >= city.FoodNeededToGrow(), blocker,
					city.residents.Count(r => r.mood == CityResident.Mood.Happy),
					city.residents.Count(r => r.mood == CityResident.Mood.Unhappy),
					city.isInCivilDisorder));
			}
			if (player.government?.transitionType == true && player.inAnarchyUntilTurn > gameData.turn) {
				facts.AnarchyTurns = player.inAnarchyUntilTurn - gameData.turn;
			}
		}

		private static void GatherTrade(GameData gameData, Player player, AdvisorFacts facts) {
			// Civ3 FUN_005bdf80 and FUN_005be460 read a table of the
			// resources the civ has to trade that OpenCiv3 doesn't keep;
			// here they are the luxuries and strategic resources on the
			// capital's network, and those it has only one of.
			Dictionary<Resource, int> local = TradeReport.LocalResources(gameData, player);
			facts.HasNoResources = local.Count == 0;
			facts.SingleSourceResources = local.Where(r => r.Value == 1).Select(r => r.Key.Name).OrderBy(n => n).ToList();

			List<Player> rivals = gameData.GetKnownRivals(player);
			TradeNetwork network = gameData.GetTradeNetwork();
			// Civ3 FUN_005be0a0 and FUN_005be210 only look at human players'
			// civs (the mask at 0xa74eb4, which FUN_00572460 and
			// FUN_005693a0 also read as "human"), so in a game against the
			// computer these topics never come up.
			if (!player.isHuman) {
				return;
			}
			foreach (Player rival in rivals.Where(r => r.isHuman)) {
				if (!CanTradeWith(network, player, rival)) {
					facts.CivsWithoutTradeRoute.Add(ForeignReport.CivNoun(rival));
				}
			}
			foreach (Player enemy in rivals.Where(r => r.isHuman && PlayerRelationship.AtWar(player, r))) {
				foreach (Player partner in gameData.GetRivals(player).Where(p => p.isHuman && p != enemy)) {
					// Civ3 FUN_0050b810: the partner trades with our enemy and
					// has no embargo against it.
					if (TradeReport.StatusWith(partner, enemy) != TradeStatus.CannotTradeWith) {
						facts.EmbargoPartners.Add((ForeignReport.CivNoun(partner), enemy.civilization?.name ?? ""));
					}
				}
			}
		}

		// Civ3 FUN_0056aa60: the two have met, aren't at war, neither has an
		// embargo against the other, and their capitals are connected.
		private static bool CanTradeWith(TradeNetwork network, Player player, Player other) {
			return !PlayerRelationship.AtWar(player, other)
				&& !TradeReport.HasEmbargoAgainst(player, other) && !TradeReport.HasEmbargoAgainst(other, player)
				&& network.CapitalsConnected(player, other);
		}

		private static void GatherMilitary(GameData gameData, Player player, Player rival, AdvisorFacts facts) {
			if (rival != null && rival != player && !rival.defeated) {
				facts.Strength = (MilitaryScore(gameData, player), MilitaryScore(gameData, rival));
			}
			facts.CityCount = player.cities.Count;
			facts.NetGoldPerTurn = player.CalculateGoldPerTurn();
			facts.UnitSupportCost = player.TotalUnitsAllowedUnitsAndSupportCost().Item3;
			facts.BestUnit = BestUnit(gameData, player)?.name;
			facts.BarbariansNear = BarbariansNear(gameData, player);
			facts.CanPlantSpy = Espionage.HasIntelligenceAgency(player)
				&& gameData.GetKnownRivals(player).Any(r => Espionage.Capital(r) != null && !Espionage.HasSpy(player, r));
			facts.NuclearWeaponsAllowed = UnitPrototype.NuclearWeaponsAllowed(gameData);
		}

		// A civ's army as the military advisor weighs it (Civ3
		// FUN_0042b3d0): for each unit, three times its attack times its hit
		// points, plus its bombard, plus twice its defence times its hit
		// points with a fortified land unit's bonus (FUN_0042d120).
		public static long MilitaryScore(GameData gameData, Player player) {
			int fortifyPercent = (int)Math.Round(gameData.fortificationBonus.amount * 100);
			long score = 0;
			foreach (MapUnit unit in player.units) {
				UnitPrototype type = unit.unitType;
				if (type == null) {
					continue;
				}
				int hp = Math.Clamp(unit.hitPointsRemaining, 0, 9999);
				int bonus = unit.isFortified && type.IsLandUnit() && unit.location?.cityAtTile == null ? fortifyPercent : 0;
				score += (long)type.attack * hp * 3 + type.bombard + (long)(bonus + 100) * type.defense * hp / 100 * 2;
			}
			return score;
		}

		// The land unit the player can build, and has at least one of, that
		// scores best on three times its attack, twice its defence and its
		// bombard, with twice its moves added to a score over 0 (Civ3
		// FUN_0044aa20). Null if none scores over 0.
		public static UnitPrototype BestUnit(GameData gameData, Player player) {
			HashSet<UnitPrototype> buildable = [];
			foreach (City city in player.cities) {
				foreach (IProducible option in city.ListProductionOptions(gameData)) {
					if (option is UnitPrototype type) {
						buildable.Add(type);
					}
				}
			}
			HashSet<UnitPrototype> owned = player.units.Select(u => u.unitType).Where(t => t != null).ToHashSet();
			UnitPrototype best = null;
			int bestScore = 0;
			foreach (UnitPrototype type in gameData.unitPrototypes) {
				if (!type.IsLandUnit() || !buildable.Contains(type) || !owned.Contains(type)) {
					continue;
				}
				int score = type.attack * 3 + type.defense * 2 + type.bombard;
				if (score > 0) {
					score += type.movement * 2;
				}
				if (score > bestScore) {
					bestScore = score;
					best = type;
				}
			}
			return best;
		}

		// The barbarian camp nearest one of our cities on its landmass, if
		// within 10 tiles, with the camp's tribe and the city (Civ3
		// FUN_0050b3e0, whose tribe is the barbarians' list entry the tile
		// names). UNVERIFIED: that it looks at camps rather than barbarian
		// units.
		private static (string, string)? BarbariansNear(GameData gameData, Player player) {
			(string, string)? result = null;
			int nearest = 10;
			foreach (Tile camp in gameData.map.barbarianCamps) {
				foreach (City city in player.cities) {
					if (city.location == null || city.location.continent != camp.continent) {
						continue;
					}
					int distance = camp.DistanceTo(city.location);
					if (distance < nearest) {
						nearest = distance;
						result = (BarbarianInteractions.TribeName(gameData, camp), city.name);
					}
				}
			}
			return result;
		}

		private static void GatherForeign(GameData gameData, Player player, Player selected, bool tradeView, AdvisorFacts facts) {
			TradeNetwork network = gameData.GetTradeNetwork();
			int ourCulture = CultureReport.TotalCulture(player);
			foreach (Player civ in gameData.GetKnownRivals(player)) {
				int theirCulture = CultureReport.TotalCulture(civ);
				(string opinion, AdvisorMood mood) = Opinion(gameData.rules, ourCulture, theirCulture);
				bool canTrade = CanTradeWith(network, player, civ);
				string theirSurplus = null;
				string ourSurplus = null;
				if (canTrade) {
					theirSurplus = TradeReport.ExcessResources(gameData, civ, player).Select(r => r.resource.Name).FirstOrDefault();
					ourSurplus = TradeReport.ExcessResources(gameData, player, civ).Select(r => r.resource.Name).FirstOrDefault();
				}
				if (civ == selected) {
					facts.SelectedForeignCiv = facts.ForeignCivs.Count;
				}
				facts.ForeignCivs.Add(new ForeignAdviceFacts(ForeignReport.CivNoun(civ), Adjective(civ),
					ForeignReport.AttitudeTowardViewer(gameData, player, civ), opinion, mood,
					ForeignReport.BestKnownUnit(player, civ)?.name, canTrade, theirSurplus, ourSurplus));
			}
			facts.ForeignTradeView = tradeView;
		}

		private static string Adjective(Player civ) =>
			civ.civilization?.adjective is string adjective && adjective.Length > 0 ? adjective : ForeignReport.CivNoun(civ);

		// What a civ with theirCulture thinks of ours, with the face Civ3
		// gives it (FUN_00502530): happy when ours is over 4/3 of theirs, sad
		// when it is over 3/4 of theirs but not that, and otherwise left
		// happy.
		public static (string opinion, AdvisorMood mood) Opinion(Rules rules, int ourCulture, int theirCulture) {
			string opinion = rules == null ? "" : CultureReport.OpinionOf(rules, theirCulture, ourCulture);
			bool farAhead = (long)theirCulture * 4 < (long)ourCulture * 3;
			bool sad = !farAhead && (long)ourCulture * 4 > (long)theirCulture * 3;
			return (opinion, sad ? AdvisorMood.Sad : AdvisorMood.Happy);
		}

		private static void GatherCulture(GameData gameData, Player player, AdvisorFacts facts) {
			// Civ3 FUN_00504fe0 and FUN_00504e20: a building for each city
			// that can build it, so the more cities can, the likelier it is.
			// Improvements are those with culture that aren't wonders or a
			// palace.
			foreach (City city in player.cities) {
				foreach (IProducible option in city.ListProductionOptions(gameData)) {
					if (option is not Building building) {
						continue;
					}
					if (building.IsGreatWonder()) {
						facts.WonderChoices.Add(building.name);
					} else if (!building.isSmallWonder && !building.isCenterOfEmpire && building.culturePerTurn > 0) {
						facts.CultureImprovementChoices.Add(building.name);
					}
				}
			}
			// Civ3 FUN_00504cb0: the civs we've met.
			int ourCulture = CultureReport.TotalCulture(player);
			foreach (Player civ in gameData.GetKnownRivals(player)) {
				(string opinion, AdvisorMood mood) = Opinion(gameData.rules, ourCulture, CultureReport.TotalCulture(civ));
				facts.CultureOpinions.Add(new CultureOpinionFacts(Adjective(civ), opinion, mood));
			}
		}

		private static void GatherScience(GameData gameData, Player player, AdvisorFacts facts) {
			int ours = player.knownTechs.Count;
			foreach (Player civ in gameData.GetKnownRivals(player)) {
				if (civ.knownTechs.Count > ours) {
					facts.CivsAheadInScience++;
				} else {
					facts.CivsNotAheadInScience++;
				}
			}
			facts.ScienceRate = player.scienceRate;
			facts.Researching = player.currentlyResearchedTech != null && !player.knownTechs.Contains(player.currentlyResearchedTech);
			if (!facts.Researching) {
				// Civ3 FUN_0044b450: the tech the AI values most.
				int beakers = player.BeakersPerTurn();
				Tech best = null;
				double bestValue = 0;
				foreach (Tech tech in player.GetAvailableTechsToResearch(gameData.techs)) {
					double value = DevelopmentAI.TechValue(gameData, player, tech, beakers);
					if (value > bestValue) {
						bestValue = value;
						best = tech;
					}
				}
				facts.RecommendedTech = best?.Name;
			}
		}
	}
}
