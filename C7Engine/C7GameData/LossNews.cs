using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine {
	// The news a player gets of losing a city or units to another civ, in
	// Civ3's words (Conquests/Text/script.txt). The city entries carry
	// "#map_center 1" and "#advisor Military Angry": the map is centred on the
	// city as the military advisor tells of it, which the UI does for news
	// with a location (MsgShowMilitaryAdvisorPopup.location). The script's
	// "^" starts a new line and its [ ] highlights are dropped, as
	// ScriptText.Format does.
	public static class LossNews {
		// THEY_CAPTURE_CITY_GOLD.
		public static string CityCaptured(string rulerTitle, string city, string captorNoun, int gold) =>
			$"Terrible news, {rulerTitle}!\n{city} was captured by the evil {captorNoun}!\nThey have stolen {gold} gold from its vaults.";

		// CITY_LOST_WITH_WONDER.
		public static string CityCapturedWithWonder(string rulerTitle, string city, string captorNoun, int gold, string wonder) =>
			$"Terrible news, {rulerTitle}!\nThe evil {captorNoun} have captured {city} -- along with {wonder}!\nThey have also stolen {gold} gold from its vaults.\nWe must make them pay!";

		// THEY_RAZE_CITY_GOLD (THEY_DESTROY_CITY_GOLD, for a city destroyed
		// as it is taken, reads the same).
		public static string CityRazed(string rulerTitle, string city, string razerNoun, int gold) =>
			$"Terrible news, {rulerTitle}!\nThe evil {razerNoun} have stolen {gold} gold from {city} and burned it to the ground!\nThey should pay dearly for this atrocity!";

		// CITY_RAZED_WITH_WONDER.
		public static string CityRazedWithWonder(string rulerTitle, string city, string razerNoun, int gold, string wonder) =>
			$"Terrible news, {rulerTitle}!\n{city} was razed by the evil {razerNoun}!\nThey have stolen {gold} gold from its vaults and destroyed {wonder}!\nThey are beasts, and must be dealt with harshly.";

		// UNITCAPTURED, or UNITSCAPTURED for more than one.
		public static string UnitsCaptured(IReadOnlyList<string> unitNames) =>
			unitNames.Count == 1 ? $"Our {unitNames[0]} was captured!" : "Our units were captured!";

		// The great wonder in the city that the news names, if any: the first
		// one built there. UNVERIFIED which one Civ3 names when there are
		// several; its entries have room for one.
		public static string GreatWonderIn(City city) =>
			city.constructed_buildings.Select(cb => cb.building).FirstOrDefault(b => b.IsGreatWonder())?.name;

		// How the news addresses the player (Civ3's $RULER_TITLE0): one of
		// their government's ruler titles, picked at random as Civ3 does
		// (see NewEra.RulerTitle). Not from the game's random numbers, so
		// telling the news doesn't change the game.
		public static string RulerTitle(Player player) => NewEra.RulerTitle(player, Random.Shared);

		// Tells the player they've lost the city: captured, or razed (or
		// destroyed as it was taken), with the gold taken and its great
		// wonder, if any. The news event is the one the others told of it
		// share (see MessageToUI.newsEvent).
		public static void TellCityLost(Player loser, City city, Player taker, int gold, bool razed, string wonder, NewsEvent newsEvent = null) {
			if (loser == null || !loser.IsToldNews || taker == null) {
				return;
			}
			string title = RulerTitle(loser);
			string noun = taker.civilization?.noun;
			string message = (razed, wonder) switch {
				(false, null) => CityCaptured(title, city.name, noun, gold),
				(false, _) => CityCapturedWithWonder(title, city.name, noun, gold, wonder),
				(true, null) => CityRazed(title, city.name, noun, gold),
				(true, _) => CityRazedWithWonder(title, city.name, noun, gold, wonder),
			};
			new MsgShowMilitaryAdvisorPopup(loser, message, happy: false) {
				location = city.location,
				// A razing comes after the city was taken: the news of the
				// capture, if the player hasn't seen it yet, gives way to it.
				supersedesNewsHere = razed,
				newsEvent = newsEvent,
			}.send();
		}
	}
}
