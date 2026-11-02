# Play log: recording a human's game

The play log records a game played by a human: what they did each turn, and what the Advanced AI would have done at the same decisions. It's for tuning the AI (doc/ai-roadmap.md, Track 2) and for finding new benchmarks.

## Turning it on

Settings → Developer → **Record play log**. It's off by default and saved in C7.ini (`[developer] recordPlayLog`). It takes effect for the next game you start or load.

Each game started or loaded then writes one file to the `play-logs` folder beside C7.ini. That's the game's folder when it can write there (playing from an extracted zip or the editor), otherwise `%APPDATA%\OpenCiv3\play-logs`. The name is `<date>-<time>-<civ>-<seed>.jsonl`. The game's log says where, on the line "Recording the play log to ...". A loaded game starts a new file. The seed is the same for every session of a game, so give the summary tool all of its files.

Headless games and tests record too when `C7_PLAY_LOG_DIR` names a folder.

### Hotseat and LAN

The log is written by the engine that plays the game:
- in single player and hotseat, on that machine, for every human;
- in a LAN game, on the **host**, for every human, whichever machine they play at. A guest's machine writes nothing, and the guest's own setting doesn't count.

No new network messages and no save changes: recording only reads the game.

## What's in it

One JSON object per line. Every row except the state rows has a `"type"`.

- `header`: one when the file opens: game (the seed), turn, difficulty, map size, the civs and who plays them.
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
python tools/play-log/summarize.py play-logs/20261101-213759-Rome-4321.jsonl [more files of the game] [--bin 50] [--civ Rome]
```

Plain Python 3, no packages. For each range of turns (`--bin`, default 50) it prints:
- how often the human agreed with the Advanced AI on research, production, government, revolutions and tax (the last rates set each turn);
- the most common disagreements, such as "Settler -> Archer x15": the human built Settlers where the AI would have built Archers;
- each city site's score against the best site the AI knew of, and where the AI would have settled instead;
- the human's benchmarks (`cities50`, `cities100`, `cities150`, `cities200`, `govTurn`, `techs100`, workers per city at turn 100, military at turns 100 and 150) next to the Advanced AI's typical values from doc/ai-roadmap.md;
- fights won and lost.

`--civ` picks one human of a hotseat or LAN game.
