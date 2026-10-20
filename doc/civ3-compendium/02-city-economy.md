# 02 - City economy

How a Civ3 city turns tiles into food, shields and commerce, and what eats them. Scope is the base game, PTW and Conquests at their final patches (1.29f / PTW 1.27f / C3C 1.22). Scenarios, mods and the special Conquests game modes are out of scope.

## Source key

Local sources come from the installed Civ3 Complete (Steam) and are quoted exactly:

- **[PEDIA]** - Conquests Civilopedia, `Conquests/Text/Civilopedia.txt` (C3C 1.22). Cited by entry key, e.g. `#GCON_Food`.
- **[BIQ]** - default C3C rules, `Conquests/conquests.biq`, read with the repo's `QueryCiv3` reader. Values are raw BIQ fields. Shield costs are not quoted from the BIQ here: the building cost field as read (e.g. Library 8) does not match the in-game cost (80), so the reader layout for that field is unconfirmed.
- **[EDHELP]** - C3C editor help, `Conquests/Civ3ConquestsEdit.chm`, cited by topic.
- **[ED-SUPP]** - `Conquests/EditorSupplement.txt`.
- **[RM129]** - vanilla patch notes, `README.txt` (sections 1.16f to 1.29f).
- **[RMPTW]** - `civ3PTW/readme.txt`.
- **[RMC3C]** - `Conquests/readme.txt` (v1.20 and v1.22 notes).

Web sources are cited by URL. A fetch tool extracted their quotes, so they may differ slightly from the page text. Spot-check any web quote before treating its exact wording as load-bearing. The corruption formulas are transcribed from alexman's article and are worth re-checking against the page before implementation.

---

## 1. Tile yields

- **Base terrain yields (C3C defaults).** Each worked tile gives its terrain's food/shields/commerce plus improvement bonuses. BIQ values:

  | Terrain | Food | Shields | Commerce | Irrigate | Mine | Road | Cities allowed |
  |---|---|---|---|---|---|---|---|
  | Grassland | 2 | 0 | 0 | +1 | +1 | +1 | yes |
  | Plains | 1 | 1 | 0 | +1 | +1 | +1 | yes |
  | Desert | 0 | 1 | 0 | +1 | +1 | +1 | yes |
  | Flood Plain | 3 | 0 | 0 | +1 | 0 | +1 | yes |
  | Tundra | 1 | 0 | 0 | 0 | +1 | +1 | yes |
  | Hills | 1 | 1 | 0 | 0 | +2 | +1 | yes |
  | Mountains | 0 | 1 | 0 | 0 | +2 | +1 | no |
  | Forest | 1 | 2 | 0 | 0 | 0 | +1 | yes |
  | Jungle | 1 | 0 | 0 | 0 | 0 | +1 | yes |
  | Marsh | 1 | 0 | 0 | 0 | 0 | +1 | no |
  | Volcano | 0 | 3 | 0 | 0 | 0 | 0 | no |
  | Coast | 1 | 0 | 2 | - | - | - | no |
  | Sea | 1 | 0 | 1 | - | - | - | no |
  | Ocean | 0 | 0 | 0 | - | - | - | no |

  Source: [BIQ] TERR records. The Civilopedia tables agree on the land values, e.g. [PEDIA] `#GCON_Food`: "Floodplains 3 4 if irrigated. ... Hills 1 cannot be irrigated." and `#TFRM_Mine`: "Hills 1 shields, 3 if mined. ^Mountains 1 shields, 3 if mined." The water values are under UNVERIFIED (open question 1).
- **Bonus grassland** (grassland "with shield") gets +1 shield. Source: [PEDIA] `#TERR_Grassland_with_Shield`: "These have a rock outcropping to symbolize this and produce a $LINK<shield=GCON_Shields> if worked in a $LINK<city radius=GCON_Radius>." [PEDIA] `#TFRM_Mine`: "w/ outcropping 1 shields, 2 if mined."
- **Rivers** add +1 commerce to every tile they border and supply fresh water for irrigation. Source: [PEDIA] `#TERR_River`: "Any square with a river along its edge produces one extra $LINK<commerce=GCON_Commerce> if worked by citizen laborers within a $LINK<city radius=GCON_Radius>."
- **Irrigation** needs a river, a lake or an irrigated tile next to it, until Electricity. Source: [PEDIA] `#TFRM_Irrigation`: "Only squares adjacent to fresh water (river or lake) or to another irrigated square may be irrigated. After the discovery of $LINK<Electricity=TECH_Electricity>, you can irrigate a tile without proximity to fresh water."
- **Roads** give +1 commerce on all the land terrains listed (per-terrain `RoadBonus`). Source: [PEDIA] `#GCON_Commerce` table, e.g. "Grasslands 0 1 with road. ... Forests 0 1 with road." [BIQ] RoadBonus=1 for every land terrain except Volcano.
- **Railroads** add to irrigation or mine output. Neither source gives the exact amount (UNVERIFIED, open question 2). Source: [PEDIA] `#TFRM_Railroad`: "Railroads also increase the output of $LINK<irrigation=TFRM_Irrigation> or $LINK<mines=TFRM_Mine>."
- **Bonus resources** on a tile add (food/shields/commerce). From [BIQ] GOOD records (type 0 = bonus, 1 = luxury, 2 = strategic):

  | Resource | F/S/C | Resource | F/S/C | Resource | F/S/C |
  |---|---|---|---|---|---|
  | Cattle (0) | 2/1/0 | Wheat (0) | 2/0/0 | Game (0) | 2/0/0 |
  | Fish (0) | 2/0/1 | Whales (0) | 1/1/2 | Oasis (0) | 2/0/0 |
  | Gold (0) | 0/0/4 | Sugar (0) | 1/0/1 | Tobacco (0) | 0/0/1 |
  | Tropical Fruit (0) | 1/0/1 | Gems (1) | 0/0/4 | Silks (1) | 0/0/3 |
  | Ivory (1) | 0/0/2 | Incense (1) | 0/0/2 | Spices (1) | 0/0/2 |
  | Furs (1) | 0/1/1 | Dyes (1) | 0/0/1 | Wines (1) | 1/0/1 |
  | Horses (2) | 0/0/1 | Iron (2) | 0/1/0 | Saltpeter (2) | 0/0/1 |
  | Coal (2) | 0/2/1 | Oil (2) | 0/1/2 | Rubber (2) | 0/0/2 |
  | Aluminum (2) | 0/2/0 | Uranium (2) | 0/2/3 | | |

  Source: [BIQ] GOOD. Patch history: [RM129] 1.17f: "Game bonus resource now gives +2 food to its tile."
- **Harbor**: +1 food on Coast/Sea/Ocean tiles. **Offshore Platform**: +1 shield on them. **Commercial Dock** (C3C): +1 commerce on water tiles that already make 1. Source: [PEDIA] `#BLDG_Harbor`: "Coastal, sea, and ocean squares produce one extra $LINK<food=GCON_Food>." `#BLDG_Offshore_Platform`: "causes all worked coastal, sea, and ocean squares to produce one $LINK<shield=GCON_Shields>." `#BLDG_Commercial_Dock`: "produce one extra Commerce in every water square that already produces one."
- **Colossus**: +1 commerce in its city's tiles that already make at least 1. Source: [PEDIA] `#BLDG_Colossus`: "causes its city to produce one extra $LINK<Commerce=GCON_Commerce> in every square that already produces at least one."
- **Lakes**: [PEDIA] says a fresh-water lake gives 2 food ("Fresh Water Lake 2 cannot be irrigated."). [RMPTW] says lakes are not their own terrain type: "Fresh water lakes are not a terrain type. They are composed of coastal tiles surrounded by land." So a lake tile is a Coast tile flagged as fresh water. Its food is under UNVERIFIED (open question 1).
- **City radius**: tiles within 2 squares of the city that are also inside its cultural borders. Source: [PEDIA] `#GCON_Radius`: "within the city's $LINK<cultural borders=GCON_Territory>, and ^* within two squares of the city."

### Government tile modifiers

- **Despotism (and Anarchy) tile penalty**: any tile yield above 2 is cut by 1. Both governments carry the BIQ flag. Sources:
  - [PEDIA] `#GOVT_Despotism`: "any city production square which produces more than two $LINK<food=GCON_Food>, $LINK<shields=GCON_Shields>, or $LINK<commerce=GCON_Commerce> in a despotic government instead produces one less."
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "In Despotism, the standard government, all tiles that produce 3 or more food/shields/commerce will produce one less." Also: "That's why grassland (2 food), when irrigated (+1 food) in Despotism, will only produce 2 food (2 + 1 – 1 = 2)."
  - [BIQ] GOVT `TilePenalty=1` for both Anarchy and Despotism, 0 for all others.
  - The editor's wording differs (see Disputed): [EDHELP] "Flags (Government Properties)": "Standard tile penalty Under the selected government, no tile may generate more than 2 food and shields."
- **The penalty still applies during a Golden Age.** Source: CFC FAQ, https://civfanatics.com/civ3/faq/: "The bonuses from a Golden Age, however, are still subject to the Despotism tile penalty."
- **Republic and Democracy trade bonus**: +1 commerce on tiles. Sources:
  - [BIQ] `TradeBonus=1` for Republic and Democracy only.
  - CFC Civilopedia mirror, https://civfanatics.com/civ3/civilopedia/governments/ (Republic, Democracy): "Adds 1 commerce to any tile already producing at least 1".
  - [EDHELP] "Flags (Government Properties)": "Standard trade bonus Under the selected government, each tile generates an additional +1 to commerce." This wording omits the "already producing 1" condition (see Disputed).

### City center tile

- **Civ traits change the center tile.** Source: [PEDIA] `#GCON_Strengths`:
  - Agricultural: "the center city square and irrigated deserts produce one more food."
  - Commercial: "The center city squares of all cities and metros produce extra $LINK<commerce=GCON_Commerce>, and less $LINK<corruption=GCON_Corruption> is experienced."
  - Industrious: "the center city square of all cities produces extra $LINK<shields=GCON_Shields> in cities and metros."
  - Seafaring: "Each city built along the coast receives a commerce bonus in the center city square".

  C3C Agricultural detail, from https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ (Arathorn): "Provides one bonus food in the city square in all governments except despotism." and "In despotism, the bonus food only appears if the city is by fresh water."
- **A city tile normally gets a road.** It gets a railroad when the owner has Steam Power and the city is captured. Sources:
  - [RM129] 1.29f: "Changed city code so that a road/railroad is not placed under a city if a road/railroad is not allowed on the terrain type the city is built upon."
  - [RMC3C] 1.20: "Railroads will always appear under captured cities if you have Steam Power."
- **Free irrigation effect and minimum 1 shield on the center tile:** UNVERIFIED. The only sources found are a Civ2-forum thread (https://forums.civfanatics.com/threads/food-shield-trade-of-city-tile.535361/: "in a city you automatically have irrigation and roads", "if the city center does not already produce a shield, one will be produced"), so they cannot be used for Civ3.
- **Founding on forest/jungle clears it:** disputed and unverified. https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes (inca911): "When you found a city on a forest or jungle square, that square is immediately cleared to grassland." Vipsanius disputes the result terrain in the same thread.
- **Settling on a food bonus loses it.** Source: CFC City Placement (C3C), https://civfanatics.com/civ3/strategy/empire-management/city-placement/: "If you settle on food bonuses, you lose the food bonus."

---

## 2. Food, growth and city size

- **Each citizen eats 2 food per turn.** Source: [BIQ] `FoodConsumptionPerCitizen=2`. [PEDIA] `#GCON_Food`: "[Every citizen must eat two food per turn or die.]"
- **Growth**: surplus food fills the food box. When the box is full it empties and the city gains 1 citizen. Source: [PEDIA] `#GCON_Food`: "When it is full, the storage is emptied and the city grows, adding one citizen to its population."
- **Food box sizes: 20 for sizes 1-6 (town), 40 for 7-12 (city), 60 for 13+ (metropolis).** No editor field exists for these, so they appear to be hard-coded.
  - Realms Beyond micromanagement guide (1.17f), http://www.realmsbeyond.net/civ/mmguide.html: "At and below six food boxes are 20 squares, while they're 40 from size 7-12."
  - https://forums.civfanatics.com/threads/granaries-dont-work.53078/ (MadScot, 1.29f): "A size 5 city needs 20 food to grow" and a size 7 city "needs a full 40 to gro to size 8".
  - https://forums.civfanatics.com/threads/pottery-is-underrated.683382/ (Spoonwood): "But, the box changes from 40 total food to 60 total food iirc." and "that test confirms that changing from size 12 to 13 keeps 20 food in the box".
  - https://forums.civfanatics.com/threads/is-there-a-way-to-manipulate-the-number-of-accumulated-food-required-for-city-growth.338390/ (Czulu): "However i cant find that option in civilization 3 conquests editor."
- **AI cities' boxes are scaled by the difficulty cost factor.** The human factor is 10. AI factors are Chieftain 20, Warlord 12, Regent 10, Monarch 9, Emperor 8, Demigod 7, Deity 6, Sid 4 ([BIQ] DIFF CostFactor). Source: [EDHELP] "Cost Factor (AI Bonuses)": "Determines the cost factor that is applied to AI growth, shields, and research. The player's cost factor is always 10."
- **Granary**: when the city grows, only half the box is used up, so it keeps half its food. The Pyramids give a Granary in every city on the continent (C3C). Sources:
  - [PEDIA] `#BLDG_Granary`: "effectively doubles the rate of a city's growth by causing only half of a city's $LINK<food store=GCON_Food> to be depleted when the city grows."
  - [BIQ] Granary flag `DoublesCityGrowthRate`.
  - [PEDIA] `#BLDG_Pyramids`: "Puts a $LINK<Granary=BLDG_Granary> in every city on the same continent."
  - https://civfanatics.com/civ3/strategy/empire-management/the-case-for-food-or-how-to-become-economically-dominant/: "A town size 6 or below has a food box of 10 when the Granary is full."
- **Granary at the 6→7 and 12→13 steps:** disputed (see Disputed). The best-tested statement is that the granary keeps half of the *previous* box (10 food at 6→7, 20 food at 12→13) inside a box that has grown larger. Source: https://forums.civfanatics.com/threads/pottery-is-underrated.683382/ (Spoonwood): "The granary keeps half of the food in the box that it had at the previous size." Patch history: [RM129] 1.17f: "Fixed infinite city growth exploit (involving granaries and size 6 cities)."
- **Starvation**: if production plus stored food cannot feed everyone, one citizen starves. The population number shows red while food is negative. Sources:
  - [PEDIA] `#GCON_Food`: "If a city cannot feed its population either from new production or stored food, one of its citizens {starves} and disappears."
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "if it is red, the city is losing food and citizens are starving."
  - Resisters do not eat. [RM129] 1.21f: "Corrected Resistance Civilopedia chapter to reflect resisting citizens do not consume food."
- **Civil disorder** stops food storage, production and revenue. Source: [PEDIA] `#GCON_Disorder`: "^* $LINK<Production=GCON_Shields> ceases, ^* No revenue comes in from $LINK<commerce=GCON_Commerce>, and ^* No excess $LINK<food=GCON_Food> is stored."
- **Size limits**: a town (size 1-6) needs an Aqueduct to pass 6, unless it is next to fresh water. A city (7-12) needs a Hospital to pass 12, and the Hospital needs size 7 or more. Shakespeare's Theatre also has the "allows level 3" flag. No cap is listed for metropolises. Sources:
  - [BIQ] RULE `MaximumLevel1CitySize=6`, `MaximumLevel2CitySize=12`, level names Town/City/Metropolis; Aqueduct `AllowsCitySize2`; Hospital and Shakespeare's Theatre `AllowsCitySize3`.
  - [PEDIA] `#BLDG_Aqueduct`: "A city with an [Aqueduct] can grow beyond population six. ^Note that a city adjacent to fresh water never requires an aqueduct."
  - [PEDIA] `#BLDG_Hospital`: "A city with a [Hospital] can grow beyond population 12. You must have a population of 7 or higher to build a hospital."
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "need a Hospital (available with Sanitation) to grow past size 12."
- **No food carryover** beyond the box. Source: CFC FAQ, https://civfanatics.com/civ3/faq/: "No – there is no shield or food carryover for cities, just like there is no beaker carryover when researching a technology."

---

## 3. Shields, production and switching

- **Shield cost** = BIQ cost × cost factor (human 10, AI per difficulty). Source: [EDHELP] "Cost (Improvement Properties)": "Note that this value is multiplied by a cost factor which determines how many shields it actually costs to build the selected improvement/wonder. The human players cost factor is always 10."
- **Production multipliers are a percentage of base shields and add together.** BIQ `Production` is in 25% steps: Factory 2 (+50%), Manufacturing Plant 2, Coal/Hydro/Solar 2, Nuclear 4 (+100%), Iron Works 4. One power plant per city. Sources:
  - [EDHELP] "Production (Improvement Properties)": "in multiples of 25% (so if the value is set at 2, the building gives +50% total shield production)."
  - [PEDIA] `#BLDG_Factory`: "increases $LINK<shield production=GCON_Shields> in its city by 50%."
  - https://civfanatics.com/civ3/strategy/game-mechanics/everything-you-wanted-to-know-about-power-plants/ (PTW 1.21f): Nuclear "adds a 100% production bonus to base shields". Its example: 32 base + 16 (factory) + 16 (plant) + 16 (Manufacturing Plant) = 80. Hoover Dam gives a Hydro Plant to every city on the continent ([PEDIA] `#BLDG_Hoover_Dam`).
- **Waste** comes off shields first (see section 5).
- **No shield overflow.** Surplus shields beyond the item's cost are lost. Sources:
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "If you have extra shields being produced then are needed, they'll just go to waste."
  - https://forums.civfanatics.com/threads/short-rushing.119305 (SJ Frank): "then you end up wasting 20 shields of production on the next turn."
- **Wealth** turns shields into gold. Disputed rate (see Disputed). Sources:
  - [PEDIA] `#BLDG_Wealth`: "[Wealth] causes shields to be converted to gold at a ratio of four to one. ^When your civilization learns $LINK<Economics=TECH_Economics> the ratio is reduced to two to one."
  - [BIQ] `ShieldsCostPerGold=4`; Wealth has the `Capitalization` flag.
- **Mobilization** (Nationalism) gives +1 shield on tiles making at least 1 while building military units, and only military units and improvements can be built. Source: [PEDIA] `#GCON_Mobilization`: "During mobilization a city building a military unit produces one extra $LINK<shield=GCON_Shields> in every square that is already producing at least one."
- **Clearing a forest** sends 10 shields to the nearest city's build, once per tile per game, and never to a wonder. Sources:
  - [BIQ] `ForestValueInShields=10`.
  - [RM129] 1.16f: "Shield bonus from clearing forest can only be received once per game."
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "When you chop them down, 10 shields will be added to the current thing being built in the nearest city (provided it isn't a wonder)".
- **Civil Engineer** (C3C, Replaceable Parts) gives +2 shields, for improvements and wonders only. Sources:
  - [BIQ] CTZN `Construction=2`.
  - [EDHELP] "Construction Bonus (Citizen Properties)": "Applies only while building improvements and wonders (never to units)."
- **Production switching penalty between categories (unit / improvement / wonder): UNVERIFIED.** No Civ3 source confirming a percentage penalty was found (open question 3). The rules that are confirmed:
  - **A hurried item cannot be switched to a wonder.** CFC FAQ, https://civfanatics.com/civ3/faq/ ("Why can't I build a wonder" entry). The page states it as: if you hurried production on an item, the game "won't let you" switch it to a wonder (wording as extracted).
  - **Shields from disbanding cannot go into a wonder.** https://forums.civfanatics.com/threads/disbanding-units-obtaining-extra-shields.118679 (Renata): "you won't be able to switch to a wonder until the current build completes."
  - **Unit-to-unit switches keep the shields.** https://forums.civfanatics.com/threads/production-loss-when-switching-from-building-one-unit-to-another.137918/: "Its not lost, but saved until you switch back to the previous unit." Use with caution, because the thread's game version is unclear.
  - **C3C 1.22 rush-then-switch exploit with Civil Engineers.** https://apolyton.net/forum/civilization-series/civilization-iii/143965-v1-29-gold-rush-bug-exploit-spolier (pvzh): "It happens if civil engeneers were used, these extra shileds generated for every rush/switch cycle."
- **Disbanding a unit in a city** adds 1/4 of its shield cost, rounded down. This does not work for wonders, does nothing outside a city, and is not subject to waste. Sources:
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "The number of shields added is 1/4 the shield cost of the unit, rounded down." and "If you disband a unit on any tile that is not a city, nothing happens: the unit just disappears."
  - https://forums.civfanatics.com/threads/disbanding-units-obtaining-extra-shields.118679 (Ginger_Ale): "none are lost due to corruption." (Zakharov): "all of the shields are added to the current build (except wonders)."
  - https://forums.civfanatics.com/threads/disbanding-question.18365 (BillChin): "I believe it is 25% of production cost."

---

## 4. Hurrying production

- **The hurry method is set per government.** Paid labor (gold): Monarchy, Republic, Democracy. Forced labor (population): Despotism, Communism, Feudalism, Fascism. Anarchy has none. Sources:
  - [BIQ] GOVT `Hurrying` (0 none: Anarchy; 1 forced: Despotism, Communism, Fascism, Feudalism; 2 paid: Monarchy, Republic, Democracy).
  - [PEDIA] `#GCON_Hurry_Production`: "Under $LINK<Despotism=GOVT_Despotism>, $LINK<Communism=GOVT_Communism>, $LINK<Feudalism=GOVT_Feudalism>, or $LINK<Fascism=GOVT_Fascism> you hurry production in your cities by forced labor." Also: "Under $LINK<Monarchy=GOVT_Monarchy>, $LINK<Republic=GOVT_Republic>, and $LINK<Democracy=GOVT_Democracy> you can pay overtime and hire temporary workers to complete a project." Also: "You have [no] production if your civilization is in $LINK<Anarchy=GOVT_Anarchy>."
- **Gold cost = 4 gold × remaining shields, doubled if the box is empty.** Sources:
  - [BIQ] `ShieldValueInGold=4`.
  - [EDHELP] "Shield Value in Gold": "Determines how many gold pieces it costs to buy each shield when hurrying production of a city improvement. Default is 4 gold."
  - https://forums.civfanatics.com/threads/save-50-on-rush-buying.13252/ (tetley): "The price for hurrying is 4 gold/shield, if you already have a few shields produced. Otherwise it doubles."
  - https://forums.civfanatics.com/threads/whipping-to-rush-vs-rushing-with-gold.257616/ (swift_like_wind, C3C 1.22): "the shield value in gold is doubled from the editor setting if no work has been done on that build yet". Its example: Temple "240 to rush with no shields already there which is 8 gold per shield", "116 to rush, 4 gold per shield" with 1 shield in.
  - https://forums.civfanatics.com/threads/hurry-production.95782/ (ManOfMiracles): "4 gold per shield still needed".
  - Whether units cost more per shield than improvements is UNVERIFIED (open question 4).
- **Population rush ("whip")**: each citizen is worth 20 shields. The number of citizens is ceil(remaining / 20), doubled if the box is empty. You can never sacrifice more than half the city at once. Each sacrificed citizen causes 20 turns of unhappiness. Sources:
  - [BIQ] `CitizenValueInShields=20`, `TurnPenaltyForEachHurrySacrifice=20`.
  - https://forums.civfanatics.com/threads/how-is-pop-rush-done-now.128518/ (DBear, C3C 1.22): "Pop rush costs 1 population per 20 shields or fraction thereof." and "you cannot pop rush more than half your population at the same time."
  - https://forums.civfanatics.com/threads/whipping-to-rush-vs-rushing-with-gold.257616/ (PaperBeetle): "when you whip on an empty box, the _cost_ is doubled, just as with cash rushing."
  - Patch history, [RM129] 1.21f: "The first citizen is now worth 20 shields instead of 40 shields, just like all the other citizens." and "Draft anger and hurrying unhappiness reduced to 20 turns." (1.17f had raised it to 40.)
- **Wonders cannot be hurried with gold or population.** Sources:
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "you can't rush a wonder by sacrificing population (Despotism, Communism, Fascism), by rushing it with gold (other governments)".
  - Great Leaders differ by type (see Disputed). [PEDIA] `#GCON_Leaders`: a Military Leader can "$LINK<hurry=GCON_Hurry_Production> production of an Improvement or Small Wonder", and a Scientific Leader can hurry "any [improvement] (including a Great Wonder)".
  - C3C changes list, https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/: "Military Great Leaders (MGLs) can no longer rush Great Wonders."
- **Draft** (Nationalism): cities of size 7 or more turn citizens into conscript defenders. The limit is 1 per turn in Republic/Democracy and 2 per turn in Despotism, Monarchy, Communism, Fascism and Feudalism. Anarchy cannot draft. Each draft causes 20 turns of unhappiness. Sources:
  - [PEDIA] `#GCON_Draft`: "A city in a $LINK<Democracy=GOVT_Democracy> or $LINK<Republic=GOVT_Republic> can draft [one] citizen per turn." Also: "You may only draft citizens from a city with ^a population of 7 or higher."
  - [BIQ] GOVT `DraftLimit` (Anarchy 0; Despotism, Monarchy, Communism, Fascism, Feudalism 2; Republic, Democracy 1); `TurnPenaltyForEachDraftedCitizen=20`.
  - https://civfanatics.com/civ3/strategy/empire-management/how-to-pop-rush-in-republicdemocracy/: "1 citizen unhappy for 20 turns for each citizen drafted/whipped."
- **Partial ("short") rushing**: rush part of the cost by switching to a cheaper item, then switch back. Source: https://forums.civfanatics.com/threads/short-rushing.119305 (WackenOpenAir): "short rushing is rushing part of an object by swiching to something else and then back."

---

## 5. Corruption and waste

Corruption takes commerce from worked land tiles. Waste takes shields. Both use the same model unless noted.

- **Causes**: distance from the capital, number of cities, and government. Source: [PEDIA] `#GCON_Corruption`: "In general, the farther a city is from its capital city, the more corruption and waste it experiences. Also the more cities in your empire, the higher rates of both, overall."
- **C3C formula** (alexman). Source: https://civfanatics.com/civ3/strategy/game-mechanics/everything-about-corruption-c3c-edition/ : "The total corruption in each city is equal to the sum of two independent components: distance corruption, and rank corruption."
  - Distance to the nearest palace (Palace or FP, see Disputed): `d = max(x,y) + min(x,y)/2`, with integer division rounded down. x and y are the tile offsets along the map's two diagonal axes.
  - `MaxD = (MapW + MapH) / 4`.
  - Communal governments use `d = MaxD / 4` for every city.
  - `da = 0.5^Ni * min(Gd * t * d, MaxD)`, then `Cd = da / MaxD`.
  - `Nopt = max(OCN * (L/100 * (1 + c + Gr + Gp*Nwe) + 0.25*Ni), 1)`.
  - Rank R: "all cities of an empire are ranked in order of distance to the capital, starting at zero for the capital itself." Ties go "in order of founding" and then database order. Communal: "all cities have the same rank, which is half the total number of cities in the empire, rounded down."
  - `Cr = R / (2*Nopt)` if `R < Nopt`, else `(2*R - Nopt) / (2*Nopt)`.
  - Cap: `Cmax = max(0.9 - (0.1*Ni + 0.7*Nwc), 0)`. Total = min(Cd + Cr, Cmax). The article states the sum and the cap separately; combining them with min() is my reading.
  - Corrupted commerce = total × raw commerce from worked land tiles, "rounded to the closest integer".
  - Factors:

    | Symbol | Meaning | Values |
    |---|---|---|
    | Gd | government distance factor | Rampant (Despotism) 3/2; Minimal (Democracy) 3/4; others 1 |
    | t | trade network | 1 if connected to the capital by road, harbor or airport; 5/4 if not |
    | Gr | government rank factor | Minimal or Nuisance 0.1; Communal 2; others 0 |
    | Gp | per-wonder OCN factor | non-communal 3/8; communal 3 |
    | c | Commercial trait | 0.25, else 0 |
    | Ni | Courthouse / Police Station in the city | each halves distance corruption, raises Nopt, lowers the cap by 0.1 |
    | Nwe | active corruption wonders in the empire (FP, SPHQ) | count |
    | Nwc | corruption wonder in this city | lowers the cap by 0.7 |
    | OCN | optimal cities for the map size | Tiny 14, Small 17, Standard 20, Large 28, Huge 36 ([BIQ] WSIZ) |
    | L | % of optimal cities for the difficulty | Chieftain 100, Warlord 95, Regent 90, Monarch 85, Emperor 80, Demigod 70, Deity 60, Sid 50 ([BIQ] DIFF) |
  - **Waste only**: during WLTKD, "divide da by 2" and "add OCN/4 to Nopt".
  - **Policemen** remove 1 corrupted commerce and 1 wasted shield each, before the cap is applied. Source: [EDHELP] "Corruption Bonus (Citizen Properties)": "this citizen makes 1 corrupted commerce and 1 corrupted shield become uncorrupted." [BIQ] Policeman `Corruption=1` (Nationalism).
- **Government corruption levels** ([BIQ] GOVT `Corruption`; [PEDIA] GOVT entries):

  | Government | Level | Hurry | Draft | Tile penalty | Trade bonus | Building maintenance |
  |---|---|---|---|---|---|---|
  | Anarchy | Catastrophic | none | 0 | yes | no | none |
  | Despotism | Rampant | forced | 2 | yes | no | yes |
  | Monarchy | Problematic | gold | 2 | no | no | yes |
  | Feudalism | Problematic | forced | 2 | no | no | yes |
  | Communism | Communal | forced | 2 | no | no | yes |
  | Fascism | Nuisance | forced | 2 | no | no | yes |
  | Republic | Nuisance | gold | 1 | no | yes | yes |
  | Democracy | Minimal | gold | 1 | no | yes | yes |

  Source: [EDHELP] "Corruption and Waste (Government Properties)": "Minimal is the lowest possible value; communal is the highest."
- **The capital has zero corruption.** Source: [PEDIA] `#BLDG_Palace`: "It eliminates $LINK<corruption/waste=GCON_Corruption> in the capital, and decreases it in nearby cities." https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/ (alexman): "The Palace always has zero corruption".
- **The difficulty corruption modifier does not apply in some cases.** It is 100% at every level by default, and it is skipped for catastrophic governments, cities in disorder and the capital. Source: [EDHELP] "Corruption Modifier (Difficulty Level)": "The Corruption Modifier has no effect on players whose government experiences catastrophic corruption, cities in civil disorder, or capital cities. In these situations, corruption is automatically set and no formula is calculated." [BIQ] DIFF `CorruptionPercentage=100` at every level. In Anarchy no taxes are collected ([PEDIA] `#GOVT_Anarchy`: "corruption and waste are so high that no production occurs and no taxes are collected").
- **Forbidden Palace**: a Small Wonder, one per civ. It needs OCN/2 cities and raises OCN (via Nwe). Sources:
  - [EDHELP] "Reduces Corruption (Small Wonder Flag)": "the selected Small Wonder acts as a second "capitol city" ... The number of cities a player must have to enable construction of any wonder with this flag is equal to the Optimal Number of Cities divided by 2."
  - [PEDIA] `#BLDG_Forbidden_Palace`: "Requires: The civilization must have at least ten cities (on a standard size map)".
  - [RM129] 1.17f: "Forbidden Palace cost decreased to 200."
  - [RMC3C] 1.20: "Corruption calculation error that resulted in negative effects from the Forbidden Palace is now correct."
- **Secret Police HQ** (C3C): Communism only. It works like the FP and stops working if you leave Communism. Source: [PEDIA] `#BLDG_Secret_Police_HQ`: "Only civilizations under Communist government can build the Secret Police HQ. Changing from Communism will cause the Secret Police HQ bonuses to stop." [RMC3C] 1.20: "Secret Police HQ no longer gives corruption OR OCN bonus when player leaves Communism."
- **Courthouse** (maintenance 1) and **Police Station** (maintenance 2): reduce corruption in their own city. Source: [PEDIA] `#BLDG_Courthouse`: "decreases $LINK<corruption=GCON_Corruption> in its city and makes the city more resistant to propaganda." Patch history:
  - [RM129] 1.16f: "Police Stations now reduce corruption in the city they are built in."
  - [RM129] 1.21f: "Increased corruption/waste fighting ability of courthouses and police stations."
  - [RM129] 1.16f: "Corruption calculations for Communism are now flat for the number-of-cities factor."
- **Vanilla/PTW corruption formula**: the article only says "It's slightly different than how it used to work in PTW and vanilla Civ3." The pre-C3C formula is UNVERIFIED (open question 5).

---

## 6. Commerce, the trade network and the sliders

- **Commerce sources**: tiles (terrain, road, river, resources), the government trade bonus, Golden Age, Colossus, Commercial Dock, and trait center-tile bonuses (sections 1 and 9).
- **Order of operations**: corruption is removed from raw commerce. The rest is split by the sliders, then each share gets its own multipliers. Bonuses add together (a percentage of the base, not compounded) and are rounded down. Specialists are not multiplied. Source: https://civfanatics.com/civ3/strategy/empire-management/multiplier-buildings-a-practical-primer/: "Raw commerce has corruption taken off the top, with the remainder being allocated to science and luxury spending." Also: "Note that the increase is not compound but simple as it adds to the base rate." Also: "specialist output is never affected by city improvements".
- **Multiplier buildings** (+50% each, [BIQ] flags): tax = Marketplace, Bank, Stock Exchange; science = Library, University, Research Lab. Marketplace also raises luxury output. Source: [PEDIA] `#BLDG_Marketplace`: "increases tax revenue allocated to the $LINK<treasury=GCON_Commerce> by 50%". The Marketplace's luxury-resource table is in [PEDIA] (1/2/4/6/9/12/16/20 happy faces for 1-8 luxuries).
- **Sliders move in 10% steps.** Source: https://civfanatics.com/civ3/strategy/empire-management/effective-commericial-management/ ("intervals of 10").
- **Rate cap**: C3C defaults have no cap. Every government's `ScienceRateCap` is 10 (100%). Vanilla and PTW caps are UNVERIFIED (open question 6). Source: [EDHELP] "Rate Cap (Government Properties)": "Determines the maximum science funding (0-10) that the selected government can engage in. Default is always 10 (100%)." [BIQ] all 8 GOVT `ScienceRateCap=10`.
- **Specialists**: no specialist output is reduced by corruption. Sources:
  - [BIQ] CTZN values: Entertainer 1 luxury, Tax Collector 2 gold, Scientist 3 beakers, Policeman 1 corruption, Civil Engineer 2 shields.
  - https://civfanatics.com/civ3/strategy/empire-management/the-role-of-the-specialist-citizen/: in 1.29 and PTW "three tax collectors return three gold to the treasury" (so 1 each), and in Conquests "the scientist's beaker output rose to three". Also: "Corruption and waste do not reduce the specialist's output".
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "The taxman provides 2 gold per turn (only 1 in Vanilla and Play the World)."
- **Wall Street** pays 5% interest on the treasury, capped at 50 gold per turn. Source: [PEDIA] `#BLDG_Wall_Street`: "The $LINK<treasury=GCON_Treasury> begins earning 5% interest every turn with a maximum cap of 50 gold per turn." It needs 5 Stock Exchanges.
- **Trade network / connected cities.** Connected cities share every resource. Source: [PEDIA] `#GCON_Trade`: "Two cities are connected if any one of the following are true: ^* there is a $LINK<road=GCON_Worker_Jobs> or $LINK<railroad=GCON_Worker_Jobs> running between them, ^* there are $LINK<harbors=BLDG_Harbor> in both cities and a visible water route between them, ^* or both cities contain an $LINK<airport=BLDG_Airport>." Conditions:
  - **Water routes** need the right sea tech for every tile crossed. [PEDIA] `#GCON_Trade`: "your civilization must be able to safely traverse ^every square of the route and this depends upon having $LINK<Map Making=TECH_Map_Making>, $LINK<Navigation=TECH_Navigation>, and/or $LINK<Magnetism=TECH_Magnetism>."
  - **Enemy territory and blockades break the link.** [PEDIA] `#GCON_Trade`: "two cities are not connected if the only road/rail that connects them passes through enemy territory, or if a harbor is blockaded by enemy naval forces."
  - **Foreign trade** needs both capitals connected. [PEDIA] `#GCON_Trade`: "Excess resources may be traded to other civilizations if the capital cities of both civilizations are connected."
  - **Colonies** outside your borders need a road to a city. [PEDIA] `#GCON_Colony`: "you need a road connecting the colony to one of your cities to get the benefits of the resource."
  - **Corruption**: being connected to the capital lowers distance corruption (factor t, section 5). Source: [PEDIA] `#GCON_Corruption`: "Being attached to your capital via road, harbor, or airport also reduces corruption and waste."
- **No per-route trade commerce.** No source found describes Civ2-style trade-route income. The [PEDIA] `#GCON_Trade` entry covers only resource sharing and connection rules. Treat "trade routes" as connections only. The absence of route income is UNVERIFIED.
- **Luxury resources**: each connected luxury makes one citizen happy (Marketplace scales this). Source: CFC FAQ, https://civfanatics.com/civ3/faq/: "For each luxury resource connected to your cities, you'll make one citizen happy."

---

## 7. Building maintenance

- **Maintenance per turn** (C3C, [BIQ] BLDG `MaintenanceCost`):
  - 0: Palace, Walls, Coastal Fortress, Wealth, all wonders.
  - 1: Barracks, Granary, Temple, Marketplace, Library, Courthouse, Aqueduct, Bank, Harbor, Civil Defense.
  - 2: Cathedral, University, Colosseum, Recycling Center, Hospital, Research Lab, Mass Transit, SAM Battery, Airport, Police Station, Commercial Dock.
  - 3: Factory, Manufacturing Plant, Coal/Hydro/Nuclear/Solar Plant, Offshore Platform, Stock Exchange.

  The [PEDIA] `#GCON_Maintenance` lists match these. Patch history: [RM129] 1.17f "Coastal Fortress maintenance reduced to 0."; [RMPTW] 1.01f "The maintenance cost of the Stock Exchange is increased from 2 to 3."
- **No maintenance in Anarchy.** Source: [BIQ] Anarchy `RequiresMaintenance=0`. [PEDIA] `#GOVT_Anarchy`: "There is no improvement maintenance when a civilization is in Anarchy." [EDHELP] "Flags (Government Properties)": "Requires maintenance Under the selected government, all city improvements costs must be paid for".
- **Smith's Trading Company** pays upkeep for trade buildings. The list changed by version (see Disputed). Source: [PEDIA] `#BLDG_Trading_Company` (C3C): "Pays $LINK<maintenance=GCON_Maintenance> costs for all trade-based city improvements ($LINK<Harbors=BLDG_Harbor>, $LINK<Marketplaces=BLDG_Marketplace>, $LINK<Banks=BLDG_Bank>, $LINK<Airports=BLDG_Airport>, $LINK<Commerical Dock=BLDG_Commercial_Dock> and $LINK<Stock Exchanges=BLDG_Stock_Exchange>)."
- **Buildings granted by wonders** (e.g. the Pyramids' granaries) are tied to a maintenance fix. Their exact upkeep is UNVERIFIED. Source: [RM129] 1.21f: "Fixed bug involving maintenance costs and granaries/Pyramids, barracks/Art of War, etc."
- **Unit upkeep** in gold is a separate cost (free units per town/city/metro per government; Republic 2 gold and Feudalism 3 gold per extra unit). Source: [PEDIA] `#GCON_Unit_Support` and GOVT entries. It is covered in the units section of this compendium.
- **What happens when the treasury cannot pay maintenance** (forced building sales?) is UNVERIFIED (open question 7).

---

## 8. Pollution and global warming

- **Causes**: some buildings, city population, nuclear meltdowns, volcanic eruptions and nuclear explosions. Source: [PEDIA] `#GCON_Pollution`: "It is caused by certain city improvements, the meltdown of a $LINK<Nuclear Plant=BLDG_Nuclear_Plant>, $LINK<volcanic eruptions=GCON_Volcanic_Eruptions>, nuclear explosions, and large city populations."
- **Building pollution** ([BIQ] BLDG `Pollution`): Factory 2, Manufacturing Plant 2, Coal Plant 2, Offshore Platform 2, Iron Works 4, Research Lab 1, Airport 1, Commercial Dock 1. Hydro, Nuclear and Solar plants are 0.
- **Population pollution**: 1 point per citizen above 12. Source: CFC FAQ, https://civfanatics.com/civ3/faq/: "For each population point you have above 12, you will get 1 pollution 'point'". https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/54339-pollution (roadcage) adds a possible tech dependence: "The threshold appears to be size 12 during the industrial age."
- **Mitigation**: Mass Transit cuts population pollution to 1, and Recycling Center cuts building pollution to 1. Sources:
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "building Mass Transit (which caps population pollution at 1)".
  - https://apolyton.net/forum/miscellaneous/archives/civ3-play-the-world/82366-how-does-pollution-occur (Catt): "Recycling Centers (available with Recycling) eliminates all but one of the pollution triangles caused by city improvements." and "a city could be size 40, with every possible polluting improvement and still have only 2 pollution triangles."
  - [BIQ] flags: Mass Transit `RemovesPopulationPollution`, Recycling Center `ReducesBuildingPollution`.
- **Effect**: each turn, the pollution points ("triangles") give a chance that a random tile in the radius becomes polluted. The exact percentage is UNVERIFIED (open question 8). Source: same Apolyton thread (Catt): "The pollution triangles represent the probability that a random city radius tile will be hit by pollution each turn".
- **Cleanup**: Workers use "Clear Damage". Source: [PEDIA] `#GCON_Worker_Jobs`: "{Clear Damage (Shift-C)} ^Removes $LINK<pollution=GCON_Pollution>, including fallout from a $LINK<volcanic eruption=GCON_Volcanic_Eruptions>, and $LINK<craters=GCON_Craters>."
- **What a polluted tile yields** is UNVERIFIED (open question 8).
- **Terrain decay** from long-standing pollution. Per-terrain `PollutionEffect` in [BIQ]: Grassland→Plains, Plains→Desert. Forest and Jungle point to index 14, read as "Base Terrain Type". Marsh points to index 11, which is Coast in the BIQ terrain order. This is how the values read; check it. Source: [EDHELP] "Pollution Effect (Terrain Properties)": "Selects the terrain type (if any) that a tile of the selected terrain type will become if pollution stays on that tile for an extended length of time. For example, grassland would become plains, and plains would become desert."
- **Global warming**: world pollution (all city pollution plus nukes plus meltdowns) can trigger terrain loss. The trigger threshold and the number of tiles hit are UNVERIFIED (open question 9). Sources:
  - [PEDIA] `#GCON_Pollution`: "If world pollution reaches sufficiently critical levels, there is a chance that global warming will occur. It can cause forests and jungles to disappear, and grasslands to become plains, and plains to become desert. World pollution is the sum of all city pollution, nuclear explosions, and meltdowns."
  - [RM129] 1.16f: "The Sun Icon on the Info Box represents the amount of global warming affecting the game. If the sun does not display, there is insufficient global warming to affect the terrain."

---

## 9. Golden Ages

- **Length**: 20 turns (editable; 0 disables Golden Ages). Source: [BIQ] `GoldenAgeDuration=20`. [EDHELP] "Duration (Golden Age General Setting)": "Setting this to zero will disable the triggering of any Golden Age. Default duration is 20 turns."
- **Effect**: +1 shield and/or +1 commerce on worked tiles already producing at least 1. Source: [PEDIA] `#GCON_Golden_Age`: "every citizen laborer in the empire is more productive, producing one extra $LINK<shield=GCON_Shields> and/or $LINK<commerce=GCON_Commerce> if already producing at least one."
- **The Despotism penalty still applies** (section 1).
- **Triggers**:
  - **Unique unit victory.** The civ's unique unit wins a battle against another civ; barbarians don't count. [PEDIA] `#GCON_Golden_Age`: "Victories versus barbarians will not initiate a Golden Age in this way."
  - **Wonders.** The civ builds Great Wonder(s) covering *both* of its traits. [PEDIA] `#GCON_Golden_Age`: "If a civilization builds a Great Wonder (or Wonders) that corresponds to [both] its unique $LINK<strengths=GCON_Strengths>, its Golden Age can be triggered." Each wonder's Civilopedia entry lists the traits it counts for (e.g. Pyramids: "Industrious, Religious, and Agricultural"). [RM129] (1.16f additions): "If you build sufficient wonders that match your civilization themes, a Golden Age will trigger."
- **One Golden Age per civ per game.** Source: CFC FAQ, https://civfanatics.com/civ3/faq/: "A civilization can only have 1 in a game."
- **Age of Science** is separate: a Scientific Leader starts 20 turns of +25% research. Source: [PEDIA] `#GCON_Age_of_Science`: "a 20-turn period in which the $LINK<scientific research=GCON_Research> of each city in that civilization's empire is 25% more productive."

---

## 10. We Love the King Day (economic effects)

- **Requirements**: not starving, no resisters, no unhappy citizens, more happy than content citizens, and size at least 6. Source: [PEDIA] `#GCON_We_Love_The_King_Day`: "The city cannot be starving, ... The city cannot have any unhappy citizens, ^ There must be more happy citizens than content citizens, and ^ The city must be large enough (typically size 6)." [BIQ] `MinimumPopulationForWeLoveTheKing=6`.
- **Economic benefit**: waste is reduced (formula in section 5). Source: [PEDIA] `#GCON_We_Love_The_King_Day`: "Waste in the city is reduced". [RM129] 1.21f: "Increased waste fighting ability of We Love The King Day."
- **Whether WLTKD lifts the Despotism tile penalty** (as in Civ2) is UNVERIFIED (open question 10).

---

## 11. Founding, joining, razing and abandoning cities

- **Settler**: 30 shields, costs 2 population. It founds a city, or joins one for +2 population. Source: [BIQ] PRTO Settler `ShieldCost=30`, `PopulationCost=2`. [PEDIA] `#PRTO_Settler`: "A city that builds a [settler] loses two citizens from its population." Also: "The settler disappears and the city's population gains two citizens."
- **Worker**: 10 shields, costs 1 population. It joins a city for +1 population. Source: [BIQ] Worker `PopulationCost=1`. [PEDIA] `#PRTO_Worker`: "A city that builds a [worker] loses one citizen from its population." Also: "The worker disappears and the city's population gains one citizen."
- **Joining in general**: joining adds the unit's population cost. Workers cannot join a starving city. Sources:
  - [EDHELP] "Join City": "The population cost of the unit will then be added the city population, and the unit will disappear."
  - [RMPTW] 1.01f: "Workers can no longer join starving cities."
  - Whether a join can push a city past its Aqueduct or Hospital cap is UNVERIFIED (open question 11).
- **Founding rules** (terrain): cities can be built on Grassland, Plains, Desert, Flood Plain, Tundra, Hills, Forest and Jungle. They cannot be built on Mountains, Marsh, Volcano or water. Source: [BIQ] TERR `AllowCities`.
- **Minimum distance between cities**: UNVERIFIED (open question 12). The C3C city-placement article treats "CxCxC" (one tile between cities) as the tightest pattern but states no rule. Source: https://civfanatics.com/civ3/strategy/empire-management/city-placement/: infinite sprawl is "CxCxC".
- **Maximum number of cities**: 512. Source: [RM129] 1.16f: "The maximum number of cities permitted in a game is 512, increased from 256."
- **Abandoning**: available from the right-click menu since 1.21f. You can never abandon your last city. Sources:
  - [RM129] 1.21f: "Added "Abandon City" to right-click menu."
  - [RMPTW]: "Fixed a bug where the user could abandon all of their cities if they had more than one."
  - Whether building a Settler at size 1-2 can empty a city is UNVERIFIED (open question 13).
- **Razing**: capturing a size-1 city with no culture razes it automatically. A razed Great Wonder is lost for good. Sources:
  - [RM129] 1.16f: "Capturing a population 1 city that has no culture will automatically raze it."
  - CFC FAQ, https://civfanatics.com/civ3/faq/: "If that wonder is razed, no other civilization can ever build it again; it is lost forever."
  - Other raze rules (when the raze option is offered, what happens to population and buildings) are UNVERIFIED (open question 13).

---

## 12. Capitals and palaces

- **One Palace per civ.** It marks the capital and has the "Center of Empire" flag. Source: [PEDIA] `#BLDG_Palace`: "Each civilization can have only one palace at a time." [EDHELP] "Center of Empire": "the center of the empire is set to the city in which the improvement is built." Building it elsewhere moves the capital. The exact handling of the old Palace (removed vs sold) is UNVERIFIED.
- **Palace cost (since 1.16f)** depends on the number of cities: `PC = C × 600 / OCN`, rounded down to a multiple of 100, clamped to 300-1000 shields. On a Standard map that is 300 for up to 13 cities, 400 for 14-16, and 1000 for 34 or more. Sources:
  - [RM129] 1.16f: "The cost of building a Palace is now based on the number of cities in your civilization."
  - https://civfanatics.com/civ3/strategy/game-mechanics/cost-of-the-palace-for-c3c/: "PC = (C * 600) / OCN" and "The palace can never cost less than 300 shields and never more than 1000 shields".
  - Only one city can build the Palace at a time. [RM129] 1.16f: "Palaces and spaceship parts can only be built in one city at a time."
- **Free palace jump**: when the capital is razed or abandoned, the Palace moves for free to the highest-scoring city within 8 tiles walking distance of the old capital. Ties go to the first city in the database. Source: https://civfanatics.com/civ3/strategy/game-mechanics/free-palace-jump/: "When the capital city is razed or abandoned, each city in the empire scores." Also: "3 points per national citizen", "1 point per military unit", and "The city with the most points is the new capital!" The full scoring and whether a *captured* capital also jumps are UNVERIFIED for C3C (the article names no version). A civ can never be left without a capital: [RM129] 1.17f: "Fixed bug which allowed civs with no capitals."

---

## Disputed

1. **Despotism penalty wording.** [PEDIA] and the CFC FAQ say any of food, shields *or commerce* above 2 drops by 1 (so 3→2, 4→3). [EDHELP] says "no tile may generate more than 2 food and shields". That wording reads as a hard cap at 2 and leaves out commerce. Firaxis' own Civilopedia and the FAQ agree on −1 including commerce.
2. **Republic/Democracy trade bonus condition.** The CFC Civilopedia mirror says "+1 commerce to any tile already producing at least 1". [EDHELP] says "each tile generates an additional +1 to commerce", which reads as unconditional.
3. **Forbidden Palace and distance.** alexman (C3C article and thread): the FP counts as a second palace for *distance*, but not for rank. [RMC3C] 1.20: "Updated Civilopedia.txt Forbidden Palace to explain that all cities are still affected by distance from the Real Capital." The C3C changes list (Arathorn): "The Forbidden Palace no longer provides a second core like a palace."
4. **Optimal number of cities, Standard map.** [EDHELP] "Optimal Number of Cities": "On a standard 100 x 100 map, the default is 16." The C3C [BIQ] value is 20. The help text looks stale, so use the BIQ table.
5. **Wealth conversion rate.** [PEDIA]: 4 shields → 1 gold, 2→1 after Economics. [BIQ] `ShieldsCostPerGold=4`. [EDHELP] "Shield Cost Per Gold": "Default is 8 gold." No source besides [PEDIA] backs the Economics change.
6. **Military Great Leaders hurrying.** [PEDIA] `#GCON_Leaders`: an MGL can hurry "an Improvement or Small Wonder". [ED-SUPP]: "NOTE: Military leaders can not rush." That note sits next to the Allow Scientific Leaders rule and may only mean Great Wonders. The C3C changes list: "Military Great Leaders (MGLs) can no longer rush Great Wonders."
7. **Granary at 6→7 and 12→13.** Bamspeedy: "When you hit size 7 or size 13, the granary is emptied". MadScot: Firaxis "decided to zero out the granary instead". Both are at https://forums.civfanatics.com/threads/granaries-dont-work.53078/. Against them: Hygro in the same thread ("you DO keep the food in the granary box") and Spoonwood's save test at https://forums.civfanatics.com/threads/pottery-is-underrated.683382/ ("changing from size 12 to 13 keeps 20 food in the box").
8. **Smith's Trading Company coverage.** [RM129] (1.16f additions): Smith's pays "all trade-based city improvements (Marketplaces, Banks, Harbors, and Airports) instead of all improvements with a maintenance cost of one gold". The C3C [PEDIA] adds Commercial Dock and Stock Exchange. The coverage follows the version.
9. **Pop-rush history.** The 1.17f Realms Beyond guide says "For 1 to 39 shields remaining ... the cost is the same, one citizen". The [RM129] 1.21f fix made the first citizen 20 shields. For the target patches, use 20 per citizen. Hurry and draft unhappiness: 40 turns in 1.17f, back to 20 in 1.21f ([RM129]).
10. **Founding on forest/jungle.** inca911 says the tile becomes grassland. Vipsanius says clearing forest gives plains. Both are at https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes.

## UNVERIFIED / open questions

1. **Water and lake tile yields.** The BIQ reads Coast 1/0/2, Sea 1/0/1, Ocean 0/0/0. [PEDIA] lists Coastal and Sea at 1 food and a fresh-water lake at 2 food, and gives no Ocean food. The Ocean value and the lake food bonus need checking in game.
2. **Railroad bonus.** The exact amount is unknown (+1 shield on mined/shield tiles? +1 food on irrigated tiles?). [PEDIA] only says railroads increase the output of irrigation or mines.
3. **Production switching penalty.** Is there a percentage penalty for switching between unit, improvement and wonder categories in 1.29f/PTW/C3C? None was found. Only the wonder lockouts after hurrying, disbanding or chopping are sourced.
4. **Gold hurry cost by item type.** Is the 4 gold per shield rate the same for units as for improvements, or do units cost more? Is there any non-linear term? Only the improvement rate and the empty-box doubling are sourced.
5. **Pre-C3C corruption formula.** The vanilla and PTW formula (government distance multipliers, the old FP behavior) is unsourced.
6. **Vanilla/PTW science rate caps.** The C3C BIQ has none (all 100%). The vanilla and PTW values were not checked; `civ3mod.bic` and `civ3X.bix` cannot be read with the current reader.
7. **Running out of gold.** What happens when the treasury cannot pay building maintenance or unit upkeep (forced sale of buildings, unit disbanding)? Unsourced.
8. **Pollution details.** The per-turn chance per pollution point, the yields of a polluted tile, and whether the size-12 threshold depends on tech are all unsourced.
9. **Global warming.** The trigger threshold, the number of tiles changed and the frequency are unsourced. The BIQ PollutionEffect for Marsh points at index 11 (Coast). Check whether that is a misread.
10. **WLTKD under Despotism.** Does a celebration lift the tile penalty? There is no Civ3 source.
11. **Joining past the size caps.** Can a Settler or Worker join a city past its Aqueduct or Hospital cap? Is there a maximum city size in C3C?
12. **Minimum city distance.** What is the default rule (no adjacent cities? one tile between)? There is no BIQ field for it. The C3C-era sources found only describe CxCxC as the tightest pattern.
13. **Settler at small size, abandoning and razing.** What is the minimum size to build a Settler? Can a size-2 city build one, and is the city then disbanded? When is razing offered, and what happens to population and buildings?
14. **City center tile.** Does it get an automatic irrigation effect? Does it always yield at least 1 shield (and 1 food or commerce)? Only Civ2-forum sources were found. A free road is implied by [RM129].
15. **Palace moving.** What happens to the old Palace when a new one is built? Does a captured capital (not razed or abandoned) also trigger the free palace jump in C3C?
16. **Golden Age edge cases.** Does a Golden Age that starts under Despotism carry over a revolution? Exactly which wonder combinations count? Each wonder lists its traits in [PEDIA], but whether "both strengths" can be met across two wonders needs confirming. [PEDIA] gives the Babylon example ("the Great Library (scientific) [and] the Oracle (religious)"), which suggests yes.
17. **Web search budget.** It ran out during this research. Global warming, the city-center tile, minimum city distance and the production switching penalty could not be searched further.
