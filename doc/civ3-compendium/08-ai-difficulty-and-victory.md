# 08 - AI behaviour, difficulty levels, civ traits, scoring and victory

Scope: Civ3 1.29f, PTW 1.27f and C3C 1.22, standard (non-scenario) games. The special Conquests
game modes (Regicide, Mass Regicide, Elimination, Capture the Flag/Princess, Victory Points) are out of
scope.

Each rule gives a source URL and a short quote from that source. Forum posts are labelled with the
poster's name. "Tested" means the poster says they tested it. "BIQ (repo)" means the value is in
`C7/Lua/civ3/ruleset.json` in this repository. `_Console/RegenerateRuleset/readme.md` says that file's
`difficulties` and `civilizations` sections come from Civ3's `conquests.biq`, and that the only change is
that "the highest is renamed from Sid to Creator". Those values are used here only to back up web
sources, never as the only source. Treat them as secondary because the import code could have bugs.

---

## 1. Difficulty levels (BIQ DIFF section)

### 1.1 Table (C3C 1.22 defaults)

| DIFF field | Chieftain | Warlord | Regent | Monarch | Emperor | Demigod | Deity | Sid |
|---|---|---|---|---|---|---|---|---|
| Citizens born content (human) | 4 | 3 | 2 | 2 | 1 | 1 | 1 | 1 |
| Max AI government transition time (turns) | - | - | - | 4 | 3 | 2 | 2 | 1 |
| AI extra defensive units at start | 0 | 0 | 0 | 2 | 4 | 6 | 8 | 12 |
| AI extra offensive units at start | 0 | 0 | 0 | 1 | 2 | 3 | 4 | 6 |
| AI extra start unit 1 (Settler) | 0 | 0 | 0 | 0 | 0 | 1 | 1 | 2 |
| AI extra start unit 2 (Worker) | 0 | 0 | 0 | 0 | 1 | 2 | 2 | 4 |
| AI additional free unit support (per civ) | 0 | 0 | 0 | 4 | 8 | 12 | 16 | 24 |
| AI unit support bonus per city | 0 | 0 | 0 | 1 | 2 | 3 | 4 | 8 |
| Attack bonus vs barbarians (%) (human) | 800 | 400 | 200 | 100 | 50 | 25 | 0 | 0 |
| AI cost factor (human = 10) | 20 | 12 | 10 | 9 | 8 | 7 | 6 | 4 |
| % of optimal cities (human OCN) | 100 | 95 | 90 | 85 | 80 | 70 | 60 | 50 |
| AI-to-AI trade rate | 110 | 120 | 130 | 140 | 150 | 160 | 170 | 200 |
| Citizens quelled by military law | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| Score multiplier | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |

Sources for the table:

- **Vanilla/PTW six-level table (Pyrkaige, 25 Nov 2002).** https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/post-614347
  gives Chieftain..Deity. It agrees with the table above except Deity, which it gives as OCN 70% and
  AI-AI trade 160 (the PTW values). Rows quoted: "AI cost factor (player is 10) | 20 | 12 | 10 | 9 | 8 | 6";
  "Optimal-city % before corruption | 100 | 95 | 90 | 85 | 80 | 70".
- **PTW to C3C Deity change (Pyrkaige, 13 Nov 2003).** https://forums.civfanatics.com/threads/difficulty-levels-ptw-vs-conquests.68642
  reports "In PTW, the Deity AI to AI trade rate is at 160, while in C3C it's at 170." He gives the OCN
  as 70% in PTW and 60% in C3C. He also corrects the editor help: a Sid AI starts with 2 settlers and 4 workers.
- **Same change (TimBentley).** https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/
  says "The AI-AI trade ratio for diety increased from 160 in PTW to 170 in C3C, and the OCN decreased from 70% to 60%."
- **Demigod and Sid (Arathorn), same thread.** "Demi-God AIs have a cost factor of .7"; "3 starting
  offensive units, 6 starting defensive units, 1 bonus settler, 2 bonus workers"; "Sid AIs have a cost
  factor of .4, 6 starting offensive units, 12 starting defensive units"; "human has 0% bonus vs. barbs
  and human has 50% of the normal OCN".
- **Sid (SirPleb, 14 Nov 2003).** https://forums.civfanatics.com/threads/some-comments-after-playing-a-bit.68813
  says "The big one - the AI's 'cost factor' is 4!" and "This means that the AIs need only 4/10th as much
  of anything as the human needs."
- **CFC FAQ.** https://civfanatics.com/civ3/faq/ lists the barbarian bonus as 800/400/200/100/50/25/0/0 and
  the AI anarchy cap as "4 turns on Monarch, 3 on Emperor, and 2 on Demigod". It also says "At Regent, the
  AI and you don't have any advantages nor disadvantages."
- **Score multiplier.** See section 6.
- **BIQ (repo).** `difficulties` in `C7/Lua/civ3/ruleset.json` has every value in the table above.
  Field names: `numberOfCitizensBornContent`, `maxAiGovernmentTransitionTime`,
  `numberOfAIDefensiveStartingUnits`, `numberOfAIOffensiveStartingUnits`, `extraStartUnit1/2`,
  `additionalFreeUnitSupport`, `unitSupportBonusForEachSettlement`, `attackBonusAgainstBarbarians`,
  `aiCostFactor`, `percentageOfOptimalCities`, `aiToAITradeRate`, `militaryLaw`.

### 1.2 What the fields mean

- **AI cost factor.** It scales the AI's cost to build and research (units, improvements, wonders and
  techs), as a fraction of the human's 10. It does not change combat or movement.
  - https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/post-614347 (thread summary):
    the cost factor applies to wonders, improvements and units, but not to combat or movement rates.
  - https://civfanatics.com/civ3/faq/ lists the bonuses above Regent as "extra starting units, faster
    production and research, and lower corruption".
- **Research penalty after patch 1.29f.** For research, the patched game makes the human's techs cost
  more instead of making the AI's cost less. Tested by theNiceOne, 19 Jan 2003, patch 1.29:
  https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/86979-ai-human-trade-revisited
  says "the human player's research cost is divided by the AI's cost factor (as found in the editor)".
  This only applies when the factor is below 1, so from Monarch up. Bamspeedy, in the 37490 thread
  above, also reports that later patches made the human, not the AI, the one adjusted by difficulty.
- **AI-to-AI trade rate.** It sets how much AIs pay each other in tech trades (and maybe other trades).
  theNiceOne, same Apolyton thread: "the AI-AI trade bonus that can be found in the editor does _not_
  affect AI-human trade in any way." Nobody found the exact formula. Nor Me: "I cannot get reasonable
  results out of an AI-AI trade test."
- **% of optimal cities.** This is a percentage applied to the **human's** optimal city number, the
  number of cities before corruption from city count rises. Arathorn (104294): "human has 50% of the normal OCN" at Sid.
- **Citizens born content, quelling and barbarian bonus apply to the human.** The AI uses a fixed
  default level for these, and which level that is is disputed (see Disputed).
  - TheNiceOne (37490 thread) says the AI plays by Regent rules for the content, quelling, barbarian and
    corruption factors. Bamspeedy says the "default AI intelligence" setting controls this.
  - Pyrkaige (68642) says AI civs "play at the Monarch level (or did in PTW, and most likely still do)"
    for four variables.
  - BIQ RULE has a `DefaultDifficultyLevel` field (`QueryCiv3/BiqSections/Rule.cs`).
- **Max AI government transition time** caps the AI's anarchy. CFC FAQ: "on Emperor, the cost factor is
  .8, and the AI gets a reduced anarchy time!"

### 1.3 Other AI advantages ("cheats") beyond DIFF

- **Map knowledge.** The AI knows the map, and where resources will appear, before it should. These are
  community findings and are not confirmed by Firaxis.
  - TheNiceOne, 17 Jul 2002: https://forums.civfanatics.com/threads/the-complete-cheat-guide.27273/post-376681
    lists as proven "The AI knows the map even before exploring." He says he "tested by making a simple
    map" and that the AI "went for future resources spots".
  - Bamspeedy, same thread: "If the AI has seen the map, it does in fact know if a future resource will be there or not."
  - TheNiceOne, https://forums.civfanatics.com/threads/the-ai-dont-cheat.33788/post-530614: "They know where your units are and a few other things."
- **No combat, border or wonder cheats (tested).**
  - TheNiceOne (27273): combat "outcomes are as they should be" over many battles; "there's no AI wonder production cheats".
  - TheNiceOne (33788): on border rules, "the AI follows exactly the same rule as the human player."
- **AI galleys (Firaxis, second-hand).** TheNiceOne (27273) reports that Soren Johnson said "an AI galley
  will never end a turn where it could sink."
- **Tech trading.** On higher levels the AI charges the human more. theNiceOne (86979) gives the example
  "1000 / 0.6 * 1.15 = 1917 gold" on Deity.

---

## 2. Civilization traits (Conquests)

Each civ has two traits, and each trait gives one free starting tech. CFC FAQ
https://civfanatics.com/civ3/faq/: "Each civilization has two traits, and each trait grants one starting technology."
The trait bits are in BIQ RACE `Flags[0]` bits 0-7, in the order Militaristic, Commercial, Expansionist,
Scientific, Religious, Industrious, Agricultural, Seafaring (`QueryCiv3/BiqSections/Race.cs`). The BIQ
RULE value `buildingDiscountForCivTraits` is 0.5 (repo ruleset.json `rules`).

| Trait | Effect in C3C | Free tech | Source / quote |
|---|---|---|---|
| Militaristic | Promotions are twice as likely. Barracks, Walls, Harbor and Coastal Fortress cost half. | Warrior Code (or The Wheel) | CFC FAQ: "Unit promotions to higher levels are twice as likely as non-MIL civilizations."; "Militaristic buildings like Barracks, Walls, Harbors, etc. are all half cost." Sullla https://sullla.com/Civ3/strat3b.html: "double the chance to promote every time one of their units wins a battle" |
| Commercial | OCN +25%, which means less corruption. Extra commerce on the city tile at size 7+ and again at 13+. | Alphabet | CFC FAQ: "The OCN (Optimal City Number) is increased 25% for Commercial civilizations." The FAQ also says city centres get an extra commerce on becoming a City and again at Metropolis. Sullla (pre-C3C): "an extra gold piece for each city (size 7-12) and 2 extra gold for each metropolis (size 13+)". |
| Expansionist | Starts with a Scout and is the only trait that can build Scouts. Never gets barbarians from goody huts, and gets better hut results. | Pottery | CFC FAQ: "Expansionist civilizations do not get barbarians from goody huts". Sullla: "they cannot get barbarians from goody huts." |
| Industrious | Workers are +50% faster in C3C (+100% in vanilla/PTW). Extra shield on the city tile at larger sizes. | Masonry | CFC FAQ: Industrious workers build "100% faster in Vanilla and Play the World, and 50% faster in Conquests." Artifex: https://forums.civfanatics.com/threads/updated-trait-rankings-for-c3c-emperor.75226/ "+50% instead of +100%", no bonus for slaves. |
| Religious | Temples and Cathedrals cost half. Anarchy lasts 2 turns in C3C (1 turn in vanilla/PTW). | Ceremonial Burial | CFC FAQ: "Religious buildings (Temples, Cathedrals) are all half price." The FAQ gives anarchy as 1 turn in Vanilla and PTW and 2 turns in Conquests. Artifex (75226): "2 turns instead of 1 turn". |
| Scientific | Library, University and Research Lab cost half. Free tech on entering each new era. Scientific Great Leaders appear in C3C. | Bronze Working | CFC FAQ: "Scientific buildings (Libraries, Universities, Research Labs) are half price." The FAQ adds a free technology on entering a new age. Sullla: "free tech at the start of each era". |
| Agricultural (C3C only) | +1 food on the city tile under every government except Despotism (under Despotism, see Disputed). Irrigated desert gives 2 food instead of 1. Aqueduct, Recycling Plant and Solar Plant are cheaper. | Pottery (Masonry if also Expansionist) | Arathorn (104294): "Provides one bonus food in the city square in all governments except despotism." TimBentley, same thread: "When in despotism, agricultural only adds 1 food to cities if they are on a river." LouLong https://forums.civfanatics.com/threads/conquests-features-comprehensive-survey.61791: "Cheaper aqueducts, recycling plants and solar plants." |
| Seafaring (C3C only) | +1 commerce on the city tile of cities on salt-water coast. Ships +1 movement and a lower chance of sinking. Coastal start much more likely. Cheaper Harbor (see UNVERIFIED). | Alphabet (Pottery if also Commercial) | Arathorn (104294): "Cities on the coast of salt water gain one extra commerce in the city square." LouLong (61791): "Ships have movement increased by 1"; "Decreased chance of sinking"; "Coastal start much more likely." |

More trait rules:

- **Military Great Leaders.** In C3C they can no longer rush Great Wonders, which affects Militaristic
  civs. stachnie (75226): "MGL's can't rush Great Wonders now".
- **C3C trait reassignments.** Arathorn (104294) lists them, for example "Aztecs - from Militaristic,
  Religious to Agricultural, Militaristic" and "England - from Commercial, Expansionist to Commercial, Seafaring".
- **Golden ages** come from owning Great Wonders that match the civ's traits, or from the first UU
  victory. That rule belongs in the golden-age section of this compendium and is not re-sourced here.

### 2.1 Per-civ AI personality (BIQ RACE, C3C defaults; BIQ (repo) values)

Aggression runs from -2 to +2 (`Race.cs`: `AggressionLevel; // -2 to 2`). Longasc's C3C list, in Ision's
article https://forums.civfanatics.com/threads/your-ai-opponent-by-ision.92917, uses a 1-5 scale. It puts
Germany, Zulu and Mongols at 5 and France and India at 1, which matches +2 and -2 below.

| Civ | Traits | Aggr. | Favourite gov | Shunned gov |
|---|---|---|---|---|
| Rome | Mil, Com | 1 | Republic | Communism |
| Egypt | Rel, Ind | 0 | Despotism | Republic |
| Greece | Com, Sci | 0 | Democracy | Despotism |
| Babylon | Sci, Rel | 0 | Monarchy | Despotism |
| Germany | Mil, Sci | 2 | Republic | Communism |
| Russia | Exp, Sci | 1 | Communism | Democracy |
| China | Mil, Ind | -1 | Communism | Feudalism |
| America | Exp, Ind | 0 | Democracy | Communism |
| Japan | Mil, Rel | 1 | Feudalism | Republic |
| France | Com, Ind | -2 | Republic | Fascism |
| India | Com, Rel | -2 | Democracy | Despotism |
| Persia | Sci, Ind | 1 | Monarchy | Republic |
| Aztecs | Mil, Agr | 1 | Despotism | Monarchy |
| Zululand | Mil, Exp | 2 | Despotism | Republic |
| Iroquois | Com, Agr | -1 | Communism | Monarchy |
| England | Com, Sea | 0 | Democracy | Fascism |
| Mongols | Mil, Exp | 2 | Despotism | Democracy |
| Spain | Rel, Sea | 0 | Monarchy | Despotism |
| Scandinavia | Mil, Sea | 1 | Monarchy | Republic |
| Ottomans | Sci, Ind | 0 | Republic | Despotism |
| Celts | Rel, Agr | 1 | Despotism | Feudalism |
| Arabia | Exp, Rel | 1 | Republic | Despotism |
| Carthage | Ind, Sea | -1 | Republic | Communism |
| Korea | Com, Sci | -1 | Monarchy | Republic |
| Sumeria | Sci, Agr | -1 | Monarchy | Republic |
| Hittites | Com, Exp | 1 | Fascism | Republic |
| Netherlands | Agr, Sea | 0 | Monarchy | Despotism |
| Portugal | Exp, Sea | 0 | Democracy | Fascism |
| Byzantines | Sci, Sea | 0 | Republic | Feudalism |
| Inca | Exp, Agr | 0 | Communism | Republic |
| Maya | Ind, Agr | 0 | Feudalism | Democracy |

The C3C setup screen also has an AI aggression setting that applies to all AIs. LouLong (61791):
"Random World Size & Adjustable AI aggression levels."

---

## 3. How the AI behaves

### 3.1 Architecture and production choice (Firaxis)

- **Leader AI and unit AI.** A leader AI handles the high-level picture and unit AIs ask it questions.
  Soren Johnson, interview of 26 Oct 2001:
  https://apolyton.net/forum/apolyton-civilization-site-general/civilization-lost-history/255541-soren-johnson-civilization-iii-interview-2-26-oct-2001
  says "A leader AI keeps track of high-level concepts, like whether enough science is being generated".
  The same interview, as quoted in
  https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/41443-interview-soren-johnson-on-ai?p=926594,
  says "the unit AI controlling a tank might ask the leader AI which enemy city would be best to attack."
- **Production.** The city picks whatever item scores highest. Soren (255541): "the city simply builds the item with the highest value."
- **Personality.** Leaders are modelled on history. Soren (255541): "an aggressive ruler preferring
  despotism and offensive ground units".
- **Naval and air.** Soren (255541): "the AI is capable of launching naval invasions using a variety of
  land, sea, and air units". C3C added to this. LouLong (61791): "AI improvemement : picking bombardment
  targets; escorting key naval transports like carriers and transports."

### 3.2 Build-often / build-never flags (RACE) and unit AI strategies

- **The flags.** RACE has three groups of flags:
  - Never flags in `Flags[8..9]`: Offensive, Defensive and Artillery land units, Settlers, Workers,
    Naval, Air, Growth, Production, Happiness, Science, Wealth, Trade, Explore and Culture.
  - Often flags in `Flags[12..13]`, for the same categories.
  - Governor flags in `Flags[4]`: ManageCitizens, Emphasize Food/Shields/Trade, ManageProduction,
    NoWonders and NoSmallWonders.
  Source: `QueryCiv3/BiqSections/Race.cs`.
- **Small effect on units (tested, Ozymandias, 29 Nov 2023).** https://forums.civfanatics.com/threads/manipulating-ai-unit-builds.686973
  found the flags "Don't affect Unit Builds by more than ~5%". He also found "Selecting more than 3
  (_maybe_ 4) of these options will "confuse" the AI, causing it to ignore all of your selections".
  Predator145 (Jan 2025) gives a different, second-hand limit: "Alexmann who worked with Firaxis has
  confirmed many years ago that 6 is the max effective number". Source:
  https://forums.civfanatics.com/threads/ai-build-often-building-categories.692300/
- **Which buildings each category covers** (Predator145, 3 Oct 2024, same thread).
  - Growth: "Granary, Aqueduct, Hospital".
  - Production: Factory and power plants.
  - Science: buildings that multiply beakers.
  - Trade: "Improvements that give a gold income multiplier (market, bank, stock exchange)", plus Harbor and Airport.
  - Wealth: the shield-to-gold project.
  - There is no Military category: "there is no Military category to encourage the AI to build barracks/harbors/walls."
- **Unit mix (tested, Ozymandias, 686973, about 560 AI builds).**
  - In peacetime the split is "D : O : other ~= 53% : 43% : 4%". The AI "likes" "between a 3:2 and 4:3
    ratio of D:A units".
  - Artillery is about 1% of builds, and "the AI was proven not to take bombard into account."
  - The AI will "Re-Flag" a unit from offence to defence or back as it sees fit.
  - The AI keeps offensive and defensive units in balance ("YES" to "Does the AI seek equilibrium").
  - Builds are partly random, and difficulty and aggression did not change the mix.
- **Unit abilities the AI favours** (tom2050, relayed in 686973): it "absolutely loves and adores" extra
  hit points. Blitz "would only increase the AI building them ... by around 4%."
- **The human can steer governors.** Soren (255541): you can "tell its governors to never build artillery and air units."

### 3.3 Settling: spacing, gaps and expansion

- **Early spacing of about 4 tiles.** Dominae, 31 Mar 2002, writing about a 4-tile spacing plan:
  https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/57896-early-gaem-deity-base-spacing-paradigm
  says "The AI pretty much follows this plan to the letter at all difficulty levels". Aeson, same thread:
  "I can sometimes build cities at a 5:6 ratio of what the AI does with 4 spacing."
- **Gaps that the AI later fills.**
  - Brain, Mar 2005: https://forums.civfanatics.com/threads/other-civs-encrouching-on-my-borders.113953
    says "The AI often leaves big gaps between their cities, which allows you to squeeze in."
  - The AI also fills empty land left inside a human's area. Grogs, same thread: the AI "just walked in
    and grabbed" an unclaimed area in the middle of his empire. Rohirrim: "If you can't block them
    completely, they just usually walk right past you and plop a city."
- **No settling in foreign borders or first-ring conflict.**
  - Grogs (113953): "You can't build a city within another nation's borders, and the AI cannot build one within yours."
  - SJ Frank: "The AI will never settle a city that has first-ring conflict."
  - Exception: eldar says that if an AI is "reduced to a single settler, they will settle in place (if they can)".
- **Expansion.**
  - Ision (92917) says the AI often builds settlers before its growth can support them.
  - "A sprawling Expansionist AI by the mid-Middle Ages is common".
  - The AI "will NEVER attempt a suicide run to find another land mass."
  - SirPleb (68813): "The AIs do not explore effectively, particularly large bodies of water."
- **Settler escorts.** No Civ3-specific source was found (see UNVERIFIED). The thread on blocking AI
  settlers that the search found is about Civ5.

### 3.4 War: when it declares

No Firaxis formula was found. These are community observations from
https://forums.civfanatics.com/threads/when-does-ai-declare-war.244088:

- MAS: "The most important factor is probably military strength."
- Nergal: "General attitude, ie annoyed or furious are way more likely to attack than Cautious." Also "The
  Zulus and Germans are pretty much always up for a war" (aggression +2 in RACE).
- Lord Emsworth: "If the AI gets something from you on per turn basis (esp.for free) it is extremely
  unlikely that you will be attacked." He also says that tribute demands follow aggression.
- rysingsun: "they are far less likely to declare war if you have rop with them"; "they don't declare
  much when there is lots of land still to be settled".
- civverguy: "they move a couple units in your borders and declare war on you the next turn."
- I. Larkin: "if AI have reach the point that they can't support their military anymore they declare war no matter what..."
- **Units in their territory.** The AI first asks you to leave. If you are still there the next turn, it
  can force you out or go to war. This is a forum summary from the search for "units in our territory"
  and has not been checked against a primary quote (see UNVERIFIED).

### 3.5 War: how it fights

- **Piecemeal attacks and poor artillery use.** Ision (92917): AI attacks are "piecemeal", and "the AI has
  no concept of proper artillery use". AI armies are "rare as a white rhino".
- **Artillery mostly stays in cities (C3C).** From
  https://apolyton.net/forum/civilization-series/civilization-iii/121250-does-the-computer-use-siege-units-in-conquests:
  - Jeem: "The AI does 'attempt' to use ground artillery offensively now, but it's still pretty rare".
  - ChrisiusMaximus: "they still seem to keep them holed up in their cities, and only seem to use them when I attack as a defensive strike."
  - Catt: "the AI won't usea stack of 10+ artillery units, with appropriate defense, to lay siege to a city".
- **Heavy garrisons, defensive escorts, quick naval retreat, small landings.** Ision (92917): the AI keeps
  heavy garrisons in every city and escorts its offensive stacks with many defensive units. It retreats
  ships to port after minor damage, and its amphibious invasions use too few units.
- **AI-vs-AI wars.** Ision: they are "wars of attrition" that drain the winner too.

### 3.6 Workers, sliders, government, trade

- **Too few workers.** Ision (92917): "the AIs never-ever build anywhere near enough workers."
- **Luxury slider.** Ision: the AI "will NOT use the luxury slider to compensate for any happiness
  issues". Instead it uses entertainers.
- **Changes government often.** Ision: "Changing governments is a favorite AI past time, doing so FAR too
  often through the course of a game."
- **Government switching rules (chiefpaco, 27 Apr 2002).** https://forums.civfanatics.com/threads/about-governments.21298
  - A peaceful AI in Monarchy or Communism "often switches to Democracy or Republic".
  - After a long war it "switches to Comm or Monarchy".
  - "If the AI discovers a new govt while in Desp, it switches."
  - chiefpaco: "I think it is entirely based on War & war weariness."
  - It is unclear whether the favourite government is a goal or only affects attitude. Propaganda said
    that even with Democracy shunned, "all of them always choose Democracy." (see Disputed)
- **Scientific AIs broker techs.** Ision: they gain techs and gold by trading techs on, and often end up
  with the most gold. Commercial AIs "rarely translates its inherent gold advantage out properly."
- **Early tech trading.** eric2075 (68813): "the AI won't trade techs very easily in the early game either."

### 3.7 "The AI never / rarely does X" (community)

| Observation | Source / quote |
|---|---|
| Few or no armies | Ision (92917): armies are "rare as a white rhino" |
| Coordinated artillery sieges | Catt (121250), quoted in 3.5 |
| Sending ships across ocean to explore | Ision (92917): "will NEVER attempt a suicide run to find another land mass" |
| Ending a galley's turn where it could sink | Soren via TheNiceOne (27273): "an AI galley will never end a turn where it could sink." |
| Building Barracks, Walls or Harbors in time (no Military category) | Predator145 (692300) |
| Upgrading most units | a modder in https://forums.civfanatics.com/threads/ai-auto-producing-units-but-wont-upgrade.692035/ (search summary): it upgrades "occasionally, but has a habit of leaving most as the near useless initial unit" |
| Coming back from behind | Ision (92917): the AI is "almost always incapable of 'coming back from behind.'" |

---

## 4. Victory conditions (C3C defaults)

| Victory | Condition | Source / quote |
|---|---|---|
| Conquest | Defeat all other civs | CFC FAQ https://civfanatics.com/civ3/faq/: "defeat all other civilizations" |
| Domination | 66% of the land **and** 66% of the population. Ocean tiles don't count. Sea tiles count for score but not for domination. | FAQ: "66% of the land AND population". BIQ GAME `DominationTerrain` / `DominationPopulation` (`Game.cs`). https://civfanatics.com/civ3/strategy/game-mechanics/maximizing-your-score/: "Ocean does not matter here, it is not included in your territory nor in the domination limit." and "Sea is important though – it counts toward increasing your territory but does not count towards the domination limit." |
| Space race | Build all 10 spaceship parts after the Apollo Program. In the C3C BIQ, Apollo Program is a Small Wonder (`isSmallWonder: true`, 500 shields, `buildSpaceshipParts`, BIQ (repo)). | FAQ: "build all 10 required spaceship parts" |
| Diplomatic (UN) | The UN builder can call an election every 11 turns. A civ is a candidate if it has 25% of the land **or** population. One vote per civ, and winning needs a majority. | FAQ: "a majority of the votes in the election"; "25% of the land OR population"; "every 11 turns" |
| Cultural (C3C) | One city with 20,000 culture, **or** a whole-civ total that depends on map size: 60k Tiny, 80k Small, 100k Standard, 130k Large, 160k Huge. The whole-civ total must also be at least twice any other civ's (see UNVERIFIED). | FAQ: "have either 20,000 culture in one city, or 100,000 culture across your empire"; in Conquests "60,000 for a Tiny map, 80,000 for Small, 100,000 for Standard, 130,000 for Large, and 160,000 for Huge." SirPleb (104294): "The required amount of culture for the "100K" cultural victory condition is variable in C3C, depending on the map size." BIQ GAME `OneCityCultureWin` / `AllCitiesCultureWin` (`Game.cs`). |
| Histographic (time) | Highest score at the end of the game (2050 AD on default timescale) | FAQ: "have the highest score at the end of the game" |
| Wonder victory (C3C, editor) | Build all wonders. It is off by default. | Thunderfall (104294): "Add Wonder Victory (all wonders built)"; repo `allowWonderVictory: false` |

C3C's F8 screen shows the domination limit and how far the leader is with its spaceship. Arathorn
(104294): "F8 now shows domination limit, spaceship parts completed by the leader, and a lot of other win
condition information."

---

## 5. Score

### 5.1 Per-turn score and final score

- **Per-turn score.** `(Territory + 2*HappyCitizens + ContentCitizens + Specialists) * Difficulty`
  - https://civfanatics.com/civ3/strategy/game-mechanics/maximizing-your-score/ gives this formula.
    Territory: "“Territory” is the number of tiles which are within your sphere of influence."
  - CFC FAQ (https://civfanatics.com/civ3/faq/) gives the same formula.
  - SirPleb, 20 Jan 2002: https://forums.civfanatics.com/threads/counting-final-points.14617 says
    "Multiply the total by the difficulty level." He counts 1 per tile, 2 per happy citizen and 1 per
    content citizen or specialist. He calls it his understanding from testing.
- **Final score is the average of the per-turn scores.** Maximizing-your-score: "The total of all your
  per-turn scores is divided by the number of turns played so far to get your actual game score." So
  late changes do little. col, https://forums.civfanatics.com/threads/increasing-final-score.37091:
  "Unlike Civ2 where your score is calculated at the end, this doesnt help much."
- **Things that don't count.**
  - Maximizing-your-score: "Wonders, cultural value, military size, science learned, etc. – none of these affect the score."
  - Bamspeedy (37091): "score doesn't take into account improvements, wonders, great leaders, diplomacy, etc."
  - Future techs are the one small exception (see UNVERIFIED for their value). Maximizing-your-score:
    "it counts for so little in score that it is not worth devoting any energy to it."
- **Difficulty multiplier.** In C3C it is 1 to 8. SirPleb (68813): Conquests multipliers are "Chieftain:
  1, Warlord: 2, Regent: 3, Monarch: 4, Emperor: 5, Demigod: 6, Deity: 7, Sid: 8". Before Conquests they
  were "Chieftain: 1, ... Deity: 6".

### 5.2 Early-victory ("finish") bonus

- **The bonus.** A win before 2050 AD adds `(2050 - FinishYear) * Difficulty`, and BC years count as
  negative.
  - Maximizing-your-score: "If you win before 2050AD, you get a bonus which is added to your regular
    score." Its example is a Regent win in 310 BC: (2050-(-310))*3 = 7080.
  - SirPleb (14617): "The bonus is (2050 - CurrentDate) * difficulty." He says it is the same for every
    victory type.
  - Bamspeedy (37091): "the finish bonus which is only dependant on what year you win."
- **Based on year, not turns.** Because it is based on the year, earlier wins get a larger bonus. BC-era
  wins benefit most because BC years count as negative. The term "Jesus score" was not found in any
  source (see UNVERIFIED).

---

## Disputed

1. **Which difficulty the AI uses for content citizens, quelling, barbarian bonus and corruption/OCN.**
   - TheNiceOne (37490) says Regent.
   - Pyrkaige (68642) says Monarch ("or did in PTW, and most likely still do").
   - Bamspeedy (37490) says the BIQ "default AI intelligence" setting (RULE `DefaultDifficultyLevel`) controls it.
2. **Deity values.**
   - Vanilla/PTW Deity: OCN 70%, AI-AI trade 160 (Pyrkaige 37490).
   - C3C Deity: OCN 60%, AI-AI trade 170 (Pyrkaige 68642; TimBentley 104294).
   - Some posts called Deity's cost factor 0.7 (37490 thread summary). Others conclude Deity is 0.6 in
     every version and 0.7 is C3C Demigod.
3. **How the cost factor applies to research.** Pre-patch, the AI's research is cheaper. In 1.29 and
   later, the human's research cost is divided by the cost factor (theNiceOne 86979; Bamspeedy 37490).
   Production is still scaled on the AI side.
4. **Commercial.**
   - Sullla (pre-C3C): corruption -12.5%, raised to 25% in 1.29f, plus +1/+2 gold at City/Metropolis.
   - CFC FAQ: OCN +25%, plus +1 commerce at City and again at Metropolis.
   - These may describe the same mechanism in different words (OCN vs corruption %). Not resolved.
5. **Industrious in C3C.**
   - CFC FAQ and Artifex: workers +50%.
   - stachnie (75226) thinks workers are no faster than non-Industrious civs' workers.
6. **Agricultural under Despotism.**
   - Arathorn: no bonus under Despotism.
   - TimBentley: under Despotism the bonus applies only to cities on a river.
7. **Seafaring city-tile commerce.**
   - Arathorn and CFC FAQ: all salt-water coastal cities.
   - StrategyWiki (via search summary only): the capital. StrategyWiki returned 403, so this is unchecked.
8. **Favourite government.**
   - Soren (255541) implies it is a real preference ("aggressive ruler preferring despotism").
   - chiefpaco and Propaganda (21298): it may only affect attitude, and AIs "always choose Democracy" even when it is shunned.
9. **Build-often/never: how many flags are effective.**
   - Ozymandias (tested): more than 3-4 and the AI ignores them all.
   - Predator145, citing alexman second-hand: 6.
   - C0rTeZ48 recalls more than 2 Build Often having no effect.
10. **AI amphibious ability.**
    - Soren (255541) and the C3C notes say it can mount naval invasions and escorts transports.
    - Ision (92917) and several posters say its landings are too small to hold what they take.
11. **UN election interval.**
    - CFC FAQ: every 11 turns.
    - A forum post in the search results: "usually every 20 turns or so". That post was not fetched.
12. **AI city spacing.**
    - Dominae (57896): the AI follows about 4-tile spacing at all levels.
    - Brain (113953): the AI "often leaves big gaps".
    - These are consistent if the AI aims for wide spacing and fills gaps later, but nobody measured it.

## UNVERIFIED / open questions

- **"Jesus score."** No source uses this term. The likely meaning is the year-based finish bonus, where
  BC years count as negative, so a BC win gets more than 2050 points × difficulty. Confirm what the
  requester meant.
- **Future tech score value.** Only "tiny" is sourced. A forum estimate of about one content citizen per
  future tech was seen in a search summary. https://forums.civfanatics.com/threads/scoring-of-future-tech.31261
  returned HTTP 429.
- **C3C optimal city number per world size.**
  - Vanilla: Tiny 12, Small 14, Standard 16, Large 24, Huge 32 (Pyrkaige 37490).
  - The repo's `worldSizes` has 14/17/20/28/36, but the regenerate readme says `worldSizes` is "not
    imported from the BIQ". Read BIQ WSIZ from conquests.biq to settle this.
- **Exact culture victory rule.**
  - Unsourced here: whether the whole-civ total must be at least twice the second civ's (Giant Bomb
    search summary only).
  - The one-city 20,000 threshold in C3C (FAQ says 20,000). Read `Game.OneCityCultureWin` and
    `AllCitiesCultureWin` from conquests.biq.
- **When the space race is won.** At launch or on arrival? Does capturing the capital destroy the ship?
  No Civ3-specific source.
- **Domination thresholds.** The 66%/66% values are C3C editor defaults. In Civ3 1.29f, are they hard-coded
  at 66%? Only the FAQ is the source.
- **UN details.** Is the UN builder always a candidate? Is the next most populous civ added when there is
  only one candidate? Seen only in the search summary of the CFC FAQ, not in a fetched quote.
- **Seafaring.** The cheaper Harbor (Giant Bomb search summary only) and the exact sinking reduction have no number.
- **Industrious city-tile shield bonus.** The size thresholds are paraphrased from the FAQ and not quoted verbatim.
- **Expansionist.** The exact goody-hut table shift is unsourced.
- **Scout availability.** Can only Expansionist civs build Scouts? Seen in a search summary only.
- **Settlers and workers.** Does the AI always escort settlers in Civ3? No Civ3 source. How does the AI
  pick tiles for workers? No source.
- **War triggers.**
  - The exact war-declaration formula (power ratio, attitude, aggression weight, effect of the C3C aggression setting).
  - The "units in our territory" sequence. The search summary says the AI asks once, then can force you
    out or declare war. No verbatim primary quote was fetched.
- **AI sliders.** Is the AI's science/tax split fixed, or chosen? Only the luxury claim (Ision) is sourced.
- **AI free starting units.** Which unit types are "defensive" and "offensive" (the best ones available at
  game start)? Not sourced.
- **Golden-age trigger.** It depends on traits, but no verbatim source was fetched in this pass. Leave it
  to the golden-age section.
