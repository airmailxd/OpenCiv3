using System.Collections.Generic;
using C7GameData;
using C7GameData.Save;

namespace C7Engine.Network;

// How a game hosted on the network is set up, as the Multiplayer Setup
// screen lists it, for the lobby to show everyone: the settings down its
// left side and the rules in its Game Rules box (see LobbyDetails).
public static class LobbySummary {
	// A new game, from what the host chose on the Multiplayer Setup screen.
	// rules is the save the game is made in, whose rules stand for any the
	// setup leaves to them.
	public static (List<LobbySetting> settings, List<LobbyRule> rules) Of(GameSetup setup, SaveGame rules) {
		Rules defaults = rules?.Rules;
		WorldCharacteristics world = setup.worldCharacteristics;
		List<LobbySetting> settings = [
			new("Scenario:", LanGameList.GameTypeName(setup.scenario)),
			new("Game Mode:", GameModeName(setup.victoryConditions)),
		];
		if (world != null) {
			settings.AddRange([
				new("World Size:", world.worldSize?.name ?? "Random"),
				new("Barbarian Activity:", world.barbarianActivity.ToString()),
				new("Land Mass:", world.landform.ToString()),
				new("Water Coverage:", WaterText(world.oceanCoverage)),
				new("Climate:", world.climate.ToString()),
				new("Temperature:", world.temperature.ToString()),
				new("Age:", AgeText(world.age)),
			]);
		}
		if (setup.difficulty?.Name is string difficulty) {
			settings.Add(new("Difficulty:", difficulty));
		}
		if (setup.eternalWarCitiesPerCiv is int cities) {
			settings.Add(new("Cities per Civ:", cities.ToString()));
		}
		return (settings, Rules(setup.victoryConditions,
			setup.acceleratedProduction ?? defaults?.AcceleratedProduction ?? false,
			setup.allowCultureFlips ?? defaults?.AllowCultureFlips ?? true,
			setup.scientificLeaders ?? defaults?.ScientificLeaders ?? true,
			setup.coreCitiesFreeOfCorruption,
			setup.showScoreboard));
	}

	// A saved game, from what it holds.
	public static (List<LobbySetting> settings, List<LobbyRule> rules) Of(SaveGame save) {
		List<LobbySetting> settings = [];
		if (LanGameList.GameTypeOf(save) is string type) {
			settings.Add(new("Scenario:", type));
		}
		if (save.VictoryConditions != null) {
			settings.Add(new("Game Mode:", GameModeName(save.VictoryConditions)));
		}
		if (save.Map != null) {
			settings.Add(new("Map:", $"{save.Map.tilesWide}x{save.Map.tilesTall}"));
		}
		if (save.GameDifficulty?.Name is string difficulty) {
			settings.Add(new("Difficulty:", difficulty));
		}
		Rules rules = save.Rules ?? new Rules();
		return (settings, save.VictoryConditions == null ? [] : Rules(save.VictoryConditions, rules.AcceleratedProduction,
			rules.AllowCultureFlips, rules.ScientificLeaders, rules.CoreCitiesFreeOfCorruption, rules.ShowScoreboard));
	}

	// The rules, in the Multiplayer Setup screen's order and words.
	private static List<LobbyRule> Rules(VictoryConditions vc, bool acceleratedProduction, bool cultureFlips, bool scientificLeaders,
		bool noCorruptionNearCapital, bool showScoreboard) {
		vc ??= new VictoryConditions();
		return [
			new("Allow Domination Victory", vc.AllowDominationVictory),
			new("Accelerated Production", acceleratedProduction),
			new("Allow Space Race Victory", vc.AllowSpaceRaceVictory),
			new("Elimination", vc.CityElimination),
			new("Allow Diplomatic Victory", vc.AllowDiplomaticVictory),
			new("Regicide", vc.Regicide),
			new("Allow Conquest Victory", vc.AllowConquestVictory),
			new("Mass Regicide", vc.MassRegicide),
			new("Allow Cultural Victory", vc.AllowCulturalVictory),
			new("Victory Point Scoring", vc.VictoryLocations),
			new("Allow Wonder Victory", vc.AllowWonderVictory),
			new("Turn Limit", vc.UseTurnLimit, "The game ends at the turn limit, scoring who leads."),
			new("Culture Flipping", cultureFlips),
			new("Scientific Leaders", scientificLeaders),
			new("No Corruption Near Capital", noCorruptionNearCapital),
			new("Show Scoreboard", showScoreboard),
		];
	}

	// The Multiplayer Setup screen's name for the game mode the special
	// rules make.
	public static string GameModeName(VictoryConditions vc) {
		if (vc == null) {
			return "Standard";
		}
		return MultiplayerSlots.GameModeOf(vc) switch {
			MultiplayerGameMode.Elimination => "Elimination",
			MultiplayerGameMode.Regicide => "Regicide",
			MultiplayerGameMode.MassRegicide => "Mass Regicide",
			MultiplayerGameMode.VictoryPointScoring => "Victory Point Scoring",
			MultiplayerGameMode.Custom => "Custom",
			_ => "Standard",
		};
	}

	private static string WaterText(WorldCharacteristics.OceanCoverage coverage) => coverage switch {
		WorldCharacteristics.OceanCoverage.Percent_60 => "60% Water",
		WorldCharacteristics.OceanCoverage.Percent_70 => "70% Water",
		WorldCharacteristics.OceanCoverage.Percent_80 => "80% Water",
		_ => coverage.ToString(),
	};

	private static string AgeText(WorldCharacteristics.Age age) => age switch {
		WorldCharacteristics.Age.Billion_3 => "3 Billion",
		WorldCharacteristics.Age.Billion_4 => "4 Billion",
		WorldCharacteristics.Age.Billion_5 => "5 Billion",
		_ => age.ToString(),
	};
}
