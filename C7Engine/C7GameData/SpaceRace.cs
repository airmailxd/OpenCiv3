using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine {
	// The Civ3 space race: the Apollo Program, spaceship parts and the ship.
	//
	// Spaceship parts are buildings with a part index (Building.spaceshipPart).
	// They don't stay in the city that builds them: each completed part is
	// added to its owner's ship (Player.spaceshipParts). The ship needs
	// Rules.SpaceshipPartsNeeded of each part, one of each of ten parts in
	// Conquests.
	//
	// Assumptions about Civ3 (Conquests) behaviour:
	//  - Completing the Apollo Program (a building with buildSpaceshipParts)
	//    reveals the whole map to every civ, and from then on every civ with
	//    the techs can build spaceship parts, not only the one that built it.
	//    The completion is recorded in GameData.GreatWondersBuilt (as the
	//    Manhattan Project is) so that losing the Apollo city doesn't undo it.
	//  - Parts can be built in any city; the BIQ gives them no required
	//    building (such as a factory).
	//  - The ship launches as soon as its last part is built and there is no
	//    travel time: the space race is won at the end of that turn.
	//  - Losing the capital (to capture or destruction) destroys every part
	//    of the ship built so far.
	//  - With the space race victory disabled, parts can't be built.
	public static class SpaceRace {
		private static Serilog.ILogger log = Serilog.Log.ForContext(typeof(SpaceRace));

		// Whether any civ has completed the Apollo Program (or another
		// building allowing spaceship parts).
		public static bool ApolloProgramBuilt(GameData gameData) {
			if (gameData == null) {
				return false;
			}
			foreach (Building b in gameData.Buildings) {
				if (b.buildSpaceshipParts && gameData.GreatWondersBuilt.Contains(b.name)) {
					return true;
				}
			}
			// Games imported from Civ3 only record the building in its city.
			foreach (Player p in gameData.players) {
				foreach (City c in p.cities) {
					foreach (CityBuilding cb in c.constructed_buildings) {
						if (cb.building.buildSpaceshipParts) {
							return true;
						}
					}
				}
			}
			return false;
		}

		public static bool SpaceRaceAllowed(GameData gameData) {
			return gameData?.victoryConditions == null || gameData.victoryConditions.AllowSpaceRaceVictory;
		}

		public static int PartsBuilt(Player player, int partIndex) {
			if (partIndex < 0 || partIndex >= player.spaceshipParts.Count) {
				return 0;
			}
			return player.spaceshipParts[partIndex];
		}

		public static int PartsNeeded(GameData gameData, int partIndex) {
			return gameData.rules?.SpaceshipPartsNeeded(partIndex) ?? (partIndex >= 0 ? 1 : 0);
		}

		// Whether the city may start building the part: the Apollo Program
		// exists, and the ship still needs more of the part than the owner has
		// built or is building in its other cities. The tech, resource and
		// other requirements are checked by Building.CanProduce.
		public static bool CanBuildPart(GameData gameData, City city, Building part) {
			if (!part.IsSpaceshipPart || gameData == null || !SpaceRaceAllowed(gameData)) {
				return false;
			}
			Player owner = city.owner;
			int remaining = PartsNeeded(gameData, part.spaceshipPart) - PartsBuilt(owner, part.spaceshipPart);
			if (remaining <= 0) {
				return false;
			}
			foreach (City c in owner.cities) {
				if (c != city && c.itemBeingProduced is Building b && b.IsSpaceshipPart && b.spaceshipPart == part.spaceshipPart) {
					if (--remaining <= 0) {
						return false;
					}
				}
			}
			return ApolloProgramBuilt(gameData);
		}

		// One building per part index (the first, should several share one).
		public static List<Building> PartTypes(GameData gameData) {
			List<Building> result = new();
			HashSet<int> seen = new();
			foreach (Building b in gameData.Buildings) {
				if (b.IsSpaceshipPart && seen.Add(b.spaceshipPart)) {
					result.Add(b);
				}
			}
			result.Sort((a, b) => a.spaceshipPart.CompareTo(b.spaceshipPart));
			return result;
		}

		// How many parts the player has built and how many the ship needs in
		// all.
		public static (int built, int needed) Progress(GameData gameData, Player player) {
			int built = 0, needed = 0;
			foreach (Building part in PartTypes(gameData)) {
				int n = PartsNeeded(gameData, part.spaceshipPart);
				needed += n;
				built += System.Math.Min(n, PartsBuilt(player, part.spaceshipPart));
			}
			return (built, needed);
		}

		// Whether the player has built every part of the ship.
		public static bool IsSpaceshipComplete(GameData gameData, Player player) {
			(int built, int needed) = Progress(gameData, player);
			return needed > 0 && built >= needed;
		}

		// Adds a part built in the city to its owner's ship.
		public static void OnPartCompleted(GameData gameData, City city, Building part) {
			Player owner = city.owner;
			while (owner.spaceshipParts.Count <= part.spaceshipPart) {
				owner.spaceshipParts.Add(0);
			}
			++owner.spaceshipParts[part.spaceshipPart];
			(int built, int needed) = Progress(gameData, owner);
			log.Information("{Player} built {Part} in {City} ({Built}/{Needed} spaceship parts)", owner, part, city, built, needed);

			foreach (Player p in gameData.players) {
				if (!p.isHuman || p.defeated) {
					continue;
				}
				if (p == owner) {
					string message = built >= needed
						? $"Our spaceship is complete! It launches for Alpha Centauri."
						: $"{city.name} has completed the {part.name}. Our spaceship has {built} of its {needed} parts.";
					new MsgShowScienceAdvisorPopup(p, message, MsgShowScienceAdvisorPopup.Mood.Happy).send();
				} else if (p.playerRelationships.ContainsKey(owner.id) || built >= needed) {
					new MsgShowScienceAdvisorPopup(p, $"The {owner.civilization.noun} have added {part.name} to their ship.",
						MsgShowScienceAdvisorPopup.Mood.Surprised).send();
				}
			}
		}

		// Called when a city completes the Apollo Program.
		public static void OnApolloCompleted(GameData gameData, City city, Building apollo) {
			gameData.GreatWondersBuilt.Add(apollo.name);
			RevealMapToAll(gameData);
			log.Information("{Player} completed {Building} in {City}; the map is revealed to all", city.owner, apollo, city);

			foreach (Player p in gameData.players) {
				if (!p.isHuman || p.defeated) {
					continue;
				}
				string who = p == city.owner ? "We have" : $"The {city.owner.civilization.noun} have";
				new MsgShowScienceAdvisorPopup(p,
					$"{who} completed the {apollo.name}! The whole world is revealed, and every civilization may now build spaceship parts.",
					p == city.owner ? MsgShowScienceAdvisorPopup.Mood.Happy : MsgShowScienceAdvisorPopup.Mood.Surprised).send();
			}
		}

		public static void RevealMapToAll(GameData gameData) {
			foreach (Player p in gameData.players) {
				if (p.isBarbarians) {
					continue;
				}
				foreach (Tile t in gameData.map.tiles) {
					p.tileKnowledge.AddTileToKnown(t);
				}
				p.tileKnowledge.RecomputeActiveTiles();
			}
		}

		// Destroys the player's spaceship, as happens when they lose their
		// capital. The destroyer may be null (e.g. the capital was razed with
		// no captor known).
		public static void DestroySpaceship(Player owner, Player destroyer) {
			if (!owner.spaceshipParts.Any(n => n > 0)) {
				return;
			}
			owner.spaceshipParts.Clear();
			log.Information("{Player}'s spaceship was destroyed by {Destroyer}", owner, destroyer);

			if (owner.isHuman) {
				string message = destroyer != null
					? $"The {destroyer.civilization.noun} have destroyed our spaceship!"
					: "With the loss of our capital, our spaceship has been destroyed!";
				new MsgShowScienceAdvisorPopup(owner, message, MsgShowScienceAdvisorPopup.Mood.Angry).send();
			}
			if (destroyer != null && destroyer.isHuman) {
				new MsgShowScienceAdvisorPopup(destroyer, $"We have destroyed the {owner.civilization.adjective} spaceship!",
					MsgShowScienceAdvisorPopup.Mood.Happy).send();
			}
		}
	}
}
