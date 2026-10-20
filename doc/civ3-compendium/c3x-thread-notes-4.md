# C3X thread notes, part 4 (pages 145-192)

Source: CivFanatics thread 666881, "C3X: EXE mod including bug fixes, stack bombard and much more".
Post links have the form `https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-NNNNNNNN`.

- **Pages read:** 145-192, all of them. Page 192 is the last page of the thread.
- **Pages that failed:** none. Pages 158, 160, 161, 162 and 170 returned HTTP 429 at first and loaded on a later attempt.
- **Pages with nothing relevant:** none were completely empty. Pages 151, 152, 159, 171 and 188 had only minor items.
- **What is collected:** only statements about what the stock game does (Conquests 1.22, PTW or vanilla). Statements about the mod's own features are left out.
- **Section tags:** [1] combat & units, [2] city economy, [3] happiness & culture, [4] science & wonders, [5] governments, [6] diplomacy & espionage, [7] map, terrain & workers, [8] AI, difficulty & victory.

**Caveat:** WebFetch pulled the quotes through a summarizing model. Re-check any quote against the post before it goes into the compendium. Many posters play with C3X or other mods. Where a claim is hedged, speculative, or might describe modded play, it is marked "(hedged)".

## [1] Combat & units

- **Stealth attack needs two targets**
  - [p148 #2955](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16853888), Civinator: "In normal C3C stealth attack can only be performed if there are at least two valid stealth attack targets in a stack."
  - Flintlock (quoted in #2957) said the fix is "just patching a check for "> 1" into ">= 1"."
- **Unit AI ignores stealth attack**
  - [p148 #2959](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16853988), Flintlock: "I don't see anywhere that the unit AI considers stealth attack at all."
- **Air interception roll**
  - [p150 #2987](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16858118), Flintlock: the game "rolls for that unit to be intercepted flying over the tile based on its strength"
  - Same post, on fortified naval units: "I can see how it gives fortified naval units extra visibility"
  - [p169 #3369](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16915751), Flintlock: "I've called the method try_flying_over_tile and it gets called just when you'd expect, like before a bomber hits a tile."
- **Missiles and interception**
  - [p169 #3367](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16915396), Predator145: "If you make cruise missile ability units air units they can be intercepted already." Ground-type missiles are not intercepted.
- **Experience levels are only the named ranks**
  - [p170 #3399](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16921233), Flintlock: "the game doesn't track experience outside of conscript, regular, etc."
  - Same post: "some unit stats like fire rate are properties of unit types not the units themselves". He also mentions a game function "that determines which army member fights in each round of combat."
- **Spawned units' experience is set after creation**
  - [p151 #3017](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16866100), Flintlock: "The game does not set the experience levels in that method, it sets them in bits of code that run after that method."
- **Promotion and Great Leader chances are hardcoded**
  - [p152 #3022](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16867456), tjs282: "These probabilities are hardcoded in the .exe"
- **Leader kinds**
  - [p179 #3580](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16941749), Flintlock: "When a leader is spawned during a game, that field is set to 1 if it's an MGL and 2 if it's a scientific leader."
  - Same post: "the game doesn't properly initialize leader units that are placed for scenarios."
  - Same post: "I'm pretty sure the zero-kind leaders are left over from before Conquests when scientific leaders where added."
- **Leaders and hurrying (hedged: test may have run under C3X)**
  - [p176 #3520](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16936989), Erlend Philip Wood: an MGL "can't rush the Barracks. He can only build an Army at this point." He reports that it can rush Small and Great Wonders.
- **Only one battle-created leader at a time**
  - [p175 #3488](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16934723), Erlend Philip Wood: "It seems there is zero chance to get another battle-created unit if you already have one!"
  - [p175 #3491](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16935268), Flintlock refers to "the blocker that's stopping battle created units from being spawned for players that already have one"
  - #3488 also says: "If I run a vanilla game without C3X, the AI will just let the MGL stand still right where it was created."
- **Armies: passenger counting**
  - [p161 #3205](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16893099), Flintlock: "the game does not keep track of how many passengers each unit has loaded inside, in general." He adds: "For whatever reason, they added a counter only for armies."
- **AI never unloads armies**
  - [p161 #3217](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16894024), Flintlock: "the AI isn't programmed to unload units from armies"
- **Army pillage**
  - [p173 #3447](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16925195), Flintlock: "pillaging by armies doesn't cost any movement"
  - Context in #3443/#3446: AI armies pillage any tile not in their own territory. Charon117 ([p180 #3591](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16944392)) says this includes unowned tiles inside a city's radius.
- **Army upgrades (tjs282)**
  - [p172 #3434](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16923099): "Armies can only be upgraded in a town which has both a building which "Allows veteran land-units""
  - The rest of the requirement (paraphrased): the town also needs a small wonder with "Build Armies without a Leader".
- **Unit-flag ability to double captured gold**
  - [p162 #3222](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16894490), Flintlock: "Already in the base game using the official editor, you can give units the ability to double gold captured from cities"
- **Sacrifice ability bug**
  - [p153 #3053](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16869943), Flintlock changelog: "Fix AI players being able to sacrifice units that don't have the sacrifice special ability set on their type"
- **Enslave targets**
  - [p184 #3671](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16960954), Arexander: "In base game those would be Worker and Captured Worker (Slave)"
- **Nuke damage is hardcoded**
  - [p163 #3246](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16896015), Flintlock: "Modifying damage from nuclear explosions isn't easy since it's hardcoded."
- **Great Wall bombard bug**
  - [p160 #3187](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16891617), Pifou42: "vanilla Civ3 has a known bug, where bombarding a city with virtual walls given by Great Wall". The next building can be destroyed instead.
- **Airdrop onto airfields**
  - [p171 #3412](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16921470), Flintlock: the base airdrop check "normally accept[s] units on a tile with an airfield". This quote is a fragment.
- **Paradrops end the unit's turn**
  - [p178 #3541](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16938405), Kokuryutei: without C3X, a seaborne landing would "lose precious turn points".
  - [p176 #3510](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16936314), Kokuryutei: helicopters "could only transport 2 units of infantry (which, among other things, lost their turn after landing)"
- **Healing in cities**
  - [p192 #3826](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-17000888), Arexander: "By default units in cities heal two HP per turn". #3828 implies the same default.
- **Unique units after a Golden Age**
  - [p187 #3732](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16978459), tjs282: a unique unit stays buildable after the Golden Age only if "your UU is the last link in an upgrade-chain". Otherwise it is lost once the upgrade tech and resources are acquired.
  - In [#3733](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16978601), timerover51 checked the stock BIQ: "you are correct and I stand corrected."
- **Obsolescence by upgrade target**
  - [p158 #3145](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16884159), Android Assembly: "you can put an "innocent" unit that they upgrade too, and that makes them go obsolete."
- **Disband refund**
  - [p157 #3130](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16881426), Flintlock: "disbanding only returns 25% of the shield cost"
- **Combat math is integer**
  - [p166 #3309](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16902126), Flintlock: corruption and combat "are all integers under the hood"
  - Same post: "the only places I recall seeing floating point ops in game logic are for worker strength and the science age bonus"
- **RNG**
  - [p165 #3294](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16899923), Flintlock: "Its main, and probably only, random number generator is a basic linear congruential generator"
- **Pathfinder**
  - [p147 #2940](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16850792), Flintlock: "the pathfinder in rare cases generates paths through other civs' cities for AI naval units." He adds: "when I tested with the unmodded EXE, the bug still occurred." This is the case of AI ships attacking cities without privateers (WildWeazel #2935/#2937).
  - [p148 #2943](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16852049), Flintlock: "The bug is that the pathfinder gives up doing that once it's added 256 directions to the list."
  - [p148 #2945](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16852810), Flintlock: "The game simply stores the destination tile and recomputes the path along the way." He continues: "If I'm reading the code right, it actually recomputes it after every single move."

## [2] City economy

- **Capitals have zero corruption**
  - [p167 #3321](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16906674), Flintlock: "There's a rule in the corruption calculation that capitals always have zero corruption."
  - See also ZergMazter #3319.
- **Corruption function**
  - [p148 #2959](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16853988), Flintlock: "The formula for corruption is rather complex and it's computed by one large function in the game, about 350 lines decompiled."
- **Luxury slider is weaker in far cities**
  - [p173 #3449](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16926492), Flintlock: "cities farther from the capital benefit less from the luxury slider since they're more corrupt"
- **Treasury interest cap**
  - [p157 #3125](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16880155), Civian III: "The 5% treasury interest has a limit of 50 gold per turn."
  - [p157 #3130](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16881426), Flintlock: "Whenever the game needs to compute a player's gold income, it separately computes the interest then applies the limit."
- **Bankruptcy disbanding**
  - [p154 #3073](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16871456), Flintlock: "I can see that the function skips over units with the "king" and "flag unit" abilities at least."
  - [p155 #3084](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16874607), Flintlock: "The method to find a unit to disband on bankruptcy is surprisingly complicated". He adds: "Someone wrote all those conditions but forgot about the requires support flag."
- **Free building maintenance bug**
  - [p153 #3053](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16869943), Flintlock changelog: "Do not charge maintenance for buildings provided for free by shared wonder effects". This implies the stock game charges it.
- **Zero-production crash**
  - [p148 #2946](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16853321), Flintlock changelog: "Fix crash due to division by zero when cities have zero production"
- **Tile assignment**
  - [p147 #2928](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16848272), Flintlock: "the game never considers which city would be the best to work a given tile." He continues: "It's always the other way around, cities choose what to work from among the tiles available to them."
- **Some base value (context unclear)**
  - Same post, #2928, Flintlock: a function "returns 10 scaled by difficulty and divided by 2 if accelerated production is on." Context unclear, so re-check.
- **Fascism switch costs population**
  - Same post, #2928, Flintlock: "you lose a pop point in every city when switching to fascism". Tag [5] as well.
- **Food box values (hedged)**
  - [p147 #2925](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16847885), SG52 implies stock town/city/metropolis food boxes of "20/40/60".
- **Bonus resources are outside the trade network**
  - [p167 #3321](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16906674), Flintlock: "Wheat is a bonus resource so it's not part of the trade network."
- **Resource copies are not counted**
  - [p162 #3233](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16895053), Virote_Considon, relaying Flintlock: "the game doesn't count how many seperate copies of each resource a city has access to if it isn't connected"
- **Barbarians have no trade network**
  - [p150 #2996](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16859226), Flintlock: "The game does not compute a trade network for the barb player."
- **City-tile food from resources**
  - [p190 #3788](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16985727), Virote_Considon asks for cities to get "the food bonuses of resources on tiles they settle, as is already the case for shield and commerce bonuses". This implies the city tile gets the resource's shield and commerce bonus but not its food bonus. Hedged.
- **Global warming (hedged)**
  - [p184 #3677](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16961916), churchillpines: "It appears that global warming only affects forest and jungle tiles". He adds: "This is also the case with Civ III Conquests version 1.22".
- **Free buildings and pollution (hedged)**
  - [p181 #3601](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16950399), Erlend Philip Wood: "Negative pollution is not applied if the building is provided for free by a great wonder." This was seen in a custom setup.
- **Power plant replacement**
  - [p186 #3716](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16974921), timerover51: "If you build a coal power plant and then build a nuclear or solar plant, the nuclear or solar plant replaces the coal plant"
  - [p187 #3727](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16977520), Thorvald of Lym: the power-plant flag "chains across _every_ improvement" with that flag.
  - Same post: river cities skip the aqueduct, and "It's a hard code".
- **Golden Ages and unique units**
  - [p157 #3130](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16881426), Flintlock refers to "the existing GA logic" triggered by unique units.
  - [p185 #3699](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16966910), Flintlock: with multiple UUs "you'd have to modify the rules around golden ages so that unique units can only trigger one once"
- **City limit**
  - [p154 #3073](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16871456), Flintlock: "There are two points in the game code where it enforces the city limit". He continues: "it's why the game stops you from founding and displays "Too many cities" once you reach the limit".
  - Myceanean_Searaider (#3183) gives the stock limit as 512.

## [3] Happiness & culture

- **Disorder raises flip chance**
  - [p173 #3449](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16926492), Flintlock, answering whether rioting raises a city's flip chance: "Yes, I believe it's twice as likely to flip." (hedged)
- **Flip code**
  - [p160 #3198](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16892444), Flintlock: "The game internally doesn't break out each factor like culture ratio, foreign citizen count, disorder, and so forth"
  - [p166 #3314](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16902815), Flintlock: "the formula to compute the risk is embedded in the method that flips cities."
- **Disorder yields**
  - [p180 #3583](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16941923), Kokuryutei: "During riots, you don't receive any products or income from them, and the cost of maintenance doesn't go anywhere."
- **War weariness**
  - [p150 #2981](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16857204), Flintlock: "I'm pretty sure war weariness depends only on the number of units lost and whether they were lost in foreign territory." (hedged)
- **Border expansion thresholds**
  - [p179 #3562](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16940225), Erlend Philip Wood: "Border Factor" is under General Settings. In his words, "the first border expansion wil come at N¹, the second at N², the third at N³".
  - MARK V (#3560, #3603) implies the default culture thresholds step by multiples of 10.
- **AI and disorder**
  - [p146 #2910](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16844914), Flintlock: "For the AI, I don't think I've ever seen it leave a city in disorder for multiple turns"
  - [p174 #3467](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16929609), Flintlock: "The AI won't let its cities disorder"
- **Tourism**
  - [p149 #2971](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16855772), Flintlock: "The game has a separate little function that determines the tourism amount for any given building"

## [4] Science & wonders

- **Great Library threshold**
  - [p158 #3147](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16884982), Flintlock: changing the Great Library trigger count is "only a matter of overwriting '2' in the game code with some other number." This implies 2 civs is hardcoded.
- **Research minimum (hedged)**
  - [p160 #3187](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16891617), Pifou42 mentions "Hitting the 4 turns minimum cap". He also notes that research-turn predictions drift with mid-turn yield changes.
- **AI tech valuation**
  - [p153 #3041](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16868555), Erlend Philip Wood links an article that "reveals most of the factors the AI uses to calculate the value of each tech".

## [5] Governments

- **Fascism switch**
  - See [2] #2928: "you lose a pop point in every city when switching to fascism".
- **No anarchy for religious civs**
  - [p147 #2921](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16847511), Spoonwood: "Anarchy doesn't exist for a civ like Russia." This is in context and refers to a Religious-trait civ.
- **Unit maintenance per government**
  - [p152 #3029](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16867728), Flintlock refers to "governments that charge 2+ gold per unit in maintenance". This is free support counted in units, not gold.
- **No shield upkeep in Civ3**
  - [p167 #3321](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16906674), Flintlock: "One of the reasons they dropped that for Civ 3 was that the AI could never handle it well."

## [6] Diplomacy & espionage

- **Espionage missions need a target city**
  - [p156 #3119](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16878439), Flintlock: "Even the code for the espionage screen itself is all written assuming that every mission requires a target city."
- **No poison-water mission in Conquests**
  - [p147 #2930](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16848926), Flintlock: "Sadly, I don't see any evidence of this mission in the Conquests EXE."
- **Steal plans; failed missions**
  - [p177 #3539](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16938325), MARK V: "invisible units become visible as a result of an espionage mission to steal plans". He adds: "almost always, a failure will have a declaration of war as a result".
  - #3603 implies steal plans lasts one turn.
- **Spy placement**
  - [p156 #3105](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16876177), vmxa: "No palace, no spy."
- **Barbarians are always enemies**
  - [p188 #3741](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16980354), Flintlock: "the unit AI considers units owned by the barbs to be enemies unconditionally". He adds: "Diplomacy with the barbarians is not possible even if they own cities."
- **City trades**
  - [p191 #3807](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16990905), forestar: city trading is "normally impossible during regular diplomacy, but it becomes allowed during dealing with peace treaties." (hedged)
- **City-trade list limit**
  - [p158 #3141](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16883917), Flintlock changelog: "Fix inability to trade cities beyond the first 512 or units beyond the first 8192"
- **MPP at peace**
  - [p172 #3429](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16922524), robotlincoln: you can "demand a Mutual Protection Pact at the end of a war"
- **AI war declarations by attack (hedged)**
  - [p190 #3796](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16987435), Alekseyev_: "If they want to declare war on you, they will usually do so by attacking with a military unit." He continues: "Basically, only surprise attacks as war declarations."
- **Tribute**
  - [p180 #3583](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16941923), Kokuryutei: "The AI player usually gets scared and agrees to pay tribute over the next 20 turns."
- **AI cheating in trades**
  - [p160 #3198](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16892444), Flintlock: "I wasn't able to find the point where the AI decides how much to try to cheat the player."
- **Strategic resource price (hedged)**
  - [p172 #3421](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16921922), Predator145 on AI Saltpeter price: "I remember in stock game it would be around 70gpt."
- **Map trading**
  - [p165 #3294](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16899923), Flintlock: map trading runs through "Leader::give_map_knowledge which transfers revealed tiles from one player to another after maps have been traded"
- **Vision**
  - [p179 #3580](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16941749), Flintlock: "In general, the fog of war doesn't apply to the AI and unit invisibility does."

## [7] Map, terrain & workers

- **Volcano eruptions**
  - [p156 #3119](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16878439), Flintlock: "The game only rolls for volcanos to become active at most every two years." He continues: "The chance for each volcano to become active, when it does roll, is proportional to the number of years" since the last roll.
- **Disease and Sanitation (Firaxis bug)**
  - [p183 #3643](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16955706), Civinator: "In original C3C you only have to learn Writing." He adds that C3X has a setting to fix this, disabled by default.
  - [#3642](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16955698), tjs282: "The pop-loss happens simply because the disease-causing terrain is (still) in the BFC."
- **Railroad requirements**
  - [p169 #3371](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16915830), tjs282: "the only absolute logic in the unpatched game for railroading a tile is that it must (first) be roadable"
  - [p169 #3374](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16916404), Flintlock on the railroad-to bug: the game "gives a worker the railroad-to command after checking only that it has the tech & resources". He adds: "the function that checks whether the needed resources are present always reports that they are".
  - Civinator #3375 and #3380 confirm that the railroad-to button bypasses restrictions.
- **Worker automation**
  - [p156 #3108](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16876809), tjs282: with no pollution left, "any Workers on mop-up duty just go to sleep"
- **Landmine/LM terrain (hedged, scenario)**
  - [p156 #3120](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16878467), Civinator: "Damaged units on LM terrain can not heal !"
- **Goody huts**
  - [p191 #3804](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16989322), Erlend Philip Wood: "You're not supposed to get cities on Deity." He adds: "You get less useful rewards the higher the difficulty." And: "you won't get a city if you have more cities already than the average of the other players."
- **Resource respawn**
  - [p181 #3618](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16953376), Kokuryutei: "some resources disappear after depletion ... and then the game will randomly revive the resource"
- **Resource limits**
  - [p184 #3665](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16959940), Flintlock: "The game is limited to 256 resources."
- **Map generator civ choice**
  - [p182 #3621](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16953404), tjs282: the generator places "some/ all of the American Culture-group civs as "Random" opponents, before picking any other civs". He calls it "a known bug in the base game".
- **Impassable terrain**
  - [p160 #3198](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16892444), Flintlock: there are no impassable terrains "under the standard rules."
- **Barbarian settings**
  - [p174 #3477](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16931060), Virote_Considon: for fixed-map scenarios the game "will copy the settings from the last random map played"
  - [p174 #3476](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16931051), Flintlock refers to the INI keys BarbarianActivity and ActualBarbarianActivity.
  - Civinator #3471 mentions the stock "Barbarian Diagonal Bug": barbarians attack only along the NE/SW axis (haluu #3472).
- **Barbarian production**
  - [p150 #2996](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16859226), Flintlock: "it seems the AI production chooser logic just isn't running for the barbs at all."
  - [p188 #3741](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16980354), Flintlock: "the game treats the barbarian player specially in many ways"

## [8] AI, difficulty & victory

- **AI upgrades: gold reserve gate**
  - [p177 #3521](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16937054), Flintlock, correcting his #3432 ("the AI will never upgrade air units or artillery"): "there already is such a pass in the AI logic, only it's behind a weird little check".
  - Same post: "The reserve amount is 16 gold per city plus enough to earn the AI full interest income". It also says: "the AI will not upgrade anything when it's below the minimum gold reserve even if the upgrade is free."
  - [p173 #3453](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16928510), Flintlock refers to the AI upgrading units in "the three places I found before".
- **AI swarming**
  - #3521, Flintlock: "The base game behavior of swarming attacker units with no artillery support may actually be superior."
- **Unit coordination**
  - [p164 #3269](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16898219), Flintlock: "One of the biggest limitations of the AI is that there's almost no coordination among its units." He adds: "the unit AI for the attackers never considers artillery and vice-versa". And: "The captured artillery must return to a city so it can be assigned an escorter."
- **Naval escorts; shield costs**
  - [p150 #2981](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16857204), Flintlock: "The only place I know of that the AI considers the shield costs of units is when deciding how many escort units to assign to naval transports."
  - Same post: "The AI almost always ends up assigning three escorts to each transport."
  - Same post: "The AI can't transport caravans between continents. It can't even transport workers between continents, as far as I know."
- **AI transports**
  - [p165 #3287](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16899522), Flintlock on land transports: "As far as I know, it will never use a land transport."
  - [p169 #3365](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16914583), Flintlock: "As far as I know too, the AI will never load explorers onto transports." He gives the explorer routine step "2B. Otherwise, if there are other explorers on the continent, disband the unit if it is in a city."
  - Predator145 (#3363): the AI loads "offensive, defensive, settlers and artillery AI strat units."
  - [p149 #2963](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16854620), Flintlock: "the offensive unit AI can perform airdrops and load up for naval invasions"
- **AI production**
  - [p166 #3314](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16902815), Flintlock: "The AI doesn't account for generated resources at all when deciding what to build."
  - [p150 #2996](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16859226), Flintlock: "There are (annoyingly) multiple production chooser methods even for the regular civ AI"
  - [p174 #3468](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16929618), Predator145: "The AI has a hardcoded habit to switch to "panic" build the cheapest offensive unit..." He adds that the palace respawns "to the next biggest city" when the capital falls.
- **AI nukes**
  - [p185 #3694](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16965737), Flintlock: "the AI considers the value of resources on a tile when deciding whether to nuke it"
- **AI city sites and tiles**
  - [p146 #2920](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16847462), Flintlock: "The AI is not able to juggle tiles between cities at all, as far as I know."
  - [p145 #2886](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16843718), Flintlock: "it's not obvious in the AI's logic where this restriction is." This is the AI not founding cities inside its own borders.
  - Kokuryutei [p191 #3801](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16988707): "the AI sets routes to the goal only once for one task, when it creates a new settler."
- **AI and units without a strategy**
  - [p145 #2892](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16843785), Civinator: "AI units without any strategy are "dead" in the game."
  - [#2888](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16843748), Civinator: AI civs cannot cash in flag units.
- **Explorer disband**
  - [p168 #3360](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16913968), Predator145: "The AI will disband all "explore" AI strategy flag units if it runs out of land to explore."
  - [p190 #3800](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16988394), philiquaz: "explore ai will always prefer a hill < mountain < water tile with adjacent land tile"
- **AI bombard use (hedged)**
  - [p163 #3256](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16896963), vmxa: "The AI does not understand that bombarding a unit that has 1 HP is a waste."
  - [p164 #3265](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16897636), Predator145: the AI uses that order only with "Air Bombard" strategy units, "as this special order was only meant for those in stock game."
  - El_Constructor (#3784): the AI is "generally incapable of using bombers to attack ships".
- **AI-vs-AI war is rare**
  - [p189 #3765](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16981321), Kokuryutei: "Computer players in the original Civilization 3 rarely fight each other."
- **AI respawn**
  - [p174 #3467](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16929609), Flintlock: "The reason AI players don't get respawned is that there's no space for them."
- **Civ cap**
  - [p159 #3163](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16888798), Flintlock: "The 31 civ + barbs limit is baked very deeply into the game, it's even baked into the BIQ format"
- **Leader AI crash fix**
  - [p158 #3141](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16883917), Flintlock changelog: "Fix possible crash in base game leader unit AI"
  - [p150 #2981](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16857204) mentions "the base game's threat avoidance logic for the leader AI".
- **Late-game slowness**
  - [p184 #3665](https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16959940), Flintlock: "There are two things that make late game turns slow: the unit AI and determining which cities are connected by sea."
