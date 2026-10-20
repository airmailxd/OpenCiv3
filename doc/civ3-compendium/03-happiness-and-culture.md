# 3. Happiness, culture and borders

How citizens' moods, civil disorder, celebrations, war weariness, culture, borders, culture flips, resistance and nationality work in Civ3 (base game 1.29f, PTW 1.27f, Conquests 1.22). Scenario-only and special game modes are excluded.

Where sources are cited:

- **Civilopedia (C3C)** is the game's own `Conquests/Text/Civilopedia.txt` (Steam "Civilization III Complete" install). It's a local file, so there's no URL.
- **C3C editor help** is `Conquests/Civ3ConquestsEdit.chm` (Firaxis' editor help, local).
- **labels.txt / script.txt** are `Conquests/Text/labels.txt` and `script.txt` (the game's own UI strings, local).
- **1.29f readme** is `README.txt` in the install root. It holds the base game's patch history from 1.16f to 1.29f (local).
- **Manual (Complete)** is https://cdn.akamai.steamstatic.com/steam/apps/3910/manuals/manual.pdf. Its text partly predates Conquests.

## 3.1 Content citizens and "overcrowding"

- **Base content citizens per city, by difficulty.** In each city the first N citizens are born content and the rest are born unhappy. The BIQ field is `DIFF.NumberOfCitizensBornContent`.

  | Difficulty | Chieftain | Warlord | Regent | Monarch | Emperor | Demigod | Deity | Sid |
  |---|---|---|---|---|---|---|---|---|
  | Born content | 4 | 3 | 2 | 2 | 1 | 1 | 1 | 1 |

  - Civilopedia (C3C), `#GCON_Overpopulation`: "In each city, a number of citizens are born content. Thereafter, the rest are born unhappy." The same table follows ("Chieftain 4 … Sid 1").
  - Spoonwood, https://forums.civfanatics.com/threads/overcrowding-happiness-mechanics.355989: "c(Cheiftain)=4 c(Warlord)=3 c(Regent)=2 c(Monarch)=2 c(Emperor)=1 c(Demi-God)=1 c(Deity)=1 c(Sid)=1."
  - C3C editor help, "Number of Citizens Born Content": "Determines how many citizens in a new city are born content for the selected difficulty level."
- **The unhappiness reason for over-the-base citizens is "It's just way too crowded."** It depends only on that city's size and the difficulty level.
  - labels.txt `#CITIZEN_UNHAPPY_REASONS`: "\"It's just way too crowded.\""
  - Skanky Burns, https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/50384-its-just-way-too-crowded: it "is related soley to the population of that city". On city count: "I dont think its related to number of cities at all."
- **Civ3 has no empire-size ("number of cities") unhappiness.** The optimal number of cities (OCN) affects only corruption/waste and the Forbidden Palace requirement. No content face is removed for going over the OCN. The evidence is indirect but consistent:
  - Civ3's complete list of unhappiness reasons (labels.txt `#CITIZEN_UNHAPPY_REASONS`) is: way too crowded, "give peace a chance" (war weariness), "Stop the aggression against our mother country!", "All the propaganda surrounding your regime is confusing.", "No More Draft!…", "We cannot forget the cruel oppression you have bore down upon us." (hurry/forced labour), and "Some improvements in this/another city are annoying". None of them is about city count.
  - C3C editor help, "Optimal Number of Cities": "Determines the optimal number of cities before rampant corruption occurs."
  - CFC FAQ, https://civfanatics.com/civ3/faq/: OCN "has two main purposes": the Forbidden Palace check, and "If a player's number of cities exceeds that number, corruption will increase dramatically".
  - alexman et al., https://civfanatics.com/civ3/strategy/game-mechanics/everything-about-corruption-c3c-edition/, uses OCN only for rank corruption, with `Nopt = max(OCN * (L/100 * (1 + c + Gr + Gp*Nwe) + 0.25*Ni), 1)`. Here L is `DIFF.PercentageOfOptimalCities`.
  - The "riot factor"/"each new city adds an unhappy citizen" rule players remember is Civ2's. For example, DaveV, https://codehappy.net/apolyton/threads/2061-1.htm (a Civ2 thread from 2000): "Each time you exceed a multiple of the magic number, your happiness calculation goes up one difficulty level in some cities."

## 3.2 Faces: what makes citizens content or happy

- **Content faces vs happy faces.** A content face turns one unhappy citizen content. A happy face turns one content citizen happy. Buildings, wonders and military police give content faces. Luxuries, entertainers and the luxury slider give happy faces.
  - Civilopedia (C3C), `#GCON_Happy_Faces`: "Each [content face] makes one unhappy citizen content." / "Each [happy face] makes one content citizen happy." The table there lists: "City improvements content faces / Wonders content faces / Military police content faces / Luxuries happy faces / Entertainers happy faces / Entertainment happy faces".
- **Excess faces carry over at reduced efficiency.** Surplus happy faces work on unhappy citizens at reduced efficiency. The exact rate isn't given (UNVERIFIED).
  - Civilopedia (C3C), `#GCON_Happy_Faces`: "the remaining 3 happy faces will carry over to unhappy citizens at reduced efficiency."
  - Manual, luxuries: "Each type makes one content citizen happy or (if there are no content citizens) one unhappy citizen content."
- **Citizens per face.** This is the BIQ field `RULE.CitizensAffectedByEachHappyFace`, default 1.
  - C3C editor help: "Determines how many citizens' moods are improved by each \"happy face\"… Default is 1."

### Buildings (C3C Civilopedia)

| Building | Effect | Culture/turn (CFC Civilopedia) |
|---|---|---|
| Temple | 1 content face | 2 |
| Marketplace | +50% tax; boosts luxury resources (table below) | 0 |
| Colosseum | 2 content faces | 2 |
| Cathedral (needs Temple) | 3 content faces | 3 |
| Police Station | "combats war weariness", reduces corruption | 0 |
| Courthouse | reduces corruption, "more resistant to propaganda" | 0 |

- Civilopedia (C3C), `#BLDG_Temple`: "produces one content face, making one unhappy citizen content". `#BLDG_Colosseum`: "produces two content faces". `#BLDG_Cathedral`: "produces three content faces … You must have a Temple". `#BLDG_Police_Station`: "combats war weariness and decreases corruption".
- Culture values: https://civfanatics.com/civ3/civilopedia/improvements/ ("Temple 2 … Makes 1 unhappy citizen content"), and the CFC Info Center, https://civfanatics.com/civ3/infocenter/: "Cathedral(3), Colosseum(2), Library(3), Palace(1), Research Lab (2), Temple (2), University (4)". The Complete manual's culture chart (p.147) gives the same 3/2/3/1/2/2/4 sequence.

### Wonders affecting happiness

| Wonder | Effect (C3C Civilopedia) |
|---|---|
| Temple of Artemis (C3C) | "Puts a Temple in every city on the same continent." |
| The Oracle | "Doubles the effect of all Temples in the Empire; they produce two content faces instead of one." |
| Hanging Gardens | "makes three unhappy citizens content in its city and one unhappy citizen content in all other friendly cities." |
| Mausoleum of Mausollos | "makes three unhappy citizens content in its city." |
| Sistine Chapel (Michelangelo's) | "Doubles the effects of all Cathedrals in your cities." |
| JS Bach's Cathedral | "Decreases the number of unhappy citizens on the continent by two per city." |
| Shakespeare's Theater | "Eight unhappy citizens in the city are made content." |
| Cure for Cancer | "Makes one unhappy citizen content in every city." |
| Universal Suffrage | "Reduces war weariness in all cities." |

- The Civilopedia (C3C) `#GCON_Moods` list of happiness wonders is: Oracle, Mausoleum, Sistine Chapel, Hanging Gardens, Shakespeare's Theater, JS Bach's Cathedral, Cure for Cancer.
- See Disputed for content-versus-happy wording differences between the manual, the Civilopedia and CFC.

## 3.3 Luxury resources and the luxury rate

- **Each distinct luxury type available to a city gives happy faces.** The city must be connected (by road, river, harbour or airport) to the luxury inside your borders or at a colony, or receive it by trade. Traded luxuries reach the cities connected to your capital.
  - Manual: "one content citizen of the city is made happy for each of these luxuries", plus "If you receive a luxury in trade, all your cities that are connected to your capital … have access to it".
  - Civilopedia (C3C), `#GCON_ResourcesL`: "When available to a city from foreign or domestic trade routes, luxury resources produce [happy faces], making content citizens happy."
- **Duplicates add nothing.** One source per type supplies the whole civ.
  - Manual: "it takes only one square's worth of a resource to supply your entire civilization, any surplus … is available for trading".
- **Diminishing (actually increasing) order with a Marketplace.** The bonus applies through the building flag "Increases Luxury Trade". Faces per luxury: 1st and 2nd 1 each, 3rd and 4th 2 each, 5th and 6th 3 each, 7th and 8th 4 each. Without a Marketplace each luxury is 1 face.

  | Luxuries | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | Faces with Marketplace | 1 | 2 | 4 | 6 | 9 | 12 | 16 | 20 |

  - Civilopedia (C3C), `#BLDG_Marketplace`: "3 luxuries = 4 happy faces … 8 luxuries = 20 happy faces".
  - C3C editor help, "Increases Luxury Trade": "1st and 2nd luxury = 1 happy face 3rd and 4th luxury = 2 happy faces 5th and 6th luxury = 3 happy faces 7th and 8th luxury = 4 happy faces".
- **The luxury slider ("Entertainment")** turns commerce into happy faces. The C3C editor has a separate building flag, "+50% Luxury Output": "luxury revenue produced in the city … is increased by 50%". Two things are UNVERIFIED: which default buildings carry that flag (the Marketplace is assumed), and how many luxury points make one face.
- **Base-game luxury types:** Dyes, Furs, Gems, Incense, Ivory, Silk, Spice, Wine. Civilopedia (C3C), `#GCON_ResourcesL`, lists their tile yields, e.g. "Gems +0 +0 +4".

## 3.4 Martial law (military police)

- **Each military unit in the city makes one unhappy citizen content, up to a limit set by the government.** Only units with attack > 0 count. The BIQ fields are `GOVT.MilitaryPoliceLimit` and the per-government "Number of Citizens Quelled by Military".
  - Manual: "A small number of military units, each with an attack factor of at least one, can be stationed in a city as military police. Each military unit makes one unhappy citizen in a city content."
  - C3C editor help: "Determines how many military units (units with attack rating greater than zero) can be garrisoned in a city to reduce unhappiness."

  | Government | Anarchy | Despotism | Monarchy | Feudalism | Communism | Fascism | Republic | Democracy |
  |---|---|---|---|---|---|---|---|---|
  | Military police limit | 0 | 2 | 3 | 3 | 4 | 4 | 0 | 0 |

  - Civilopedia (C3C) government entries, e.g. `#GOVT_Communism`: "Military Police Limit 4". The same figures appear at https://civfanatics.com/civ3/civilopedia/governments/.
  - The manual's older text names only "Despotism, Monarchy, or Communism", because Feudalism and Fascism are PTW/C3C additions.

## 3.5 War weariness

- **Which governments suffer war weariness (C3C):**

  | Government | Despotism | Monarchy | Communism | Fascism | Anarchy | Feudalism | Republic | Democracy |
  |---|---|---|---|---|---|---|---|---|
  | War weariness | None | None | None | None | None | Low | Low | High |

  - The BIQ field is `GOVT.WarWeariness`, with editor labels "Citizens do not get unhappy / Citizens get unhappy / Citizens get very unhappy" (labels.txt).
  - Civilopedia (C3C), `#GCON_War_Weariness`: "citizens living under a Republic, Feudalism, or Democracy become [war weary]". Government entries, e.g. `#GOVT_Democracy`: "War Weariness High".
  - The base-game manual names only Republic and Democracy: "If you're ruling under a representative form of government (Republic or Democracy), war weariness makes your citizens unhappy."
- **What raises and lowers it** (Firaxis' description). Attacking raises it. Being declared on, or fighting at home, is more tolerable.
  - Civilopedia (C3C): "When [you] initiate the conflict your people will tolerate it for a while … When someone else declares the war, especially a traditional enemy, your people are more forgiving. Carrying on a defensive war, that is keeping your troops at home, is more tolerable than offensive campaigns."
  - Manual: "The most important factors in causing war weariness are stationing your units in a rival's territory, enemies having troops in your territory, declaring war, and engaging in battle. Having a rival declare war on you actually decreases war weariness".
- **Community-measured model** (credited to Bamspeedy and DaveMcW), from https://civfanatics.com/civ3/strategy/game-mechanics/how-does-war-weariness-work/:
  - **Points.** Each civ keeps war weariness points (wwp) against each other civ: "Each civ have one wwp number against each of the other civs."
  - **Gains:**
    - +1 for each lost unit without defence, each improvement pillaged or bombed, and each unit bombarded down to 1 hp.
    - "Add 2 wwp when a human attacker is defeated".
    - "Add 2 wwp when a unit with defence value is attacked. (Even if you win)".
    - "Add 16 wwp when a size 1 city is captured 17 wwp for bigger cities".
    - +1 per turn while you have units in enemy territory.
  - **Losses:**
    - "Subtract 30 wwp if the AI attacks you", unless the AI was provoked (e.g. by nuclear use or a failed spy mission).
    - "Subtract 1/20 of current wwp each turn in peace (round up)".
  - **Levels:** 0–30 no effect, 31–60 level 1, 61–90 level 2, 91–120 level 3, 121 and up level 4. A level of −1 (being attacked) makes 25% of people happy ("war happiness").
  - **Effects:**
    - Republic: L1 25%, L2 50%, L3 50%, L4 100% unhappy.
    - Democracy: "Level 1: 50% unhappy people", "Level 2: 100% unhappy people", "Level 3: Revolt".
    - Rounded down and summed over enemies, then reduced: "subtract 25% for police station and 1 for US (Universal Sufferage)".
  - **Decay example:** from 121 wwp at peace, "It will take 19 turns to get down to level 0, and 43 turn until the war is totally forgotten."
- **Democracy falls into Anarchy when war weariness reaches 100%.**
  - https://civfanatics.com/civ3/civilopedia/governments/: "It also happens when WW reaches 100% when under Democracy."
  - script.txt: "Our people are overthrowing our $GOVERNMENT0. Our Civilization is descending into Anarchy!"
- **Ending it.** Peace stops new war weariness, and so does eliminating the enemy civ.
  - Civilopedia (C3C): "The only way to end [war weariness] is to stop the war."
  - Civ3 Strategy Compilation, https://www.civfanatics.com/wp-content/uploads/2024/07/Civ3_Strat_Compilation.pdf: "Wiping out a Civ stops war weariness with it (just as peace would)".
- **There is no Senate in any Civ3 version.** War weariness replaces it.
  - Manual: "Republics and Democracies no longer have those pesky Senators refusing to let you go to war and forcing you into unwanted treaties. However, your citizens' war weariness affects your decisions in a similar way."
  - PTW/C3C: no source mentions a Senate (UNVERIFIED, but no readme adds one).

## 3.6 Nationality-based unhappiness, draft and hurry

- **"Stop the aggression against our mother country!"** Foreign nationals are unhappy while you're at war with their mother country, and become unhappy "more quickly than others". This applies under every government and is separate from war weariness. The exact count or fraction is UNVERIFIED.
  - Civilopedia (C3C), `#GCON_Nationality`: "Having a city with foreign nationals is not a problem unless you are at war with the nation that originally produced them. When at war, foreign nationals become unhappy more quickly than others and are more likely to be affected by hostile propaganda from their mother country."
  - labels.txt: "\"Stop the aggression against our mother country!\"".
  - Qitai, https://forums.civfanatics.com/threads/why-are-the-citizens-of-my-newly-conquered-cities-so-unhappy.79817/: they "will remain unhappy until you peace with the civ concern".
- **Draft.**
  - Requires Nationalism and a city of size 7 or more.
  - The draft limit per city per turn is Republic/Democracy 1, and Despotism, Monarchy, Feudalism, Communism and Fascism 2. Anarchy can't draft.
  - Each drafted citizen makes the city unhappy for 20 turns (`RULE.TurnPenaltyForEachDraftedCitizen`).
  - Civilopedia (C3C), `#GCON_Draft`: "You may only draft citizens from a city with a population of 7 or higher." / "drafting citizens will cause unhappiness in the city."
  - C3C editor help: "Determines how long your citizens' moods will be affected by each citizen drafted. Default is 20 turns."
  - 1.29f readme, Changes v1.21f: "Draft anger and hurrying unhappiness reduced to 20 turns."
  - Which nationality a drafted citizen or unit takes is UNVERIFIED.
- **Hurrying by forced labour.** This applies under Despotism, Communism, Feudalism and Fascism. Each sacrificed citizen causes unhappiness ("cruel oppression") for 20 turns (`RULE.TurnPenaltyForEachHurrySacrifice`). Hurrying is impossible while the city has resisters.
  - C3C editor help: "Default is 20 turns."
  - 1.29f readme, Changes v1.17f: "Unhappiness from population rushing production increased to 40 turns." This was later cut to 20 in v1.21f (above).
  - script.txt `#HURRY_RESISTANCE`: "We cannot hurry production while our city is in resistance."

## 3.7 Civil disorder

- **Trigger.** A city is in disorder when its unhappy citizens outnumber its happy ones. Content citizens, specialists and resisters are ignored.
  - Manual: "A city suffers civil disorder when unhappy people outnumber happy people. Resistors, content people, and Specialists are ignored in the calculation."
  - Civilopedia (C3C), `#GCON_Moods`: "Content citizens and specialists are ignored."
- **Effects.**
  - No shields, no commerce, and no surplus food is stored.
  - Each turn in disorder carries a `RULE.ChanceOfRioting` chance (default 20%) of a riot that can destroy improvements.
  - The city's culture flip chance is doubled (§3.11).
  - Prolonged disorder can overthrow the government.
  - A Nuclear Plant may melt down.
  - Order returns the turn after the cause is fixed.
  - Civilopedia (C3C), `#GCON_Disorder`: "Production ceases, No revenue comes in from commerce, and No excess food is stored." It continues: "Prolonged periods of civil disorder can lead to: Destruction of existing city improvements … Defection of the city … An overthrow of your government and descent of your empire into anarchy."
  - C3C editor help, "Chance of Rioting": "Determines the percentage chance of rioting occurring in any city experiencing disorder. Default is 20%."
  - Manual: "When order is restored, the city returns to normal operation the next turn." / "The risk of meltdown always exists when a city that has a Nuclear Plant goes into civil disorder."
  - How long "prolonged" disorder must last before the government falls is UNVERIFIED.

## 3.8 We Love the King Day (celebration)

- **Conditions:**
  - not starving;
  - no resisters;
  - no unhappy citizens;
  - happy at least equal to content (see Disputed);
  - size at least `RULE.MinimumPopulationForWeLoveTheKing`, default 6.
  - Civilopedia (C3C), `#GCON_We_Love_The_King_Day`: "The city cannot be starving, The city cannot have any resisters, The city cannot have any unhappy citizens, There must be more happy citizens than content citizens, and The city must be large enough (typically size 6)."
  - Manual: "There must be at least as many happy citizens as content citizens" (example: "a city with five happy citizens, four content citizens, and no unhappy citizens celebrates").
  - C3C editor help: "Default is 6 citizens."
- **Effects:**
  - The culture-flip chance is halved.
  - Waste (shield loss) is reduced. Commerce corruption isn't, per alexman.
  - Enemy propaganda is less effective.
  - The effects start on the first full turn of celebrating.
  - Civilopedia (C3C): "The chance of the city converting to another civ is halved, Waste in the city is reduced, and The effectiveness of enemy spy propaganda missions against the city is reduced."
  - alexman et al., https://civfanatics.com/civ3/strategy/game-mechanics/everything-about-corruption-c3c-edition/: "For waste calculations only, when the city is in a WLTKD celebration, divide da by 2." / "… add OCN/4 to Nopt."
  - Manual: "You will see the effects of celebration begin on the first full turn that a city celebrates".
  - 1.29f readme, Changes v1.21f: "Increased waste fighting ability of We Love The King Day."
- **Civ3 has no rapture growth and no per-government celebration bonus.** These sources contain none: the Civilopedia benefit list above is complete, and the C3C editor's government flags are unconditional. They are "Standard tile penalty – Under the selected government, no tile may generate more than 2 food and shields" and "Standard trade bonus – … each tile generates an additional +1 to commerce", with no "when celebrating" option. Rapture growth is a Civ1/Civ2 mechanic. The manual's sentence "the city enjoys certain benefits, depending on your civilization's type of government" conflicts with this (see Disputed).

## 3.9 Specialists

| Specialist | Base game 1.29f / PTW | C3C | Availability |
|---|---|---|---|
| Entertainer | 1 happy face | 1 happy face | always |
| Tax collector | 1 gold | 2 gold | always |
| Scientist | 1 beaker | 3 beakers | always |
| Police officer | – | reduces corruption/waste | Nationalism (C3C) |
| Civil engineer | – | +2 shields toward improvements and wonders only | Replaceable Parts (C3C) |

- Civilopedia (C3C), `#GCON_Specialists`: "An entertainer produces one happy face." / "a police officer can be used to help keep a lid on corruption" / "a Civil Engineer can be used to produce additional shields which can be applied toward the production of city Improvements and wonders."
- https://civfanatics.com/?p=145 ("The Role of the Specialist Citizen"):
  - "1 scientist equals 1 beaker in CivIII 1.29f and PTW" / "the scientist adds thee beakers" (C3C).
  - "The tax collector now adds two gpt" (C3C).
  - "Each hardhat adds two shields to the net shield output of the city when building city improvements".
  - "Corruption and waste do not reduce the specialist's output."
- Specialists are ignored in the disorder test (§3.7) and count toward score: manual, "your content and happy citizens (including Specialists)".
- The BIQ CTZN section holds per-type `Luxuries`, `Research`, `Taxes`, `Corruption` and `Construction`. The C3C editor's "Luxury Bonus (Citizen Properties)" is "The bonus to a city's luxuries produced by one population point of the selected citizen type." The default entertainer value in luxury points is UNVERIFIED.

## 3.10 Culture: points, doubling, totals, borders

- **Culture per turn.** Each building and wonder makes its BIQ `culture` value per turn. Per-city totals accumulate, and the civ's culture is the sum over its cities.
  - Civilopedia (C3C), `#GCON_Culture`: "All Wonders of the World and many city improvements generate culture points every turn." / "The sum of the cultural values of all of your cities is your [civilization's cultural value.]"
  - Building values are in §3.2.
  - Great wonder values from the Complete manual (p.147): Adam Smith 3, Colossus 3, Copernicus 4, Cure for Cancer 5, Great Library 5, Great Lighthouse 2, Great Wall 2, Hanging Gardens 4, Hoover Dam 3, Internet 4, JS Bach 5, Knights Templar 2, Leonardo 2, Longevity 3, Magellan 3, Manhattan 2, Michelangelo 4, Mausoleum 2, Newton 5, Oracle 4, Pyramids 4, SETI 3, Shakespeare 5, Statue of Zeus 4, Sun Tzu 2, Temple of Artemis 4, Theory of Evolution 3, UN 4, Universal Suffrage 4.
  - Small wonder values in that chart are garbled in the PDF text (UNVERIFIED).
- **Doubling after 1000 years.** A building's or wonder's culture doubles once it has stood 1000 game years. A rebuilt building starts over.
  - Civilopedia (C3C): "The amount of culture each Wonder or improvement contributes doubles after 1000 years."
  - 1.29f readme, Changes v1.17f: "Added mention of Wonder and improvement culture contributions doubling after 1000 years to Culture Civilopedia entry."
  - Turner, https://forums.civfanatics.com/threads/about-culture-points.116213, on rebuilt buildings: "those do not get back the 1000 yr doubling if it already had it."
  - This is game years, not turns. MTheil3508, https://forums.civfanatics.com/threads/culture.33034: "early in the game, the years go by faster".
  - Corollary: with a 2050 AD end, anything built after 1050 AD never doubles.
- **Mobilization halves culture.** Culture from improvements is halved while the civ is on a war footing.
  - Manual: "During wartime footing, cultural improvements produce half the number normally produced per turn."
  - CFC FAQ: "All culture per turn from your city is halved as well".
- **Fascism (C3C) is xenophobic.** A city makes no culture while fewer than half its citizens are of the owner's nationality.
  - C3C editor help, Government flags: "Xenophobic – Under the selected government, no culture is gained in cities where less than half of the population of the city is from the same race as the controlling civilization."
  - Civilopedia (C3C), `#GOVT_Fascism`: newly conquered cities "do not generate culture until a popular majority has been assimilated."
- **Per-civ memory in each city; capture.**
  - A city stores how much culture each civ made in it.
  - On capture, every building with culture > 0 is destroyed, plus 25% of other buildings and all small wonders. Great wonders survive, but give the captor no culture.
  - The captor gets the city's culture only with the scenario flag "Retain Culture on Capture".
  - C3C editor help, "Retain Culture on Capture": "the full cultural value of a city is retained by a conquering civ when an enemy captures that city, even though all possible buildings with a culture value > 0 are still destroyed, as well as 25% of other buildings and any small wonder (as normal)."
  - ainwood, https://forums.civfanatics.com/threads/about-culture-points.116213: "The city stores the culture generated by the buildings of each civilization that has owned the city at any time."
  - Turner, same thread: "captured wonders do not give culture to the capturing civ."
  - 1.29f readme, Changes v1.16f: "Capturing a population 1 city that has no culture will automatically raze it."
- **Border levels by city culture.** With the default `RULE.BorderFactor` of 10, the levels are at 0, 10, 100, 1000, 10000 and 100000 city culture, i.e. `BorderFactor^n`. Each level grows the border radius by one. A new city starts at level 0 with radius 1 (its 9 tiles). The exact tile shape of each radius is UNVERIFIED.
  - C3C editor help, "Border Factor": "There are 6 levels of border expansion and under the default Border Factor of 10, they occur at 0, 10, 100, 1000, 10000, 100000."
  - CFC Info Center, https://civfanatics.com/civ3/infocenter/: "When culture reaches a certain point, the radius of your borders grows by one." / "The first increase comes at 10 Culture points" / "The amount you need to grow your borders increase by a factor of 10 each time".
  - Renata, https://forums.civfanatics.com/threads/culture-expansion.52924: "a city's original nine tiles".
  - The C3C Civilopedia instead lists 20,000 as the fifth threshold (see Disputed).
  - 1.29f readme, Changes v1.16f: "Editor: On General Settings, added Border Factor which controls border expansion."
- **Civ-wide culture levels** are a separate scale (`RULE` "Lvl. Multiplier", default 1000). They are used for the cultural level labels, not borders.
  - C3C editor help, "Level Multiplier": "Under the default level multiplier of 1000, the values for these cultural levels are 1000, 2000, 4000, 8000, 16000, and 32000."
  - What these levels do in play beyond labels is UNVERIFIED.
- **Borders never shrink for culture reasons, except to a stronger neighbour.** On contested tiles, the closer city keeps its own first ring.
  - CFC Info Center: "Your borders never shrink due to culture." / "the only real way for your borders to shrink is if the borders of a nearby city with higher culture expand".
  - Civilopedia (C3C), `#GCON_Culture`: civ culture "comes into play … when you and a rival both have a legitimate claim on a terrain square."
  - Sirp and Shillen, https://forums.civfanatics.com/threads/culture-expansion.52924: you can't take a tile closer to another civ's city unless that city exerts no cultural control over it. Shillen: "Their city will only control their first ring, while you control their second ring." Renata retracted her "equidistant only" claim: "Yep, you're both right."
  - The exact tie-break rule (city culture vs civ culture vs distance) is UNVERIFIED.
- **What borders do** (Civilopedia (C3C), `#GCON_Territory`):
  - "Rival civilizations cannot benefit from the roads or rails within your territory without a right of passage agreement."
  - "You can always 'see' squares in friendly territory."
  - Resources inside borders need no colony.
  - "You can eject rival military units from your territory".
  - The city radius is only the tiles within 2 that are inside the city's borders (`#GCON_Radius`): "within the city's cultural borders, and within two squares of the city."
- **Tourist attractions.** A great wonder older than 1000 years earns gold for its city.
  - Civilopedia (C3C), `#GCON_Tourist_Attraction`: "1000 (years) - 1500 +2 (gold) … 2251 - 2500 +12".

## 3.11 Culture victory (as relevant)

- **Thresholds.** One city with 20,000 culture wins. The civ-wide threshold depends on map size: Tiny 60,000, Small 80,000, Standard 100,000, Large 130,000, Huge 160,000. The BIQ fields are `GAME.OneCityCultureWin` and `GAME.AllCitiesCultureWin`.
  - Civilopedia (C3C), `#GCON_Victory`: "A single city with 20,000 culture points will satisfy this victory condition." … "Tiny: 60,000 Small: 80,000 Standard: 100,000 Large: 130,000 Huge: 160,000".
  - CFC FAQ (standard map): "have either 20,000 culture in one city, or 100,000 culture across your empire".
  - The rule "no other civ can have over half your culture points" (dac, https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/58212-culture-victory) is from memory: UNVERIFIED.

## 3.12 Cultural opinion: diplomacy, propaganda, resistance

- **Cultural opinion.** Each civ holds a cultural opinion of every other, set by their culture ratio. Defaults are from Dan Magaha (Firaxis), https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42123-propaganda-chances:

  | Opinion (target's view of you) | Your culture : theirs | Propaganda success | Initial resistance | Continued resistance (per resister per turn) |
  |---|---|---|---|---|
  | disdainful of | 1:3 | 3% | 90% | 80% |
  | dismissive of | 1:2 | 5% | 80% | 70% |
  | unimpressed by | 3:4 | 10% | 70% | 60% |
  | impressed with | 1:1 | 20% | 60% | 50% |
  | admirers of | 2:1 | 25% | 50% | 40% |
  | in awe of | 3:1 | 30% | 40% | 30% |

  - Magaha: "Chance of resistance means that if you take one of their cities, the chance of resisters in the city is 90%," / "and each turn there is an 80% chance that each resister will continue to resist." / "the more culture advantage you have over another civ, the easier it is to take over cities".
  - C3C editor help, "Culture Ratio": "if the Germans had 3000 culture points and the Americans had only 1000, the Americans would be said to be \"in awe of\" the Germans."
  - The BIQ CULT section has `ChanceOfSuccessfulPropaganda`, `CultureRatioPercentage`, `CultureRatioNumerator/Denominator`, `InitialResistanceChance` and `ContinuedResistanceChance`.
- **Diplomacy.** Culture affects AI attitude, and the diplomacy screen shows the opinion. The size of the effect is UNVERIFIED.
  - Civilopedia (C3C), `#GCON_Culture`: civ culture "comes into play when you … conduct diplomacy with a rival".
  - script.txt: "Our people are [$CULTURAL_OPINION0] the $CIVADJ1 cultural achievements."
- **Propaganda (spy mission).** A success makes the city defect. Some sources say it can instead cause only unhappiness.
  - Democracies are immune. Courthouses, capitals and WLTKD resist it. Cities in disorder are easier targets.
  - Manual: "Cities of a Democracy are immune to propaganda. Enemy capitals and cities with Courthouses are less likely to revolt. Also, it is easier to push a city already in civil disorder into open revolt".
  - Bluemofia, https://forums.civfanatics.com/threads/da-rules-explained.210035/post-5161431: "propaganda will either cause the citizens to join your civilization, or simply cause unhappiness."
  - labels.txt has the unhappiness reason "All the propaganda surrounding your regime is confusing."

## 3.13 Resistance in captured cities

- **Which citizens may resist.** On capture, each foreign national of the defeated civ may resist, with the "initial resistance" chance from the opinion table. Your own former citizens don't resist when you retake a city, but citizens born under the enemy do.
  - Civilopedia (C3C), `#GCON_Resistance`: "a comparison of the civilization cultural values of the two civilizations determines if any of the foreign nationals will resist."
  - Manual: "If you recapture one of your cities from the Romans, the folks who were previously under your rule won't resist. Any new citizens created after the city was originally taken, however, might resist".
- **Resisters:**
  - don't work;
  - don't eat;
  - block hurrying;
  - block WLTKD;
  - count double toward culture flips.
  - Civilopedia (C3C): "Resisters are unwilling to work under the new regime."
  - 1.29f readme, Changes v1.29f: "Corrected Resistance Civilopedia chapter to reflect resisting citizens do not consume food."
  - Flips: §3.14.
- **Ending resistance.** Each turn, each resister keeps resisting with the "continued" chance, modified by the owner's government and by the garrison ("the more the better"). Making peace with the mother country ends resistance.
  - C3C editor help, "Resistance Modifier (Government Properties)": "the modifier to a single population point's chance of continuing to resist each turn. Positive numbers mean that the resisting population has a greater chance of continuing to resist".
  - Civilopedia (C3C): "Resisters can be quelled by ending the war or garrisoning strong military units in the city -- the more the better."
  - Manual: "They stay that way until you make peace with their mother country or 'win them over'". Also: "A more impressive culture, a government that allows more personal freedom, and a greater supply of luxuries all help."
  - script.txt: "Our troops have quelled $NUM0 $<#0:resister:resisters>!"
  - How much each unit counts in the garrison is UNVERIFIED.

## 3.14 Assimilation and nationality

- **Nationality.** Every citizen has a nationality. Citizens born in a city take the owner's nationality; captured citizens keep theirs. Captured workers or settlers that join a city become foreign nationals.
  - Civilopedia (C3C), `#GCON_Nationality`: "Each civilization produces citizens of its own [nationality], a characteristic that persists until assimilated" / "Captured workers and settlers retain their original nationality as well, and, if ordered to join a city, become foreign nationals within that city."
  - CFC Info Center: "the citizens in the conquered city will retain their nationality".
- **Assimilation.** Each turn, each foreign national has a per-government percentage chance (`GOVT.AssimilationChance`) of taking the city's nationality. Firaxis put the defaults at 1–4%; the per-government values are UNVERIFIED. A culture flip does not change citizens' nationality.
  - C3C editor help: "Determines the percentage chance, per turn, that a foreign national in city with the selected government type will assimilate into the current nationality of that city."
  - alexman (Firaxis), https://codehappy.net/apolyton/threads/51715-4.htm #118: "the rate is between a 1 and 4 percent chance per citizen per turn, depending on your government."
  - CFC Info Center: "How fast this happens depends both on their national culture and the culture of the city they currently reside in," "as well as the government type of their current city."
  - CKS, https://forums.civfanatics.com/threads/culture-flips.661194/: "Your citizens don't change nationality in a flip".
- **Effects of nationality:**
  - culture-flip input F (§3.15);
  - resistance (§3.13);
  - "mother country" unhappiness while at war (§3.6);
  - greater vulnerability to the mother country's propaganda;
  - lower city defence while resisting;
  - Fascist culture gating (§3.10).
  - CFC Info Center: resisters "will lower the defense value of the city so that Babylonian forces can easily retake it."

## 3.15 Culture flips (cultural reversion / "City Defection")

- **Firaxis' qualitative list** (Civilopedia (C3C), `#GCON_Conversion`):
  - Defections are made more likely by: "The presence of foreign nationals of the other civilization in the city", "The application of propaganda by the other civilization", "Civil disorder in the city", "The proximity of foreign territory", "The proximity of a foreign capital".
  - They are made less likely by: "The presence of ground combat units in the city", "We Love The ____ Day", "The proximity of your capital".
- **Formula.** Per interturn, for each rival civ:

  `P = [(F + T) * Cc * H * (Cte / Cty) - G] / D`

  Sources:
  - Dan Magaha (Firaxis), https://forums.civfanatics.com/threads/culture-flipping-exposed.14337/. He describes the terms but not the arithmetic.
  - Soren Johnson's worked examples, https://codehappy.net/apolyton/threads/51715-1.htm #30 and 51715-2 #36/#48.
  - Flip Calc, https://forums.civfanatics.com/threads/flip-calc-calculates-the-probability-of-culture-flips.53157/: "P=[(F+T)*Cc*H*(Cte/Cty) - G]/D".
  - F: foreign nationals (of that civ), resisters twice. Magaha: "The number of foreign nationals in the city in question (resisters are counted twice)".
  - T: city-radius tiles inside that civ's borders, of 21. Magaha: "The number of the 21-tile city-radius squares … that fall inside your cultural borders." Soren: "they count just as much as citizens."
  - Cc = 2 if that civ has more historical culture in the city than the owner, else 1. Magaha: each city "remembers the total amount of culture generated by any civ who has ever occupied it".
  - H = 2 in disorder, 0.5 in WLTKD, else 1. Magaha: "Civil Disorder in a city doubles the chance" / "We Love the King (or whatever) Day in a city halves the chance".
  - Cte / Cty: the two civs' total culture. Soren confirmed the ratio always applies, "it is the latter...", 51715-2 #48.
  - G: the owner's land units with attack ≥ 1 and defence ≥ 1 in the city. Magaha: "any unit with at least 1 point of offensive and defensive capability". Soren: "16 combat (don't count artillery) units". alexman, 51715-4: every unit in an army counts, "including the actual army unit"; "If the army is empty, it doesn't count at all though"; air units don't count. Troops count against each rival's roll separately (Soren: "one spearman could negate both the Greek and the French citizen").
  - D: the die size, 2000 × d, where d is the ratio of the city's distance to the rival capital over its distance to its own capital, capped to 0.25–4. That makes D range 500–8000. Soren: "The capital factor affects how big the 'die' is". Soren via DeepO, 51715-3 #69: "it goes from 500 to 8000, with an average of 2000". Flip Calc v1.5 distance: max(x,y) + 0.5·min(x,y).
- **Patch history:**
  - 1.29f readme, Changes v1.16f: "It's slightly easier to culturally acquire cities."
  - 1.29f readme, Changes v1.17f: "It is now possible to completely suppress a city's cultural reversion with enough military units."
- **Timing and consequences.** A city never flips on the interturn it was captured. On a flip, the units inside are lost, citizens keep their nationality, and the new owner gets a free defender.
  - tjs282, https://forums.civfanatics.com/threads/culture-flips.661194/: "a newly-captured town will _never_ flip on the first interturn."
  - Units: https://forums.civfanatics.com/threads/armies-captured-in-culture-flips.90904/ ("All units that are in the city are lost"); Strat Compilation: "if the city revolts you loose the troops in it!"
  - Flips happen at peace too. justanick, 661194: "Once at peace you cannot undo a flip by retaking the city".
  - The BIQ game option: Bluemofia, "Allow Cultural Conversions: Allow cities to convert to a rival civ culturally."

### Where our implementation (`C7Engine/C7GameData/CultureFlip.cs`) stands against the sources

| Point | Sources | CultureFlip.cs | Note |
|---|---|---|---|
| Formula shape and terms | as above | Same formula; F counts only that civ's nationals, resisters twice; T counts tiles within `MaxRankOfWorkableTiles` (21 by default, excluding the city tile) | Matches. |
| Where Cte/Cty applies | Flip Calc multiplies influence. Soren's example "16 - (4 X 2) = 8" instead scales the garrison. | Multiplies influence | Same zero point, but different slope when the ratio is far from 1. Already flagged in code. |
| Integer truncation of Cte/Cty | Fitz speculated truncation (51715-3 #78); unconfirmed | Real-valued | UNVERIFIED. |
| Distance metric | Magaha: "I would assume … pathfinding". Calculators use max+½min straight-line distance. | Straight-line max+½min | Disputed; flagged in code. |
| Propaganda as a flip factor | The Civilopedia lists "The application of propaganda by the other civilization" as making defection more likely | Not modelled (propaganda handled as its own mission) | Possibly only the propaganda mission itself, not a modifier. UNVERIFIED. |
| Capitals | No source exempts them; d is capped at 4 | Allowed, D = 8000 | UNVERIFIED either way. |
| Courthouse | Civilopedia: courthouse resists *propaganda* only | No effect on flips | Consistent with sources. |
| Order of rolls among several rivals | No source | Likeliest first | UNVERIFIED. |
| Free defender type | "The 'capturing' civ gets one free defender in the city" (https://forums.civfanatics.com/threads/cultural-conversion-question.87425/, as quoted in CultureFlip.cs; not re-fetched) | Best-defence buildable land unit | Type UNVERIFIED. |
| WLTKD and disorder | ×0.5 and ×2 | Same | Matches. Civilopedia adds that "prolonged" disorder leads to defection; no extra factor is known. |
| Fascism, Retain Culture, assimilation | Change F and the culture totals over time | Inputs only | Make sure F, the city memory and Cte/Cty follow assimilation, Fascist xenophobia and capture rules (§3.10, §3.14). |

## Disputed

- **When happy citizens are enough for WLTKD.** Civilopedia (C3C): "There must be more happy citizens than content citizens." The manual says "at least as many happy citizens as content citizens", with the example of 5 happy and 4 content celebrating; that example satisfies both readings. CFC FAQ: "at least half the citizens must be happy".
- **Fifth border threshold: 20,000 vs 100,000.** Civilopedia (C3C): "(10, 100, 1,000, 10,000, 20,000, etc.)". C3C editor help: "0, 10, 100, 1000, 10000, 100000". CFC Info Center: "increase by a factor of 10 each time". The editor's `BorderFactor^n` description supports 100,000. The 20,000 may be confused with the one-city culture victory.
- **Content base at Deity in early patches.** Skanky Burns (Dec 2001, base game ~1.16f): "On Deity it is 2 people". The C3C Civilopedia and Spoonwood give Deity 1. This may be a per-version BIQ difference, or a mistake.
- **Which governments suffer war weariness.** Manual (base game text): only "Republic or Democracy". C3C Civilopedia: "Republic, Feudalism, or Democracy". The CFC war weariness article gives effect tables for Republic and Democracy only, with no Feudalism numbers.
- **Communism draft rate.** CFC Info Center: "Communism can draft 3 citizens per turn." Firaxis fixed this to 2: 1.29f readme v1.17f, "Fixed Communism Civilopedia entry to reflect the correct draft rate of 2", and the C3C Civilopedia lists 2.
- **WLTKD: corruption and waste, or waste only.** The manual says WLTKD "lowers the levels of corruption and waste". The C3C Civilopedia says only "Waste in the city is reduced". alexman's article applies it to waste only. Tassadar, https://forums.civfanatics.com/threads/we-love-the-king-days.34249/: "it cut down shield waste like a courthouse but it doesnt cut down commerce corruption."
- **Celebration benefits by government.** The manual says "the city enjoys certain benefits, depending on your civilization's type of government", but neither it nor the Civilopedia lists any government-specific benefit. The C3C editor has no celebration-conditional government flag.
- **Content or happy faces from wonders.**
  - Hanging Gardens: the manual says "Makes 3 content citizens happy in the city where it's built and 1 in all others"; the C3C Civilopedia says "makes three unhappy citizens content … and one unhappy citizen content in all other friendly cities".
  - Mausoleum: the manual says "3 happy faces"; the Civilopedia says "makes three unhappy citizens content".
  - Cure for Cancer: the C3C wonders guide (https://civfanatics.com/civ3/strategy/empire-management/a-guide-to-great-wonders-and-how-to-use-them/) says "1 happy citizen in every city"; the Civilopedia says "one unhappy citizen content".
- **Wonder culture values.** CFC's Civilopedia page (https://civfanatics.com/civ3/civilopedia/wonders/, probably the base game) gives Great Library 6, Sistine Chapel 6, Shakespeare 6, Newton 6, Cure for Cancer 4. The Complete manual chart gives 5, 4, 5, 5, 5. This is likely a version difference.
- **How war weariness ends.** The Civilopedia says "the war weariness remains until the war stops". The CFC article measures a decay of 1/20 per peace turn, so war weariness lingers after peace ("43 turn until the war is totally forgotten").
- **Distance for flips.** Pathfinding distance (Magaha, hedged) vs straight-line max+½min distance (the calculators).
- **Where Cte/Cty applies in the flip formula** (see the table above).

## UNVERIFIED / open questions

- The number of luxury points (slider) per happy face, and whether the default Marketplace carries the "+50% Luxury Output" flag.
- The entertainer's output in luxury points (the CTZN `Luxuries` default). The Civilopedia says only "one happy face".
- The order in which the game applies faces (base, luxuries and entertainers, buildings, military police, wonders, war weariness). Forum posts give an order, but none could be sourced verbatim.
- The "reduced efficiency" of carried-over happy faces.
- How many citizens become unhappy from "mother country" aggression. Forum guesses range from "one more unhappy" to "about 50%" of the foreign nationals.
- The per-government `AssimilationChance` and resistance modifier defaults. Firaxis gives only "between a 1 and 4 percent".
- How much each garrison unit counts toward quelling resistance.
- War weariness under Feudalism ("Low"): is its level table the same as Republic's? And exactly how Police Stations and Universal Suffrage reduce it ("25%" and "1" per the CFC article).
- How long disorder must last before improvements are destroyed (beyond the 20% riot chance) or the government falls.
- The exact border shape at each radius level, and the tie-break rule for contested tiles between two cities.
- What the civ-wide culture levels (Lvl. Multiplier: 1000, 2000 … 32000) do besides the labels.
- How much AI attitude depends on cultural opinion.
- The "less than half the leader's culture" condition for culture victory.
- The flip-related items in the table above: the order of rival rolls, capitals, the free defender's type, propaganda as a flip modifier, and integer truncation of the culture ratio.
- Which nationality a drafted unit or citizen has, and whether foreign nationals are drafted or sacrificed first.
- Whether specialists count as content for the WLTKD "happy vs content" test.
