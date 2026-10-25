using System.Linq;
using C7Engine;
using C7GameData.Save;
using Serilog;

namespace C7GameData;

// Civ3 Conquests mobilization: a civ that knows a tech with the BIQ's
// "Enables Mobilization" flag (Nationalism in conquests.biq) may put its
// economy on a war footing, "War-Time" rather than "Normalcy" (labels.txt;
// script.txt #CHANGEMOBILIZATION), from the Domestic Advisor: "Mobilize your
// economy using the "Mobilization" button on the Domestic Advisor" (the
// Conquests Civilopedia, GCON_Mobilization). Owner's decision, 2026-10-21:
// add the mode, the player's toggle and the AI's use of it.
//
// The rules, from the Conquests Civilopedia (GCON_Mobilization):
//   "During mobilization a city building a military unit produces one extra
//   shield in every square that is already producing at least one"; a
//   mobilized civ "May only build military units and military city
//   improvements", "May not build peacetime improvements" and "May not
//   return to Normalcy until a Peace Treaty is signed". "During Mobilization
//   workers, settlers and explorers can be built, but will not receive the
//   production bonus."
// "Fundamentals of Mobilization Revealed"
// (https://civfanatics.com/civ3/strategy/game-mechanics/fundamentals-of-mobilization-revealed/,
// compendium 02, section 3, and sources.md): "You are free to mobilize your
// economy at any time after you discover Nationalism -- you need not be at
// war", and "only a peace treaty or the elimination of your opponent will
// allow you to return your economy to a peace-time role". Every unit stays
// buildable; the improvements and wonders it lists as allowed (barracks,
// walls, the SAM battery, coastal fortress, harbor, airport, the Great Wall,
// Sun Tzu's, the Pentagon and the rest) are the ones with the BIQ's
// "Military" characteristic, which the engine reads as the Militaristic
// trait (QueryCiv3 Bldg.Militaristic); spaceship parts are allowed too.
// Something already under way may be finished.
// A unit gets the shield bonus only if its attack is above zero: "A unit
// being built receives the mobilization bonus if its attack strength is
// greater than zero" (Flintlock, reading the code,
// https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16215014).
// The bonus is the golden age's, and the two don't add up: DaveMcW lists
// one "+1 if in golden age/mobilization"
// (https://codehappy.net/apolyton/threads/78438-1.htm; compendium 02).
// Culture is halved: "During wartime footing, cultural improvements produce
// half the number normally produced per turn" (the manual; compendium 03).
//
// Assumptions: culture is halved on the city's total, rounding down;
// Wealth, being no military improvement, can't be started; and peace with
// any one enemy, or any enemy's destruction, lets the civ demobilize.
public static class Mobilization {
	private static readonly ILogger log = Log.ForContext(typeof(Mobilization));

	// Whether the player may mobilize now: it knows the tech and isn't
	// mobilized already.
	public static bool CanMobilize(Player player) {
		return player != null && !player.IsIndependent && !player.mobilized
			&& player.HasTechEnabling(SaveTech.Flag.EnablesMobilization);
	}

	// Whether the player may return to Normalcy: once a peace treaty has
	// been signed, or an enemy destroyed, since it mobilized.
	public static bool CanDemobilize(Player player) {
		return player != null && player.mobilized && player.mayEndMobilization;
	}

	// Why the player can't change its mobilization now, for the UI, or null.
	public static string WhyCannotToggle(Player player) {
		if (player.mobilized) {
			return CanDemobilize(player) ? null : "We may not return to Normalcy until a Peace Treaty is signed.";
		}
		return CanMobilize(player) ? null : "We must learn Nationalism before we can mobilize.";
	}

	public static bool Mobilize(Player player) {
		if (!CanMobilize(player)) {
			return false;
		}
		player.mobilized = true;
		player.mayEndMobilization = false;
		log.Information("{Player} mobilizes for war", player);
		return true;
	}

	public static bool Demobilize(Player player) {
		if (!CanDemobilize(player)) {
			return false;
		}
		player.mobilized = false;
		player.mayEndMobilization = false;
		log.Information("{Player} returns to Normalcy", player);
		return true;
	}

	// A peace treaty, or the destruction of an enemy, frees a mobilized
	// civ to return to Normalcy.
	public static void OnWarEnded(Player player) {
		if (player != null && player.mobilized) {
			player.mayEndMobilization = true;
		}
	}

	// Whether a mobilized civ may start building the item: any unit, and
	// military improvements, military wonders and spaceship parts.
	public static bool AllowsStarting(Player player, IProducible item) {
		if (player == null || !player.mobilized) {
			return true;
		}
		return item switch {
			UnitPrototype => true,
			Building b => b.traits.Contains(Civilization.Trait.Militaristic) || b.IsSpaceshipPart,
			_ => false,
		};
	}

	// Whether the city gets the mobilization shield bonus on its tiles: it
	// belongs to a mobilized civ and builds a unit with attack above zero.
	public static bool CityGetsShieldBonus(City city) {
		return city?.owner != null && city.owner.mobilized
			&& city.itemBeingProduced is UnitPrototype unit && unit.attack > 0;
	}

	// The city's culture this turn, halved while its owner is mobilized.
	public static int AdjustCulture(Player owner, int culture) {
		return owner != null && owner.mobilized ? culture / 2 : culture;
	}

	// HEURISTIC: the AI mobilizes when it is at war with a civ it shares a
	// landmass with whose military is at least this share of its own, and
	// returns to Normalcy, once it may, when it is at war with no civ.
	private const double RivalStrengthToMobilize = 0.75;

	// The AI's use of mobilization (owner's decision, 2026-10-21: mobilize
	// during a major war). The stock AI's own rule isn't known; Predator145
	// reports it builds military-flagged buildings only when mobilized
	// (CFC 666881, pages 65-66; reading-log-05), so it does mobilize. AI
	// trials of 350 turns (doc/ai-review-2026-10.md, 21 October) found it
	// within the noise.
	public static void PlayAi(Player player, GameData gameData) {
		if (player.isHuman || player.IsIndependent || player.cities.Count == 0) {
			return;
		}
		if (player.mobilized) {
			if (CanDemobilize(player) && !PlayerRelationship.IsInAnyWar(player, gameData.players)) {
				Demobilize(player);
			}
			return;
		}
		if (CanMobilize(player) && InMajorWar(player, gameData)) {
			Mobilize(player);
		}
	}

	internal static bool InMajorWar(Player player, GameData gameData) {
		int ours = MilitaryStrength(player);
		foreach (Player other in gameData.players) {
			if (other == player || other.IsIndependent || other.defeated || !PlayerRelationship.AtWar(player, other)) {
				continue;
			}
			bool neighbour = other.cities.Any(c => player.cities.Any(mine => mine.location.continent == c.location.continent));
			if (neighbour && MilitaryStrength(other) >= RivalStrengthToMobilize * ours) {
				return true;
			}
		}
		return false;
	}

	private static int MilitaryStrength(Player player) {
		int result = 0;
		foreach (MapUnit u in player.units) {
			result += System.Math.Max(u.unitType.attack, u.unitType.defense);
		}
		return result;
	}
}
