# 7. Map, terrain, resources, workers, barbarians and huts

Scope: Civ3 1.29f, PTW 1.27f, Conquests (C3C) 1.22, standard rules (`conquests.biq`).
Scenarios, mods (C3X etc.) and the special C3C game modes are excluded.

## Sources and how they are cited

Primary sources were read straight from the Steam "Civilization III Complete" install
(`D:\Steam\steamapps\common\Sid Meier's Civilization III Complete\`, called `<civ3>` below).
They are local files, not URLs, so each citation gives the file and the entry key.

| Tag | Source |
|---|---|
| **BIQ** | `<civ3>/Conquests/conquests.biq`, the C3C 1.22 default rules. Read with this repo's `QueryCiv3` (sections TERR, TFRM, GOOD, RULE, GOVT, DIFF, WSIZ). The quote is the field name and value. |
| **PEDIA** | `<civ3>/Conquests/Text/Civilopedia.txt` (C3C). The quote follows the `#KEY` of the entry. |
| **EDHELP** | The C3C editor help `<civ3>/Conquests/Civ3ConquestsEdit.chm`, decompiled. Cited by page name. |
| **README-129** | `<civ3>/README.txt`, the vanilla patch notes up to 1.29f. |
| **README-PTW** | `<civ3>/civ3PTW/readme.txt` (PTW 1.27f). |
| **README-C3C** | `<civ3>/Conquests/readme.txt` (C3C 1.20/1.22). |
| **README-CMPL** | `<civ3>/Civ Complete Readme.txt`. |
| **SCRIPT** | `<civ3>/Conquests/Text/script.txt`, the in-game messages. |
| **MANUAL** | The original Civilization III manual (2001, vanilla). It came from a PDF text extract with no URL available, so page numbers are approximate. |
| **INFOC** | CivFanatics Civ3 Info Center (Firaxis pre-release info): https://civfanatics.com/civ3/infocenter/ |

Web sources are cited by URL. Forum findings are community testing, not official
documentation, so they are marked as such.

---

## 1. Terrain types

### 1.1 Base table (C3C BIQ TERR)

| Terrain | Food | Shield | Commerce | Irrigation + | Mine + | Road + | Defence % | Move cost | City allowed | Colony | Wheeled impassable | Fort / Airfield / Radar / Outpost | Pollution / global-warming effect (TERR "PollutionEffect") |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Desert | 0 | 1 | 0 | 1 | 1 | 1 | 10 | 1 | yes | yes | no | all yes | none |
| Plains | 1 | 1 | 0 | 1 | 1 | 1 | 10 | 1 | yes | yes | no | all yes | becomes Desert |
| Grassland | 2 | 0 | 0 | 1 | 1 | 1 | 10 | 1 | yes | yes | no | all yes | becomes Plains |
| Tundra | 1 | 0 | 0 | 0 | 1 | 1 | 10 | 1 | yes | yes | no | all yes | none |
| Flood Plain | 3 | 0 | 0 | 1 | 0 | 1 | 10 | 1 | yes | yes | no | all yes | none |
| Hills | 1 | 1 | 0 | 0 | 2 | 1 | 50 | 2 | yes | yes | no | all yes | none |
| Mountains | 0 | 1 | 0 | 0 | 2 | 1 | 100 | 3 | **no** | yes | **yes** | all yes | none |
| Forest | 1 | 2 | 0 | 0 | 0 | 1 | 25 | 2 | yes | yes | no | all yes | base terrain (14) |
| Jungle | 1 | 0 | 0 | 0 | 0 | 1 | 25 | 3 | yes | yes | **yes** | all yes | base terrain (14) |
| Marsh | 1 | 0 | 0 | 0 | 0 | 1 | 20 | 2 | **no** | **no** | **yes** | outpost only | becomes Coast (11) |
| Volcano | 0 | 3 | 0 | 0 | 0 | 0 | 80 | 3 | no | no | yes | none | none |
| Coast | 1 | 0 | 2 | 0 | 0 | 0 | 10 | 1 | no | no | — | none | none |
| Sea | 1 | 0 | 1 | 0 | 0 | 0 | 10 | 1 | no | no | — | none | none |
| Ocean | 0 | 0 | 0 | 0 | 0 | 0 | 10 | 1 | no | no | — | none | none |

- Source for every cell: BIQ TERR fields `Food`, `Shields`, `Commerce`, `IrrigationBonus`,
  `MiningBonus`, `RoadBonus`, `DefenseBonus`, `MovementCost`, `AllowCities`, `AllowColonies`,
  `ImpassableByWheeled`, `AllowForts`/`AllowAirfields`/`AllowRadarTowers`/`AllowOutposts`, `PollutionEffect`.
  For example, Hills reads `IrrigationBonus=0 MiningBonus=2 RoadBonus=1 DefenseBonus=50 MovementCost=2 Food=1 Shields=1 Commerce=0`.
- Meaning of the bonus fields. EDHELP "Irrigation (Terrain Terraform Bonuses)": "Determines the bonus to food production if a tile of the selected terrain type is irrigated."
  EDHELP "Road (Terrain Terraform Bonuses)": "Determines the bonus to commerce if a road is built on a tile of the selected terrain type."
- A zero RoadBonus means no road can be built. README-129: "Restricted roads/railroads from being built on terrain that has no road bonus." So Volcano and water tiles cannot have roads.
- Meaning of `PollutionEffect`. EDHELP "Pollution Effect": "Selects the terrain type (if any) that a tile of the selected terrain type will become if pollution stays on that tile for an extended length of time. For example, grassland would become plains, and plains would become desert." Value 14 is "Base Terrain Type".
- **Bonus grassland ("Grassland with shield").** PEDIA `#TERR_Grassland_with_Shield`: "Some grasslands contain mineral deposits ... These have a rock outcropping to symbolize this and produce a shield if worked in a city radius."
  README-PTW: "Updated placement of forests and jungles on bonus grassland to leave the bonus shield under the forest/jungle."
- **Fresh-water lakes are not a terrain type.** README-PTW: "NOTE: Fresh water lakes are not a terrain type. They are composed of coastal tiles surrounded by land."
  PEDIA `#GCON_Food` nevertheless lists "Fresh Water Lake 2 cannot be irrigated", against 1 food for Coastal. See Disputed.
- **Mountains cannot hold cities.** MANUAL p.~131 (Build/Join City): "You also cannot build on Mountains." This matches BIQ `AllowCities=0`. BIQ also has `AllowCities=0` for Marsh and Volcano.
- **Wheeled units need a road.** MANUAL ch.7: "wheeled units require a road to travel through Jungles and Mountains." BIQ also sets `ImpassableByWheeled=1` for Marsh and Volcano.
- **Movement.** EDHELP "Movement Cost (Terrain Properties)": "Determines the base movement cost for a tile of the selected terrain type."

### 1.2 Defence bonuses (cross-check)

PEDIA `#GCON_Terrain_Combat` lists Grasslands, Plains, Desert, Floodplains, Tundra, Ocean, Sea, Coast and
Fresh Water Lake at 10%. It gives "Marsh 20%", "River 25%*", "Forest 25%", "Jungles 25%", "Hills 50%",
"Volcano 80%" and "Mountains 100%", with the footnote "* This bonus applies to the defender if attacked from the
other side of a river." These figures match BIQ exactly. The structural bonuses listed there are Fortified +25%,
Fortress +50%, Barricade +100% and City +50%. The combat section owns those.

### 1.3 Despotism tile penalty (affects every tile yield)

- PEDIA `#GOVT_Despotism`: "any city production square which produces more than two food, shields, or commerce in a despotic government instead produces one less."
- The same penalty applies under Anarchy per the government section. That is not re-verified here.

---

## 2. Rivers

| Effect | Rule | Source |
|---|---|---|
| Placement | Rivers run along tile edges. | PEDIA `#TERR_River`: "[Rivers] do not run [in] squares: instead they run along the edges of squares." |
| Commerce | +1 commerce to any tile with a river on one of its edges. | PEDIA `#TERR_River`: "Any square with a river along its edge produces one extra commerce if worked by citizen laborers within a city radius." |
| Fresh water | Lets adjacent tiles be irrigated before Electricity. | PEDIA `#TERR_River`: "rivers are sources of fresh water, allowing irrigation of adjacent squares without electricity." |
| Defence | +25% (`RULE.RiverDefensiveBonus=25`), only when attacked across the river. It stacks with other bonuses. | EDHELP "River (Defensive Bonuses)": "Default is 25. Note that this bonus is cumulative with other terrain and fortification-related defensive bonuses." The cross-river condition is from PEDIA §1.2. |
| Movement | No movement bonus along rivers. Crossing a river cancels the road bonus until Engineering. | MANUAL p.~59: "This game offers no movement bonus for river travel." and "Until you discover Engineering, your units do not enjoy the road bonus to movement when they cross a river." PEDIA `#TECH_Engineering`: "Roads now bridge rivers." |
| Flood plains | Flood plains only appear along rivers, so they always carry the river +1 commerce. | PEDIA `#TERR_Flood_Plain`: "[floodplains] appear along river banks". Sullla, https://sullla.com/Civ3/strat7.html: "The base numbers for floodplains tiles are 3 food, 0 shields, and 1 gold". |
| Ships | Ships cannot enter rivers. | MANUAL ch.7: "Ships cannot navigate any ground terrain in the game, including rivers, deltas, and flood plains." |

---

## 3. Water tiles, harbour, offshore platform

- The yields are in §1.1: Coast 1/0/2, Sea 1/0/1, Ocean 0/0/0 per BIQ.
- **Harbour.** PEDIA `#BLDG_Harbor`: "Coastal, sea, and ocean squares produce one extra food."
- **Offshore Platform.** PEDIA `#BLDG_Offshore_Platform`: "causes all worked coastal, sea, and ocean squares to produce one shield."
- **Movement safety.** PEDIA `#TERR_Coast`: naval units risk loss outside coast "But after the discovery of astronomy, this danger vanishes."
  PEDIA `#TERR_Sea`: "[Sea] squares are significant barriers to trade and travel until the discovery of astronomy."
  PEDIA `#TERR_Ocean`: "... until the discovery of Navigation or Magnetism."
- **Resources on water.** BIQ TERR/GOOD: Coast takes Fish, Sea takes Whales and Fish, Ocean takes none.

---

## 4. Tile improvements (worker jobs)

### 4.1 Allowed terrain and effects

| Job | Allowed on | Effect | Tech / resource prerequisite (BIQ TFRM `Required`, `RequiredResource1/2`) |
|---|---|---|---|
| Irrigate | Desert, Plains, Grassland, Flood Plain. These are the terrains with IrrigationBonus > 0. | + the terrain's IrrigationBonus in food (+1 on each). | none. Irrigating without fresh water needs Electricity. |
| Mine | Desert, Plains, Grassland, Tundra (+1); Hills, Mountains (+2). | + MiningBonus in shields. | none |
| Road | All land except Volcano. | + RoadBonus commerce (+1); movement cost 1/3 (`RULE.MovementAlongRoads=3`). | none |
| Railroad | A tile that already has a road. | Movement free; raises irrigation and mine output (see §4.3). | Steam Power + Iron + Coal |
| Plant Forest | Plains, Grassland, Tundra (BIQ `WorkerJobAllowed`=Plant Forest). | Tile becomes Forest. | Engineering |
| Clear Forest | Forest | Becomes its underlying base terrain; 10 shields go to the nearest city. | none |
| Clear Wetlands | Jungle, Marsh | Becomes Grassland or Flood Plain. | none |
| Clear Damage | Polluted, fallout or cratered tiles | Removes the damage. | none |
| Fortress | Land with `AllowForts=1`. | +50% defence, ZOC. | Construction |
| Barricade (C3C) | A fortress tile. | +100% defence; stops enemy ground movement. | Construction |
| Airfield (PTW+) | `AllowAirfields=1`, neutral or own land. | Air base. The worker is consumed. | Flight |
| Radar Tower (PTW+) | Own territory only. | +25% attack and defence within 2 tiles. The worker is consumed. | Advanced Flight (C3C 1.20+) |
| Outpost (PTW+) | Neutral or own land. | Vision 2/3/4 on flat/hills/mountains. The worker is consumed. | Masonry |
| Colony | A tile with a tradable resource outside your borders. | Gives access to the resource. The worker is consumed. | none |

Sources for §4.1:

- BIQ TFRM: `Fortress Required=20` (Construction), `Railroad Required=44 RequiredResource1=1 RequiredResource2=3` (Steam Power, Iron, Coal), `Plant Forest Required=23` (Engineering), `Airfield Required=58` (Flight), `Radar Tower Required=63` (Advanced Flight), `Outpost Required=1` (Masonry), `Barricade Required=20`. Mine, Irrigation, Road, Clear Forest, Clear Wetlands and Clear Damage all have `Required=-1`.
- PEDIA `#TFRM_Irrigation`: "Only squares adjacent to fresh water (river or lake) or to another irrigated square may be irrigated. After the discovery of Electricity, you can irrigate a tile without proximity to fresh water."
- MANUAL p.114 (Irrigate): "A suitable square can always be irrigated if it shares a side or a diagonal with a source of fresh water (terrain with a river running through it, a freshwater lake, or another irrigated square)." So the adjacency includes diagonals.
- MANUAL p.113 (orders table): the Irrigate row lists "Desert, Grassland, Plains, Flood Plains". The Mine row lists "Desert, Hills, Mountains, Plains, Grassland". The Reforest row lists "Engineering Grassland, Plains, Tundra".
- PEDIA `#GCON_Food`: "Hills 1 cannot be irrigated", "Tundra 1 cannot be irrigated", "Desert 0 1 if irrigated".
- PEDIA `#TFRM_Mine`: "Grasslands 0 shields, 1 if mined. w/ outcropping 1 shields, 2 if mined. Plains 1 shields, 2 if mined. Hills 1 shields, 3 if mined. Mountains 1 shields, 3 if mined."
- PEDIA `#TFRM_Road`: "units moving along roads expend one third of a movement point per square, regardless of terrain type." The table there gives +1 commerce with a road on Floodplains, Grasslands, Plains, Hills, Mountains, Forests and Jungles. PEDIA `#GCON_Commerce` adds "Marsh 0 1 with road."
- MANUAL p.~86: "All terrain types produce commerce once penetrated by roads."
- PEDIA `#TFRM_Railroad`: "Workers can build railroads after the discovery of Steam Power, but only if your civilization has access to the Strategic Resources Iron and Coal." MANUAL p.116: "You can only build them where you have already built roads."
- PEDIA `#TFRM_Clear_Forest`: "contribute 10 shields to the nearest controlled city. The resulting terrain will be either grasslands, plains, or tundra, depending upon the base terrain beneath the forest." BIQ `RULE.ForestValueInShields=10`.
- PEDIA `#TFRM_Clear_Wetlands`: "A worker can clear the jungle or marsh from a square, creating either floodplains or grasslands."
- PEDIA `#TFRM_Plant_Forest` says "any grassland or plains square". BIQ and MANUAL also allow Tundra. README-129 (v1.16f): "Forests planted on tundra always display as pine." See Disputed.
- Forest shields cannot be used for wonders. Apolyton (piratebrun, quoted in https://apolyton.net/forum/miscellaneous/archives/civ3-play-the-world/85881-questions-about-terrain-and-the-explaination-the-prima-s-strategy-guide-gives): forests "CANNOT be harvested to speed up PALACES, SMALL WONDERS or WONDERS." This is community testing; the cities/production section should confirm it.
- **Fortress.** PEDIA `#GCON_Fortresses`: "They get a 50% defensive bonus if attacked." and "They have a zone of control and fire at passing enemy units without receiving return fire." BIQ `RULE.FortressDefensiveBonus=50`.
- **Barricade.** PEDIA `#GCON_Barricades`: "A Barricades can only be built on a square already containing a Fortress. Even if unoccupied, barricades automatically stops movement of a ground unit entering its square" ... "They get a 100% defensive bonus if attacked" ... "Barricades become a fortress if their square is pillaged."
- **Airfield.** PEDIA `#GCON_Airfields`: "Construction of the Airfield consumes the worker." and "If it falls into the territory of another civilization, however, the airfield becomes solely useable by the player whose territory it lies in (unless they have yet to research Flight, in which case the airfield is destroyed)." The GCON text says "Airfields can only be built in tiles that have no other improvements, an airfield, a colony, a road or a railroad." PEDIA `#TFRM_Airfield` says "Airfields can only be built in tiles that are empty or have a road and/or railroad."
- **Outpost.** PEDIA `#GCON_Outposts`: "The range of vision for the Outpost is 2 tiles on flat terrain, 3 on hills and 4 on mountains." ... "If an Outpost falls within the territory of any other civilization, it will be destroyed." ... "Outposts can only be built in a tile that is empty, has a mine, irrigation, a road and/or a railroad. You cannot build an Outpost on a tile with an Airfield or a Colony."
- **Radar Tower.** PEDIA `#GCON_Radar_Towers`: "The range of the effected area is 2 tiles. Friendly units within the Radar Towers range get a +25% attack and defense bonus. This bonus can only be applied once, even if multiple towers overlap." ... "Should a Radar Tower fall into the territory of another civilization, it will be destroyed." README-C3C v1.20: "Radar Towers require Advanced Flight" and "Radio Tech removed".
- **Colony.** PEDIA `#TFRM_Build_Colony`: "A colony is necessary only if the resource is not inside your borders. Note that when a city's borders expand to include a colony, the colony disappears because it is no longer needed. The worker is consumed by building a colony." PEDIA `#GCON_Colony`: "you need a road connecting the colony to one of your cities to get the benefits of the resource." SCRIPT `#COLONY_BARBS`: "Our $RESOURCE0 colony was overrun by a tribe of $BARBARIAN1s." So barbarians destroy colonies.
- **City tiles and roads.** README-129: "Changed city code so that a road/railroad is not placed under a city if a road/railroad is not allowed on the terrain type the city is built upon." README-C3C v1.20: "Railroads will always appear under captured cities if you have Steam Power."

### 4.2 Craters, pollution damage

- PEDIA `#GCON_Craters`: "When a bombardment attack which eliminates the last unit or terrain improvement in a square, a crater may be formed. Like pollution, the presence of craters reduces a square's production, commerce, and food output by one."
- MANUAL p.89 ("Pollution's Effects"): "It reduces the production of food, raw materials, and commerce income to zero. Once the terrain is detoxified, production returns to pre-pollution levels." See Disputed.
- Long-term pollution transforms the terrain (`PollutionEffect`, §1.1). PEDIA `#GCON_Pollution` (Global Warming): "It can cause forests and jungles to disappear, and grasslands to become plains, and plains to become desert. World pollution is the sum of all city pollution, nuclear explosions, and meltdowns." SCRIPT `#GLOBALWARMING_RISING_COASTLINES`: "Coastlines are rising!" This matches Marsh→Coast in BIQ.
- MANUAL p.88: "Pollution appears within the City Radius of any city that is excessively productive (produces lots of shields) or has a very large population. ... The number of symbols corresponds to the probability each turn of a square within the city radius becoming polluted." The pollution formula belongs to the cities section.

### 4.3 Railroad yield bonus

- PEDIA `#GCON_Worker_Jobs`: "Increases irrigation or mine output, and provides unlimited movement." MANUAL p.116: "Railroads also increase the yields of both irrigation and mines."
- Community tests, Apolyton (David Weldon), https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes?p=953406:
  "RR through something that has been irrigated = +1 food. RR through something that has been mined = +1 shield."
  "RR through forest does nothing. RR through unimproved squares? Nothing."
  "on a coal mine with RR I get 5, not 6".
- Community tests, CFC (Yeti), https://forums.civfanatics.com/threads/does-railroad-provide-extra-shield-on-mined-grassland-plain.74236/: "If there's a mine, RR adds 1 shield." and "If there's irrigation, RR adds 1 food." Also "Tundra + Mine + Railroad = 1 food, 2 shields, 1 gold".
- Sullla, https://sullla.com/Civ3/strat7.html: forests "cannot be irrigated or mined, and they therefore get no benefit from rails." Deserts "with railroads can actually manage 2 food".
- **Working rule:** +1 food on an irrigated tile and +1 shield on a mined tile. No bonus on an unimproved or forest tile. A competing reading exists (Disputed).

---

## 5. Worker job times

### 5.1 Base turns (TFRM "Turns to Complete")

| Job | C3C 1.22 (BIQ) | PTW (CFC table) |
|---|---|---|
| Mine | 12 | 12 |
| Irrigation | 8 | 8 |
| Fortress | 16 | 16 |
| Road | 6 | 6 |
| Railroad | 12 | 12 |
| Plant Forest | 18 | 18 |
| Clear Forest | **4** | **10** |
| Clear Wetlands / Clear Jungle | 16 | 16 |
| Clear Damage / Pollution | 24 | 24 |
| Airfield, Radar Tower, Outpost | 1 | 1 |
| Barricade | 16 | — (C3C only) |

- C3C column: BIQ TFRM `TurnsToComplete`, for example `Name=Clear Forest TurnsToComplete=4`.
- PTW column: Shiplord, https://forums.civfanatics.com/threads/hi-how-many-turns-for-each-worker-action.80432, which gives both a C3C table ("Clear forest 4") and a PTW table ("Clear forest 10").
- EDHELP "Turns to Complete": "Determines the base rate (how many turns it takes) for workers to complete the selected worker job. This value can be further modified, depending on government type."

### 5.2 The formula

turns = base × terrain movement cost ÷ (government WorkerRate × worker strength/100 × trait × tech multipliers), with several workers pooling their work.

- Apolyton (Nero Would, 19 Dec 2001), https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/50097-worker-improvement-times:
  "To calculate the number of turns to complete a job, multiply the "effort" for the job by the basic Movement Cost";
  "foreign workers only count as half a worker";
  "The Work Rate is 1 for Anarchy, 3 for Democracy and 2 for all other governments.";
  "Also, divide by 2 for Industrious civs, divide by 2 if you have researched Replaceable Parts."
  The same page says the effect of roads and railroads on movement cost is ignored, so the base terrain cost is used.
- EDHELP "Worker Rate (Government Properties)": "The "base" worker rate is 2. Each digit increases the rate by 50%. Thus, 1=50% work rate (bad), 2 = 100% (normal), and 3 = 150% (speedy workers!!)."
- EDHELP "Worker Strength (Unit Statistics)": "If the Worker Strength value of a worker is set at "50" that worker works at "half" (50%) speed, and thus would take twice as long".
- Observed data (DoubleEdged, CFC 80432, 1 Mar 2004, non-industrious, Ancient era) matches base × move cost ÷ 2:
  "Build Mine: (6 grasslands,12 hills,18 mountains)" and "Build Road: (6 in Forest,Hills; 9 in Mountains,Jungle; 3 grasslands)".
- Jack Merchant (same thread): "'Base' turns is not for your regular non-industrious worker, but for slave workers (who work at half speed)."

**Government worker rate (BIQ GOVT `WorkerRate`)**

| Government | Anarchy | Despotism | Monarchy | Communism | Republic | Democracy | Fascism (C3C) | Feudalism (C3C) |
|---|---|---|---|---|---|---|---|---|
| WorkerRate | 1 | 2 | 2 | 2 | 2 | 3 | 4 | 2 |
| PEDIA "Worker efficiency" | 50% | 100% | — | — | — | 150% | 200% | — |

PEDIA `#GOVT_Fascism`: "Worker efficiency 200%". `#GOVT_Democracy`: "Worker efficiency 150%". `#GOVT_Anarchy`: "Worker efficiency 50%".

**Other modifiers**

- **Replaceable Parts.** PEDIA `#TECH_Replaceable_Parts`: "{New Ability} Workers work twice as fast."
- **Captured or enslaved workers.** README-C3C: "Note: Captured workers work at 50% the speed of regular workers." README-CMPL says the same. BIQ `RULE.Slave=1` (Worker). EDHELP "Slave Unit": "settlers represent two population points, so a captured settler would convert into two workers."
- **Industrious.** PEDIA `#GCON_Strengths`: "Workers complete tasks faster". The PEDIA gives no figure; see Disputed for the multiplier (×2 in vanilla/PTW, ×1.5 in C3C).
- **Rounding** of fractional turns is not documented (UNVERIFIED).

### 5.3 Derived turns, one normal native worker, WorkerRate 2, no trait, before Replaceable Parts

This table is DERIVED from base × move cost ÷ 2. The cells marked "obs" match DoubleEdged's observed numbers.

| Job | Move cost 1 (Desert/Plains/Grassland/Tundra/Flood Plain) | Move cost 2 (Hills/Forest/Marsh) | Move cost 3 (Mountains/Jungle) |
|---|---|---|---|
| Road | 3 (obs) | 6 (obs) | 9 (obs) |
| Irrigation | 4 | — | — |
| Mine | 6 (obs) | 12 (Hills, obs) | 18 (Mountains, obs) |
| Railroad | 6 | 12 | 18 |
| Fortress | 8 | 16 | 24 |
| Plant Forest | 9 | — | — |
| Clear Forest | — | C3C 4 / PTW 10 (obs 10) | — |
| Clear Wetlands | — | 16 (Marsh) | 24 (Jungle, obs "Clear jungle: 24") |
| Clear Damage | 12 | 24 | 36 |

---

## 6. Natural resources

### 6.1 Resource list (C3C BIQ GOOD)

| Resource | Type | Food/Shield/Commerce bonus | Prerequisite (revealed by) | Appearance ratio | Disappearance (1 in N per turn) | Terrain (BIQ TERR possible resources) |
|---|---|---|---|---|---|---|
| Horses | Strategic | 0/0/+1 | The Wheel | 160 | never (0) | Plains, Grassland, Hills |
| Iron | Strategic | 0/+1/0 | Iron Working | 160 | 800 | Hills, Mountains |
| Saltpeter | Strategic | 0/0/+1 | Gunpowder | 120 | 800 | Desert, Hills, Mountains |
| Coal | Strategic | 0/+2/+1 | Steam Power | 120 | 400 | Hills, Mountains, Jungle |
| Oil | Strategic | 0/+1/+2 | Refining | 120 | 200 | Desert, Tundra, Marsh |
| Rubber | Strategic | 0/0/+2 | Replaceable Parts | 120 | never (0) | Forest, Jungle, Marsh |
| Aluminum | Strategic | 0/+2/0 | Rocketry | 120 | 400 | Tundra, Hills |
| Uranium | Strategic | 0/+2/+3 | Fission | 100 | 100 | Mountains, Forest |
| Wines | Luxury | +1/0/+1 | — | 0 (random) | never | Plains, Grassland, Hills |
| Furs | Luxury | 0/+1/+1 | — | 0 | never | Tundra, Forest |
| Dyes | Luxury | 0/0/+1 | — | 0 | never | Forest, Jungle |
| Incense | Luxury | 0/0/+2 | — | 0 | never | Desert, Hills |
| Spices | Luxury | 0/0/+2 | — | 0 | never | Forest, Jungle |
| Ivory | Luxury | 0/0/+2 | — | 0 | never | Plains, Forest |
| Silks | Luxury | 0/0/+3 | — | 0 | never | Forest, Jungle |
| Gems | Luxury | 0/0/+4 | — | 0 | never | Mountains, Jungle |
| Whales | Bonus | +1/+1/+2 | — | 0 | never | Sea |
| Game | Bonus | +2/0/0 | — | 0 | never | Tundra, Forest, Marsh |
| Fish | Bonus | +2/0/+1 | — | 0 | never | Marsh, Coast, Sea |
| Cattle | Bonus | +2/+1/0 | — | 0 | never | Plains, Grassland |
| Wheat | Bonus | +2/0/0 | — | 0 | never | Plains, Grassland, Flood Plain |
| Gold | Bonus | 0/0/+4 | — | 0 | never | Hills, Mountains |
| Sugar | Bonus | +1/0/+1 | — | 0 | never | Plains, Hills |
| Tropical Fruit | Bonus | +1/0/+1 | — | 0 | never | Jungle |
| Oasis | Bonus | +2/0/0 | — | 0 | never | Desert |
| Tobacco | Bonus | 0/0/+1 | — | 0 | never | Grassland, Hills |

- Source: BIQ GOOD `Type` (0 = bonus, 1 = luxury, 2 = strategic), `FoodBonus`, `ShieldsBonus`, `CommerceBonus`, `Prerequisite`, `AppearanceRatio`, `DisappearanceProbability`, and the BIQ TERR→GOOD bit array. For example, `Name=Iron ... Type=2 AppearanceRatio=160 DisappearanceProbability=800 Prerequisite=7 ShieldsBonus=1`.
- The PEDIA `#TERR_*` "may contain" lists match the terrain column exactly.
- The PEDIA bonus tables (`#GCON_ResourcesB/L/S`) match BIQ, except that Dyes and Incense are swapped. See Disputed.
- README-129: "Game bonus resource now gives +2 food to its tile."
- CFC Resource reference, https://civfanatics.com/civ3resources.shtml, gives the same prerequisites ("Rubber requires Replaceable Parts", and so on).

### 6.2 Resource rules

- **Placement count.** EDHELP "Appearance Ratio": "In an eight-player game, a value of 160 would mean 2 of each resource per player. ... Setting this value to zero means the resource will be distributed at random. Note that resource placement is also limited by which terrain types can support the given resource." INFOC: "Appearance ratio has no effect on the bonus resources."
- **Disappearance.** EDHELP "Disappearance Probability": "A value of 800 would mean that there is a 1 in 800 chance each turn to "lose" the resource. Setting this value to zero means the resource will never disappear. Note that this value is not influenced by use; a resource can disappear even if a civilization has never used the resource." and "Note 2: "Lost" resources will always re-appear somewhere else on the map, so the fundamental amount of resources in a game world will never drop below the amount set in the appearance ratio."
- README-129: "Fixed bug which sometimes caused depletion of resources so that there would be < 1 per civ."
- SCRIPT `#GOODDISAPPEARED`: "This source of $RESOURCES0 has been exhausted!" and `#GOODAPPEARED`: "We have discovered a new source of $RESOURCES0!"
- **Visibility.** PEDIA `#GCON_ResourcesN`: "Bonus resources are always visible in explored areas of the map." and "Strategic resources appear on the map when a Civilization Advance exposes their usefulness."
- **Terraforming leaves resources alone.** Apolyton (inca911), https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes?p=953406: "changing the terrain of a resource square does not change the resource as it did in Civ2 via terraforming." This is community testing.
- **Connection.** PEDIA `#GCON_Trade`: "Two cities are connected if any one of the following are true: there is a road or railroad running between them, there are harbors in both cities and a visible water route between them, or both cities contain an airport." The same entry adds: "two cities are not connected if the only road/rail that connects them passes through enemy territory, or if a harbor is blockaded by enemy naval forces." PEDIA `#GCON_Territory`: tradable resources "are easier to get when found within your territory (no need for a colony)." INFOC: "To access any resource, you need to build a road to that resource."
- **One source is enough.** PEDIA `#GCON_Trade`: "A single resource icon is enough to supply the needs of an entire civilization."
- **Required resources.** BIQ TFRM Railroad needs Iron and Coal (§4.1). Units and buildings are covered in their own sections.

---

## 7. Flood plains, disease, plague

- BIQ TERR `TerrainFlags`: the "causes disease" bit is set on Flood Plain, Jungle and Marsh. The "cured by Sanitation" bit is set on Flood Plain only. `DiseaseStrength=50` on all of them.
- PEDIA `#GCON_Disease`: "[Disease] kills citizens and units that spend too much time in unhealthy areas like floodplains, jungles and marsh. Military units that fortify in these terrains are susceptible to disease." ... "Your workers can clear jungles or marsh and eliminate that risk. Floodplains continue to be risky until you discover sanitation."
- PEDIA `#TECH_Sanitation`: "Disease from Floodplains is eliminated, and the effect of Plague is reduced."
- **Workers are immune.** README-129: "Corrected Disease Civilopedia chapter to reflect workers are immune to disease."
- EDHELP "Disease (Terrain Properties)": "By default, both Floodplain and Jungle are flagged as disease-causers. The "Disease Strength" dialog indicates how often disease will strike the indicated terrain. As an example, both Floodplain and Jungle have a 50% disease strength; the higher the number, the stronger the disease." See Disputed on marsh.
- SCRIPT `#UNITJUNGLEDISEASE`: "Our $UNIT0 has died of disease from the jungle!" and `#SUMMARY_DISEASE`: "Disease strikes $CITY0."
- **Plague (C3C).** PEDIA `#GCON_Plague`: "Plague, if and when it occurs, can wipe out city population and land units (except Kings). The effect of plague is exacerbated in larger cities, walled cities and cities with commerce and a trade network."
- **No disasters in multiplayer.** README-C3C: "Disasters such as plague and volcanic eruption do not occur in multiplayer games."
- The per-turn disease probability formula is UNVERIFIED.

## 8. Volcanoes

- Volcano tile: 0/3/0, defence 80%, move cost 3. It allows no cities, colonies, roads or improvements (BIQ).
- EDHELP "Volcano Eruptions (Disasters)": "Each volcano on the map will erupt at least once within the indicated eruption period (shown in years)."
- PEDIA `#GCON_Volcanic_Eruptions`: "Most of the time, however, a dark cloud begins to emanate from the volcano's "mouth" a few turns in advance of an eruption -- but such a warning cannot be guaranteed." and "When a volcano erupts it spews forth lava -- in the form of pollution -- that will mar the landscape of many of the tiles adjacent to the volcanic mountain." Workers clear the fallout with Clear Damage.
- SCRIPT: `#VLC_ACTIVE` "Volcano is now active!", `#VLC_KILLS_UNIT` "Our units were burned to a crisp by a lava flow!", `#VLC_KILLS_CITY` "$CITY0 is torched by the volcanic eruption!"
- Community (Optional, CFC), https://forums.civfanatics.com/threads/questions-regarding-volcanos.330741: "the chance decreases significantly as the game goes on." The cause is that the period is in game years.
- The damage radius is disputed (Disputed list).

---

## 9. City radius and city spacing

- **21-tile radius.** PEDIA `#GCON_Radius`: "A square is part of a city's radius if it is within the city's cultural borders, and within two squares of the city."
  MANUAL p.75: "The potential area of development, called the City Radius, extends out from a city in an area three map squares wide-- two squares to the northeast, northwest, southwest, and southeast. The resulting "radius" looks like a fat X."
  MANUAL ch.3: "each city can assign citizens to generate resources in any of the 20 surrounding terrain squares. Since this city is new, however, the workable radius is temporarily limited to the nearest eight. The pattern of 21 squares with the city at the center is called the City Radius."
- **No radius growth in standard C3C.** No base-game, PTW or C3C source found mentions the radius growing past the 21-tile fat cross. The C3C PEDIA `#GCON_Radius` text is unchanged. Larger radii exist only in mods (C3X), which are out of scope.
- **The city tile is always worked.** MANUAL p.75: "The terrain a city occupies is especially important, because it is always being worked. You cannot take the workforce off this square".
- **Minimum distance.** MANUAL p.~131: "Note that you cannot build cities in terrain squares directly adjacent to an existing city." So two cities need at least one tile between them, which makes the minimum distance 2. A Steam thread (https://steamcommunity.com/app/3910/discussions/0/3453716885198230413) shows the editor has no setting for this: "I didn't find an option for that in the editor." It is changeable only with C3X, which is out of scope.
- **Terrain under a new city.** Apolyton (piratebrun, thread 85881 above): when a city is built on a forest, "it is converted to its base terrain." This is community testing.

---

## 10. Map generation basics (cheaply sourced only)

**World sizes (BIQ WSIZ)**

| Size | Width × Height | Default civs | DistanceBetweenCivs | OptimalNumberOfCities | TechRate |
|---|---|---|---|---|---|
| Tiny | 60×60 | 4 | 11 | 14 | 160 |
| Small | 80×80 | 6 | 11 | 17 | 200 |
| Standard | 100×100 | 8 | 12 | 20 | 240 |
| Large | 130×130 | 12 | 18 | 28 | 320 |
| Huge | 160×160 | 16 | 24 | 36 | 400 |

- EDHELP "Distance Between Civs": "Determines the desired minimum distance (in tiles) between the starting locations of civilizations on maps of the selected world size."
- **World setup.** MANUAL p.~45: "Arid: Choosing this option gives your world a larger number of dry terrain squares, such as Plains and Desert. ... Wet: This option produces a larger number of wet terrain squares, such as Jungle and Flood Plain."
- **Seed.** README-C3C v1.20: "Implemented ability to enter a seed value on the world setup screen." README-129: "Made a slight change to placement of goody huts and bonus grassland tiles." The generator algorithm itself is UNVERIFIED and out of cheap scope.

---

## 11. Barbarians

### 11.1 Activity levels

| Level | Meaning |
|---|---|
| No Barbarians | "No goody huts and no barbarian camps" |
| Sedentary | "Goody huts only; no barbarian camps" (EDHELP). The MANUAL instead says "Barbarians are restricted to their encampments. The surrounding terrain is free of their mischief." |
| Roaming | "Lowest level of barbarian camp activity". MANUAL: "less frequently and in smaller numbers than at higher levels. This is the standard level of barbarian activity." |
| Restless | "Median level of barbarian camp activity". MANUAL: "appear in moderate up to significant numbers, at shorter intervals than at lower levels." |
| Raging | "Highest level of barbarian camp activity". MANUAL: "The world is full of barbarians, and they appear in large numbers." |
| Random | "Randomly-selected level of barbarian camp activity" |

- Source: EDHELP "Barbarian Activity (Scenario Properties)" for the quoted level meanings, and MANUAL p.~45 as noted.
- README-129: "Added Barbarian Activity: No Barbarians."
- EDHELP "Select Barbarian Activity": "This controls how often barbarians will spawn, and how far they will stray from their encampments."

### 11.2 Camps, raids, uprisings

- **Camps replace leaders.** MANUAL p.90: "If you find and obliterate an active tribe's village, you end the threat from that tribe (and get a bit of spare change in the process)." and "The village takes the place of the barbarian leader."
- **Camp dispersal pays 25 gold.** SCRIPT `#CAPTURE_BARBARIAN`: "We dispersed a $BARBARIAN0 encampment and took 25 gold!"
- **No barbarian leaders and no ransom in Civ3.** MANUAL p.~49 and INFOC: "There are no barbarian leaders. To get their gold, seek out and invade the barbarian encampments." The leader ransom does not exist. The CFC "ransom 50/100/150" thread (https://forums.civfanatics.com/threads/barbarian-leaders.27479) is about Civ2.
- **Where camps respawn.** MANUAL p.90: "new ones arise in areas that are outside your cultural borders, in areas that are not currently seen. As time passes, they appear at even farther distances from civilization."
- **Coastal camps.** INFOC: "Barbarian villages on the coast can spawn seagoing vessels."
- **Raids on cities.** INFOC: "Barbarians do not capture undefended cities. Now, they just pillage the place, take some of your gold, and move on." SCRIPT `#BARBARIAN_CAPTURE_CITY_PRODUCTION`: "Our work on ... has been destroyed!" `#BARBARIAN_CAPTURE_CITY_POPULATION`: "Some of our people were lost!" `#BARBARIAN_CAPTURE_CITY_GOLD`: "They have carried away $NUM0 gold!" `#BARBARIAN_DESTROY_WALLS`: "They have destroyed the $WALLS3!"
- **Uprisings.** INFOC (pre-release): "Barbarian uprisings are triggered the second time a civ enters a new age." SCRIPT `#SUMMARY_BARBARIAN_EXPLOSION_CITY`: "We have heard reports of a massive barbarian uprising near $CITY0!"
  Community (Apolyton, https://codehappy.net/apolyton/threads/40765-1.htm): "The major uprisings come from a camp that you neglected to wipe out." Harovan: "Their number seems to depend on the barb setting (raging produces stacks of about 24)." ledj: "only 8 barb horsies will show on an uprising episode" on Roaming.
- **Spawn limits (community, TimBentley 2008).** https://forums.civfanatics.com/threads/barbarian-information.267101: "No barb camps will appear until everyone has settled and there are at least (2*NumPlayers) cities." "The maximum number of camps is the number of players". The maximum number of barbarians is "2, 3, or 4" "(for roaming, restless, and raging) * NumPlayers."
- **Units.** BIQ RULE `BasicBarbarianUnitType=6` (Warrior), `AdvancedBarbarianUnitType=11` (Horseman), `BarbarianSeaUnitType=29` (Galley). EDHELP: "Default is Warrior." / "Default is Horseman." / "Default is Galley." No source was found for barbarian units changing by era in the standard rules; open question.
- **Combat bonus against barbarians (BIQ DIFF `AttackBonusAgainstBarbarians`, %):**

| Chieftain | Warlord | Regent | Monarch | Emperor | Demigod | Deity | Sid |
|---|---|---|---|---|---|---|---|
| 800 | 400 | 200 | 100 | 50 | 25 | 0 | 0 |

  EDHELP: "Determines the attack bonus enjoyed by any player (human or AI) against barbarians. A value of zero means the barbarians have no combat disadvantage". How it enters the combat formula belongs to the combat section.
- **Expansionist.** PEDIA `#GCON_Strengths`: "passive minor barbarians are friendlier." MANUAL: "Barbarian villages are more lucrative."

---

## 12. Goody huts

### 12.1 Outcomes

| Outcome | In-game message (SCRIPT) | Condition (CFC 93244, copied from Apolyton) |
|---|---|---|
| Gold | `#GOODY_MONEY` "We got $NUM0 gold from the $BARBARIAN0 tribe's village." | "The tile must not have any type of resource or luxury on it." |
| Map | `#GOODY_MAPS` "The friendly $BARBARIAN0 tribe gave us maps of their region." | "always available" |
| Nothing | `#GOODY_NOTHING` "This $BARBARIAN0 village is deserted." | "always available" |
| Tech | `#GOODY_TECH` "The $BARBARIAN0 tribe has taught us ..." | "Player must still be in Ancient Times." |
| Free unit ("mercenaries") | `#GOODY_MERCENARIES` "This friendly $BARBARIAN0 village gave us a skilled $UNIT1." | "There must be a unit available to the Barbarians as well as the player" |
| Settler | `#GOODY_SETTLERS` "A friendly $BARBARIAN0 $UNIT1 wants to join our $GOVERNMENT2." | "Player must not have a settler (active or in production) or any unit with the Settle AI strategy." "Player must have less cities than (TotalCities / NumActivePlayers)." |
| City | `#GOODY_CITY` "An advanced $BARBARIAN0 village has joined us!" | no listed condition. Community: "On deity you will never get a city". |
| Barbarians | `#GOODY_BARBARIANS` "We have disturbed an angry $BARBARIAN0 $UNIT1." | "Player must not have Expansionist trait." "There must not be a city within a 1-tile radius." "The player must have at least 1 city." "The player must have at least 1 military unit." "The unit popping the hut must not have the "All Terrain As Roads" ability." |

- Condition source: https://forums.civfanatics.com/threads/probabilities-of-goody-huts-c3c.93244 (C3C; the author built an Excel odds calculator; the attached image and file were not readable).
- The MANUAL p.91 list of outcomes matches: town, advance, gold, military unit, hostile units, deserted, settler, map.
- **Difficulty.** INFOC: "Greater chance of getting goodies when you visit a goody hut on lower levels." INFOC: "Conscript units can be received when exploring goody huts".
- **Expansionist (community).** Same thread 93244: "expansionist can only get gold, map and warrior (equal probability." and "None-expansionists will only get map, warrior and barbarians" (both said of a specific high difficulty). Also "On deity you will never get a city, but settler is quite likely for expansionists."
- **Settler or city right after founding.** Thread 93244: "You cant get a settler or city when building a city, even if you use your last settler to build the city."
- **Community odds (small samples).** https://forums.civfanatics.com/threads/goody-hut-probabilities.24911/latest:
  - Regent: tech about 30%, gold about 10%, unit about 10%, map about 10%, barbarians about 35%, deserted about 5%.
  - Chieftain: tech about 50%, gold about 15%, unit about 10%, barbarians about 10%, map about 10%, nothing about 5%. "only Warriors and Horsemen are produced".
  - Deity, non-expansionist, after the first city: deserted about 55%.
- **Huts and barbarian settings.** EDHELP: huts exist on Sedentary and above; "No barbarians" means "No goody huts". EDHELP "Goody Hut (Tile Properties)": a hut cannot share a tile with "a barbarian camp or starting location".
- **Bug fixes.** README-129: "Fixed bug in Civ3 where Scout runs an extra space after revealing a goody hut." README-129 also changed hut placement in map generation (§10).

---

## Disputed

1. **Railroad bonus.** PEDIA and MANUAL say only "increases irrigation or mine output".
   - Community tests: +1 food if irrigated, +1 shield if mined, nothing on unimproved or forest tiles (Weldon on Apolyton 42626; Yeti on CFC 74236; Sullla).
   - Strollen (same Apolyton page): "railroads do work as they say in the manual that add 50% to any improvement (fractions round up)".
   - A Civ3 fandom wiki page (https://civilization.fandom.com/wiki/Improvement_(Civ3), not fetchable, HTTP 402) is reported to say "+1 shield to any tile that already produces at least one shield". No verbatim quote was obtainable.
   - Recommended: +1 food / +1 shield on improved tiles.
2. **Industrious worker multiplier.**
   - Shiplord (CFC 80432): "In VC3/PTW an industrious native worker has a work rate of 4." / "In C3C, that same worker has a work rate of 3." That is ×2, then ×1.5.
   - Padma (https://forums.civfanatics.com/threads/industrious-civs.70768/post-1408719): "In versions prior to Conquests, the Industrious Workers worked at 200% the speed" / "In Conquests, they toned down the Industrious capabilities to only 150% on Non-Industrious (IIRC)."
   - Anarres (same thread): "I thought the bonus was set at 175% for C3C, not 150%?"
   - Frostyboy (same thread): "industrious civs work beteween 25% and 33% faster depending on the task."
   - Nero Would (2001, vanilla): "divide by 2 for Industrious civs".
   - Jack Merchant's figures ("2 for a road, 3 for irrigation and 4 for rails/mines") fit ×1.5.
   - Recommended: ×2 for vanilla/PTW, ×1.5 for C3C.
3. **Industrious and captured workers.**
   - Frostyboy: "Captured workers work at half speed of NON-industrous workers whether or not the tribe you are playing is industrous."
   - Jack Merchant: "the industrious trait does NOT make slave workers faster."
   - Anarres: "a bug AFAIK ... they seem to have re-introduced the bug for Conquests."
   - bg2soatob: "My captured workersw always got the double bonus."
4. **Mountain worker multiplier.** Nero Would and DoubleEdged's observations give 3× (mountain mine 18 turns). BNut on Apolyton 50097: "if its on a mountain it takes 4 times as long."
5. **Clear Forest base.** C3C BIQ gives 4. The PTW table gives 10. A Civilopedia citation in CFC 80432 also gives 10 ("Clear forest 10").
6. **Pollution yield effect.**
   - MANUAL: polluted tile output reduced "to zero".
   - PEDIA `#GCON_Craters`: "Like pollution, ... reduces a square's production, commerce, and food output by one."
   - An Apolyton bug report (https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/48566-bug-in-food-production) reportedly says polluted tiles still give food; it was not fetched verbatim.
7. **Road commerce terrains.** PEDIA, MANUAL and BIQ all give +1 on every land terrain except Volcano. No counter-source was found, but in-game confirmation for Hills, Mountains, Forest, Jungle, Marsh and Tundra is advised.
8. **Plant Forest terrains.** PEDIA `#TFRM_Plant_Forest` says "any grassland or plains square". BIQ, the MANUAL ("Grassland, Plains, Tundra") and README-129 (pine on tundra) also allow Tundra.
9. **Volcano yield.** BIQ gives Shields=3. PEDIA `#GCON_Shields` gives "Volcano 0 cannot be mined".
10. **Volcano damage radius.** Tom2050 (CFC 330741): "within the 8 tiles immediately surrounding the volcano"; "a city is ok if 2 tiles away". Commander Bello and timjack (https://forums.civfanatics.com/threads/volcanoes.73430/latest): "They may damage up to two tiles away." / "random tiles within a two tile radius."
11. **Disease on marsh.** BIQ flags and PEDIA `#GCON_Disease` include Marsh. EDHELP says "By default, both Floodplain and Jungle are flagged".
12. **Ocean food.** BIQ Ocean is 0/0/0. Sullla: "ocean tiles produce only 1 food."
13. **Lake food.** PEDIA `#GCON_Food` says "Fresh Water Lake 2". README-PTW says lakes are ordinary Coast tiles, and Coast is Food=1 in BIQ. The mechanism behind any +1 is unknown.
14. **Dyes and Incense commerce.** BIQ gives Dyes +1 and Incense +2. PEDIA `#GCON_ResourcesL` gives "Dyes +0 +0 +2" and "Incense +0 +0 +1".
15. **Radar Tower prerequisite.** PEDIA GCON/TFRM text and MANUAL say Radio. README-C3C v1.20 says "Radar Towers require Advanced Flight" and "Radio Tech removed". BIQ `Required=63` is Advanced Flight. Use Advanced Flight for C3C.
16. **Sedentary barbarians.** EDHELP: "Goody huts only; no barbarian camps". MANUAL: "Barbarians are restricted to their encampments."
17. **Hut barbarians before the first city.** The Apolyton condition list requires "at least 1 city". etj4Eagle's Deity sample (CFC 24911) reports barbarians about 55% "before the first city".
18. **Airfield-eligible tiles.** PEDIA `#GCON_Airfields`: "no other improvements, an airfield, a colony, a road or a railroad". PEDIA `#TFRM_Airfield`: "empty or have a road and/or railroad".

## UNVERIFIED / open questions

- **Goody hut gold amounts.** No Civ3 source found for the low and high amounts. Exact outcome probabilities by difficulty are also unknown: the CFC 93244 calculator was not readable, and the CFC 24911 samples are small.
- **Hut tech outside the Ancient era.** The condition says Ancient only, but a search summary of CFC 93244 quotes a commenter saying the Middle Ages odds are identical. That was not seen verbatim.
- **Mercenary unit choice.** Which unit a hut gives, and how "Conscript" status is set, is unknown.
- **City tile yields.** Free irrigation, a minimum of 1 shield or 1 commerce, and whether the city tile counts as an irrigation source were not sourced. Apolyton thread 85881 quotes the Prima guide as saying the city tile "is automacally improved in every way possible: irrigation, mining, and roads." Hand this to the cities section.
- **Whether rivers alone connect resources or cities for trade.** PEDIA `#GCON_Trade` lists only road, rail, harbour and airport.
- **Rounding** of fractional worker turns, and how several workers on one tile combine. "Divide by the number of workers" is the community view.
- **Global warming:** the trigger thresholds and the per-turn odds.
- **Disease:** the per-turn chance formula (`DiseaseStrength=50`) and its effect size.
- **Volcano eruption period.** The default comes from the scenario/GAME data; conquests.biq has no GAME value read here.
- **Barbarian units by era** in the standard rules: any upgrade beyond Warrior, Horseman and Galley.
- **Uprising trigger.** The rule ("second time a civ enters a new age") comes only from pre-release INFOC.
- **The `RULE.Slave` default** and the Worker Strength of captured workers in BIQ. The 50% may be a hard-coded flag rather than the Worker Strength field.
- **Hills irrigation.** PEDIA, BIQ and Sullla all say hills cannot be irrigated. No counter-source was found. Listed here only because the question is commonly asked.
- **Search budget.** The web search limit for this session was used up before the city-tile and hut-gold questions could be searched further.
