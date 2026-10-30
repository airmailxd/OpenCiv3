using System;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// On trial (switch "archer-rush", owner's idea 4, 2026-10-30): a weak
	// AI can be overrun early by a stack of cheap attackers. In the first
	// turns of the game, if a rival city we know of is on our landmass
	// within a few tiles of one of ours, and our best attack beats their
	// best defence, the civ gathers a stack of attackers (the best attack
	// per shield, so archers early) and one defender, and marches on the
	// nearest city of that rival, its capital first. Small cities are
	// taken or razed: the point is to remove a neighbour and take its land.
	// The march is an ordinary WarCampaign (a rush, like EarlyRush's); it
	// works without the early-rush switch.
	//
	// Civ3 on small cities (compendium 02, section 11): "Capturing a
	// population 1 city that has no culture will automatically raze it"
	// (1.16f readme); any other city is kept, and a captor may raze a city
	// taken by force (manual p.73). What a size 2 city loses on capture is
	// UNVERIFIED here beyond what CityInteractions.CaptureCity does.
	public static class ArcherRush {
		public const string Switch = "archer-rush";

		// HEURISTIC: the turns from the start of the game in which a civ
		// rushes.
		public const int WindowTurns = 100;
		// HEURISTIC: how far (tiles, from one of our cities on the same
		// landmass) a rival's city may be for us to rush it.
		public const int Radius = 10;
		// HEURISTIC: the attack of the attacker the civ researches for
		// when it has none (archers 3 in the default rules).
		public const int WantedAttack = 3;
		// HEURISTIC: the attackers a rush gathers.
		public const int Attackers = 6;
		// HEURISTIC: the defenders that march with the stack.
		public const int Escorts = 1;
		// HEURISTIC: the healthy attackers left after a win with which the
		// stack goes on to the rival's next city rather than wait out the
		// cooldown.
		public const int AttackersToContinue = 4;
		// HEURISTIC: the turns the stack waits at the gathering city for
		// its defender before going without.
		public const int EscortWaitTurns = 8;
		// HEURISTIC: what the tech of the attacker we lack adds to its
		// value in research; the same size as the other switched techs.
		public const double TechBonus = DevelopmentAI.SwitchedTechBonus;

		public static bool On(Player player) {
			return AiExperiments.Has(player, Switch);
		}

		private static bool InWindow(GameData gameData) {
			return gameData.turn <= WindowTurns;
		}

		// The distance from the city to the nearest of the player's cities
		// on its landmass, or int.MaxValue if there is none.
		public static int DistanceTo(Player player, City city) {
			return player.cities.Where(c => c.location.continent == city.location.continent)
				.Select(c => c.location.DistanceTo(city.location)).DefaultIfEmpty(int.MaxValue).Min();
		}

		// Whether the player knows a city of theirs on its landmass within
		// the radius of one of its own.
		public static bool HasCityNear(Player player, Player them) {
			return them.cities.Any(c => AIFogOfWar.KnowsTile(player, c.location) && DistanceTo(player, c) <= Radius);
		}

		private static bool Rival(Player player, Player them) {
			return them != player && !them.isBarbarians && !them.IsIndependent && !them.defeated && them.cities.Count > 0;
		}

		// Whether the civ should research for an attacker: the switch is on,
		// it is early, a rival's city is near and it has no attacker of the
		// wanted strength.
		public static bool NeedsAttacker(GameData gameData, Player player) {
			return On(player) && InWindow(gameData) && player.cities.Count > 0
				&& gameData.players.Any(p => Rival(player, p) && HasCityNear(player, p))
				&& EarlyRush.BestAttack(player) < WantedAttack;
		}

		// Whether our best attack is above their best defence (no 1.5 times
		// for a fortified defender: archers 3 against spearmen 2 will do).
		public static bool Beats(Player us, Player them) {
			return EarlyRush.BestAttack(us) > EarlyRush.BestDefense(them);
		}

		// Whether the civ should plan a rush on them.
		public static bool Wants(GameData gameData, Player player, Player them) {
			return On(player) && InWindow(gameData) && player.cities.Count > 0 && Rival(player, them)
				&& HasCityNear(player, them) && Beats(player, them);
		}

		// Whether the unit's type is the best attack per shield among the
		// land attackers the civ can build.
		public static float AttackPerShield(UnitPrototype unit) {
			return unit.shieldCost > 0 ? (float)unit.attack / unit.shieldCost : 0;
		}

		// The best attack per shield among the land attackers the civ can
		// build, or 1 if it can build none (a divisor).
		public static float BestAttackPerShield(Player player) {
			return Math.Max(float.Epsilon, EarlyRush.LandUnitsBuildable(player).Select(AttackPerShield).DefaultIfEmpty(0).Max());
		}
	}
}
