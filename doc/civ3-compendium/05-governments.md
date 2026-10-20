# 5. Governments

Scope: base game, Play the World (PTW) and Conquests (C3C), patched (1.29f / PTW 1.27f / C3C 1.22).
Scenarios, mods and the special C3C game modes are out of scope. Where PTW/vanilla differ from C3C,
the difference is called out. C3C is the target ruleset.

## Sources used

| Key | Source |
|---|---|
| **PEDIA** | Civ3 Complete install, `Conquests/Text/Civilopedia.txt` (the C3C 1.22 in-game Civilopedia). Entries are cited by key (`GOVT_Despotism`, `GCON_Unit_Support` ...). The government stat blocks are mirrored online at https://civfanatics.com/civ3/civilopedia/governments/ |
| **BIQ** | `Conquests/conquests.biq` (C3C 1.22 default rules), GOVT / RULE / DIFF / RACE sections, read with the repo's `QueryCiv3` BIQ reader (field names from `QueryCiv3/BiqSections/Govt.cs`). |
| **BIQ-PTW** | `civ3PTW/civ3X.bix` (PTW) and `civ3mod.bic` (vanilla) GOVT sections, raw-decoded with the same record layout. The two files have identical GOVT data. |
| **EDHELP** | `Conquests/Civ3ConquestsEdit.chm`, the official C3C editor help (decompiled). Cited by page name. |
| **README** | `README.txt` (vanilla patch notes up to 1.29f) and `Conquests/readme.txt` (C3C 1.20 to 1.22). |
| **SCRIPT** | `Conquests/Text/script.txt` (in-game advisor and popup text). |
| **CFC-GOV** | https://civfanatics.com/civ3/civilopedia/governments/ ("This article covers Civilization 3 Complete V1.22.") |
| **CFC-WW** | https://civfanatics.com/civ3/strategy/game-mechanics/how-does-war-weariness-work/ |
| **CFC-CORR** | https://civfanatics.com/civ3/strategy/game-mechanics/everything-about-corruption-c3c-edition/ |
| **CFC-ESP** | https://civfanatics.com/civ3/strategy/game-mechanics/a-study-of-espionage-missions/ |
| **CFC-FAQ** | https://forums.civfanatics.com/threads/frequently-asked-questions-civ3-play-the-world-conquests.170282/ |

Note: the repo copy `C7/Civilopedia/Text/Civilopedia.txt` is OpenCiv3's own text ("It is not Civ3's
Civilopedia"), so it is **not** used as a source here.

C3C has **eight** governments: "There are eight forms of government that you can use to control your
civilization." (PEDIA `GCON_Governments`). They are Anarchy, Despotism, Monarchy, Republic, Feudalism, Communism,
Fascism and Democracy. **There is no Tribalism in stock C3C.** Tribalism appears only in mods. Vanilla and PTW
have six (no Feudalism, no Fascism): the BIQ-PTW GOVT section has `count 6`.

---

## 5.1 Master table (C3C 1.22, from BIQ, matching PEDIA)

BIQ enum decoding:
- Corruption: 0 Minimal, 1 Nuisance, 2 Problematic, 3 Rampant, 4 Catastrophic, 5 Communal. This matches PEDIA's labels one for one.
- Hurrying: 0 none, 1 forced labor, 2 paid labor.
- War weariness: 0 None, 1 Low, 2 High.
- Worker rate: 2 = 100%. EDHELP *Worker Rate*: "The "base" worker rate is 2. Each digit increases the rate by 50%. Thus, 1=50% work rate (bad), 2 = 100% (normal), and 3 = 150%".

| Government | Prereq tech | Corruption/Waste | Tile penalty | Trade bonus | Worker rate | Hurry | Draft/turn | Military police | Free units Town/City/Metro | Gold per extra unit | War weariness | Assimilation %/turn | Rate cap | Spies / Diplomats | Immune to | Other flags |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Anarchy | (transition type) | Catastrophic (4) | yes | no | 1 (50%) | none | 0 | 0 | 0/0/0, FreeUnits = -1 | 1 | None | 1 | 10 | Regular / Regular | - | TransitionType=1, RequiresMaintenance=0 |
| Despotism | none (DefaultType) | Rampant (3) | yes | no | 2 (100%) | forced | 2 | 2 | 4/4/4 | 1 | None | 1 | 10 | Regular / Regular | - | DefaultType=1 |
| Monarchy | Monarchy | Problematic (2) | no | no | 2 (100%) | paid | 2 | 3 | 2/4/8 | 1 | None | 2 | 10 | Regular / Regular | - | |
| Feudalism | Feudalism | Problematic (2) | no | no | 2 (100%) | forced | 2 | 3 | 5/2/1 | **3** | Low | 3 | 10 | Regular / Regular | - | |
| Republic | The Republic | Nuisance (1) | no | **yes** | 2 (100%) | paid | 1 | 0 | 1/3/4 | **2** | Low | 2 | 10 | Regular / Regular | - | |
| Communism | Communism | **Communal** (5) | no | no | 2 (100%) | forced | 2 | 4 | 6/6/6 | 1 | None | 4 | 10 | **Veteran** / Regular | - | allows Secret Police HQ |
| Fascism | Fascism | Nuisance (1) | no | no | **4 (200%)** | forced | 2 | 4 | 4/7/10 | 1 | None | 2 | 10 | **Veteran** / Regular | - | **Xenophobic=1, ForceResettle=1** |
| Democracy | Democracy | Minimal (0) | no | **yes** | 3 (150%) | paid | 1 | 0 | 0/0/0 | 1 | **High** | 4 | 10 | Regular / Regular | **Initiate Propaganda** (mission index 6) | |

(Spies/Diplomats: BIQ `SpiesAre`/`DiplomatsAre` 1 = Regular, 2 = Veteran. EDHELP *Spy Experience*: "Determines the experience level of spies (Conscript, Regular, Veteran, Elite) under the selected government.")

### PTW / vanilla differences (BIQ-PTW)

- Only 6 governments (no Feudalism, no Fascism).
- **Communism unit support 2/4/8** in PTW/vanilla. C3C has 6/6/6.
- **Republic unit support 0/0/0 at 1 gold** in PTW/vanilla. C3C has 1/3/4 at 2 gold.
- All other GOVT fields match C3C: corruption, worker rate, hurry, draft, MP, war weariness, assimilation, rate cap and immunity.
- Corroboration for the vanilla Communism value: CFC-FAQ-era GameFAQs guide (https://civfanatics.com/civ3/strategy/misc/civilization-iii-faqstrategy-guide-v3-0/, v3.0, Jan 2002): "under the non-democratic forms of government (Monarchy, Communism), you can support 2 units for each town (size 6 or below), 4 units for each city (size 7-12), and 8 units for each metropolis (size 13+)".
- C3C note on Republic: CFC-FAQ: "in Republic in Conquests, you get very little unit support - and each unit past the limit costs 2 gpt to maintain!"

---

## 5.2 Per-government detail

### Anarchy

| Field | Value |
|---|---|
| Corruption/waste | Catastrophic: no production, no taxes, no research |
| Tile penalty | Yes (Despotism penalty) |
| Workers | 50% |
| Hurry | Impossible |
| Draft / MP | 0 / 0 |
| Unit support | No upkeep (BIQ FreeUnits = -1, per-size 0/0/0) |
| Maintenance | None (RequiresMaintenance = 0) |
| War weariness | None |

- PEDIA `GOVT_Anarchy`: "During that period corruption and waste are so high that no production occurs and no taxes are collected. Scientific research comes to halt, as well. There is no improvement maintenance when a civilization is in Anarchy."
- CFC-GOV: "No city production, no research, Despo Penalty (see Despotism). Specialists still work. No maintenance costs or unit upkeep needed." BIQ agrees on the penalty: `TilePenalty=1` for Anarchy.
- Hurry: SCRIPT `#GOVERNMENT_CANNOT_HURRY`: "We are in the midst of a revolt, sir. We cannot hurry production." PEDIA `GCON_Hurry_Production`: "You have [no] production if your civilization is in Anarchy."
- Draft: PEDIA `GCON_Draft`: "A civilization in Anarchy may not use the draft."
- EDHELP *Corruption Modifier (Difficulty Level)*: "The Corruption Modifier has no effect on players whose government experiences catastrophic corruption, cities in civil disorder, or capital cities."

### Despotism

| Field | Value |
|---|---|
| Corruption/waste | Rampant (distance factor x3/2, see 5.6) |
| Tile penalty | Yes |
| Workers | 100% |
| Hurry | Forced labor (population) |
| Draft / MP | 2 / 2 |
| Unit support | 4/4/4, 1 gold per extra unit |
| War weariness | None |
| Start government | Yes (DefaultType) |

- Tile penalty, PEDIA `GOVT_Despotism`: "any city production square which produces more than two food, shields, or commerce in a despotic government instead produces one less."
- EDHELP *Flags*, "Standard tile penalty": "Under the selected government, no tile may generate more than 2 food and shields." The wording conflicts with PEDIA (see Disputed).
- EDHELP *Flags*, "Default type": "All civilizations start the game under the selected government."

### Monarchy

| Field | Value |
|---|---|
| Corruption/waste | Problematic |
| Workers | 100% |
| Hurry | Paid (gold) |
| Draft / MP | 2 / 3 |
| Unit support | 2/4/8, 1 gold per extra unit |
| War weariness | None |

- PEDIA `GOVT_Monarchy`: "Corruption and waste are significant, but are ameliorated to an extent by loyalty to the King."

### Feudalism (C3C only)

| Field | Value |
|---|---|
| Corruption/waste | Problematic |
| Workers | 100% |
| Hurry | Forced labor |
| Draft / MP | 2 / 3 |
| Unit support | 5/2/1, **3 gold** per extra unit |
| War weariness | Low |

- PEDIA `GOVT_Feudalism`: "your vassals are wary of continual warfare and war weariness may result. You do not suffer the production problems associated with despotism, however. Maintenance cost per unit per turn is 3 gold instead of 1."
- Note the inverted support curve: towns support more than metropolises. CFC-GOV: "Good for civs with small towns (under pop 6). You might actually want small towns because of the unit support".
- **"Feudalism pikemen support" is not in stock C3C.** PEDIA, BIQ GOVT and EDHELP contain no unit-type-specific support rule. UNVERIFIED as a stock rule; probably a mod.

### Republic

| Field | Value |
|---|---|
| Corruption/waste | Nuisance |
| Trade bonus | Yes (+1 commerce, see Disputed for "only on tiles already >= 1") |
| Workers | 100% |
| Hurry | Paid (gold) |
| Draft / MP | 1 / **0** (no martial law) |
| Unit support | 1/3/4, **2 gold** per extra unit (C3C) |
| War weariness | Low |

- PEDIA `GOVT_Republic`: "your government is affected by war weariness, which can cause significant civil disorder problems in times of war, especially if you are the aggressor. Maintenance cost per unit per turn is 2 gold instead of just 1."
- CFC-GOV: "Adds 1 commerce to any tile already producing at least 1 = the commerce bonus."

### Communism

| Field | Value |
|---|---|
| Corruption/waste | **Communal**: the same in every city, independent of distance (see 5.6) |
| Workers | 100% |
| Hurry | Forced labor |
| Draft / MP | 2 / 4 |
| Unit support | 6/6/6 (C3C), 1 gold per extra unit |
| War weariness | None |
| Espionage | Spies are Veteran |
| Special | Can build the Secret Police HQ (a second "palace" for corruption) |

- PEDIA `GOVT_Communism`: "One positive aspect of Communism is its effect upon corruption and waste: all cities suffer the same, limited effects."
- PEDIA `BLDG_Secret_Police_HQ`: "Only civilizations under Communist government can build the Secret Police HQ. Changing from Communism will cause the Secret Police HQ bonuses to stop."
- README (C3C 1.20): "Secret Police HQ no longer gives corruption OR OCN bonus when player leaves Communism."
- README (vanilla 1.17f): "Corruption calculations for Communism are now flat for the number-of-cities factor."
- CFC-GOV: "you can whip foreign people in newly conquered cities to prevent revolts". Plausibly a consequence of forced-labor hurry. UNVERIFIED as a distinct rule.

### Fascism (C3C only)

| Field | Value |
|---|---|
| Corruption/waste | Nuisance |
| Workers | **200%** |
| Hurry | Forced labor |
| Draft / MP | 2 / 4 |
| Unit support | 4/7/10, 1 gold per extra unit |
| War weariness | None |
| Espionage | Spies are Veteran |
| Flags | Xenophobic, Forced Resettlement |

- PEDIA `GOVT_Fascism`: "Your cities lose population points upon a change to fascist government, Newly conquered cities also lose population, and do not generate culture until a popular majority has been assimilated."
- EDHELP *Flags*, Forced Resettlement: "When switching to the selected government, each town in the nation loses 1 population, each city loses 2 population, and each metropolis loses 3 population."
- EDHELP *Flags*, Xenophobic: "no culture is gained in cities where less than half of the population of the city is from the same race as the controlling civilization."
- CFC-GOV: "Higher chance of espionage mission success". Explained by `SpiesAre=2` (Veteran), the same as Communism.
- **"Fascism can't be bribed / is immune to propaganda": not supported by the data.** Fascism's `ImmuneTo` is -1 (none). The per-government "Cannot Bribe" setting is the `CanBribe` field of the GOVT-vs-GOVT matrix. In conquests.biq it holds the uninitialized value 0xCDCDCDCD for every pair, so it does not distinguish Fascism. UNVERIFIED.

### Democracy

| Field | Value |
|---|---|
| Corruption/waste | Minimal (distance factor x3/4) |
| Trade bonus | Yes |
| Workers | 150% |
| Hurry | Paid (gold) |
| Draft / MP | 1 / 0 |
| Unit support | **0/0/0**: every unit costs 1 gold |
| War weariness | **High**. Can cause a revolt into Anarchy |
| Propaganda | **Immune** |

- PEDIA `GOVT_Democracy`: "war weariness is a significant problem and war must be entered into only after much consideration."
- Propaganda immunity: BIQ `ImmuneTo=6` (Initiate Propaganda). SCRIPT `#ESPIONAGE_MISSION_INITIATE_PROPAGANDA_IMMUNE`: "The $CIVADJ1 people are immune to propaganda." CFC-ESP: "Democracy is immune against propaganda."
- CFC-GOV, Anarchy: "It also happens when WW reaches 100% when under Democracy." CFC-WW, Democracy "Level 3: Revolt".

---

## 5.3 City size categories and unit support

- Size thresholds, BIQ RULE: `MaximumLevel1CitySize=6`, `MaximumLevel2CitySize=12`, level names `Town`, `City`, `Metropolis`.
  - EDHELP *Level One City Max Size*: "Default is 6." EDHELP *Level Two City Maximum Size*: "Default is 12."
  - CFC-GOV: "Town: 1-6 population / City: 7-12 population (requires fresh water or an aquaduct) / Metro: 12+ population (requires a hospital ...)". (Metro is really 13+, per the RULE maximum of 12 for level 2.)
- Support rule, PEDIA `GCON_Unit_Support`: "Generally, each military unit must be supported with one gold per turn from its treasury. However, each form of government defers these costs for a certain number of units, depending upon how many towns, cities, or metros the civilization controls."
- Formula, CFC-GOV: "4 towns and 3 cities under Monarchy have 4×2 + 3×4 = 20 unit support. That means that every unit over the cap of 20 cost you 1gpt." For each extra unit, the cost is `UnitCost` gold: Republic 2, Feudalism 3, all others 1.
- EDHELP *Free Units Per Town*: "how many "free" units the selected government is allowed per level one city. Note that this number is above and beyond any free units allowed by default." The "by default" link points to the government `FreeUnits` field (0 for all real governments, -1 for Anarchy).
- AI-only difficulty bonuses, BIQ DIFF (`AdditionalFreeSupport` / `UnitSupportBonusForEachSettlement`):

  | Level | Bonus |
  |---|---|
  | Chieftain to Regent | 0/0 |
  | Monarch | 4/1 |
  | Emperor | 8/2 |
  | Demigod | 12/3 |
  | Deity | 16/4 |
  | Sid | 24/8 |

  EDHELP *Additional Free Support*: "how many extra units the AI can support, in addition to any existing governmental bonuses ... cumulative with all other support bonuses."
- Which units count: the vanilla-era guide (URL above) says "This count includes Workers, and also includes any Settlers that haven't built or joined cities yet." This is UNVERIFIED for C3C, and PEDIA says only "each military unit".

## 5.4 Martial law (military police)

- EDHELP *Military Police Limit*: "how many military units (units with attack rating greater than zero) can be garrisoned in a city to reduce unhappiness."
- CFC-GOV: "Military Police Limit: Max number of units in a city that makes 1 unhappy citizen content."
- Citizens quelled per unit come from the difficulty level: BIQ DIFF `MilitaryLaw=1` on all levels. EDHELP *Number of Citizens Quelled by Military*: "how many rioting-citizens are quelled with the presence of military units."
- Limits: Despotism 2, Monarchy 3, Feudalism 3, Communism 4, Fascism 4. Republic, Democracy and Anarchy: 0.

## 5.5 Hurrying (rushing) and the draft

- Method by government, PEDIA `GCON_Hurry_Production`:
  - "Under Despotism, Communism, Feudalism, or Fascism you hurry production in your cities by forced labor. Disillusioned by your harsh rule, citizens will leave your city".
  - "Under Monarchy, Republic, and Democracy you can pay overtime ... This costs gold from your treasury; the amount depends upon how much work remains to be done."
- Forced labor rate. BIQ RULE `CitizenValueInShields=20`. EDHELP: "how many shields are produced by "sacrificing" a point of population ... Default is 20 shields."
  - README 1.21f: "The first citizen is now worth 20 shields instead of 40 shields, just like all the other citizens."
  - README 1.17f: "Eliminated 'Despot Pop Rushing'." (The exact meaning is not stated.)
- Pop-rush unhappiness lasts 20 turns. BIQ RULE `TurnPenaltyForEachHurrySacrifice=20`. README 1.21f: "Draft anger and hurrying unhappiness reduced to 20 turns." It was 40 in 1.17f: "Unhappiness from population rushing production increased to 40 turns."
- Pop-rush limits:
  - SCRIPT `#HURRY_NOT_ENOUGH_PEOPLE`: "Rushing this project would cost the lives of too many citizens!"
  - Forum (eg577, https://forums.civfanatics.com/threads/hurry-production.95782/): "you cannot "whip" more than half of your citizens at once" and "twice that many citizens if there are no shields contributed yet". UNVERIFIED.
  - Wonders cannot be pop-rushed. CFC-FAQ: "you can't rush a wonder by sacrificing population (Despotism, Communism, Fascism)".
- Gold rush rate. BIQ RULE `ShieldValueInGold=4`. EDHELP *Shield Value in Gold*: "how many gold pieces it costs to buy each shield when hurrying production of a city improvement. Default is 4 gold."
  - Empty production box doubles the cost. Forum (petermarkab, same thread): "Multiply by 8 if no shields have been contributed."
  - Unit vs improvement vs wonder multipliers are UNVERIFIED (see open questions).
- Leaders: EditorSupplement.txt: "NOTE: Military leaders can not rush." Great (scientific) leaders can (PEDIA `GCON_Hurry_Production`).
- Draft:
  - PEDIA `GCON_Draft`: "When a civilization develops Nationalism its citizens can be drafted ... A city in a Democracy or Republic can draft [one] citizen per turn ... Communism, Despotism, Fascism, Feudalism, or Monarchy can draft [two] ... You may only draft citizens from a city with a population of 7 or higher."
  - Draft anger lasts 20 turns: BIQ RULE `TurnPenaltyForEachDraftedCitizen=20`.
  - Units are conscripts: Spearman, Pikeman, Musketman, Rifleman, Infantry or Mech. Infantry by tech.
  - README 1.29f: "Corrected the Communism draft rate listed in the Draft Civilopedia entry to 2."

## 5.6 Corruption types (government factors only; full model in the corruption section)

CFC-CORR (C3C), quoted:

- Distance factor `Gd`: "Gd = 3/2 for Rampant corruption (Despotism) / 3/4 for Minimal corruption (Democracy) / 1 otherwise".
- Optimal-city-number factors: "Gr = 0.1 for minimal or nuisance corruption / 2 for communal corruption / 0 otherwise" and "Gp = 3/8 non-communal corruption / 3 for communal corruption" (Gp multiplies the number of corruption-reducing wonders: Forbidden Palace, SPHQ).
- Communal distance: "For communal governments, distance corruption does not actually depend on distance. Each city is considered to be the same distance from the Palace." The value is "d = (MaxD) / 4 where MaxD = (MapW+MapH)/4".
- Communal rank: "all cities have the same rank, which is half the total number of cities in the empire, rounded down."
- Catastrophic (Anarchy) is fixed and bypasses the formula (EDHELP quote under Anarchy).

## 5.7 Revolution, anarchy length, and switching

- When you can switch:
  - Script: "We cannot have a revolution until we discover different forms of government." Each government needs its prerequisite tech (table above). EDHELP *Prerequisite*: "Selects the civilization advance (if any) required before this government type is enabled."
  - You cannot re-revolt during anarchy. SCRIPT `#ALREADYREVOLTING`: "Sir! we are already undergoing a revolt..!"
  - Revolution is started from the Domestic Advisor (PEDIA `GCON_Revolution`).
  - Learning a new government's tech triggers an offer. SCRIPT `#NEW_GOVERNMENT_AVAILABLE`: "our people yearn for a change in government to ... Shall we encourage a revolution?"
- Involuntary anarchy:
  - PEDIA `GCON_Disorder`: prolonged disorder can lead to "An overthrow of your government and descent of your empire into anarchy."
  - Democracy war weariness can do the same (CFC-WW level 3 "Revolt").
- Transition government. BIQ: Anarchy has `TransitionType=1`. EDHELP *Flags*: "When changing governments (i.e., having a revolution), civilizations temporarily transition to the selected government."
- Anarchy length:
  - PEDIA `GOVT_Anarchy`: "Anarchy seldom lasts longer than a few turns."
  - The domestic advisor reports the remaining turns. SCRIPT: "We should have the government back under control in about $NUM0 turns."
  - CFC-FAQ (C3C-aware): "For Religious civilizations, anarchy only lasts 1 turn in Vanilla and Play the World, and 2 turns in Conquests." and "For non-Religious civilizations, the formula is: 1 (2 for Conquests) + random number between 1-4".
  - CFC-GOV: "It lasts from 2 to 8 turns, dependent on the size of your civilization / map size."
  - Forum relay of Soren Johnson (vanilla era, Padma, 2003-04-22, https://forums.civfanatics.com/threads/going-to-monarchy.51241/): "Soren confirmed (way back in February) that the length is always between 1 - 5 turns on a random basis" plus "an additional 0-3 turns depending on number of cities".
  - Forum (https://forums.civfanatics.com/threads/number-of-turns-for-revolution-calculation.108456/, Turner): "Conquests raised that briefly. 8 and 9 turn anarchy was seen."
  - See Disputed.
- Religious trait. PEDIA `GCON_Strengths`: "Religious civilizations do not experience prolonged periods of anarchy during revolutions". No wonder affects anarchy length in C3C: the Statue of Zeus entry (`BLDG_Zeus`) only says it "produces an Ancient Cavalry every 5 turns". The "Statue of Zeus/Liberty shortens anarchy" idea is Civ2.
- AI cap. BIQ DIFF `MaxGovernmentTransitionTime`: 0 (no cap) for Chieftain, Warlord and Regent; Monarch 4, Emperor 3, Demigod 2, Deity 2, Sid 1. EDHELP: "the maximum number of turns the AI must remain in the transition government type ... The default value of zero means there is no maximum".
- Government-locked buildings switch off when you leave the government. README C3C 1.20: "Fixed error with Government required buildings not turning off once switched from that government."

## 5.8 AI favourite / shunned government

- EDHELP *Favorite Government*: "If a government is selected, the civilization will immediately switch to this government type as soon as it becomes available."
- EDHELP *Shunned Government*: "the civilization will avoid this government type at all costs, and if it is forced to operate under this government type, it will immediately switch to another government type as soon as another government type becomes available."
- C3C defaults (BIQ RACE, Favourite / Shunned):

| Civ | Fav | Shun | Civ | Fav | Shun |
|---|---|---|---|---|---|
| Rome | Republic | Communism | Iroquois | Communism | Monarchy |
| Egypt | Despotism | Republic | England | Democracy | Fascism |
| Greece | Democracy | Despotism | Mongols | Despotism | Democracy |
| Babylon | Monarchy | Despotism | Spain | Monarchy | Despotism |
| Germany | Republic | Communism | Scandinavia | Monarchy | Republic |
| Russia | Communism | Democracy | Ottomans | Republic | Despotism |
| China | Communism | Feudalism | Celts | Despotism | Feudalism |
| America | Democracy | Communism | Arabia | Republic | Despotism |
| Japan | Feudalism | Republic | Carthage | Republic | Communism |
| France | Republic | Fascism | Korea | Monarchy | Republic |
| India | Democracy | Despotism | Sumeria | Monarchy | Republic |
| Persia | Monarchy | Republic | Hittites | Fascism | Republic |
| Aztecs | Despotism | Monarchy | Netherlands | Monarchy | Despotism |
| Zululand | Despotism | Republic | Portugal | Democracy | Fascism |
| Byzantines | Republic | Feudalism | Inca | Communism | Republic |
| Maya | Feudalism | Democracy | Barbarians | none | none |

- "Favourite Despotism" (Egypt, Aztecs, Zulu, Mongols, Celts) means the AI never voluntarily leaves the start government, if read literally. UNVERIFIED in play.

## 5.9 War weariness

- Who suffers. PEDIA `GCON_War_Weariness`: "citizens living under a Republic, Feudalism, or Democracy become [war weary]". BIQ WW levels: Republic 1, Feudalism 1, Democracy 2, all others 0.
- PEDIA, same entry: "When [you] initiate the conflict your people will tolerate it for a while ... When someone else declares the war, especially a traditional enemy, your people are more forgiving. Carrying on a defensive war, that is keeping your troops at home, is more tolerable ... the war weariness remains until the war stops."
- Mitigation: Police Station ("combats war weariness", PEDIA `BLDG_Police_Station`) and Universal Suffrage ("Reduces war weariness in all cities", PEDIA `BLDG_Universal_Suffrage`).
- Formula, CFC-WW (community research, version not stated, probably PTW):
  - Points: "Each civ have one wwp number against each of the other civs."
  - Levels: "-1: - 0 wh (war happiness) / 0: 0 - 30 normal, no effect / 1: 31 - 60 / 2: 61 - 90 / 3: 91 - 120 / 4: 121 -".
  - Republic: "Level 1: 25% unhappy people / Level 2: 50% / Level 3: 50% / Level 4: 100%".
  - Democracy: "Level 1: 50% unhappy people / Level 2: 100% unhappy people / Level 3: Revolt".
  - War happiness: "Level -1: 25% happy people" (all governments).
  - Combining: "Number of unhappy people is round down. The number from each civ is added together and subtract 25% for police station and 1 for US (Universal Sufferage)."
  - Point events:
    - "Subtract 30 wwp if the AI attacks you, except when AI is provoked by: use of nuclear weapons, failed spy mission"
    - "Add 1 wwp if you have units in enemys territory when in war."
    - "Add 1 wwp for each lost unit without defence value / improvement pillage/bombed / unit that are bombard down to 1 hp"
    - "Add 2 wwp when a human attacker is defeated"
    - "Add 2 wwp when a unit with defence value is attacked. (Even if you win)"
    - "Add 16 wwp when a size 1 city is captured 17 wwp for bigger cities."
  - Decay: "Subtract 1 wwp if level >= 1, no enemy inside your territory and no units in enemys territory." and "Subtract 1/20 of current wwp each turn in peace (round up)".
  - Bug (as reported): "For a human-AI battle, the AI gets the same penalty as the human".
  - Feudalism (Low) presumably uses the Republic (Low) table. UNVERIFIED inference: the article predates Feudalism.

## 5.10 Senate / forced ceasefire

- **Civ3 has no Senate.** No Senate, ceasefire-forcing or forced-peace rule appears in the PEDIA, SCRIPT (advisor and popup texts), labels.txt, diplomacy.txt, BIQ GOVT fields or EDHELP. The only "Senate" mentions in Civilopedia.txt are historical prose (e.g. `DESC_GOVT_Republic`).
- The Civ2 Senate is the origin of the idea. In Civ3 the equivalent pressure is war weariness, and the Democracy WW revolt.
- Status: verified by absence in the game files. No CivFanatics page states it outright (my web searches turned up only Civ2 pages).

## 5.11 Espionage / propaganda modifiers (GOVT-vs-GOVT matrix)

BIQ stores three values per ordered pair of governments: `CanBribe`, `BriberyModifier` and `ResistanceModifier`. The editor shows `BriberyModifier` as "Propaganda Modifier Vs". EDHELP: "higher means a better chance of bribing ... Selecting "Cannot Bribe this Government Type" effectively prevents bribery from occurring."

C3C `BriberyModifier` (row = BIQ record, column = "vs"). It matches the CFC-ESP "Government modifier" table exactly:

| row \ vs | Anar | Desp | Mon | Comm | Rep | Demo | Fasc | Feud |
|---|---|---|---|---|---|---|---|---|
| Anarchy | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Despotism | 15 | 0 | -10 | -15 | -20 | -30 | 0 | 0 |
| Monarchy | 20 | 15 | 0 | -5 | -10 | -25 | 0 | 0 |
| Communism | 25 | 20 | 5 | 0 | -10 | -20 | 0 | 0 |
| Republic | 30 | 25 | 10 | 8 | 0 | -5 | 0 | 0 |
| Democracy | 35 | 30 | 20 | 10 | 5 | 0 | 0 | 0 |
| Fascism | 0 | 0 | -5 | -20 | -10 | -10 | 0 | 0 |
| Feudalism | 20 | 15 | 0 | -10 | -10 | -20 | -25 | 0 |

C3C `ResistanceModifier` (row/col as above):

| row \ vs | Anar | Desp | Mon | Comm | Rep | Demo | Fasc | Feud |
|---|---|---|---|---|---|---|---|---|
| Anarchy | 0 | 5 | 5 | 5 | 5 | 5 | 0 | 0 |
| Despotism | -5 | 0 | 5 | 5 | 5 | 5 | 0 | 0 |
| Monarchy | -5 | -5 | 0 | 5 | 5 | 5 | 0 | 0 |
| Communism | -5 | -5 | -5 | 0 | -5 | 5 | 0 | 0 |
| Republic | -5 | -5 | 5 | 5 | 0 | 5 | 0 | 0 |
| Democracy | -5 | -5 | -5 | 5 | -5 | 0 | 0 | 0 |
| Fascism | -5 | -5 | 0 | -10 | -5 | -5 | 0 | 0 |
| Feudalism | -5 | -5 | 5 | 5 | 0 | 0 | 5 | 0 |

- EDHELP *Resistance Modifier*: "the modifier to a single population point's chance of continuing to resist each turn. Positive numbers mean that the resisting population has a greater chance of continuing to resist".
- Propaganda success per citizen, CFC-ESP: "spy + government + culture - 5 * units - improvement - capital + nationality", and "If at least 3/4 of the citizen are persuading, the city will flip."
- `CanBribe` is 1 for all pairs in PTW/vanilla and the uninitialized value 0xCDCDCDCD in C3C. Treat it as "can bribe" (no government is protected by it).
- Assimilation. EDHELP *Assimilation Chance*: "the percentage chance, per turn, that a foreign national in city with the selected government type will assimilate". Values are in the master table.

## 5.12 Rate cap (max slider rate)

- BIQ `ScienceRateCap = 10` for **every** government in C3C, PTW and vanilla.
- EDHELP *Rate Cap*: "Determines the maximum science funding (0-10) that the selected government can engage in. Default is always 10 (100%)."
- So stock Civ3 has **no per-government slider cap** (unlike Civ2's 60/70/80%).
- Weak corroboration, Apolyton (https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/44662-low-science-quick-monarchy-tips): "I changed to a 100 percent science rating." (The government is not stated.) Mods set lower caps. A modder's Apolyton post (seen only as a search snippet) used Anarchy 5, Despotism 7 and Monarchy 8.

## 5.13 Implementation notes (C7 cross-check)

- `C7/Lua/civ3/ruleset.json` `governments[]` matches BIQ on corruption, hurry, rateCap (10), war weariness, draft, MP, workerRate, free units and unitCost.
- It has **no fields** for:
  - AssimilationChance
  - ImmuneTo (Democracy propaganda immunity)
  - DiplomatsAre / SpiesAre
  - Xenophobic / ForceResettle (Fascism)
  - the GOVT-vs-GOVT propaganda and resistance matrices

  Check whether the engine hardcodes these elsewhere before relying on them.
- BIQ fields `Toggle1/Toggle2/Toggle3` (QueryCiv3 names; meaning unknown) are uninitialized (0xCCCCCCCC) for the two C3C-only governments, Fascism and Feudalism. That strongly suggests the game ignores them.

---

## Disputed

1. **Anarchy length.**
   - CFC-FAQ: C3C = 2 + rand(1..4) turns (non-Religious), Religious = 2 (C3C) / 1 (vanilla/PTW).
   - CFC-GOV: "2 to 8 turns, dependent on the size of your civilization / map size".
   - Soren relay (vanilla, 2003): rand(1..5) + 0..3 by number of cities.
   - Forum reports of 8 and 9 turns in C3C, and a forum guess of "1-4 by size, and 1-5 random" (https://forums.civfanatics.com/threads/number-of-turns-for-revolution-calculation.108456/).
   - PEDIA only says Religious civs avoid "prolonged" anarchy.
2. **Religious anarchy in vanilla.** The GameFAQs guide says "no period of anarchy between governments". CFC-FAQ and the forums say 1 turn.
3. **Despotism tile penalty wording.**
   - PEDIA: a tile producing more than 2 of food, shields **or commerce** "produces one less" (so 3 becomes 2, 4 becomes 3).
   - EDHELP: "no tile may generate more than 2 food and shields" (reads like a hard cap, and omits commerce).
   - PEDIA and CFC-GOV agree on "1 less" and include commerce.
4. **Trade bonus scope.**
   - EDHELP: "each tile generates an additional +1 to commerce".
   - CFC-GOV: "Adds 1 commerce to any tile already producing at least 1".
5. **Gold-rush rate.**
   - CFC-GOV and BIQ: 4 gold per shield.
   - Forum (petermarkab): "Multiply by 8 if no shields have been contributed" (doubling).
   - An older forum recollection (https://forums.civfanatics.com/threads/anyone-know-the-formulae-for-rush-building.18881, seen only as a search snippet) claims about 2 gold per shield for improvements and higher for units and wonders.
6. **Pop-rush rate.** Forum posts disagree: "1 pop point per 30 shields" vs "1 citzen per 20 shields". BIQ/EDHELP/README say 20 (C3C and 1.21f+).
7. **Communism support.** 6/6/6 (C3C BIQ and PEDIA) vs 2/4/8 (PTW/vanilla BIQ and the vanilla guide). These are version differences, not errors.
8. **Republic support.** 1/3/4 at 2 gold (C3C) vs 0/0/0 at 1 gold (PTW/vanilla).
9. **Metropolis threshold.** CFC-GOV says "Metro: 12+". BIQ RULE (max level-2 size 12) and the vanilla guide ("metropolis (size 13+)") say 13+.

## UNVERIFIED / open questions

1. The exact C3C anarchy formula (random range, and how civ size or city count adds turns). Needs a code-level source or a controlled test.
2. Gold hurry cost structure beyond "4 gold/shield, doubled from an empty box": unit vs improvement vs wonder multipliers, and any non-linear term. My web search budget ran out before this was resolved.
3. Pop-rush limits: the "max half the citizens" claim, the "double citizens from an empty box" claim, minimum city size, and whether foreign citizens are whipped first under Communism.
4. Which units count against support in C3C: workers, settlers, explorers? Do they consume free slots? PEDIA says "military unit". The vanilla guide says workers and settlers count.
5. Whether Feudalism's WW uses the same table as Republic (both BIQ level 1). CFC-WW predates Feudalism and Fascism.
6. Whether CFC-WW's numbers (thresholds 30/60/90/120, 1/20 decay, +16/17 for city capture) hold in C3C 1.22. The article does not state a version.
7. Direction of the GOVT-vs-GOVT matrices: is the row the initiator's or the target's government? CFC-ESP's prose does not say.
8. "Fascism cannot be bribed": no supporting data found (CanBribe is uninitialized for every pair in C3C).
9. "Feudalism pikemen support" and "Tribalism veteran chance": not stock C3C (no data in PEDIA, BIQ or EDHELP). Probably from mods.
10. Whether favourite-government AIs really "immediately" revolt when the tech arrives. EDHELP says so; not observed. Also how the AI decides when it has no favourite.
11. Whether Golden Age or WLTKD removes the Despotism tile penalty. No source found; WLTKD is documented only as reducing waste.
12. Anarchy upkeep: PEDIA/CFC-GOV say no building maintenance and no unit upkeep. BIQ Anarchy `FreeUnits=-1` with `UnitCost=1`; the reading "-1 = unlimited free units" is inferred.
13. Does re-revolting (choosing a different government during anarchy) re-roll the length? It is blocked by "#ALREADYREVOLTING" for a new revolution, but whether the target government can be changed is unknown.
