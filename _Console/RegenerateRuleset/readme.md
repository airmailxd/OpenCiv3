# RegenerateRuleset
Regenerates the bundled ruleset, `C7/Lua/civ3/ruleset.json`, from Civ3's `conquests.biq`: imports the BIQ with
`ImportCiv3.ImportBiq`, and replaces the sections of `ruleset.json` that come from the BIQ with the imported ones.
The file keeps its order of sections, its formatting and its line endings, so the diff shows only real changes. With
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
- `victoryConditions`: the victory conditions of new games.

The program stops if `ruleset.json` has a section in neither list; add it to one of the lists in `Program.cs`.
