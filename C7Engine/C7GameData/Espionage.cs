using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine {
	public enum EspionageMission {
		// Diplomatic missions. An embassy is established with a civ that has
		// been met; the others need an embassy with the target civ.
		EstablishEmbassy,
		InvestigateCity,
		StealTechnology,
		SabotageProduction,
		InciteRevolt,
		// Espionage missions, which need the Intelligence Agency. A spy is
		// planted in the capital of a civ with an embassy, and the other
		// missions need that spy.
		PlantSpy,
		ExposeEnemySpy,
	}

	// Civ3's diplomatic and espionage missions, launched from the actor's
	// capital against another civ, or one of its cities.
	//
	// Civ3 doesn't document its formulas, so the costs and chances here are
	// estimates (the assumptions are listed with each):
	// - Every mission costs gold, more the further the target is from the
	//   actor's capital. Establishing an embassy and investigating a city
	//   always succeed; the others have a chance of being caught.
	// - The chance improves with a spy planted in the target civ and with
	//   the actor owning the Intelligence Agency, and is reduced by the target
	//   owning one, by a Democracy (which Civ3 makes resistant to espionage),
	//   by the target city being the capital or having a courthouse-like
	//   building, and by distance.
	// - A caught mission is a diplomatic incident, which the target remembers
	//   (it costs the actor UN votes), and a caught spy is lost.
	// - Inciting a revolt hands the city, with the units in it, to the actor.
	//   Capitals can't be incited. The cost follows Civ2's formula, which
	//   Civ3 is believed to build on: (treasury + 1000) * size / (distance to
	//   the target's capital + 3), halved for a city in disorder or mostly of
	//   the actor's nationality, doubled under Democracy, plus 20 per unit.
	// - Stolen technology is a random advance the target knows and the actor
	//   could research (it knows the prerequisites).
	public static class Espionage {
		private static readonly ILogger log = Log.ForContext(typeof(Espionage));

		// The tech Civ3 needs for embassies ("enables diplomats"), if the
		// game has a tech of that name.
		public const string EmbassyTechName = "Writing";

		// The distance used when either civ has no cities to measure from.
		private const int UnknownDistance = 20;

		public class MissionOption {
			public EspionageMission mission;
			public bool available;
			// Why the mission isn't available, if it isn't.
			public string reason;
			public int cost;
			public int successPercent;
		}

		// What an investigated city reveals, as it was on the turn it was
		// investigated.
		public class CityReport {
			public int turn;
			public string cityName;
			public int size;
			public string producing;
			public int shieldsStored;
			public List<string> buildings = new();
			public List<string> units = new();
			public int ownerGold;
		}

		public class MissionResult {
			public EspionageMission mission;
			public bool performed;
			public bool success;
			public int goldSpent;
			public string message;
			public Tech stolenTech;
			public CityReport report;
			// The capital an embassy was established in.
			public City city;
			public Player exposedSpyOwner;
		}

		public static string Describe(EspionageMission mission) {
			return mission switch {
				EspionageMission.EstablishEmbassy => "Establish Embassy",
				EspionageMission.InvestigateCity => "Investigate City",
				EspionageMission.StealTechnology => "Steal Technology",
				EspionageMission.SabotageProduction => "Sabotage Production",
				EspionageMission.InciteRevolt => "Incite Revolt",
				EspionageMission.PlantSpy => "Plant Spy",
				EspionageMission.ExposeEnemySpy => "Expose Enemy Spy",
				_ => mission.ToString(),
			};
		}

		// Whether the mission is aimed at a city rather than a whole civ.
		public static bool TargetsCity(EspionageMission mission) {
			return mission is EspionageMission.InvestigateCity or EspionageMission.StealTechnology
				or EspionageMission.SabotageProduction or EspionageMission.InciteRevolt;
		}

		public static bool IsEspionageMission(EspionageMission mission) {
			return mission is EspionageMission.PlantSpy or EspionageMission.ExposeEnemySpy;
		}

		public static City Capital(Player player) {
			return player.cities.Find(c => c.IsCapital()) ?? player.cities.FirstOrDefault();
		}

		// Whether the player owns a building that allows spy missions (the
		// Intelligence Agency).
		public static bool HasIntelligenceAgency(Player player) {
			foreach (City c in player.cities) {
				foreach (CityBuilding cb in c.constructed_buildings) {
					if (cb.building.allowsSpyMissions) {
						return true;
					}
				}
			}
			return false;
		}

		public static bool HasEmbassy(Player actor, Player target) {
			return PlayerRelationship.TryGetRelationship(actor, target, out var pr) && pr.hasEmbassy;
		}

		public static bool HasSpy(Player actor, Player target) {
			return PlayerRelationship.TryGetRelationship(actor, target, out var pr) && pr.hasSpy;
		}

		private static bool IsDemocracy(Player player) {
			return player.government?.name == "Democracy";
		}

		private static int DistanceToTarget(Player actor, Player target, City city) {
			City from = Capital(actor);
			City to = city ?? Capital(target);
			if (from == null || to == null) {
				return UnknownDistance;
			}
			return from.location.DistanceTo(to.location);
		}

		// The techs the actor could steal from the target.
		public static List<Tech> StealableTechs(GameData gameData, Player actor, Player target) {
			return gameData.techs.Where(t => target.knownTechs.Contains(t.id) && !actor.knownTechs.Contains(t.id)
				&& t.Prerequisites.All(p => actor.knownTechs.Contains(p.id))).ToList();
		}

		public static int InciteRevoltCost(Player actor, City city) {
			Player target = city.owner;
			City targetCapital = target.cities.Find(c => c.IsCapital());
			int distance = targetCapital == null ? UnknownDistance : targetCapital.location.DistanceTo(city.location);
			long cost = (long)(Math.Max(0, target.gold) + 1000) * Math.Max(1, city.residents.Count) / (distance + 3);
			if (city.isInCivilDisorder) {
				cost /= 2;
			}
			int ours = city.residents.Count(r => r.nationality == actor.civilization);
			if (2 * ours > city.residents.Count) {
				cost /= 2;
			}
			if (IsDemocracy(target)) {
				cost *= 2;
			}
			cost += 20 * city.location.unitsOnTile.Count(u => u.owner == target);
			return (int)Math.Min(cost, int.MaxValue);
		}

		public static int Cost(GameData gameData, Player actor, EspionageMission mission, Player target, City city) {
			int distance = DistanceToTarget(actor, target, city);
			return mission switch {
				EspionageMission.EstablishEmbassy => 30 + 2 * distance,
				EspionageMission.InvestigateCity => 10 + distance,
				EspionageMission.StealTechnology => 50 * (1 + Math.Max(0, target.EraIndex())) + 2 * distance,
				EspionageMission.SabotageProduction => 30 + (city?.shieldsStored ?? 0) + 2 * distance,
				EspionageMission.InciteRevolt => city == null ? 0 : InciteRevoltCost(actor, city),
				EspionageMission.PlantSpy => 100 + 2 * distance,
				EspionageMission.ExposeEnemySpy => 50 + distance,
				_ => 0,
			};
		}

		public static int SuccessPercent(GameData gameData, Player actor, EspionageMission mission, Player target, City city) {
			int chance = mission switch {
				EspionageMission.EstablishEmbassy => 100,
				EspionageMission.InvestigateCity => 100,
				EspionageMission.StealTechnology => 60,
				EspionageMission.SabotageProduction => 50,
				EspionageMission.InciteRevolt => 60,
				EspionageMission.PlantSpy => 70,
				EspionageMission.ExposeEnemySpy => 60,
				_ => 0,
			};
			if (chance >= 100) {
				return 100;
			}
			if (HasSpy(actor, target)) chance += 15;
			if (HasIntelligenceAgency(actor)) chance += 10;
			if (HasIntelligenceAgency(target)) chance -= 10;
			if (IsDemocracy(target)) chance -= 15;
			if (city != null && city.IsCapital()) chance -= 10;
			if (city != null && city.constructed_buildings.Any(cb => cb.building.reducesCorruption && !cb.building.isCenterOfEmpire)) chance -= 10;
			chance -= Math.Min(10, DistanceToTarget(actor, target, city) / 5);
			return Math.Clamp(chance, 5, 95);
		}

		// Why the mission can't be performed, or null if it can.
		// How many cities a player has: a LAN guest isn't sent those it
		// hasn't seen, so the host counts them.
		private static int CityCount(Player player) => player.hostFacts?.cities ?? player.cities.Count;

		public static string Unavailable(GameData gameData, Player actor, EspionageMission mission, Player target, City city) {
			// A LAN client's message can carry any number as the mission.
			if (!Enum.IsDefined(mission)) {
				return "There is no such mission.";
			}
			if (actor == null || target == null || actor == target || target.isBarbarians || target.defeated) {
				return "There is no one to send a mission to.";
			}
			if (!PlayerRelationship.TryGetRelationship(actor, target, out PlayerRelationship pr)) {
				return $"We have not met the {target.civilization.noun}.";
			}
			if (TargetsCity(mission)) {
				if (city == null || city.owner != target || !gameData.cities.Contains(city)) {
					return "That city doesn't belong to them.";
				}
				if (!actor.tileKnowledge.isTileKnown(city.location)) {
					return "We don't know where that city is.";
				}
			}
			if (actor.cities.Count == 0) {
				return "We have no cities to send a mission from.";
			}

			switch (mission) {
				case EspionageMission.EstablishEmbassy:
					if (pr.hasEmbassy) return "We already have an embassy with them.";
					if (pr.AtWar()) return "They won't receive an ambassador while we are at war.";
					if (CityCount(target) == 0) return "They have no capital to send an ambassador to.";
					Tech embassyTech = gameData.techs.Find(t => t.Name == EmbassyTechName);
					if (embassyTech != null && !actor.knownTechs.Contains(embassyTech.id)) {
						return $"We need {EmbassyTechName} to establish embassies.";
					}
					break;
				case EspionageMission.PlantSpy:
				case EspionageMission.ExposeEnemySpy:
					if (!pr.hasEmbassy) return "We need an embassy with them first.";
					if (!HasIntelligenceAgency(actor)) return "We need an Intelligence Agency for espionage.";
					if (mission == EspionageMission.PlantSpy && pr.hasSpy) return "We already have a spy in their capital.";
					if (mission == EspionageMission.ExposeEnemySpy && !pr.hasSpy) return "We need a spy in their capital first.";
					if (CityCount(target) == 0) return "They have no capital.";
					break;
				default:
					if (!pr.hasEmbassy) return "We need an embassy with them first.";
					if (mission == EspionageMission.InciteRevolt && city.IsCapital()) return "Their capital can't be incited to revolt.";
					if (mission == EspionageMission.StealTechnology && StealableTechs(gameData, actor, target).Count == 0) {
						return "They know nothing we could steal.";
					}
					break;
			}

			int cost = Cost(gameData, actor, mission, target, city);
			if (actor.gold < cost) {
				return $"We can't afford the {cost} gold it would cost.";
			}
			return null;
		}

		public static MissionOption GetOption(GameData gameData, Player actor, EspionageMission mission, Player target, City city) {
			string reason = Unavailable(gameData, actor, mission, target, city);
			bool known = target != null && actor != null && target != actor
				&& PlayerRelationship.TryGetRelationship(actor, target, out _);
			return new MissionOption {
				mission = mission,
				available = reason == null,
				reason = reason,
				cost = known ? Cost(gameData, actor, mission, target, city) : 0,
				successPercent = known ? SuccessPercent(gameData, actor, mission, target, city) : 0,
			};
		}

		// The missions that can be listed for a target: the civ missions, and
		// the city missions too when a city is given.
		public static List<MissionOption> GetOptions(GameData gameData, Player actor, Player target, City city) {
			List<MissionOption> result = new();
			foreach (EspionageMission mission in Enum.GetValues<EspionageMission>()) {
				if (TargetsCity(mission) && city == null) {
					continue;
				}
				result.Add(GetOption(gameData, actor, mission, target, TargetsCity(mission) ? city : null));
			}
			return result;
		}

		// Performs the mission if possible, with a roll of 0-99 to beat the
		// success chance (random if not given).
		public static MissionResult Perform(GameData gameData, Player actor, EspionageMission mission, Player target, City city, int? roll = null) {
			if (!TargetsCity(mission)) {
				city = null;
			}
			MissionResult result = new() { mission = mission };
			string reason = Unavailable(gameData, actor, mission, target, city);
			if (reason != null) {
				result.message = reason;
				return result;
			}

			int cost = Cost(gameData, actor, mission, target, city);
			int chance = SuccessPercent(gameData, actor, mission, target, city);
			actor.gold -= cost;
			result.performed = true;
			result.goldSpent = cost;
			result.success = (roll ?? GameData.rng.Next(100)) < chance;

			PlayerRelationship ours = actor.playerRelationships[target.id];
			PlayerRelationship theirs = target.playerRelationships[actor.id];

			if (!result.success) {
				theirs.espionageIncidents++;
				if (IsEspionageMission(mission) && mission != EspionageMission.PlantSpy) {
					ours.hasSpy = false;
				}
				result.message = $"Our {Describe(mission)} mission against the {target.civilization.noun} failed, and our agents were caught!";
				NotifyTarget(target, $"We caught {actor.civilization.adjective ?? actor.civilization.noun} agents attempting to {Describe(mission).ToLowerInvariant()}"
					+ (city != null ? $" in {city.name}" : "") + "!");
				log.Information("{Actor}'s {Mission} against {Target} failed", actor, mission, target);
				return result;
			}

			switch (mission) {
				case EspionageMission.EstablishEmbassy: {
						// The embassy reports on the capital as it is now, and
						// shows us the land around it.
						City capital = Capital(target);
						ours.hasEmbassy = true;
						ours.embassyReport = Investigate(capital, gameData.turn);
						RevealCityRadius(actor, capital);
						result.report = ours.embassyReport;
						result.city = capital;
						result.message = $"We have established an embassy with the {target.civilization.noun} in {capital.name}.";
						break;
					}
				case EspionageMission.InvestigateCity:
					result.report = Investigate(city, gameData.turn);
					result.message = DescribeReport(result.report);
					break;
				case EspionageMission.StealTechnology: {
						List<Tech> stealable = StealableTechs(gameData, actor, target);
						Tech tech = stealable[GameData.rng.Next(stealable.Count)];
						actor.AcquireTech(gameData, tech);
						result.stolenTech = tech;
						result.message = $"Our agents in {city.name} have stolen the secrets of {tech.Name}!";
						break;
					}
				case EspionageMission.SabotageProduction:
					city.SetStoredShields(0);
					result.message = $"Our agents have sabotaged production in {city.name}.";
					NotifyTarget(target, $"Saboteurs have destroyed the production in {city.name}!");
					break;
				case EspionageMission.InciteRevolt:
					result.message = $"The people of {city.name} have revolted and joined us!";
					NotifyTarget(target, $"{city.name} has been incited to revolt by the {actor.civilization.noun}!");
					// The city, and the target's units in it, join the actor.
					CityInteractions.TransferCity(city, actor);
					break;
				case EspionageMission.PlantSpy:
					ours.hasSpy = true;
					result.message = $"We have planted a spy in the {target.civilization.noun} capital.";
					break;
				case EspionageMission.ExposeEnemySpy: {
						Player exposed = gameData.players.FirstOrDefault(p => p != actor && p != target
						&& PlayerRelationship.TryGetRelationship(p, target, out var spyRelationship) && spyRelationship.hasSpy);
						if (exposed == null) {
							result.message = $"Our spy found no foreign agents in the {target.civilization.noun} capital.";
						} else {
							exposed.playerRelationships[target.id].hasSpy = false;
							target.playerRelationships[exposed.id].espionageIncidents++;
							result.exposedSpyOwner = exposed;
							result.message = $"We have exposed a {exposed.civilization.noun} spy in the {target.civilization.noun} capital!";
							NotifyTarget(target, $"A {exposed.civilization.noun} spy has been exposed in our capital!");
							NotifyTarget(exposed, $"Our spy in the {target.civilization.noun} capital has been exposed!");
						}
						break;
					}
			}
			log.Information("{Actor}'s {Mission} against {Target} succeeded: {Message}", actor, mission, target, result.message);
			return result;
		}

		private static void NotifyTarget(Player target, string message) {
			if (target.isHuman) {
				new MsgShowMilitaryAdvisorPopup(target, message, happy: false).send();
			}
		}

		// Shows the player the tiles in the city's radius (its big fat
		// cross), remembering them as they are now.
		public static void RevealCityRadius(Player player, City city) {
			foreach (Tile t in CityRadius(city)) {
				player.tileKnowledge.AddTileToKnown(t);
				player.tileKnowledge.RememberImprovements(t);
			}
			player.tileKnowledge.RecomputeActiveTiles();
		}

		// The tiles in the city's radius, on the map.
		public static List<Tile> CityRadius(City city) {
			return city.location.GetTilesWithinRankDistance(city.owner.rules.MaxRankOfWorkableTiles)
				.Where(t => t != Tile.NONE).ToList();
		}

		public static CityReport Investigate(City city, int turn) {
			return new CityReport {
				turn = turn,
				cityName = city.name,
				size = city.residents.Count,
				producing = city.itemBeingProduced?.name,
				shieldsStored = city.shieldsStored,
				buildings = city.constructed_buildings.Select(cb => cb.building.name).ToList(),
				units = city.location.unitsOnTile.Where(u => u.owner == city.owner).Select(u => u.unitType.name).ToList(),
				ownerGold = city.owner.gold,
			};
		}

		public static string DescribeReport(CityReport report) {
			string buildings = report.buildings.Count == 0 ? "none" : string.Join(", ", report.buildings);
			string units = report.units.Count == 0 ? "none" : string.Join(", ", report.units.GroupBy(u => u).Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}" : g.Key));
			return $"{report.cityName} (size {report.size}) is building {report.producing ?? "nothing"} ({report.shieldsStored} shields).\n"
				+ $"Buildings: {buildings}\nDefenders: {units}\nTheir treasury: {report.ownerGold} gold";
		}
	}
}
