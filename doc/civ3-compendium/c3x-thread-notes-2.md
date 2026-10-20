# C3X thread notes, part 2 (pages 49-96)

Source: CivFanatics thread 666881, "C3X: EXE mod including bug fixes, stack bombard and much more".
Every link below has the form `https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/page-N#post-ID`. `T` stands for the part before `/page-N`.

Only statements about what the STOCK game (Conquests 1.22, or PTW/vanilla where noted) does are listed. The mod's own features are left out. The quotes came from WebFetch's page summaries, so check one against the live post before relying on its exact wording. "(?)" marks a speculative or anecdotal claim, or one where it is unclear whether the poster was describing stock or modded play.

- Pages read: 49-64, 67-96.
- Pages not fetched: 65 and 66 (HTTP 429 on all 3 attempts).
- Pages with nothing relevant: none. Page 74 had only trivia, and page 77 had only borderline items.

Sections: 1 combat/units, 2 city economy, 3 happiness/culture, 4 science/wonders, 5 governments, 6 diplomacy/espionage, 7 map/terrain/workers, 8 AI/difficulty/victory.

T = https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881

## 1. Combat and units

- T/page-83#post-16536435, #1655, Flintlock, defensive bombard: "In the base game, defensive bombard can do at most one damage and will never activate against an attacker with one HP, so it can never outright kill an attacker." / "They roll only once. Defensive bombard doesn't take rate of fire into account at all."
- T/page-83#post-16536154, #1651, Civinator, defensive bombard: "the programming always cut it down to a ROF of 1"; "maximal one hitpoint can be taken away from a unit with defensive bombardment."
- T/page-84#post-16537815, #1671, Flintlock, ZoC damage chance: "The formula for the chance to do damage by ZoC is: 1 - (D*16 / (D*16 + A))". D is the moving unit's defense and A is the ZoC attack/bombard strength. He says it "also applies to coastal fortresses". Examples: strength 160 vs defense 8 gives 55.6%, strength 8 gives 5.9%. Also: "I don't think the AI understands ZoC at all".
- T/page-84#post-16537363 (#1668) and T/page-84#post-16537868 (#1674), Erlend Philip Wood, coastal fortress: "Originally the CF follows the ZoC rule". The editor value effectively needs multiplying by 16: "the correct number to multiply by is 16, not 20".
- T/page-83#post-16536518, #1659, Erlend Philip Wood, coastal fortress: "If a ship already adjacent to the Fortress moves to another adjacent tile, then the CF will (attempt to) shoot at it." / "a CF inside a bay (where there is only 1 coastal tile adjacent) will never fire at enemy ships."
- T/page-61#post-16382526, #1213, Ozymandias, ZoC: "each unit firing from a ZOC can only inflict 1 HP of damage."
- T/page-90#post-16559552, #1785, Flintlock, ZoC strength: "They use their bombard value." / "units only have the option of using their attack value for ZoC if they're in the same class as the unit they're targeting." / "otherwise ZoC can only be done using bombard strength." (?) This may describe the C3X ZoC rules.
- T/page-96#post-16589155, #1907, Flintlock, ZoC ordering: "the unit doesn't experience ZoC until it moves and it doesn't move until it has dealt with any enemy units".
- T/page-90#post-16559006, #1783, El_Constructor, ZoC and capture: "capturable units (worker, artillery) on this tile were captured before the attacking unit starts moving."
- T/page-95#post-16584231, #1889, Flintlock, tile bombard (improvement damage): "the chance to do damage on each roll equals 1 - X/(S + X)". S is the bombard strength and X = "floor((100 + D) * 16 / 100)", where D is the total tile defense in %. "The attacker gets to roll multiple times up to its rate of fire and if it succeeds on any roll then the tile is damaged." He does not say explicitly that this is stock, but it comes from reverse engineering the base code.
- T/page-95#post-16583488, #1884, Flintlock, tile bombard defense: "The chance to do damage depends only on the tile's terrain, whether it has a fortress or barricade" / "I don't see any references to the building or citizen defense bonuses."
- T/page-81#post-16523719, #1620, BlueSox14 (summarising Sulla), PTW vs C3C bombard: "In PTW, bombardment would have a random 1/3 chance of striking either units, population or buildings." / "In Conquests, this was changed to striking at units only." / "In PTW, while artillery could damage units, artillery could never kill a unit." / "This was removed in Conquests and artillery could now apply the lethal blow." (Lethal bombard is a per-unit flag in C3C.)
- T/page-56#post-16369990, #1116, Flintlock, PTW artillery: it "had a 2/3 chance of attacking buildings or pop in a city instead of the garrisoning units." Also: "precision striking is hard coded for air units".
- T/page-63#post-16393188, #1247, Flintlock, bombard targeting: "the normal bombard method prioritizes damaging units."
- T/page-51#post-16341094, #1018, Flintlock, charm attack: "it rolls a damage kind (damaging buildings, population, or units)" / "cities aren't always vulnerable to charm attacks depending on what improvements are present."
- T/page-51#post-16339648, #1014, Predator145, charm bombard: "Units targeted and hit will have THE ENTIRE STACK's defense stats halved." Also T/page-63#post-16390593 (#1244): "Charm bombard halves the defense stat of the entire stack."
- T/page-78#post-16507213, #1558, Flintlock, bombard rules: "The game rules do not allow bombarding aircraft in an airfield or destroying the airfield from underneath them." He also mentions a stock "weird little bombard rule that limits what land artillery can damage". (The details are not on this page.)
- T/page-64#post-16401109, #1266, Predator145, cruise missile flag: "giving a unit the cruise missile ability will automatically give it lethal land and sea bombard".
- T/page-62#post-16384097, #1222, Flintlock, interception: "the game doesn't search for the best available interceptor like it does for defenders in ground combat" / "Normally the game does not give any indication when a plane dodges interception." / "a plane with multiple movement points can bomb multiple times but only perform reconaissance once per turn".
- T/page-81#post-16521876, #1609, Flintlock, interception: "instead the game picks the first available fighter to intercept."
- T/page-81#post-16522285, #1615, Flintlock, interception: "Under the standard rules, intercepting causes a unit to lose all of its remaining moves".
- T/page-79#post-16512698, #1575, Flintlock, ground anti-air: "air combat involves instant kills" when ground anti-air units intercept planes.
- T/page-63#post-16395511, #1253, Flintlock, flyover: Unit::try_flying_over_tile "rolls for interception, does air combat, and also plays the flying animation."
- T/page-63#post-16395038 (#1250) and T/page-63#post-16395546 (#1254), Predator145, missiles: "air units with the cruise missile and nuclear weapon ability can never be intercepted or shot down." / "only SDIs can shoot them down. But interceptors and SAM batteries as well as Air Defense value units can't."
- T/page-78#post-16497435, #1545, Erlend Philip Wood, SDI: based on testing, SDI does not attack ICBMs only. Nukes "will be targeted by the Strategic Missile Defense, with a probability of 75% to get shot down." (?) Contrast T/page-78#post-16495192 (#1542), Alekseyev_: "Non-ICBM-nukes ... are already not intercepted." (IIRC)
- T/page-63#post-16395572, #1256, Civinator, nukes: "The nuclear flag (not the ICBM or tactical nuke strategy) also makes a unit immun against nuclear attacks."
- T/page-85#post-16539545, #1690, Flintlock, airdrop: "you can't airdrop into an enemy city even if it's undefended." / "You also can't airdrop onto an enemy worker."
- T/page-79#post-16512698, #1575, Flintlock, armies: "the condition for armies is only that the two units must have matching classes."
- T/page-95#post-16584560, #1894, tjs282, army upgrades: in unpatched Civ3, "an Army can only be upgraded in a town with both a Military Academy-type SW" (and a Barracks). Also: "when a newly generated MGL is used to build an Army, the most recent upgrade-version available will be created."
- T/page-95#post-16587093, #1897, Flintlock, armies: "the game only stores a single army count per player".
- T/page-59#post-16376244, #1174, tjs282, armies: "the AIs in Vanilla + PtW could use Armies just fine." The C3C change came from "hardcoded changes made to the game executable for the Conquests expansion".
- T/page-63#post-16396708, #1259, tjs282, army cap: "there is already existing code limiting the number of Armies which any Civ can generate/ control (based on number of towns)".
- T/page-64#post-16401798, #1270, Flintlock, pillage: when a unit pillages, "the game checks whether the unit has the army ability" and if so skips deducting a movement point. "It also checks whether the tile has a barricade, if it does the unit loses all of its remaining moves".
- T/page-64#post-16398188, #1264, Flintlock, pillage: "There is a condition stopping AI units from pillaging tiles if doing so would leave the unit itself stranded."
- T/page-60#post-16377888, #1191, Civinator, capture: "Decisive for capturing a unit is, that the civ that attacks that unit can build the attacked unit themselves." Confirmed by Flintlock at T/page-93#post-16577386 (#1847): "the game does not let you capture units you couldn't have built yourself."
- T/page-67#post-16408200, #1321, Thorvald of Lym, capture: "0-DEF units like artillery are destroyed if the civ can't build them". Workers "are killed instead of captured" when the attacker can't move into the tile.
- T/page-96#post-16589155, #1907, Flintlock, lethal ZoC and capture: "a unit that gets killed by lethal zone of control still gets to capture any units on the tile it was moving into" (a problem he was fixing).
- T/page-94#post-16581992, #1873, Moff Jerjerrod, enslave: "the 'enslave' unit capability had a 33% chance of converting a defeated enemy unit" / "this 33% chance is hardcoded." (?) From memory plus a search.
- T/page-58#post-16374946, #1152, tjs282, capture: air units "in the base-game, get destroyed on capture". T/page-58#post-16374980 (#1153), Alekseyev_: "air units with hidden nationality actually don't get destroyed".
- T/page-59#post-16376491 (#1179), tjs282, and T/page-60#post-16377056 (#1182), Civinator, Privateer bug: with a Hidden Nationality ship as the only garrison, the city "will be attacked by any passing AI-ships, regardless of any existing Peace Treaties"; "it happens even with an unmodded standard C3C game."
- T/page-60#post-16378096, #1195, Flintlock, stealth attack: "In the base game, a stealth attack won't trigger when Snake attacks Sam Hammer alone on a tile". T/page-61#post-16378838 (#1201), Civinator: "there must be at least two tanks in the stack so the bazooka can be used."
- T/page-87#post-16542582, #1721, Civinator, defense: "Civ 3 has no variable defense setting by the Rock-Paper-Scissors method, but an absolute value for the defense of a unit". T/page-86#post-16542534 (#1718), Sakharov: "In Civ3 it's impossible to add multipliers vs specific units".
- T/page-50#post-16329780 (#981) and T/page-50#post-16330131 (#984), Ozymandias, city defense: "defending in a City already gives you a 100% defense bonus." / "You get a 100% bonus for being in a Metropolis". (?) Imprecise; check against the compendium's 1.6.
- T/page-75#post-16473015, #1492, Civinator, battles: "neighboring tiles are not connected in a C3C battle."
- T/page-79#post-16512766, #1578, tjs282, loading: boats "may need to be in port in order to activate the 'Load' action". T/page-80#post-16515622 (#1589), Flintlock: "Unit::can_load is preventing you from loading naval units into a naval army."
- T/page-91#post-16569253, #1808, Flintlock, movement: the movement code "prevents units from moving onto tiles that are occupied by other civs' units, ignoring HN status".
- T/page-71#post-16441638, #1419, Flintlock, skip turn: "Skipping a unit's turn removes all its remaining moves." Pathfinder bug: it "stores the number of remaining movement points at each point along the path in a single byte".
- T/page-62#post-16388661, #1239, tjs282, Barracks: "If the Barracks is modded so that doesn't produce vets, then it won't heal or upgrade units either".
- T/page-94#post-16583053, #1879, Predator145, AI upgrades: "The AI will upgrade units as long as they have an AI strategy in the editor and as long as they have the cash for it." T/page-95#post-16583488 (#1884), Flintlock: "the AI isn't smart enough to move its obsolete units to the cities where it has barracks". T/page-95#post-16583194 (#1882), Spoonwood: "they do spend gold when upgrading." T/page-95#post-16584416 (#1891), Ozymandias: "I don't think that the AI will upgrade Settlers, Workers, or Units flagged as Kings." (?)
- T/page-54#post-16354784, #1071, Predator145, king flag: "the king unit ability makes the unit unable to defend a stack".

## 2. City economy

- T/page-64#post-16398188, #1264, Flintlock, unit maintenance: "The formula for unit maintenance is max(0,(N-F-K)*C), where N is the total number of units that player has, F is the no. of free units from gov't and difficulty, K is the no. of king-flagged and captured units, and C is the maintenance cost per unit from gov't type."
- T/page-63#post-16396708, #1259, tjs282, unit maintenance: "(excess) unit-maintenance costs are tied to the current government-type, not the unit-type". T/page-84#post-16537842 (#1672): unit maintenance involves "the AI-cost-factor for the current difficulty level".
- T/page-70#post-16429933, #1381, Flintlock, building maintenance: "each city stores a total maintenance cost" / "Maintenance is only recalculated from scratch when a building granting free improvements is gained or lost." / "Maintenance is not adjusted when techs are unlocked". The from-scratch function "does skip obsolete buildings".
- T/page-70#post-16431488 (#1384) and T/page-70#post-16432267 (#1385), Flintlock, obsolete buildings: stock bug. Obsolete buildings keep costing maintenance until a full recalculation, which happens on a government change or on gaining a wonder that grants free buildings. "the game recalculates all maintenance costs for a player when they change governments." T/page-71#post-16441638 (#1419): "Firaxis intended for obsolete buildings to be maintenance-free".
- T/page-55#post-16356899, #1081, Flintlock, disband for lack of support: "The game tries to pick the least valuable unit to disband." It avoids "captured (maintenance-free) units, king units, and armies". Ties are broken by internal list order.
- T/page-52#post-16345873, #1031, Predator145, AI selling: "The AI will not sell obsolete improvements and will only lose them if they can't pay maintenance."
- T/page-51#post-16336379 (#1005) and T/page-51#post-16336821 (#1008), tjs282 / justanick, despotism penalty: "Under the Despotism-penalty, Agri-Civs _only_ get that third FPT in town-tiles directly adjacent to freshwater". The penalty on the city tile does not apply "1. The town has access to fresh water" or "2. There is an aqueduct and city size has been reached." Flintlock (T/page-51#post-16337576, #1009) confirms: "that exception to the tile penalty is clearly apparent" in the code.
- T/page-51#post-16337774, #1010, Alekseyev_, settling on a resource: "settling on food resources deletes all the extra food, while settling on prod or money resources does not cause such a loss."
- T/page-51#post-16341094, #1018, Flintlock, specialists: "the game just checks if the city's current build is an improvement or unit to determine whether shields from specialists can contribute". (IIRC)
- T/page-53#post-16349903, #1058, Flintlock, hurry: the hurry production procedure uses the "Citizen Value in Shields" BIC variable. There is also "production switching discarding shields".
- T/page-80#post-16515622, #1589, Flintlock, pollution: "Cities store a running count of building pollution that is increased when a building is built" / "Buildings that are granted for free don't go through the same process as those that are physically built". So a free (wonder-granted) pollution-reducing building doesn't reduce pollution (see T/page-80#post-16513155, #1582, Virote_Considon).
- T/page-71#post-16439793, #1418, Erlend Philip Wood, pollution: "each city pollution (yellow biohazard sign) represents 1% chance of manifesting itself as local pollution" / "The city pollution from every city in the world is added together to determine the rate of global warming." (?) Unverified.
- T/page-52#post-16344941, #1025, tjs282, pollution: "local industrial/ population pollution can degrade terrain eventually (if not cleaned up)".
- T/page-91#post-16569253, #1808, Flintlock, Forbidden Palace: "Under the standard game rules the FP is much less effective than the palace."
- T/page-91#post-16569253, #1808, Flintlock, city spacing: "Under the standard game rules, the closest you can place cities is at a distance of 2 tiles".
- T/page-91#post-16572215, #1814, Erlend Philip Wood, auto-raze: "Under the standard rules, both the human player and the AI players will auto-raze a city if it is" size 1 with zero culture. "There is no choice in the matter." The AI chooses to raze "about 5-10% of the time".
- T/page-69#post-16429662, #1377, Civinator, city cap: "In an unmodded C3C game you cannot create more than 512 cities." The game shows "Too many cities."
- T/page-49#post-16327160, #961, Flintlock, trade network: "The game evaluates city connections for each civ independently". T/page-64#post-16397670 (#1261), sirati97: the "game completely recalculates all trade router every single time any change happens". T/page-64#post-16401798 (#1270): "The game recomputes a bunch of stuff related to trade networks whenever a road is destroyed."
- T/page-83#post-16536108, #1646, Flintlock, sea trade: "the game loops over all pairs of cities with harbors that are not already connected by roads". In one coastal tile arrangement "you actually can pass a sea trade route between them. That's an odd little aspect of the game rules".
- T/page-64#post-16401490, #1268, Civinator, trade: "For the inland trade net the number of available resources in your empire is not relevant". For a trade, "the other capital must be connected by a trade network with the other capital".
- T/page-49#post-16328138, #972, tjs282, resources: "at least one instance of a resource must be connected to your trade-net, for that resource to be usable."
- T/page-87#post-16544398, #1732, Flintlock, bonus resources: "They can't be traded and in fact aren't even propagated through trade networks."
- T/page-94#post-16581087, #1870, Flintlock, phantom resource bug: "The game only stores one full set of resource info per player." / "This is why the phantom resource bug exists." T/page-64#post-16401798 (#1270): "There is a bug in the game where it won't display the correct number of copies of resources".
- T/page-86#post-16540955, #1702, Flintlock, Required Government buildings: "the Firaxis logic never worked anyway since it's broken in several ways". T/page-85#post-16539202 (#1684): "there's no single point in the game logic that decides whether or not a building's resource requirement is satisfied."
- T/page-86#post-16541097, #1705, Civinator: "a resource that is provided to a city by a building is only virtual and not on the map".
- T/page-84#post-16537880 (#1676) and T/page-84#post-16537891 (#1677), Sakharov / tjs282, trait discounts: Sakharov: "Discount for buildings flagged as expansionist and industrious. As I see, it doesn't work in stock game now." tjs282: "The COMM building-flag also does not give half-price buildings". (?)
- T/page-55#post-16357352, #1085, Civinator, Harbor/Airport: these have "double functions for producing veteran air- or seaunits and opening sea- or airtrade."

## 3. Happiness and culture

- T/page-50#post-16335527 (#996) and T/page-50#post-16335628 (#998), justanick, war weariness bug: "It appears that war weariness is bugged." / "In single player a war between two AIs means that the AI later in the turn gets all the ww points" / "the AI earlier in the turn order gets no WW points from the war and thus no war weariness."
- T/page-51#post-16336306, #1003, Flintlock, war weariness: "There are several places war weariness could be generated, for example from city captures and unit combat" / "there are two types of WW that the game tracks, recent and long term".
- T/page-86#post-16541856, #1711, Flintlock, luxuries: "cities are limited to eleven luxuries anyway" / "Any more beyond that won't contribute happiness and won't appear on the interface."
- T/page-53#post-16349064, #1054, justanick, pop-rush: "this has the effect of autodestroying a town of size 1". (?)
- T/page-93#post-16579445, #1860, Erlend Philip Wood, culture victory: "a culture victory can be achieved with between 60.000 (Tiny map) and 160.000 culture points". (?) Says this matches vanilla on standard map proportions.

## 4. Science and wonders

- T/page-52#post-16345754 (#1029) and T/page-52#post-16346025 (#1033), tjs282 / justanick, Great Library: "you only get techs from the GLib after they've been learned by at least two other Civs that you know" / "You need to have contact with 2 civs that have a tech you donnot have." / "Those are just the mechanics of the base game."
- T/page-94#post-16581167 (#1872) and T/page-94#post-16582080 (#1874), Retvizan / Flintlock, Wonder obsolescence: "Great wonder treasury earns 5%. When it becomes obsolete, it still continues to give gold." Flintlock: "it's easy to forget to check for obsolescence. They did remember to check that the wonders aren't tied to a different government."
- T/page-51#post-16343544, #1020, haluu, disease: "Even if a tech has this flag checked, disease from flood plains still occurs (population is still lost)".
- T/page-92#post-16573587 (#1826) and T/page-92#post-16573593 (#1828), Quintillus / Spoonwood, scientific leader bug: "Fixing the scientific leader bug can affect strategy". "In PtW one might go to war early for an MGL to rush The Pyramids." / "In Conquests, one researches for an SGL for The Pyramids." T/page-91#post-16573078 (#1816) mentions "the Scientific Golden Age bug".

## 5. Governments

- T/page-61#post-16383510, #1217, ynot56, anarchy: "When in anarchy, there is no production." (?)
- T/page-53#post-16348441, #1042, Alekseyev_, Republic: "any other unit costs 2 (as per vanilla Republic)". (?) Context is unit support.

## 6. Diplomacy and espionage

- T/page-91#post-16571523, #1812, Flintlock, spy missions: "The game computes some sort of base chance ahead of time when a spy mission is configured" / "the game doesn't account for city improvements or government until the very end." / "for stealing technologies it's the chance not to fail completely" / "if you lose the roll against the base chance you fail to steal the tech and the spy gets caught". "even if you pass that roll there's another roll to determine if you fail to steal the tech but the spy gets away."
- T/page-68#post-16425620, #1356, Flintlock, trade screen: "When the trade screen is opened, the game assembles lists of tradable items, one for technologies, another for resources". T/page-91#post-16569253 (#1808): "there's one game function that applies each item after a deal is done".
- T/page-58#post-16375020, #1154, AnthonyBoscia, AI attitude: "The ai considers the total number of the player's offense-tagged units in its diplomatic calculations". (?)
- T/page-69#post-16428036, #1369, Virote_Considon, culture groups: "Civs have slightly less initial hostility to other civs in their same culture". "The Culturally Linked Start Locations option is absolutely bugged."
- T/page-84#post-16536632, #1662, Virote_Considon, war entanglement: unexpected war "can happen when one of the civs signs a mutual protection pact with a third party".
- T/page-73#post-16446100, #1459, Flintlock: "traits are a property of the civilization not the player."

## 7. Map, terrain and workers

- T/page-70#post-16434678, #1395, Flintlock, barbarian targeting bug: "The error is that it's adding the dx distance to both the unit's x & y coordinates, instead of adding dx to x and dy to y." Barbs check "the 168 (=0xA8) tiles surrounding the barb unit" but in practice "only along the NW/SE diagonal". T/page-70#post-16433709 (#1391): barb units "fortify in place until a unit appeared SW or NE of them". With the bug fixed, "barbs can target any unit within 6 tiles" (T/page-70#post-16435012). T/page-70#post-16434762 (#1397), tjs282: the vanilla 1.29f patch behaved differently.
- T/page-68#post-16426275 (#1358) and T/page-68#post-16426700 (#1360), Lord_Hill / Flintlock, barbarians: "a barbarian unit cannot capture a city at all". The code "checks if the unit's owner's ID is 0, in which case the game runs the barbarian capture gold/destroy population logic".
- T/page-80#post-16515622, #1589, Flintlock, barbarian slot: "barbarians' player slot (index 0) is hardcoded as being barbarian."
- T/page-72#post-16443119 (#1434) and T/page-72#post-16443157 (#1435), Flintlock / Civinator, barbarians: "The game doesn't compute trade routes for barbs". Civinator: "I think the Barbarian player pays for all Barbarian units on the map." T/page-71#post-16435385 (#1403), Quintillus: a barbarian-owned city "does not grow, either in food nor in shields".
- T/page-72#post-16443020, #1433, Belofon, barbarians: "barbarians actually can win the game", but only by eliminating all normal civs and then waiting for the time limit.
- T/page-60#post-16377947, #1193, Civinator: "the Barbarians cannot build workers and settlers".
- T/page-54#post-16354736, #1069, Flintlock, map generator: "This is an error in the base game's map generator". It can place AI capitals adjacent to each other.
- T/page-85#post-16540000 (#1695) and T/page-85#post-16540018 (#1696), Kangaroo262 / Virote_Considon, resources: "Strategic resources are tied to the number of players in the scenario." The generator "makes up the differences through the number of bonus resources it places".
- T/page-86#post-16542201, #1714, timerover51: "you can have a total of 32 Strategic and Luxury resources in the unmodded game".
- T/page-52#post-16344692, #1023, rysmiel, terrain change: "Civ III changes tiles only via global warming, or (in vanilla and I think PTW) as a result of nuclear explosions".
- T/page-90#post-16563821, #1796, Sutsuj, clearing: clearing actions "always produce the "base terrain type"".
- T/page-90#post-16563260, #1793, Flintlock, AI workers: there is "a function that the AI uses to decide which basic improvement to build on any given tile." / "It returns only true or false, if true the tile will be irrigated and otherwise it will be mined."
- T/page-96#post-16589155, #1907, Flintlock, AI radar towers: "AI workers consider every tile on their continent that could have a radar tower" / "Lastly they build a radar tower on the highest value tile." T/page-96#post-16587862 (#1902), Predator145: "first thing the AI does is make them next to every city."
- T/page-95#post-16587093 (#1897) and T/page-95#post-16587746 (#1900), Flintlock, AI workers: "the AI assigns a point value of zero to the worker build option when it feels it has enough of them". Of surplus workers: "I don't believe it keeps any around except for pollution cleanup." T/page-96#post-16590169 (#1912), Spoonwood: "They also won't rework tiles."
- T/page-50#post-16335948, #999, Alekseyev_, outposts: they "don't gain any extra LOS when looking at water, unlike normal units."
- T/page-85#post-16539776, #1692, Alekseyev_: colonies can be built on strategic resources, "but not for bonus resources".

## 8. AI, difficulty and victory

- T/page-56#post-16360495, #1101, Flintlock, AI build choice: "There's an element of randomness in what the AI chooses to build and it works by adding a random number of points to the AI's valuations of its options." / "IIRC, the random number is chosen between -5 and +5." / "Perfume is added at the very end of the valuation logic".
- T/page-83#post-16531281, #1641, Flintlock, AI strategies: "each unit type can only have one AI strategy." / "the AI maintains a count of how many units it has for each strategy and the only time that count is ever decremented is when a unit is destroyed." T/page-58#post-16375312 (#1157): "The unit AI definitely cannot choose a different strategy for a particular unit". T/page-59#post-16375682 (#1162): "If a unit has multiple strategies, Firaxis stores a whole 'nother copy" in the BIQ. T/page-58#post-16374794 (#1148): the unit AI "simply goes down the list offense, defense, artillery, etc."
- T/page-73#post-16444427, #1455, Flintlock, AI stacks: "there is no unit stack AI in Civ 3" / "It's every unit for itself" / "one unit may have multiple escorters but an escorting unit may only have one escortee" / "The reason AI artillery leaves its stack is because its escorting unit was destroyed".
- T/page-71#post-16441638 (#1419) and T/page-72#post-16443694 (#1438), Flintlock, AI naval escorts: "Normally the AI will assign between 1 and 3 escorts for each of its naval transports", "depending on the value of the transport and the number of units it's carrying." T/page-89#post-16553267 (#1765): "it's hard-coded in the executable which unit types require escort." / "I haven't seen the requires escort flag used anywhere in the unit AI." T/page-72#post-16441937 (#1425), Predator145: "Once it has designated warships to escort duties they won't be able to attack or bombard."
- T/page-51#post-16341094, #1018, Flintlock, AI naval invasions: "units load up for a naval invasion if they happen to find themselves in a city with a naval transport and have nothing better to do." / "There's no way for them to seek out a transport, which is why the AI often sends out transports that are only partially filled."
- T/page-63#post-16393188, #1247, Flintlock, AI recon: "As far as I know the AI never does recon missions." / "Where a human player would do a recon mission the AI cheats instead." T/page-82#post-16523964 (#1623): "the AI's fighters and bombers do not coordinate at all" / "the AI does not remember where or when it's been bombed."
- T/page-63#post-16393228 (#1248) and T/page-90#post-16561720 (#1790), Predator145, AI missiles/nukes: "The AI will not use "Tactical Nuke" and "IBCM" strat units unless you've hit them with a Nuclear Weapon Ability unit" / "the AI treats all tactical missile flag units as nukes and will only use them to bombard if nuclear war has started." T/page-56#post-16370045 (#1119): "The AI will only use Precision Strike on units with the "Air Bombard" AI strategy flag."
- T/page-54#post-16355878, #1073, tjs282, AI bombard: "the unmodded AI already used Air units to bombard ships" / "the unmodded AI already used its ships to bombard land-tiles" / "The unmodded AI could already use MGLs to rush builds".
- T/page-54#post-16355886, #1074, Predator145, AI build: "In stock game, it overwhelmingly builds swordsmen and neglects horsemen" / "stock game artillery units are capturable and thus the AI will simply end up donating them to the human player". (?)
- T/page-50#post-16335389, #993, Vuldacon: "the AI Does Not use ANY land transports." T/page-49#post-16328138 (#972), tjs282: "the AI does not know what to do with 'Naval Worker units' (it disbands them)".
- T/page-53#post-16349608, #1056, justanick, difficulty: "Unless playing below Demigod, AI starts with a plethora of extra starting units."
- T/page-94#post-16581167, #1872, Retvizan: "AI is playing without the fog of war." T/page-94#post-16582080 (#1874), Flintlock: "There's no single point in the code where it's determined whether or not AIs can see through the fog of war".
- T/page-55#post-16358543, #1097, Predator145: "the AI overvalues governments", going for "both Communism and Fascism" after Nationalism. T/page-55#post-16358416 (#1096), Alekseyev_: the AI "basically never go for sanitation on their own." (?)
- T/page-72#post-16441764, #1423, Lord_Hill, domination: "once you've cleared out all Civs except the final one, that last AI civ automatically wins by domination victory." (?) Scenario context.
- T/page-53#post-16348854, #1047, Civinator, capture the princess: works in C3C; "Victory point scoring must be additionally enabled for that option." This contradicts rysmiel at T/page-53#post-16348810 (#1043).
- T/page-93#post-16575327, #1841, Flintlock, AI settling: "Under the standard game rules the AI would be better off packing more cities in closer together".
- T/page-87#post-16544023, #1729, Flintlock: "The AI triggers surprisingly many recomputations of the network, on average about 5 per AI player per turn".
- T/page-62#post-16384097, #1222, Flintlock, hotseat: "all human players move before any AIs".
