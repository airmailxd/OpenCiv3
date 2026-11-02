# Play log: recording a human's game

The play log records a game played by a human: what they did each turn, and what the Advanced AI would have done at the same decisions. It's for tuning the AI (doc/ai-roadmap.md, Track 2) and for finding new benchmarks.

## Turning it on

Settings → Developer → **Record play log**. It's off by default and saved in C7.ini (`[developer] recordPlayLog`). It takes effect for the next game you start or load.

### A game's name and folder

With the setting on, a new game asks for its name as it starts ("Name This Game", before the first turn), offering the civilization and the date, such as `Rome 2026-11-02`. Cancel (or Escape) keeps that name. The game gets a folder of that name in the saves folder (`Saves` beside C7.ini: the game's own folder when it can write there, otherwise `%APPDATA%\OpenCiv3\Saves`), which holds everything of the game:

```
Saves/
  Rome 2026-11-02/
    Rome 2026-11-02.jsonl          the play log, one for the whole game
    Rome 2026-11-02 Turn 41.json   saved from the game menu
    Autosave Turn 40.json          the Auto-Save preference's autosaves
    Autosave Turn 41.json
```

- The name is made fit for a folder on Windows, macOS and Linux (no `<>:"/\|?*`, trimmed, at most 60 characters). If the folder is there already, the game is `Rome 2026-11-02 (2)`.
- **Save Game** opens in the game's folder with the name filled in as `<name> Turn <turn>`, as autosaves are `Autosave Turn <turn>`. Another folder or name can still be picked.
- Autosaves go in the game's folder rather than `Saves/Auto`, named and kept (the last 10) as before. A LAN host's own `LAN Autosave` stays where it is, since resuming looks for it there.
- The game remembers its name and folder in its saves (`playLog: { name, folder, logFile }`), so **loading any of its saves carries on the same log**: a `session` row marks where (the turn, the time and the save loaded), and the rows go on after it. If the game's folder has moved, the folder the save was loaded from counts when the log is beside it; if the log has gone, a new one starts.
- Loading a save from an earlier turn than the log has got to warns that carrying on will break the log's sequence (the turns from the save on repeat in it): **Continue (keep logging)** or **Don't log this session**. Loading the latest save doesn't ask.
- A save from before (or from a game played with the setting off) asks for a name as it loads, offering the save's file name, and the game gets its folder then.
- With the setting off nothing asks and nothing changes: saves go where they did, and the name a save remembers is kept in it for a later game with the setting on.

Logs written before games had names (0.12.1) are `play-logs/<date>-<time>-<civ>-<seed>.jsonl` beside C7.ini, one per session; the summary tool takes all of a game's files. The game's log says where it writes, on the line "Recording the play log to ..." (or "Carrying on the play log ...").

Headless games and tests record when `C7_PLAY_LOG_DIR` names a folder, one file per session in it, as before.

### Hotseat and LAN

The log is written by the engine that plays the game:
- in single player and hotseat, on that machine, for every human;
- in a LAN game, on the **host**, for every human, whichever machine they play at. A guest's machine writes nothing, and the guest's own setting doesn't count.

No new network messages. The host's saves remember the game's name and folder, which LAN snapshots leave out (a folder on the host's machine is of no use to a guest). Recording only reads the game.

## What's in it

One JSON object per line. Every row except the state rows has a `"type"`.

- `header`: one when the file starts: game (the seed), the game's `name`, turn, difficulty, map size, the civs and who plays them.
- `session`: the same, each time the game is loaded and its log carries on, with `loaded`, the save's file name. Rows after a `session` from an earlier turn than the rows before it repeat those turns (the player loaded an older save and chose to keep logging).
- State rows (no `type`): each human's state at the start of each of their turns, in exactly the rows of the AI trials' state log (`C7_TRIAL_STATE`), with `"ai": "Human"`, `"run": "main"`, `"game"` and `"seed"` the game's seed. So `tools/advanced-ai-training/benchmarks.py` and the other trial scripts read them like an AI civ's rows.
- `action`: every message a human sends the engine and the engine accepts (unit selection and the research prompt aside): the turn, the civ, the message (`msg`) and its fields readably (units as `Warrior@12,30`, tiles as `x,y`, cities and techs by name), plus, for some:
  - moves: `from` and `to`;
  - production: `city`, `previous` item and the item it ended up on (`now`);
  - tax: the rates `before` and `after`;
  - founding a city: the tile (`at`).
- `combat`: every fight a human's unit is in, attacking or defending: both units and their hit points, the chance of winning each round (`roundOdds`), the AI's estimate of the chance of winning the fight (`winChance`), the result and the hit points after.

### Shadow decisions

At these decisions an `action` row also has a `shadow`: what the Advanced AI, with its default tuning, its own plan and its fog of war, would have chosen in the human's place.

| Human's message | `shadow` |
|---|---|
| `MsgChooseResearch` | `pick` (the tech the AI values most), `human` (what the human is now researching, the first step to a far tech), `humanRank`, `humanShare` (the chance the AI's weighted roll picks the human's tech), `top` 3 with values and shares |
| `MsgChooseProduction` | `pick`, `humanRank`, `humanScore` ("ruled out" for an option the AI never takes, such as Settlers from a size-1 city), `top` 3 with scores |
| `StartGovernmentTransitionMsg` | `wouldRevolt` (would the AI start a revolution now), `target`, `best`, `current` |
| `SelectGovernmentMsg` | `pick`, `agree` |
| `MsgChangeSliders` | `rates` the AI would set, `agree` with the rates the human ended on |
| `MsgBuildCity` | `siteScore` and `siteRank` of the tile among the AI's city sites, `bestScore`, `top` 3, and `aiPick`: where the AI would have sent this settler |

`agree` says whether the human's choice is the AI's. A sample production row:

```json
{"type":"action","game":4321,"turn":2,"civ":"Rome","msg":"MsgChooseProduction","cityID":"city-1","producibleName":"Warrior","city":"Scripted 1","previous":"Archer","shadow":{"pick":"Warrior","agree":true,"humanRank":1,"humanScore":17.5,"options":6,"top":[{"item":"Warrior","score":17.5},{"item":"Archer","score":17.5},{"item":"Barracks","score":-23.5}]},"now":"Warrior"}
```

How the shadow is asked without changing the game (`C7Engine/AI/Advanced/ShadowAdvanced.cs`):
- For the call only, the human counts as an Advanced AI (`Player.IsAdvancedAi`), with the behaviours its tuning turns on and the AI's fog of war. Their AI type isn't changed and nothing is switched on for them.
- The AI's dice (`GameData.rng`, such as production's ±10%) roll a stand-in that always comes up in the middle. So the shadow is the AI's most likely choice, the same each time, and the game's own random numbers, which saves and LAN games depend on, are untouched.
- The human's plan slot holds the shadow plan only during the call. The tax shadow puts back the rates and the cities' moods it worked through, and doesn't move citizens. So its rates are the AI's for the citizens as the human placed them.
- `EngineTests/GameData/PlayRecorderTest.cs` plays an 80-turn game with a scripted human, with the log off and on, and checks that the whole game state (the save's hash, random number state included) is the same after every turn.

The shadow costs one AI evaluation per decision (a city's production when the human picks it, not every city every turn), so turns aren't noticeably slower.

### Not recorded

- What the AI would have done with units: moves, attacks, worker jobs. They are recorded as the human made them, with no shadow.
- Diplomacy: deals, war and peace are recorded as messages, with no shadow.
- Choices the game makes for the human: production that moves on from a queue, a governor's picks, research picked when the human closes the science screen without choosing (`MsgPickDefaultResearch` is recorded, but the pick is the AI's own).
- The AI players' own turns (the AI trials' state log has those).

## Summarizing a log

```
python tools/play-log/summarize.py "Saves/Rome 2026-11-02/Rome 2026-11-02.jsonl" [more files of the game] [--bin 50] [--civ Rome]
```

Plain Python 3, no packages. For each range of turns (`--bin`, default 50) it prints:
- how often the human agreed with the Advanced AI on research, production, government, revolutions and tax (the last rates set each turn);
- the most common disagreements, such as "Settler -> Archer x15": the human built Settlers where the AI would have built Archers;
- each city site's score against the best site the AI knew of, and where the AI would have settled instead;
- the human's benchmarks (`cities50`, `cities100`, `cities150`, `cities200`, `govTurn`, `techs100`, workers per city at turn 100, military at turns 100 and 150) next to the Advanced AI's typical values from doc/ai-roadmap.md;
- fights won and lost.

`--civ` picks one human of a hotseat or LAN game.
