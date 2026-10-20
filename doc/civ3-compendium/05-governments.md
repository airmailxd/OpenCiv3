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
| **CFC-GOV** | https://civfanatics.com/civ3/civilopedia/governments/ ("This article covers Civilization 3 Complete V1.22."). It is the Info Center copy of Theov's forum guide "Governments: Civilization 3 Complete V1.22" (https://forums.civfanatics.com/threads/governments-civilization-3-complete-v1-22.276379/, 26 May 2008). The text is the same, including "2 to 8 turns" and the "4×2 + 3×4 = 20" example. |
| **JS-TABLE** | "Civilization III: Conquests Government Breakdown" by Jesse Smith ("Firaxis Games, Producer, C3C"), posted by Tavis (Firaxis), 2 Nov 2003: https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/ |
| **BUGLIST** | Bamspeedy, "Compiled list of bugs and non-bugs", 27 Nov 2003 (C3C 1.00 era): https://forums.civfanatics.com/threads/compiled-list-of-bugs-and-non-bugs-not-a-dicussion-thread.70063/ |
| **BIQ-PRTO** | `conquests.biq` PRTO section, `RequireSupport` field (QueryCiv3 `Prto.cs`), dumped with QueryCiv3 for this pass. |
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

- Firaxis corroboration. JS-TABLE lists the same values for the seven non-Anarchy governments: cost per unit, T/C/M support, worker rate, draft limit, MP limit, corruption, hurry and WW. For example, "Feudalism | 3 | 5/2/1 | 2 | 2 | 3 | Prob. | Force | Low | Req. Maint" and "Fascism | 1 | 4/7/10 | 4 | 2 | 4 | Nuis. | Force | None | Xeno/Resettle". Its legend says: "Req. Maint/Maint = Must pay city improvement maintenance".
- Trade bonus. Tavis (Firaxis), same thread, page 1: "Democracy and Republic are the only ones that get the Trade (Commerce) Bonus in the Epic Game".
- BIQ `RequiresMaintenance` is 1 for every government except Anarchy (0). `TilePenalty` is 1 for Anarchy and Despotism only. `TradeBonus` is 1 for Republic and Democracy only.

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
- Unit upkeep is zero. PEDIA `GCON_Unit_Support` lists Anarchy at "0 0 0" free units, but the forum figures put the per-unit cost at zero as well:
  - MAS (https://forums.civfanatics.com/threads/allowed-units.191722, 8 Nov 2006): "Anarchy in all versions: 0/0/0 | 0 gpt".
  - Lanzelot (thread 276379 page 2, Jan 2026): the unit cost is "0 for Anarchy, 2 for Republic, 3 for Feudalism, 1 for pretty much any other government".
  - This fits CFC-GOV's "No maintenance costs or unit upkeep needed" and settles the BIQ reading of `FreeUnits = -1` as "no upkeep".
- Specialists still produce. Grille (https://forums.civfanatics.com/threads/anarchy-gamble.62465/, 26 Aug 2003, vanilla): "Like any other specialist, a scientist does not refuse to work during anarchy." Also: "During anarchy, some gpt income may be achieved by hiring some taxmen" (28 Aug 2003). Grey Fox (same thread, 27 Nov 2003, C3C): "I researched steam power through my 9 turn anarchy".
- Forum claim only (Chamnix, https://forums.civfanatics.com/threads/surviving-anarchy.308243, 28 Jan 2009): "He is in anarchy - no MP, no luxury slider, no war weariness." No MP and no WW match the BIQ. The luxury slider claim is not checked; Anarchy's BIQ rate cap is 10.
- C3C 1.00 bug (BUGLIST, "Moderate bugs"): rioting cities did not need food, which "makes it possible to do alot of research during anarchy by turning many citizens into scientists". It is not stated whether a later patch fixed this.

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
- EDHELP *Flags*, "Standard tile penalty": "Under the selected government, no tile may generate more than 2 food and shields." This wording is loose. The rule is -1 on any yield above 2, and it includes commerce:
  - PEDIA `GOVT_Despotism`: "produces one less".
  - CFC-GOV: "Any square which produces more than 2 food, commerce, or shields produces 1 less."
  - CFC FAQ (https://civfanatics.com/civ3/faq/, covers 1.29f/1.27f/1.22): "all tiles that produce 3 or more food/shields/commerce will produce one less".
  - Louis XXIV (https://forums.civfanatics.com/threads/what-is-standard-tile-penalty.108487/, 28 Dec 2004, PTW): "A tile that produces 4 food would only produce 3." He adds that the help file "appears to be slightly inaccurate for the tile penalty".
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
- **Feudalism pays building maintenance** in the shipped game.
  - BIQ: `RequiresMaintenance=1`. JS-TABLE: "Req. Maint".
  - Tavis (Firaxis), thread 67419, 2 Nov 2003: "No maintenance with Feudalism had a very negative side effect - the AI wouldn't switch from it!!". So free maintenance was a pre-release design that was dropped.
  - player1 fanatic, same thread, page 2: "all governments need to maintain buildings (since ability is removed from Feudalism)".
  - Contrary reports: BUGLIST lists "German version has Fuedalism still having Maintanence free improvements" as an *unconfirmed* bug, from second-hand hearsay. In thread 276379, Mursi lives (Feb 2010) says he paid none in a 100k game, and Spoonwood and CKS reply that maintenance is still paid. Treat free maintenance as not a stock rule.
- **Feudalism war weariness is the Republic level.** BIQ gives both WW level 1 ("Low"); the field is a three-value enum (None/Low/High). Supporting statements:
  - Arathorn, C3C changes list (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/, 10 Nov 2004): "low war weariness (like Republic)".
  - gunkulator (https://forums.civfanatics.com/threads/monarchy-or-feudalism.114078/, 14 Mar 2005): "WW for Feudalism and Republic are the same." This is from play experience, not a stated test.
- **"Feudalism pikemen support" is not in stock C3C.** PEDIA, BIQ GOVT and EDHELP contain no unit-type-specific support rule. UNVERIFIED as a stock rule; probably a mod.

### Republic

| Field | Value |
|---|---|
| Corruption/waste | Nuisance |
| Trade bonus | Yes: +1 commerce on tiles already producing at least 1 |
| Workers | 100% |
| Hurry | Paid (gold) |
| Draft / MP | 1 / **0** (no martial law) |
| Unit support | 1/3/4, **2 gold** per extra unit (C3C) |
| War weariness | Low |

- PEDIA `GOVT_Republic`: "your government is affected by war weariness, which can cause significant civil disorder problems in times of war, especially if you are the aggressor. Maintenance cost per unit per turn is 2 gold instead of just 1."
- CFC-GOV: "Adds 1 commerce to any tile already producing at least 1 = the commerce bonus." CFC FAQ (https://civfanatics.com/civ3/faq/): "for every tile that produces one commerce, you'll get an extra". EDHELP's "each tile generates an additional +1 to commerce" omits the >= 1 condition. No source says zero-commerce tiles get the bonus.
- Support applies to all units, not only military ones (see 5.3). SebT27 (thread 276379 page 2, Jan 2026) says the guide's "Military over the cap cost 2 gpt" distinction "must be wrong (or outdated...)". Theov, the guide's author, agrees: "true, it's all units - except the ones that don't require upkeep".

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
- C3C 1.00 had a bug where the SPHQ kept working after leaving Communism. BUGLIST, Major bug: "Secret Police HQ, still works even when you are no longer a commie." The 1.20 README line above fixed it.
- Corruption: alexman (Firaxis software engineer), https://codehappy.net/apolyton/threads/55285-1.htm, 10 Jul 2002, vanilla: "Communism does not incur corruption due to distance, but it does incur corruption due to number of cities."
- CFC-GOV: "you can whip foreign people in newly conquered cities to prevent revolts". UNVERIFIED as a distinct rule. The only statement found on which citizen a whip removes says the opposite, with the author unsure. Ision (https://forums.civfanatics.com/threads/the-benefits-of-whipping-citizens.76152/, 13 Mar 2004): "natural born citizens will always die first", "I am not completely 100% sure on this one."

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
- **"Fascism can't be bribed / is immune to propaganda" is not a stock rule.**
  - Fascism's `ImmuneTo` is -1 (none).
  - The per-government "Cannot Bribe" setting is the `CanBribe` field of the GOVT-vs-GOVT matrix. In conquests.biq it holds the uninitialized value 0xCDCDCDCD for every pair, so it does not distinguish Fascism.
  - The Fascism and Feudalism matrix entries were simply never filled in. BUGLIST, Moderate bug: "The resistance + propoganda modifiers for any and all governments vs. Fascism and Fuedalism have not been set". It adds that the modifier against Fascism "is always 0". This matches the zero Fasc/Feud columns in 5.11.
  - No source gives Fascism special protection. It does have the Veteran spy bonus.
- Tavis (Firaxis), thread 67419 page 2, 2 Nov 2003: "To clarify - Fascism DOES require maintenance".
- Roundman, thread 67419 (2 Nov 2003), clears up a rumour: "Fascism was forced labor for rush jobs, not for all production."

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
- LordKestrel (https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-3, 18 Jun 2004), on the WW revolt: "Your civ descends into Anarchy, just like if you had clicked the Revolt button".
- Worker rate 150% affects only Worker-type terraforming, not city shields. Keyser Soze 0 (https://codehappy.net/apolyton/threads/32558-1.htm, 5 Nov 2001): "the worker efficiency is referring to tile improvement workers - they work faster."

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
- **Which units count: every unit whose type has `RequireSupport` set. In stock C3C that is every unit except the leader "King" units, and except foreign (captured) units.**
  - BIQ-PRTO: `RequireSupport=1` for 110 of the 141 C3C unit records, including Settler, Worker, Scout, Explorer, Leader, Army and the Princess. The other 31 records are the civ-specific King units (Lincoln ... Theodora, flag `King`), and all have `RequireSupport=0`. King units only appear in the Regicide-style special game modes, which are out of scope.
  - EDHELP *Requires Support (Unit Statistics)*: "If checked, players will have to pay a support cost (gold) for the unit."
  - Workers and settlers count against the free slots:
    - Speedo (https://forums.civfanatics.com/threads/how-does-unit-support-work.76197, 22 Jan 2004): "remember workers and settlers count, though".
    - The vanilla-era guide says the same: "This count includes Workers, and also includes any Settlers that haven't built or joined cities yet."
    - justanick (thread 276379 page 2, Jan 2026): "All normal units including workers and settler must pay 1 gtp in a democracy because there is no free unit support."
  - Foreign units are free:
    - justanick, same post: "Foreign unit are free of unit support."
    - Lanzelot, same page: "Unit support has to be paid for every unit except for foreign units."
    - Captured workers keep their nationality (PEDIA `GCON_Nationality`: "Captured workers and settlers retain their original nationality").
  - PEDIA's "each military unit" (`GCON_Unit_Support`) is therefore loose wording.
  - Not checked: whether units inside an Army are charged separately from the Army unit.
- PTW/vanilla PEDIA (`civ3PTW/Text/Civilopedia.txt`, `Text/Civilopedia.txt`, `GCON_Unit_Support`) lists "Communism 2 4 8" and "Republic 0 0 0", which matches BIQ-PTW. MAS (thread 191722, 8 Nov 2006): "Republic in vanilla/Civ3: 0/0/0 | 1 gpt" and "Republic in Conquest/C3C: 1/3/4 | 2 gpt".

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
  - Half-the-city cap and empty-box doubling. Several forum posts agree, but none is a Firaxis source:
    - eg577 (https://forums.civfanatics.com/threads/hurry-production.95782/): "you cannot "whip" more than half of your citizens at once" and "twice that many citizens if there are no shields contributed yet".
    - DBear (https://forums.civfanatics.com/threads/how-is-pop-rush-done-now.128518/, C3C 1.22): "you cannot pop rush more than half your population at the same time".
    - Dominae (https://codehappy.net/apolyton/threads/73546-1.htm, 10 Jan 2003, PTW era): "in any given turn, you can only sacrifice up to half your population to poprush".
    - Bluefrog (same thread): "If you have no shields in the build box, the cost of rushing is doubled. This is true for pop- and cash-rushing."
    - Section 2 (city economy) §4 has the full hurry sourcing.
  - No hurrying in disorder or resistance. SCRIPT `#HURRY_CIVIL_DISORDER`: "We cannot hurry production while our city is in civil disorder." SCRIPT `#HURRY_RESISTANCE`: "We cannot hurry production while our city is in resistance."
  - Whip unhappiness follows the population. Catt (https://codehappy.net/apolyton/threads/63161-1.htm, 1 Oct 2002, vanilla) reports this as from Firaxis programmers, second-hand: "unhappiness moves to nearest city if a city is abandoned". From his own play-testing: it "travels with a settler created in disbanding a city used to pop-rush too often". Zero, in the same thread, reported a test where "it seemed to stick to only cities". UNVERIFIED.
  - Wonders cannot be pop-rushed. CFC-FAQ: "you can't rush a wonder by sacrificing population (Despotism, Communism, Fascism)".
- Gold rush rate. BIQ RULE `ShieldValueInGold=4`. EDHELP *Shield Value in Gold*: "how many gold pieces it costs to buy each shield when hurrying production of a city improvement. Default is 4 gold."
  - Empty production box doubles the cost. Forum (petermarkab, same thread): "Multiply by 8 if no shields have been contributed."
  - Unit vs improvement vs wonder multipliers are UNVERIFIED (see open questions).
- Leaders: EditorSupplement.txt: "NOTE: Military leaders can not rush." Great (scientific) leaders can (PEDIA `GCON_Hurry_Production`).
- Draft:
  - PEDIA `GCON_Draft`: "When a civilization develops Nationalism its citizens can be drafted ... A city in a Democracy or Republic can draft [one] citizen per turn ... Communism, Despotism, Fascism, Feudalism, or Monarchy can draft [two] ... You may only draft citizens from a city with a population of 7 or higher."
  - The limit is **per city**, not per civ. PEDIA `GCON_Draft`: "Each city can draft some of its population each turn, depending upon the government currently in effect." EDHELP *Draft Limit*: "how many units can be drafted (converting population points into a unit) by the selected government type per turn."
  - Draft anger lasts 20 turns: BIQ RULE `TurnPenaltyForEachDraftedCitizen=20`. Ginger_Ale (https://forums.civfanatics.com/threads/question-about-drafting-in-civ3.107429/, 16 Dec 2004): "it makes 1 citizen per unit drafted unhappy for 20 turns."
  - Forum claims of a Communism draft rate of 3 (eldar and Ginger_Ale in thread 107429; Inverse Icarus in https://codehappy.net/apolyton/threads/53252-1.htm, 2002) are wrong for the target patches. BIQ says 2, and README 1.17f says "Fixed Communism Civilopedia entry to reflect the correct draft rate of 2." Franses (https://codehappy.net/apolyton/threads/55285-1.htm, 11 Jul 2002) checked it in the editor: "Communism indeed has a draft rate of 2 now."
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
  - **Vanilla (Firaxis, first-hand).** Soren Johnson, "Did Firaxis change the length of Anarchy in 1.17f?" (https://codehappy.net/apolyton/threads/42739-1.htm):
    - 22 Feb 2002, 21:41: "oh, you wanted to know if there is a random factor. well, there is. it's between 1 and 5 turns. plus 0 - 3 extra turns depending on number of cities".
    - Same thread, 22 Feb 2002, 13:01: "anarchy length is increased by number of cities".
    - 21 Feb 2002: "If you play a religious civ, anarchy always takes 1 turn."
    - So vanilla non-Religious anarchy is 1-8 turns. Padma's 2003 relay (https://forums.civfanatics.com/threads/going-to-monarchy.51241/) repeats this ("always between 1 - 5 turns on a random basis" plus "an additional 0-3 turns depending on number of cities").
  - **C3C adds one turn.**
    - SirPleb, C3C changes list (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/, 12 Nov 2004): "When revolting to change the government the period of anarchy is one turn longer in C3C."
    - CFC-FAQ (C3C-aware, https://civfanatics.com/civ3/faq/): "For non-Religious civilizations, the formula is: 1 (2 for Conquests) + random number between 1-4" "+ number between 0-3 depending on size of your empire". For Religious civs: "anarchy only lasts 1 turn in Vanilla and Play the World, and 2 turns in Conquests."
  - **C3C observed range.** justanick (https://forums.civfanatics.com/threads/duration-of-anarchy.683384/, 19 Apr 2023):
    - "the anarchy duration of non-religious tribes in C3C can take values between 3 und 9 turns."
    - "Small empires incur 3 to 7 turns of anarchy, mid sized empires 4 to 8 turns and larger empires 5 to 9 turns."
    - He saw five different values at one empire size (5-9, Napoleon scenario, Emperor). That fits a 1-5 random part rather than the FAQ's 1-4.
    - Later (same thread, May 2026): "very small empires incur 2 to 6 turns of anarchy."
    - None of his posts give the city-count thresholds for the size bands.
  - **Synthesis (C3C, non-Religious).** Soren's vanilla formula plus SirPleb's extra turn is 1 + rand(1..5) + (0..3 by city count), which gives 2-9. This matches justanick's bands and the C3C reports of 9 turns:
    - vmxa (https://forums.civfanatics.com/threads/how-to-define-time-of-anarcy.440076, 16 Sep 2011): "Yes in C3C you can get 9 turns and I have done it. It was thought that 8 was the max, but not in C3C."
    - Turner (https://forums.civfanatics.com/threads/number-of-turns-for-revolution-calculation.108456/): "8 and 9 turn anarchy was seen."
    - The city-count thresholds and the exact random range (FAQ 1-4 vs Soren 1-5) are still unconfirmed (see open questions).
  - Older or looser statements:
    - CFC-GOV: "It lasts from 2 to 8 turns, dependent on the size of your civilization / map size." This is too low a maximum for C3C. justanick (thread 276379 page 2): "Actually 2 to 9 turns."
    - Lanzelot (thread 440076) adds difficulty as a factor for the human: non-Religious anarchy runs "from 2 to 9", with longer lengths more likely as map size, town count and difficulty increase. This is an opinion; nothing else supports a difficulty term for the human player.
  - **Religious, C3C: 2 turns, which looks like 1 if you revolt from the new-tech popup.**
    - Turner (https://forums.civfanatics.com/threads/quick-answers.100914/page-5, 9 Oct 2004, C3C): "it's 1 turn in C3C when you revolt as soon as it asks you too." and "It's two when you do it during your turn."
    - Qitai (thread 62465, 27 Aug 2003): if you accept the popup offered on learning the tech, "the revolt is considered being done LAST turn."
    - justanick (thread 683384): "Religious tribes always incur 2 turns of anarchy". Revolting right after a tech, "the old turn does still count towards the total turns of the anarchy."
    - BUGLIST, under "NOT BUGS": "Religious civs now have up to 2 turns of anarchy."
    - Lanzelot (thread 683384) reports, as hearsay, that the change was made to stop an interturn revolt exploit.
  - **Re-revolting.** CKS (https://forums.civfanatics.com/threads/anarchy-question.452205, 24 Jan 2012): "In vanilla/PTW, re-revolting changes the length of the anarchy - possibly shorter, possibly longer." Also: "In Conquests you get the same length of anarchy, though it looks a turn shorter". Forum only.
  - Golden Age during anarchy: a single anecdote says it halved the anarchy. garyg (https://forums.civfanatics.com/threads/you-say-you-want-a-revolution.116180, 6 Apr 2005): "once accidently triggered my GA on my first turn of Anarchy and it cut the time in half." UNVERIFIED.
- Religious trait. PEDIA `GCON_Strengths`: "Religious civilizations do not experience prolonged periods of anarchy during revolutions". No wonder affects anarchy length in C3C: the Statue of Zeus entry (`BLDG_Zeus`) only says it "produces an Ancient Cavalry every 5 turns". The "Statue of Zeus/Liberty shortens anarchy" idea is Civ2.
- AI cap. BIQ DIFF `MaxGovernmentTransitionTime`: 0 (no cap) for Chieftain, Warlord and Regent; Monarch 4, Emperor 3, Demigod 2, Deity 2, Sid 1. EDHELP: "the maximum number of turns the AI must remain in the transition government type ... The default value of zero means there is no maximum".
  - justanick (thread 683384) gives slightly different observed caps: "the maximum anarchy duration is zero at Sid, one at Deity, two at Demigod, three at Emperor and four at King". (He means Monarch; Civ3 has no "King" level.) He adds: "At regent and below the anarchy duration rules for AI are the same as for the human player." He also says the Religious 2 turns overrides the AI cap. His Deity and Sid values are one lower than the BIQ (see Disputed).
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
- Tavis (Firaxis), thread 67419 page 2, 2 Nov 2003, at C3C release: "only the Hittites have Fascism as their Preferred Gov't and France & Portugal as their Shunned". The 1.22 BIQ also has England shunning Fascism. Either the data changed after release, or Tavis left England out. The BIQ value stands for 1.22.
- Forum claims that the favourite and shunned settings drive AI *attitude* rather than (or as well as) AI government choice. Neither is tested, and both conflict with EDHELP:
  - wilbill (https://forums.civfanatics.com/threads/some-thoughts-on-despotism.69616/, 23 Nov 2003): "favored and shunned governments only effect the AI's attitude toward other civs" and the AI "will always go to the 'best' govenment available".
  - wilbill (thread 61628 page 2, 13 Jan 2004): "Favorite/Shunned govs have nothing to do with WW - only with AI attitude toward you."

## 5.9 War weariness

- Who suffers. PEDIA `GCON_War_Weariness`: "citizens living under a Republic, Feudalism, or Democracy become [war weary]". BIQ WW levels: Republic 1, Feudalism 1, Democracy 2, all others 0.
- PEDIA, same entry: "When [you] initiate the conflict your people will tolerate it for a while ... When someone else declares the war, especially a traditional enemy, your people are more forgiving. Carrying on a defensive war, that is keeping your troops at home, is more tolerable ... the war weariness remains until the war stops."
- Mitigation: Police Station ("combats war weariness", PEDIA `BLDG_Police_Station`) and Universal Suffrage ("Reduces war weariness in all cities", PEDIA `BLDG_Universal_Suffrage`).
- **The WW factor accumulates under every government; only the effect is government-specific.** Soren Johnson (Firaxis), "War weariness carryover" (https://codehappy.net/apolyton/threads/55847-2.htm, post #33, 11 Sep 2002, vanilla 1.29f era):
  - "there is a war weariness factor that is maintained the same way throughout your entire game".
  - "The only time this factor affects your citizens is if you are at war while in a demo/rep."
  - On peace: signing a peace treaty does lessen WW, but "the factor is still there, hidden away. It decays gradually behind the scenes."
  - So switching from Monarchy or Communism to Republic or Democracy in mid-war brings the stored factor into effect. Oystein (thread 61628 page 4, 7 Feb 2005) agrees: "The calculation of wwp is independant of government."
- **Version of CFC-WW.** The article is Oystein's forum research (https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/, Oct 2003). It was done on vanilla: "I have not PTW". It was done by save-file inspection: "I have figured out where wwp is stored in the savefile".
  - alexman briefly claimed a C3C change and then retracted it ("My bad. Sorry.", page 2, 29 Nov 2003).
  - Oystein retested on C3C (page 2, 20 Feb 2004): "Everything seems to be the same." Treat the CFC-WW numbers as valid for C3C.
- Formula, CFC-WW (community research; vanilla, retested on C3C as above):
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
  - Feudalism (Low) uses the Republic (Low) table. The BIQ level is the same enum value, and see the Feudalism notes in 5.2.
- Clarifications from the same thread (Oystein unless noted):
  - Threshold: "you need 31 wwp to get ww. 30 wwp gives no effect." (page 3, 19 Apr 2004)
  - Rounding: "The number from ww is rounded down." (page 3, 11 Mar 2004)
  - Cap: "The total number of unhappy citizen from ww can not exceed number of citizen." (page 2, 17 Oct 2003)
  - Police Station: "If you have 125% or more ww, the police station would not have any effect since you are above the limit anyway." (same post)
  - Per-civ storage: "the system stores one number for each of the other civs." and "20 turns of peace does NOT mean that ww is gone." (page 3, 23 Apr 2004)
  - Combat: "If you attack and kill an AI, none gets wwp. If the AI wins, both get wwp." (page 2, 20 Feb 2004)
  - Flak: "Bombers shot down by flaks does not add to wwp" (page 3)
  - Phoney war: Grille, page 4: "A phoney war doesn't add ww points."
  - Culture flips: Lord Emsworth (page 4, 11 Mar 2009, tested): "No WW from flipped cities." Recapturing a flipped city does add WW (page 5).
  - War happiness: negative points carry over too. Oystein, page 4: "You will get back the WH."
  - Overflow (binyo66, page 3, read with a save tool): "the highest WW is 999. After that it resets to 0". Single report.
- AI turn-order bug (C3C 1.00). BUGLIST, Minor bug, quoting a user: "When an AI player attacks an earlier AI both gets war weariness, when he attacks a later AI nothing happens." Oystein (thread 61628 page 1, 13 Oct 2003) describes the same thing: "the first player gets penalties as a human, the second gets penlties as an AI".

## 5.10 Senate / forced ceasefire

- **Civ3 has no Senate.** No Senate, ceasefire-forcing or forced-peace rule appears in the PEDIA, SCRIPT (advisor and popup texts), labels.txt, diplomacy.txt, BIQ GOVT fields or EDHELP. The only "Senate" mentions in Civilopedia.txt are historical prose (e.g. `DESC_GOVT_Republic`).
- The Civ2 Senate is the origin of the idea. In Civ3 the equivalent pressure is war weariness, and the Democracy WW revolt.
- Status: verified by absence in the game files. One forum statement says it outright. Kramsib (https://codehappy.net/apolyton/threads/84801-1.htm, 1 May 2003), on Republic: "there is no senate and no penalisation for having your units out of your cities". So there is also no Civ2-style unhappiness from units abroad. The only war-related unhappiness is war weariness.

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
  - Precondition: PEDIA `GCON_Assimilation`: "When a foreign national has been a part of a minority population longer than not, he or she assimilates and becomes a member of the majority nationality."
  - Forum readings of that precondition, all second-hand or anecdotal, PTW era:
    - Mad Bomber (https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/81314-nationality-assimilation, 27 Nov 2002): assimilation starts once a city "has belonged to your civ more than the civ that possessed the City previously".
    - Jaybe (https://codehappy.net/apolyton/threads/57182-1.htm, 27 Jul 2002): "they convert when they have been in your hands LONGER than their own civ."
    - Traelin (thread 81314) adds that this only applies while the original owner is still in the game.
  - Warpstorm (thread 81314) gives rates that match the BIQ: "Communism and Democracy have the best chance of assimilation with a 4% chance a turn (per citizen I think)", Republic and Monarchy 2%, the rest 1%.
  - War: PEDIA `GCON_Nationality`: "When at war, foreign nationals become unhappy more quickly than others and are more likely to be affected by hostile propaganda from their mother country."

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

## 5.14 Capital loss and civil war

- **Civ3 has no civil war (empire split on capital capture).**
  - No civil-war rule, popup or advisor text exists in the C3C game text. `Conquests/Text/script.txt` and `Civilopedia.txt` mention "civil war" only in historical prose.
  - Every forum source found for the split rules is about Civ1 or Civ2:
    - https://forums.civfanatics.com/threads/splitting-civilizations.6857 (Civ2 Strategy forum).
    - https://forums.civfanatics.com/threads/civil-war.131672 (Civ2 forum: "A civil war occurs when you capture an AI's capital that is ranked higher than you are in the power graph").
    - https://forums.civfanatics.com/threads/empire-split-by-fall-of-the-capital-when-playing-with-less-than-7-civs.674921/. Civ1 forum; it quotes *Rome on 640k a Day*, a Civ1 strategy guide.
  - Civ3 players say it is missing:
    - RegentMan (https://forums.civfanatics.com/threads/civil-war.84798/, 13 Apr 2004): "Loved them in Civ II. Missed them in Civ III."
    - Early Civ3 reports (https://codehappy.net/apolyton/threads/34719-1.htm, 19 Nov 2001) describe no split. ElitePersian: "it doesnt do anymore than the actual loss of a city".
  - Status: verified by absence, plus forum agreement.
- **The palace moves to another city for free when the capital is lost.**
  - Cavalier_13 (thread 34719): "you see the capital shift to the new city at no cost to the CPU AI." D4everman: "in the same turn it was moved to another city". Akaoz adds that it destroys "the civs spaceship if any". README 1.17f: "Fixed bug which did not destroy an enemy spaceship if the enemy capital was razed." and "Fixed bug which allowed civs with no capitals."
  - Which city gets it, after razing or abandoning: the "Free Palace Jump" article (https://civfanatics.com/civ3/strategy/game-mechanics/free-palace-jump/, DaveMcW, 2003, vanilla/PTW testing; forum thread https://forums.civfanatics.com/threads/free-palace-jump.47304/). Each city scores:
    - 3 points per national citizen and 1 per foreign citizen;
    - 1 / 2 / 3 per neighbouring town / city / metropolis "within 8 tiles walking distance from the target city (a 17×17 square)", not counting the old capital;
    - 1 per military unit.
    - "If there is a tie for the most points, the palace jumps to the first city in the database, which is _usually_ the oldest city."
  - Not confirmed: whether the same scoring applies when the capital is *captured*, and whether it holds in C3C. justanick (https://forums.civfanatics.com/threads/looking-for-the-formula-on-how-the-palace-gets-relocated.703374/, May 2026) quotes the same formula and says it was found "after weeks of testing". Charon117's test saves in that thread showed a result that did not fit, and the thread does not settle it.
- Losing the capital does not by itself cause anarchy or a government change. No source reports it, and the C3C texts have no such rule.

---

## Disputed

1. **Anarchy length (C3C, non-Religious).** It is mostly settled as 1 + rand(1..5) + (0..3 by city count) = 2-9 (see 5.7). Two points are still disputed:
   - The random part: Soren (vanilla) says "between 1 and 5". The CFC-FAQ says "random number between 1-4". justanick saw five values at one empire size, which supports 1-5.
   - The minimum: the CFC-FAQ formula gives 3. justanick first wrote "between 3 und 9", then "very small empires incur 2 to 6 turns".
   - Lanzelot also suggests a difficulty term for the human; no other source supports it.
2. **Religious anarchy in vanilla.**
   - Soren Johnson (codehappy 42739, 21 Feb 2002): "If you play a religious civ, anarchy always takes 1 turn." The CFC-FAQ agrees.
   - The GameFAQs guide's "no period of anarchy between governments" is wrong for 1.17f+. It may describe the observed effect of revolting from the new-tech popup (see 5.7, Turner/Qitai).
3. **AI anarchy cap at Deity and Sid.** BIQ `MaxGovernmentTransitionTime`: Deity 2, Sid 1. justanick's observations (thread 683384): Deity 1, Sid 0. Monarch 4, Emperor 3 and Demigod 2 agree. The engine may use the value as an exclusive bound, or justanick may have miscounted. Not resolved.
4. **Gold-rush rate.** 4 gold per shield, doubled from an empty box, is well sourced (section 2 §4). The older recollection of "about 2 gold per shield for improvements and higher for units and wonders" (https://forums.civfanatics.com/threads/anyone-know-the-formulae-for-rush-building.18881) is in the **Civ2** General Discussions forum. It is Civ2 data and should be ignored for Civ3. The "ms×2 + ms²/20" unit formula on CFC's "Section D" FAQ page (https://civfanatics.com/?p=128) is from the **Civ1** FAQ.
5. **Communism support.** 6/6/6 (C3C BIQ and PEDIA) vs 2/4/8 (PTW/vanilla BIQ and PEDIA, and the vanilla guide). These are version differences, not errors.
6. **Republic support.** 1/3/4 at 2 gold (C3C) vs 0/0/0 at 1 gold (PTW/vanilla). Version difference, confirmed by the PTW/vanilla PEDIA tables and MAS (thread 191722).
7. **Metropolis threshold.** CFC-GOV says "Metro: 12+". BIQ RULE (max level-2 size 12) and the vanilla guide ("metropolis (size 13+)") say 13+. playshogi (thread 67419 page 4) says "1-6 town", and 13+ for metros.
8. **Favourite/shunned Fascism.** Tavis's release-time list (Hittites favour it; France and Portugal shun it) vs the 1.22 BIQ, which also has England shunning it.

Resolved in this pass and moved to the main text: Despotism tile penalty wording (5.2 Despotism), trade bonus scope (5.2 Republic), pop-rush rate (20 per citizen, 5.5), Feudalism maintenance (5.2 Feudalism).

## UNVERIFIED / open questions

1. The C3C anarchy city-count thresholds: how many cities give +1, +2 or +3 turns. Is it pure city count (Soren: "number of cities") or does map size enter (CFC-GOV, vmxa)? Needs a controlled test.
2. Gold hurry cost by item type: unit vs improvement vs wonder multipliers, and any non-linear term. Still open; see section 2 open question 4. The Civ2 and Civ1 formulas once cited for it are discarded (Disputed 4).
3. Pop-rush: the half-the-city cap and empty-box doubling rest on consistent forum statements (5.5), not on a Firaxis source. Still open: whether foreign citizens are whipped first. CFC-GOV implies yes; Ision guesses "natural born citizens will always die first".
4. Whether units inside an Army pay support individually, and whether Leaders really cost upkeep in play (BIQ `RequireSupport=1`).
5. Direction of the GOVT-vs-GOVT matrices: is the row the initiator's government or the target's? CFC-ESP gives no explanation. The pattern suggests rows are the spy's government (the Democracy row has the largest bonuses), but this is an inference.
6. "Feudalism pikemen support" and "Tribalism veteran chance": not stock C3C (no data in PEDIA, BIQ or EDHELP). Probably from mods.
7. Whether favourite-government AIs really "immediately" revolt when the tech arrives, as EDHELP says, and how the AI chooses when it has no favourite. Forum posts (wilbill) claim the setting only affects attitude; untested.
8. Whether a Golden Age or WLTKD removes the Despotism tile penalty. No source found. PEDIA `GCON_We_Love_The_King_Day` lists only lower flip chance, less waste and weaker enemy propaganda. Dan Magaha (relayed, codehappy 25003) says WLTKD is "city-specific and can happen repeatedly", separate from the Golden Age.
9. Whether a Golden Age during anarchy shortens it (garyg anecdote, 5.7).
10. Whether the C3C 1.00 bugs listed in BUGLIST were fixed by 1.22: anarchy research through rioting scientists, and AI war weariness depending on turn order. The SPHQ bug was fixed in 1.20 (README).
11. Free Palace Jump scoring when the capital is captured rather than razed or abandoned, and in C3C.

Resolved in this pass:
- Which units count against support (5.3).
- Feudalism uses the Republic WW table (5.9).
- CFC-WW holds for C3C (5.9).
- "Fascism cannot be bribed" is not a rule (5.2 Fascism).
- Anarchy has zero unit upkeep (5.2 Anarchy).
- Re-revolting: vanilla/PTW re-rolls the length, C3C keeps it (5.7; forum only, CKS).
