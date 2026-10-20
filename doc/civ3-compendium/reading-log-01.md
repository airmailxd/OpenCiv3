# Reading log, section 1 (Combat and units), deep pass

Every source opened in the deep pass for `01-combat-and-units.md`, one line each: what it gave the section, "nothing
new", or "fetch failed". Web pages were read through a summarizing fetch tool that was asked for verbatim quotes.
"Not Civ3" marks inventory entries that turned out to be about another game.

## Local files

- `D:\Steam\...\Civilization III Complete\README.txt`, `civ3PTW\readme.txt`, `Conquests\readme.txt`, `Conquests\EditorSupplement.txt`: re-grepped for combat terms. Nothing new beyond what the first pass cited, except that README-C3C lists no combat-calculation change (used for the "less streaky" item).
- `Conquests\Text\Civilopedia.txt`: `GCON_Territory` (foreign roads and rails need a Right of Passage), `GCON_Agreements` (RoP lets units use roads and rails), `GCON_Nationality` (captured workers and settlers keep their nationality), `TECH_Engineering` ("Roads now bridge rivers."), `GCON_Combat` re-read.
- `Text\Civilopedia.txt` and `civ3PTW\Text\Civilopedia.txt`: same Engineering and Nationality wording in vanilla and PTW.
- `Conquests\Text\script.txt`: air combat messages (interceptor shot down, SAM and flak kills), `#NOTAMPHIB`, `#LANDCONQUER`, `#COMBATCONQUER`, `#NONCOMBATANT`, promotion and leader messages.
- `Conquests\Text\labels.txt`: nothing new.
- Editor help (`scratchpad\chm\Civ3XEdit\*.htm`): *Stealth Attack Barrier* (normal attacks hit the unit with the highest defense value), *Notes & Tips* (any improvement with bombard defense counts as walls), *Defense Strength* (defense-0 units are captured), *Attack Strength* (attack-0 units can't take cities), *Immobile* (can be captured), *Miscellaneous Effect Flags* (Increased Army Value +25%; Great Wall flag doubles defense of its own city vs barbarians), *Unit Abilities* (Blitz = one attack per movement point), *Battle Created Unit*, *Increases Chance of Leader Appearance*, *Air Power*, *Air Defense Strength*, *Interception*, *Upgrade Unit*, *Standard Orders*, *Build Larger Armies* (no new rule from the last seven).
- `C7/Lua/civ3/ruleset.json` (BIQ import): checked unit `actions` for fortify. Every unit has it, so it can't tell which units can fortify; nothing used.

## Combat odds, RNG, calculators

- https://civfanatics.com/wp-content/uploads/2024/01/civ3combatcalc.html (Xerxes314's calculator, raw HTML fetched with curl): JavaScript shows additive defense bonuses, HP 2-5 and 3x for armies, and a barbarian bonus applied to attack and defense with values 400/200/100/50/25/0.
- https://forums.civfanatics.com/threads/civ-iii-combat-calculator-exe.75765/ (BomberEscort): bombard no-damage formula `(B/(B+D))^RoF`; the AA formula is credited to alexman. Little else.
- https://codehappy.net/apolyton/threads/58980-1.htm ("Combat Calculations, once and for all"): alexman, "the defender bonus applies to whomever is not the attacker" (armies get defensive bonuses).
- https://codehappy.net/apolyton/threads/107356-1.htm ("Spearman vs. Tank Probability"): Tall Stranger says the C3C "4 roll" combat change was never shipped; a worked example of A/(A+D).
- https://forums.civfanatics.com/threads/civ-iii-conquests-patch-notice.71474/ (Tavis, page 1): the beta patch notice for the "less streaky" combat change.
- https://forums.civfanatics.com/threads/civ-iii-conquests-patch-notice.71474/page-2 : players quoting Tavis's change. No Firaxis follow-up.
- https://forums.civfanatics.com/threads/civ-iii-conquests-patch-notice.71474/page-3 : nothing about removing the change; alexman updated his calculator for the beta.

## Stack defender, defense bonuses, fortify

- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/83449-why-do-offensive-units-defend-before-defensive-ones : defender is chosen by defense and current HP, fortify counts, tie claims, and a second-hand Soren Johnson rule (a transport without cargo defends first).
- https://forums.civfanatics.com/threads/stacking-and-fortifying.197169 : not Civ3 (Civ2 forum). Nothing used.
- https://forums.civfanatics.com/threads/unit-stacking.121353 : not Civ3 (Civ4 pre-release). Nothing used.
- https://forums.civfanatics.com/threads/do-you-attack-up-or-down-the-stack.344134/ : not Civ3 (Civ4). Nothing used.
- https://codehappy.net/apolyton/threads/71511-1.htm ("Defense Bonuses: How do they Work?"): WarpStorm, "You add the bonuses first".
- https://codehappy.net/apolyton/threads/84569-1.htm ("How do walls really work?", page 1): main source for walls and bombardment. alexman: walls = any improvement with bombard defense; the PTW walls bug; walls hit first with defense 8; 50/25/25 city targeting; 16 for buildings and citizens; the wall bonus applies against air attack.
- https://codehappy.net/apolyton/threads/84569-2.htm (page 2): nothing new (radar towers are targeted last, nbarclay).
- https://codehappy.net/apolyton/threads/78348-1.htm ("Fortresses and PTW"): nothing new (AI avoids strong forts; fortress +50% and ZOC already in).
- https://codehappy.net/apolyton/threads/77038-1.htm ("Fortresses, Schmortresses!", page 1): nothing new.
- https://forums.civfanatics.com/threads/fortress-vs-fortifications.287626/ (named in the first pass's open list): nothing new.
- https://forums.civfanatics.com/threads/city-defense-discussion.689330/ (named in the first pass's open list): not Civ3 (Civ4). Nothing used.
- https://forums.civfanatics.com/threads/defensive-artillery-in-city.329600/ (named in the first pass's open list, page 1): a mod discussion; one line (Steph, bombard 8 vs building 16 = 33%) used as a cross-check.
- https://civfanatics.com/civ3/strategy/military/fort-cities/ : nothing new (strategy only).
- https://forums.civfanatics.com/threads/fortification.266551 : a city without a Barracks heals 2 HP; upgrading heals fully; ships heal only in port; C3C passengers heal.
- https://forums.civfanatics.com/threads/question-concerning-healing-units.113864/ : Theoden confirms 2 HP per turn in a city; pillaging uses the move except for armies; whether fortifying matters is disputed.

## Experience, promotion, leaders

- https://forums.civfanatics.com/threads/c3c-mechanics-birth-of-military-leader.526511 (Antal1987, code reading): the leader formula and the list of exclusions (naval and air units, barbarian kills, units that already made a leader, units in armies, a leader already existing); no militaristic check.
- https://codehappy.net/apolyton/threads/50260-1.htm ("One leader per elite unit!"): the rule Mike B. (Firaxis) confirmed: one leader per unit, again after an upgrade, only one leader at a time.
- https://codehappy.net/apolyton/threads/90368-1.htm ("Can they only create one leader?", page 1): an upgrade drops elite to veteran; one leader at a time; Mad Bomber's 1/16, 1/12, 1/32, 1/24.
- https://codehappy.net/apolyton/threads/104766-1.htm ("Elite Ancient Cavalry can't produce MGL's?"): nothing new (anecdotes; Ancient Cavalry can make leaders).
- https://codehappy.net/apolyton/threads/57625-1.htm ("Does tech disparity affect promotion chances?"): only the opinion that two wins in a turn always promote, noted as UNVERIFIED.
- https://codehappy.net/apolyton/threads/44520-1.htm ("Getting the most out of your Elites", page 1): an upgraded elite comes back as a veteran (Velociryx). Otherwise opinions.
- https://civfanatics.com/?p=843 : not Civ3 (Civ4 promotions page). Nothing used.
- https://forums.civfanatics.com/threads/how-much-a-unit-heals-per-turn.519747 : not Civ3 (Civ5). Nothing used.

## Armies

- https://forums.civfanatics.com/threads/weak-armies-in-vanilla-and-ptw.489054/ : vanilla and PTW armies had no strength bonus; Arathorn's C3C summary (the divisor, +1 move, healing in enemy territory 1 or 2 HP per unit); the claim that vanilla and PTW armies don't blitz (Disputed).
- https://forums.civfanatics.com/threads/increased-army-value-flag.424279/ : the help text's +25%; tests found no difference (Disputed).
- https://forums.civfanatics.com/threads/the-basics-of-armies.99688/ : TA includes the fighting unit; terrain bonuses apply to the full army defense; units in armies can be promoted; vanilla army healing was one unit per turn.
- https://codehappy.net/apolyton/threads/59461-1.htm ("How do Armies handle damage?"): tested HP sharing; DeepO on choosing the defender by defense and HP.
- https://codehappy.net/apolyton/threads/49454-1.htm ("Upgrading units in armies", page 1): no upgrades in armies; second-hand "Firaxis said" beta remark.

## Retreat

- https://forums.civfanatics.com/threads/comprehensive-retreat-study.64755/ (page 1): test setup (PTW 1.21f); BomberEscort's list of cases with no retreat.
- https://forums.civfanatics.com/threads/comprehensive-retreat-study.64755/page-2 : bonuses seem capped at 66; the defender's bonus matters (vanilla 1.29f); bombardment never causes retreat.
- https://forums.civfanatics.com/threads/comprehensive-retreat-study.64755/page-3 : final summary (11,274 trials); Oystein's formula and where it misses; the same formula applies to a fast defender.
- https://codehappy.net/apolyton/threads/47749-1.htm ("Rules for Retreat (Firaxis?)"): Jaybe, "If both units have more than 1MP, there is NO retreat", which he calls official; the 1.21f retreat-bonus table.
- https://codehappy.net/apolyton/threads/35589-1.htm ("Rules for fast unit disengagement", 2001): a fortified fast unit does not disengage (pre-1.17f). Otherwise superseded.

## Bombard and artillery

- https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/ (page 1): the C3C 1.22 target order by attacker type; Civil Defense absorbs building hits; Industrial-Age wonder immunity; lethal flags; air units killable; nuke effects; the Great Wall immunity bug (Lanzelot).
- https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/page-2 : the walls are rebuilt at once; Lanzelot suggests the order may be a probability split.
- https://forums.civfanatics.com/threads/the-probabilities-of-artillery-fire-vanilla.185892/ : a 50-shot sample per unit (too small); TimBentley on the order (walls, then units). Nothing new used.
- https://forums.civfanatics.com/threads/what-makes-bombardment-fail.307328 : PaperBeetle: in C3C, units are always targeted while they still have HP to lose.
- https://forums.civfanatics.com/threads/using-bombardment-to-wreck-great-wonder.278312/ : a Great Wonder destroyed by a trebuchet in C3C 1.22.
- https://forums.civfanatics.com/threads/coastal-forts-and-bombardment.690690/ : first try returned HTTP 429; the retry worked. Defensive bombard is once per enemy turn per unit and never from the unit attacked; walls give ZOC in towns; several ZOC shots per enemy turn; Coastal Fortress ZOC trigger.
- https://forums.civfanatics.com/threads/new-attack-ability-galleys-can-attack-units-in-cities.677328/ : the C3C galley/privateer attack bug.
- https://codehappy.net/apolyton/threads/44680-1.htm ("Bombard formula?", vanilla 2002): the one-third-each city targeting reported from korn469's tests.
- https://codehappy.net/apolyton/threads/47916-1.htm ("Lethal Bombardment Survey", page 1): a mod-settings survey. Nothing new.
- https://codehappy.net/apolyton/threads/30985-1.htm ("How do Coastal Fortresses work?"): nothing new (question only, no answers).

## Zone of control

- https://codehappy.net/apolyton/threads/55080-1.htm ("Zone of Control", 1.21f): nothing new (ZOC does not block movement; 1 HP shots).

## Naval, air, nuclear

- https://forums.civfanatics.com/threads/aa-formula-discussed.76227/ (page 1): the AA formula, the four best units, the 0.1 factor, no effect from HP, stealth or terrain; alexman's SAM mechanism.
- https://forums.civfanatics.com/threads/aa-formula-discussed.76227/page-2 : "divide it by 10"; the SAM's attack 8 (IIRC). Little new.
- https://forums.civfanatics.com/threads/air-battle-questions-only.565813/ : interception is not a shootdown (tjs282's steps); bombing a base kills planes; AA at 0.1 strength for up to 4 units; full-strength SAM at 50% or 5%; air superiority does not activate on the turn it is set.
- https://forums.civfanatics.com/threads/air-unit-interception-range.678374 : interception range is half the operational range (German vanilla manual); the air superiority timing bug.
- https://forums.civfanatics.com/threads/air-units-bombing-mechanics-in-the-open.678152/ : Ozymandias, the AA formula `1-(D/(D+A))^min(N,4)`; FAQ quote that an intercepted plane always fails its mission.
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/72889-how-do-intercept-and-air-supremacy-really-work (page 1): the PTW strategy guide says more fighters on air superiority raise the chance. Otherwise anecdotes.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/120873-has-anyone-figured-out-the-aa-formula-yet (page 1): Catt, fighter stacking did not help in vanilla; korn469, AA is a yes/no roll while fighters cause HP loss.
- https://civfanatics.com/civ3/strategy/military/death-from-above-c3c-airborne-operations/ : nothing new (tactics only).
- https://forums.civfanatics.com/threads/understanding-effects-of-nuking-a-city.442264 : not Civ3 (Civ4). Nothing used.
- https://forums.civfanatics.com/threads/nuclear-weapons.118586/ : mostly Civ4 wish lists. Nothing used.
- https://forums.civfanatics.com/threads/nuclear-weapons.413455 : 3x3 blast area, pollution, ICBMs can't move (vanilla, PTW, C3C), SDI "1 in 7" (opinion, not used).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/73776-effects-of-nuclear-weapons : 50% kill at ground zero and adjacent tiles (Proteus_MST, WarpStorm).
- https://codehappy.net/apolyton/threads/40287-1.htm ("Naval combat", page 1): wish lists. Nothing new.

## Upgrades and capture

- https://codehappy.net/apolyton/threads/43840-1.htm ("FIRAXIS, upgrade FAQ"): nothing new (an off-topic complaint).
- https://forums.civfanatics.com/threads/upgrading-units.284740/ : not Civ3 (Civilization Revolution). Nothing used.
- https://forums.civfanatics.com/threads/who-can-upgrade-their-obsoleted-units.299191 : not Civ3 (Civilization Revolution). Nothing used.
- https://forums.civfanatics.com/threads/captured-units-dying-instead-of-switching-sides.622068/ : capture needs the tech for the unit; otherwise the unit is destroyed.
- https://forums.civfanatics.com/threads/how-to-steal-workers-without-the-work.51198/ : nothing new.
- https://forums.civfanatics.com/threads/capture-settler-but-get-a-worker-instead.314474/ (found by search; a Civ4 thread): one Civ3 recollection (two workers from a captured settler), noted as disputed.
- https://forums.civfanatics.com/threads/capturing-enemy-settlers.517311/ (found by search): not Civ3 (Civ5). Nothing used.

## Movement

- https://forums.civfanatics.com/threads/rivers-and-roads.120751 : the claim that crossing a river costs a full movement point (C3C and vanilla); bridges come with Engineering; pillaging the road removes the bridge.
- https://forums.civfanatics.com/threads/river-movement.333404 : nothing new (an ideas thread).
- https://forums.civfanatics.com/threads/what-am-i-missing-re-movement-costs.398292/ : not Civ3 (Civ5). Nothing used.
- https://forums.civfanatics.com/threads/movement-on-enemy-roads.108599/ (found by search): Civ4 pre-release; nothing usable.
- https://civfanatics.com/civ3/strategy/military/early-industrial-railroad-intrusion/ : relies on roads in captured territory becoming usable. No explicit rule.

## General / FAQ / Great Wall / unit guides

- https://civfanatics.com/civ3/faq/ (both parts, via offset): the promotion table with militaristic and barbarian variants; Astronomy, Navigation and Magnetism make ships safe; AA stealth parity; unique units start a golden age; 2 HP in cities.
- https://forums.civfanatics.com/threads/frequently-asked-questions-civ3-play-the-world-conquests.170282/ (both parts, via offset): same FAQ; the 2 HP heal, C3C armies pillage free, "A plane can only engage another plane once per turn", barricades use all movement.
- https://forums.civfanatics.com/threads/offensive-defense-the-tactical-and-strategic-values-of-the-great-wall.128868/ : the Great Wall expires at Metallurgy and its walls end at size 7; the wonder-bombard dispute (Dominus Romae, TimBentley).
- https://forums.civfanatics.com/threads/everybodys-unique-unit-the-marine.84029/ : nothing new (tactics).
- https://sullla.com/Civ3/strat8b.html : the claim that Marines ignore city defense bonuses (Disputed). Otherwise unit notes.

## Searches run (WebSearch, standard mode)

- "civ3 captured settler becomes workers": led to 314474 and 517311.
- "civ3 upgrade unit elite veteran damaged hit points upgrade heals": led to 266551 and 113864.
- "civ3 can't use enemy roads railroads in enemy territory at war movement": led to 108599 and the railroad-intrusion article. The rule itself came from the local Civilopedia.
- "civ3 which unit defends stack best defender hit points chosen formula test": led to 678152.
- "civ3 unit with 1/3 movement point left enter mountain hills always or chance": no usable source.
- "civ3 promotion chance Soren Johnson ...": no primary Firaxis source; led to the 71474 patch-notice thread.

## Second reading round (2026-10-20)

Local:
- `civ3mod.bic` (1.29f) and `civ3PTW/civ3X.bix` (1.27f), decompressed and read raw at the C3C PRTO/BLDG offsets: Army cost 40 in tens of shields, so 400 in every version (Disputed 4 resolved); vanilla/PTW Great Wall flags "Doubles City Defenses" + "Double Combat vs Barbarians", C3C only the latter; C3C Military Academy has "Increased Army Value", vanilla/PTW's does not (§1.4, §1.6, Disputed 11).

Antal1987 executable research and the C3X mod (code-level sources):
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-2 : nothing new (citizen objects; the patch's worker limit).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-3 : Steph's from-memory retreat remarks (#57, #60: "If both units have more than 1 MP, then retreat never happens"; a 10-MP unit has the same chance as a 2-MP one) (§1.5).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-4 : contribution: Antal1987 #62, the retreat roll in `process_Combat` (`Get_Random(defender bonus + 50) < attacker bonus`); the RNG state is in the save (§1.5, Disputed 6 resolved).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-5 : nothing new for this section (AI artillery habits; C3C branched before the PTW submarine fix, Lanzelot #94).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-6 : nothing new.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-7 : nothing new (barbarians use two land unit types and one naval, Antal1987 #134).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-8 : nothing new.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-9 : nothing new.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-10 : nothing new.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-11 : nothing new.
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-12 : nothing new.
- https://forums.civfanatics.com/threads/patch-framework-for-c3c-v1-22.526966/page-2 : nothing new (a single-page thread; page 2 returns page 1).
- https://forums.civfanatics.com/threads/c3c-mechanics-birth-of-military-leader.526511/ (re-read for the promotion code; one page, 15 posts): contribution: promotion ratios 2/4/8, the barbarian doubling and the `CheckBonus(race, 0)` halving (§1.2; Disputed 3 and 14 resolved).
- https://github.com/maxpetul/C3X/blob/master/Civ3Conquests.h : contribution: `CheckBonus (Race *, __, enum RaceBonuses)` with `RB_Militaristic = 0x0`, which identifies the promotion check as Militaristic (§1.2).
- https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini (stock-behaviour descriptions only): contribution: defensive bombard never kills, no aerial or blitz repeats, docked ships don't fire; ZOC limits (no amphibious, no lethal, no air, works from transports); C3C bombard targets units first; aircraft in airfields immune and shielding; tile bombard defense hard-coded 16; no retreat at 1 HP, when defending a city, or defending on water; retreat-rule alternatives that imply no cavalry-from-knight retreat; king units defend last and the defense/king-priority tie-break keys; empty-army movement bug; AI army bugs; recon, interception and airdrop end the unit's moves; rebase 6x; a SAM dodge costs one move (§1.1, §1.4, §1.5, §1.7, §1.8, §1.11).
- https://github.com/maxpetul/C3X/blob/master/injected_code.c : contribution: stock round odds on a 1024 scale (`Fighter_get_combat_odds`); the tile-bombard threshold with the game's integer rounding and 1..1023 clamp; building defense = the best single building, walls only at town size, doubled by a "Doubles City Defenses" wonder (`patch_City_get_building_defense_bonus`); additive bonus sums in two helpers (§1.1, §1.6, Disputed 1).
- https://github.com/maxpetul/C3X/blob/master/civ_prog_objects.csv : the stock function addresses behind the wrappers above (`Fighter_get_combat_odds`).
- https://github.com/maxpetul/C3X/blob/master/changelog.txt : nothing new beyond the config file.
- https://github.com/maxpetul/C3X/tree/master/Notes (Antal's Hex Patches.txt, to_investigate.txt, galley_attacking_cities_bug.txt, interesting_object_notes.txt): nothing new for this section (submarine-bug internals).

Forums:
- https://forums.civfanatics.com/threads/things-civilopedia-cant-explain.470744/ : contribution: maurcus's test, Militaristic does not change upgrade costs (Disputed 8); its AA notes were already in §1.11. One page (page-2 returns the same ten posts).
- https://codehappy.net/apolyton/threads/56619-1.htm (Alamo scenario test): nothing new (scenario anecdotes, no numbers).
- https://forums.civfanatics.com/threads/defensive-units.446570/ : nothing new (anecdotes, no tests).
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/121127-use-of-fortresses : nothing new.
- https://forums.civfanatics.com/threads/a-sacrificial-retreat.43573/ : nothing new (tactics, not withdrawal mechanics).
- https://forums.civfanatics.com/threads/can-artilery-kill.313972 : nothing new (agrees with §1.7: artillery not lethal, bombers lethal, target priorities).
- https://forums.civfanatics.com/threads/anti-air-defense-combat.100020 : contribution: Vingrjoe's AA-100 vs D-1 test (90% kills at any interception setting), Zeekater on fighter attack vs bomber defense (§1.11, open questions).

## Still unread from the section 1 inventory (sources.md)

Lower value or likely duplicates, roughly in priority order:
- Leader threads (Apolyton): 36623 (3 pp), 69245, 64063, 65190, 50870, 34195; CFC 61506 (forum version of CFC-LEAD), 29874, 14095, 20538, 188393, 7716; page 2 of 90368.
- Army threads (Apolyton): 36131, 66195, 59531, 92551, 42770, 60617, 42957, 57511; CFC 76405, 435718, 38929, 444972, 115498; page 2 of 49454.
- Bombard: CFC 10438, 49517, 188525, 122424, 25078, 490195, 548715, 57071; Apolyton 61497 and the codehappy list (36804, 48678, 58783, 75586, 81641, 29393, 71619, 66946, 73164, 66909, 97132).
- ZOC: Apolyton 43413.
- Defense: CFC 333365 (probably Civ4), 48236, 85791, 9313; pages 2-3 of 77038 and page 2 of 78348.
- Combat odds: CFC 9433, 63544, 22393, 117740, 701641, the postid=1526027 link; Apolyton 31935, 32394, 30659, 68321, 78065, 80543, 39416, 35627, 60407, 38327, 36852, 36066, 35390, 74337, 34667.
- Experience: CFC 42545, 122927, 37578; Apolyton 64957.
- Upgrades and capture: CFC 282454, 315613, 49467, 125627, 53422, 223675, 74357; Apolyton 31306, 32143, 40150, 48106, 85961, 29611 and the hidden-nationality threads.
- Naval and air: CFC 109955 (forum version of CFC-SINK), 160818, 273792, 124382, 49732, 90785; Apolyton 49418, 41661, 47046, 43260, 29684, 97602, 55566, 49075, 33920.
- Nukes: CFC 431799, 497288, 76147, 39555, 49478, 693168 (probably C3X, out of scope); Apolyton 53363, 96857, 43862, 102086, 63927.
- Movement: CFC 635829, 9547, 18252, 231474, 448790, 60936 (forum version); Apolyton 35467, 49719.
- Strategy articles: CFC 105618, 83760, 93880, 13003, 55188, 65503, 66338, 66841 and the remaining War Academy military pages.
- Reference tables (CFC Civilopedia unit pages, the unit reference, Nethog's PDFs, the data zips): unit stats are already taken from the BIQ, so these are low value for rules.
- Code-level leftovers: the rest of the C3X source (`injected_code.c` is 45,000 lines; only the combat-odds, bombard, building-defense, pollution and retreat-eligibility parts were read) and the C3X forum thread (CFC 666881, 100+ pages), which may hold more of Flintlock's stock-code readings.
