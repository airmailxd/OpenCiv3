# RegenerateRuleset
Regenerates the bundled ruleset, `C7/Lua/civ3/ruleset.json`, from Civ3's `conquests.biq`: imports the BIQ with
`ImportCiv3.ImportBiq`, replaces the sections of `ruleset.json` that come from the BIQ with the imported ones, and those
C7 sets itself with its own defaults. The file keeps its order of sections, its formatting and its line endings, so the diff shows only real changes. With
an unchanged importer and BIQ the output is identical to the file.

## Build
`dotnet build`

## Usage
`dotnet run [Civ3 install path] [ruleset.json path]`

The Civ3 install path is the folder with `Conquests/conquests.biq` in it, and defaults to the one `Civ3Location` finds.
The ruleset defaults to `C7/Lua/civ3/ruleset.json` in the repository (found by looking up from the working directory or
the program's folder). The BIQ is only read.

## Sections
From the BIQ: `terrainTypes`, `resources`, `unitPrototypes`, `buildings`, `barbarianInfo`, `experienceLevels`,
`defaultExperienceLevel`, `inflows`, `cultureGroups`, `strengthBonuses`, `healRates`, `rules`, `techs`, `citizenTypes`
and `governments`.

Kept as they are in `ruleset.json`, because the import would undo deliberate edits, add only noise, or doesn't make
them:
- `version`: the ruleset's own.
- `terrainImprovements`: the import has the same ones in another order.
- `terraForms`: the railroad has its own Lua AI score (`terraforms.ai_score.railroad`), which the import doesn't set.
- `civilizations`: the import adds the default settler AI adjustments to each.
- `difficulties`: the highest is renamed from Sid to Creator.
- `timeOptions`: the import pads the last era to 50000 turns; the file has 10000.
- `worldSizes`: not imported from the BIQ.

From C7's own defaults: `victoryConditions`, the victory conditions a new game starts with
(`VictoryConditions.NewGameDefaults()`): conquest, domination, space race, diplomatic and cultural victories on, the
turn limit off.

The program stops if `ruleset.json` has a section in none of these lists; add it to one of the lists in `Program.cs`.

## MDJ
`dotnet run -- --mdj [Civ3 install path] [ruleset.json path]` writes MDJ's rules (see `C7Engine/GameSetup.Mdj.cs`) from
`Conquests/Scenarios/MPTournamentMDJ.biq` into `C7/Lua/mdj/ruleset.json`, so they need no Civ3 install. It has the
sections of `C7/Lua/civ3/ruleset.json`, in its order: those from the BIQ, and the civilizations (Medieval Japan's
clans) and world sizes (1v1 to 8 Player), from MPTournamentMDJ.biq as `GameSetup.ImportMdjBiq` imports it; the others as in Civ3's ruleset. It also keeps
the scenario's search folders, where Civ3's own art for it is, for playing with Civ3's art (standalone mode ignores
them). `MdjScenarioTest.TheBundledRulesAreTheBiqs` checks the file is up to date.

The bundled MDJ is the Civ Players League's newer `MPTournamentMDJ.biq`, not the one Civ3 ships. `--mdj` reads the BIQ
from an install's `Conquests/Scenarios`, so give it a folder laid out like one: `Conquests/conquests.biq`,
`Conquests/Text` and `Conquests/Conquests` (junctions to the install's do), and the League's file in
`Conquests/Scenarios`; pass that folder as the Civ3 install path. The test finds the League's file where
`BundledScenariosTest` finds the other mods (`C7_LEAGUE_MODS`, or `Downloads/Scenarios`), and is skipped without it.

## The League's ladder mods
`dotnet run -- --mpt <BIQ> [Civ3 install path] [ruleset.json path]`, and likewise `--qc`, `--modern` and `--future`, write the
Civ Players League's ladder mods (see `C7Engine/GameSetup.BundledRules.cs`) from the BIQ given, wherever it is, into
`C7/Lua/mptournament`, `qc`, `modern` and `future`: `MPTournament.biq`, `QC_1.74.biq`, `Modern_2.4.biq` and
`newfuture3.3.biq`, as `GameSetup.ImportBundledBiq` imports them. The Civ3 install gives `conquests.biq` for what a BIQ leaves
out. Besides the sections from the BIQ they take its civilizations, world sizes, time options (its turn limit; the last era padded
to 10000 turns as in Civ3's ruleset) and victory conditions, and their rules carry the start the BIQ's first LEAD gives every civ
(`Rules.ScenarioStart`: Modern's and Future's era, government, advances and units). Only the civilizations the BIQ lets play
(its GAME section's list) are imported, so its Spectators and the civs it doesn't use are left out. Buildings sharing a name (Modern's,
one for each government) are told apart, and refer to each other by the BIQ's index; Modern's are named as the mod names them
(`GameSetup.RenameModernBuildings`). The rules have a tech tree of their own, and the BIQ's multiplayer turn timer
(`Rules.MultiplayerTurnTimerBase`, `PerCity` and `PerUnit`). The terrain improvements' bonuses and the
terraforms' advances, resources and turns are the BIQ's, as for MDJ; the difficulties are Civ3's. A mod's own art isn't bundled.
`BundledScenariosTest.TheBundledRulesAreTheBiqs` checks the files are up to date when the BIQs are in the folder
`C7_LEAGUE_MODS` names, or in `Downloads/Scenarios`.
