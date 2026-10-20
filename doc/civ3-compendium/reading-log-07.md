# Reading log, section 7 (Map, terrain, resources, workers, barbarians and huts)

Deep pass of 2026-10-20. One line per URL or local source read: what it contributed to `07-map-terrain-and-workers.md`, or "nothing new", "fetch failed" or "not Civ3".

## Local first-hand sources

- `<civ3>/Conquests/conquests.biq` (QueryCiv3 plus a scratch reader): GAME section `PermitPlagues=0` and the stored plague values, `EruptionPeriod=5000`; RULE `Slave=1` with PRTO 1 = Worker; PRTO 6/11/29 = Warrior/Horseman/Galley; TECH #8 = Writing.
- `<civ3>/civ3mod.bic` (vanilla 1.29f) and `<civ3>/civ3PTW/civ3X.bix` (PTW 1.27f), read raw: TFRM turns (Clear Forest 10 in both), PTW Radar Tower `Required=64` = Radio, TERR (12 terrains, same yields as C3C, Ocean 0/0/0), GOOD (22 resources, Incense +1).
- `<civ3>/Text/Civilopedia.txt` (vanilla) and `<civ3>/civ3PTW/Text/Civilopedia.txt`: Dyes +1 and Incense +1, "Fresh Water Lake 2".
- `<civ3>/Conquests/Text/Civilopedia.txt`: `#GCON_Barbarians` (destroyed camps relocate), `#GCON_Food/Shields/Commerce` tables (no Ocean row), trait centre-tile lines, global warming text.
- `<civ3>/README.txt`: v1.16f "Shield bonus from clearing forest can only be received once per game", the sun icon, the 1.21f forest-planting exploit fix, the road-bonus and terraforming changes.
- `<civ3>/Conquests/readme.txt`, `<civ3>/civ3PTW/readme.txt`, `<civ3>/Civ Complete Readme.txt`: searched again; nothing new beyond what was already cited.
- Editor help (decompiled CHM): `Disasters_Page.htm` (nothing new), `Permit_Plagues_(Disasters).htm` (field definitions), `Volcano_Eruptions_(Disasters).htm` (already cited).
- Civilization III Complete manual (scratch PDF extract), "Terrain Charts" p.212: Ocean 1 food, Volcano 0/3/0, road +1 on all land but Volcano, Hills no irrigation. Barbarians p.120: camps outside borders; units outlive the camp. The river and goody hut passages had nothing new.

## Code-level (executable research)

- https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini (raw file read): the barbarian diagonal bug, the disease tech-flag bug (Writing), the phantom resource bug (more than 32 resources), city-centre food, the Agricultural fresh-water exception, minimum city separation 1, luxury appearance random 50-100, citizens taken off polluted tiles, scenario maps using conquests.ini barbarian settings.
- https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/page-70#post-16434678 : Flintlock's code reading of the NW/SE bug (dx added to both coordinates).
- https://forums.civfanatics.com/threads/hunting-down-the-barbarian-nw-se-axis-bug.674365/ : BlueSox14's tests (12 tiles on the axis in C3C, 6 tiles all round in 1.29f, same on Sedentary and Raging), and the PTW/C3C history.
- https://forums.civfanatics.com/threads/patch-framework-for-c3c-v1-22.526966/ : nothing new (data structures and a Science Age patch only).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285 : fetch failed (HTTP 429).

## CivFanatics

- https://forums.civfanatics.com/threads/on-barbarians-skilled-warriors-revealed-en-detail.103564/ : hut unit conditions (player plus 4 civs, tech, no resources, wheeled units), test counts.
- https://forums.civfanatics.com/threads/when-do-resources-deplete-tested.329362 : the depletion test (must be available to a city; not roaded alone; not colony-only).
- https://forums.civfanatics.com/threads/probabilities-of-goody-huts-c3c.93244/ (page 1): re-roll, the 2^15 draw, Expansionist and Sid outcome lists, "nothing" probability 0.
- https://forums.civfanatics.com/threads/probabilities-of-goody-huts-c3c.93244/page-2 : city conditions, microbe's "You CAN pop a city when you have a settler", the re-roll.
- https://forums.civfanatics.com/threads/probabilities-of-goody-huts-c3c.93244/page-3 : AutomatedTeller (no barbarians when founding a city), TimBentley (explorer/conquistador), Ancient-only tech.
- https://forums.civfanatics.com/threads/probabilities-of-goody-huts-c3c.93244/page-4 : justanick's 2/7 vs 1/7 (Demigod Expansionist); the link to the German CivWiki.
- https://civfanatics.com/civ3/strategy/game-mechanics/probabilities-of-goody-huts-c3c/ : nothing new (the conditions text only; the table is an image).
- https://forums.civfanatics.com/threads/disappearance-of-resource.114748/ : reappearance on the same turn (wilbill), community depletion claims.
- https://forums.civfanatics.com/threads/why-must-resources-deplete.246436/ : reappearance not necessarily in own territory (gmaharriet); the editor values again.
- https://civfanatics.com/civ3/strategy/game-mechanics/worker-moves/ : a landing page only; led to the PDF below.
- https://civfanatics.com/wp-content/uploads/2024/04/civ3_acad_worker_moves.pdf : KayEss's worker article: Industrious +50% C3C / +100% V/PTW, the bucket and pooling, single-player waste, cancelling, terrain multiplier, his "Vanilla & PtW are 5" (wrong).
- https://forums.civfanatics.com/threads/how-many-workers-do-i-need-to-do-this.452798/ : justanick's per-worker formula and rounding; Spoonwood's slave factors.
- https://forums.civfanatics.com/threads/fix-for-the-barbarians-sort-of.100704/ : NoAIPatrol defaults (PTW 0, C3C 1), Justus II's 12-tile range test, the scenario barbarian setting.
- https://forums.civfanatics.com/threads/barbarian-information.267101/ : TimBentley's targeting order, NW/SE tie-breaks, raid order, forest-chop search order.
- https://forums.civfanatics.com/threads/barbarians-spawn-logic.630389/ : not Civ3 (Civ1 thread).
- https://forums.civfanatics.com/threads/pollution-bug.251335/ : not Civ3 (Civ1 thread).
- https://forums.civfanatics.com/threads/global-warming-doesnt-work.539761/ : nothing new (an editor-reversal anecdote only).
- https://forums.civfanatics.com/threads/settle-near-a-volcano.402191/ : opinions on eruption frequency and radius (cited as opinion).
- https://forums.civfanatics.com/threads/cause-of-plague.120157 : plague off in random C3C games (Pounder, bingen); disease-terrain presence in the radius.
- https://forums.civfanatics.com/threads/disease-from-flood-plain.102771/ : two citizens lost (morchuflex); not working the tiles does not help; TimBentley on Sanitation.
- https://forums.civfanatics.com/threads/death-by-jungle-fever.113653/ : Brain's test (only fortified units die).
- https://forums.civfanatics.com/threads/do-granaries-help-combat-disease.128439/ : nothing new.
- https://forums.civfanatics.com/threads/does-colony-have-any-practical-use.702444/ : nothing new (untested remarks).
- https://forums.civfanatics.com/threads/forestry-operations-in-civ3.32469/ : nothing new beyond the once-per-tile claims (cracker's article itself is not in the thread).
- https://forums.civfanatics.com/threads/not-getting-shields-when-cutting-forests.654695/ : once per tile (justanick); no wonders or Wealth (tjs282).
- https://forums.civfanatics.com/threads/impossible-to-order-worker-to-clear-damage-unless-automated.654595/ : nothing new (scenario-specific tech tree).
- https://forums.civfanatics.com/threads/new-city-build-order-worker-mgmt-and-railroads.655880/ : PTW 10 versus C3C 4 forest turns (tjs282); no railroad-yield test.
- https://forums.civfanatics.com/threads/how-does-barbarian-city-spawning-work.265173 : not Civ3 (Civ4 thread).
- https://forums.civfanatics.com/threads/barbarian-encampement-frequency.693503 : opinions only (justanick's camp-count guess, recorded as a guess).
- https://forums.civfanatics.com/threads/barbarian-cities-in-civ-3.646253/ : barbarian cities cannot build or research (Debug test); no razing seen.
- https://forums.civfanatics.com/threads/early-barb-activity.369195 : not Civ3 (Civ4 thread).
- https://forums.civfanatics.com/threads/resource-probability-ratios.551214 : total disappearance when no valid tile is free (Kirejara); strategic count about one per player (timerover51).
- https://forums.civfanatics.com/threads/cure-for-resource-distribution.315427 : nothing new (it quotes the editor help).
- https://forums.civfanatics.com/threads/lack-of-strategic-resources-is-this-normal.13826/ : nothing new (vanilla anecdotes).
- https://forums.civfanatics.com/threads/mountain-tile-usage.689127/ : nothing new.
- https://forums.civfanatics.com/threads/what-does-the-seed-do.231006/ : which settings must match for a seed to reproduce a map.
- https://forums.civfanatics.com/threads/interturn-mechanics.443195/ : worker actions resolve first in the interturn.
- https://forums.civfanatics.com/threads/today-i-learned-about-civ3.687642/ (page 1 of 9): nothing new.
- https://forums.civfanatics.com/threads/things-civilopedia-cant-explain.470744 : fetch failed (HTTP 429, four tries).
- https://forums.civfanatics.com/threads/resourceless-ocean-tiles-useless.143299/ : not Civ3 (Civ4 thread; found by search while looking for the Ocean yield).
- `web.archive.org` fallback: fetch failed (the tool cannot reach the Wayback Machine).

## German CivWiki and civforum.de

- https://www.civ-wiki.de/wiki/Bonush%C3%BCtte_(Civ3) (rev. 50187): the full C3C hut table, gold 25/50, the tech rules, city distance 4, culture-opened huts, Explorer/Conquistador, no settler from city founding.
- https://wiki.civforum.de/wiki/Bonush%C3%BCtte_(Civ3) : fetch failed (certificate error); the same page was read at civ-wiki.de.
- http://www.civforum.de/showthread.php?t=66671 (page 1 of 3): d73070d0's tested hut rules (no settler on founding, city distance 4, maps on a known map, the Ancient-tech cut-off).
- http://www.civforum.de/showthread.php?t=66671&page=2 : city distance 4 confirmed (diagonal 1.5), the Ancient-tech-in-Middle-Ages split (justanick vs d73070d0), two barbarian warriors from an AI-opened hut (anecdote).
- https://www.civ-wiki.de/wiki/Kategorie:Civ3 and https://www.civ-wiki.de/index.php?title=Kategorie:Civ3&pagefrom=Kunst+%28Civ3%29 : index pages only.
- https://www.civ-wiki.de/wiki/Eisenbahn_(Civ3) : fetch failed (HTTP 404, no such page).
- https://www.civ-wiki.de/wiki/Barbaren_(Civ3) : camps outside borders, mass spawn when two civs change era, raid gold = treasury ÷ cities, units in a raided city destroyed.
- https://www.civ-wiki.de/wiki/Bautruppauftr%C3%A4ge_(Civ3) (rev. 54705): worker formula, rounding, job costs by move cost, Tundra forest planting.
- https://www.civ-wiki.de/wiki/Krankheit_(Civ3) (rev. 50341): disease terrains, two citizens over two turns, about 1%.
- https://www.civ-wiki.de/wiki/Wald_roden_(Civ3) (rev. 35891): once per tile, the receiving-city search, no need for borders, wonder cities skipped.
- https://www.civ-wiki.de/wiki/Verschmutzung_(Civ3) (rev. 53908): nothing new for section 7 (pollution sources belong to section 2).
- https://www.civ-wiki.de/wiki/Terrain_(Civ3) (rev. 37385): lake 2/0/2 and fresh water, Ocean 1/0/1, Volcano 0/3/0.
- https://www.civ-wiki.de/wiki/Ressourcen_(Civ3) (rev. 49836): nothing new (connection basics; Incense 2 / Dyes 1 as in the C3C BIQ).
- https://www.civ-wiki.de/wiki/Der_Phantom-Ressourcen_Bug_(Civ3) (rev. 23050): only with more than 32 resources, so not in the standard rules.

## Apolyton (apolyton.net and the codehappy.net mirror)

- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/48566-bug-in-food-production : polluted tiles and food (Trachmyr, Sevorak), unresolved; added to Disputed 6.
- https://codehappy.net/apolyton/threads/60991-1.htm : global warming inputs (city pollution icons and nukes), WarpStorm's tests, uncleaned tiles degrade.
- https://codehappy.net/apolyton/threads/60991-2.htm : Zachriel ("Global Warming is global").
- https://codehappy.net/apolyton/threads/47926-1.htm : nothing new (colony anecdotes).
- https://codehappy.net/apolyton/threads/43936-1.htm : nothing new (the Despotism penalty only).
- https://codehappy.net/apolyton/threads/4568-1.htm : nothing new (pre-release wishes; no Firaxis statement).
- https://codehappy.net/apolyton/threads/83601-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/65509-1.htm : nothing new (unroaded resources not seen to deplete; hearsay).
- https://codehappy.net/apolyton/threads/51174-1.htm : nothing new (captured workers at half speed, already cited).
- https://codehappy.net/apolyton/threads/50100-1.htm : nothing new (1.21f hut anecdotes).
- https://codehappy.net/apolyton/threads/43888-1.htm : Aeson's vanilla 1.17f hut test (no techs in the Middle Ages), Ming on the saved random number.
- https://codehappy.net/apolyton/threads/42842-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/83801-1.htm : BigFree on Sedentary (huts only), added to Disputed 16; the other claims are untested.
- https://codehappy.net/apolyton/threads/57657-1.htm : nothing new (tribe-name list).
- https://codehappy.net/apolyton/threads/48037-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/57058-1.htm : the uprising trigger "per Soren" (second-hand), uprising sizes.
- https://codehappy.net/apolyton/threads/58357-1.htm : nothing new for section 7 (Golden Age shield rule; bonus grassland arithmetic).
- https://codehappy.net/apolyton/threads/81960-1.htm : nothing new (Ctrl-R timing exploit, untested).

## Inventory entries for section 7 still not read

- CFC: `small farms` 693526; `Irrigation through Hills with Cities?` (Apolyton 121265); `Workers: What Do I Do with Them?` 276289; `Other uses for workers` 13744; `Forest chop diagram` (civfanatics.net image); `Getting the AI to use worker units...` 515992; `No Settlers, No Workers?` 704355; `Keeping Resources in Check` 391951; `Resources` 158705; `The Most Important Part of Any Endgame: Oil` 99693; `Needing more Strategic Resources` 361762; `No coal anywhere??` 340842; `How to add resources to C3C` 105217; `Never getting any strategic resources` 9405; `Strategic resources not there, why?` (Apolyton 55680); `Maya Flood Plain Start` 74466; `Are camps useful in C3C?` 361305; `Barbarian spawning` 689280; `Limit the number of barbarians?` 674182; `Barbarian spawn gamebreaking` 602454; `Custom Map - "Raging Barbarians"` 460704; `How to use barbarians as settlers?` 166058; `what is the best map settings` 297735; the seed threads 703383 / 703324 / 264492; `Settle the Land` 396622; `Worker moves` forum thread 93408; `Things civilopedia can't explain` 470744 (429); `Useful Patches for C3C v1.22` 526285 (429); `Today I Learned` pages 2-9.
- Apolyton/codehappy: `We need more terrains` 60600; `Obtaining Scarce Resources` 35769; `Impact of scarce strategic resources` 40421; `Final Verdict on Strategic Resources` 33565; `Partial and pointless formulae: resource cost` 71404; `Production, Consumption and Exchange of Resources` 52277; `Resources - The old way rules` 37568; `Horse resource is bogus` 34565; `Colony Exploit` 100571; `A Study in AI Tilework` 76212; `How to automate workers correctly?` 59554; `Worker Worries` 79678; `Absorbing initial worker` 120973; `Triple Threat` 52270; `Stronger Barbarians.` 95755; `Contact with Barbarians? (1.17f)` 43315; `Barbarians! WTF!!??` 60492; `Global Warming. It would be nice if...` 60246; `Does the Iron Works Cause Pollution?` 76575; `Do Recycling Centers work?` 35557; the start-location threads 101612 / 42110 / 46606 / 62455.
- civforum.de 66671 page 3.

## Second reading round (2026-10-20)

Local and code-level:

- https://github.com/maxpetul/C3X/blob/master/Notes/generate_resources.txt : the stock map generator's resource placement (eligibility rules, amounts, luxury clumps on one continent, bonus loop). Added to §6.2.
- https://github.com/maxpetul/C3X/blob/master/injected_code.c : water bodies of at most 21 tiles treated as lakes ("Mimic vanilla behavior"). Added to Disputed 13.
- https://github.com/maxpetul/C3X/blob/master/changelog.txt and README.md : nothing new (the Writing disease bug again: the fix "causes a lot more disease under the standard rules").
- https://github.com/maxpetul/C3X/blob/master/Notes/phantom_resource_bug.txt , sub_bug_notes.txt , expanding_work_radius.txt , "Antal's Hex Patches.txt" : nothing new for section 7 (Antal's v1 notes repeat the 2-tile settle limit).
- Manual text extract (scratchpad `manual/r.txt`, "Age" and "Temperature"): the world-setup options. Added to §10.

CivFanatics:

- https://forums.civfanatics.com/threads/things-civilopedia-cant-explain.470744/ : nothing new for section 7 (upgrade costs, flak and SAM odds, section 1).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/ (page 1) and /page-2 : nothing new for section 7 (city worker cap of 20, pollution above size 12, Steph's 2008 wish list).
- https://forums.civfanatics.com/threads/worker-moves.93408/ and /page-2 : what cancelling loses (GD_france, KayEss "mainly conjecture"); Thot on slaves and roads. Added to §5.2a.
- https://forums.civfanatics.com/threads/maya-flood-plain-start.74466/ : Qitai, disease strikes again the next turn, re-rolled on the third. Added to §7.
- https://forums.civfanatics.com/threads/small-farms.693526/ : BlackBetsy's passing "irrigate/ railroad ... 4 food". Added to Disputed 1.
- https://forums.civfanatics.com/threads/what-is-the-best-map-settings-normal-wet-arid-warm-temperate-cool.297735/ : player views on age and wetness. Added to §10.
- https://forums.civfanatics.com/threads/no-coal-anywhere.340842/ : timerover51, about one of each strategic resource per civ. Added to §6.2 with the generator arithmetic.
- https://forums.civfanatics.com/threads/keeping-resources-in-check-no-phantom-resources.391951/ : nothing new (the 32-resource limit; standard C3C unaffected).
- https://forums.civfanatics.com/threads/how-to-add-resources-to-c3c-a-guide.105217/ : nothing new (editor steps).
- https://forums.civfanatics.com/threads/are-camps-useful-in-c3c.361305/ : nothing new ("camps" are specialised cities).
- https://forums.civfanatics.com/threads/workers-what-do-i-do-with-them.276289/ : nothing new (worker counts, opinion).
- https://forums.civfanatics.com/threads/barbarian-spawning.689280/ : not Civ3 (Civ5 Vox Populi).
- https://forums.civfanatics.com/threads/limit-the-number-of-barbarians.674182 : not Civ3 (Civ4).
- https://forums.civfanatics.com/threads/barbarian-spawn-gamebreaking.602454 : not Civ3 (Civ6).
- https://forums.civfanatics.com/threads/how-to-use-barbarians-as-settlers.166058 : not Civ3 (Civ4).

Apolyton (codehappy.net mirror and apolyton.net):

- https://codehappy.net/apolyton/threads/95755-1.htm : no barbarian upgrade by era in vanilla/PTW (Kramsib, CerberusIV). Added to §11.2 and the open question.
- https://codehappy.net/apolyton/threads/60492-1.htm : second-hand Industrial-era uprising trigger (Konquest02); camp gold and spawn opinions. Added to §11.2.
- https://codehappy.net/apolyton/threads/37568-1.htm and -2.htm : Allemand's test that unroaded resources do not deplete; Skanky Burns's oil never reappearing. Added to §6.2.
- https://codehappy.net/apolyton/threads/60246-1.htm, -2.htm, -3.htm : what global warming does to terrain; the editor switch. Added to the open question.
- https://codehappy.net/apolyton/threads/76575-1.htm : Mad Bomber's Civ2-derived 1-in-9 warming ratio (recorded, not used); Iron Works pollution comparison (section 2).
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/121265-irrigation-through-hills-with-cities and /page2 : irrigation through a city on a hill in C3C; Jaybe on C3C lakes. Added to the open questions and Disputed 13.
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/55680-strategic-resources-not-there-why and /page2 : nothing new beyond the editor help (placement by original terrain; 1.17 visibility observation).
- https://codehappy.net/apolyton/threads/40421-1.htm and -2.htm : nothing new (scarcity anecdotes; editor fields).
- https://codehappy.net/apolyton/threads/35769-1.htm : nothing new (1.17f placement claims, unverified).
- https://codehappy.net/apolyton/threads/71404-1.htm : nothing new for section 7 (Nor Me's untested AI resource-price formula, section 6).
- https://codehappy.net/apolyton/threads/100571-1.htm : nothing new (colony anecdotes).
- https://codehappy.net/apolyton/threads/76212-1.htm : PTW AI stops irrigating grassland in Despotism (joncnunn); used in section 8.
- https://codehappy.net/apolyton/threads/59554-1.htm : automated workers put pollution first (Harovan); used in section 8.
- https://codehappy.net/apolyton/threads/43315-1.htm : nothing new (1.17f contact crash, hut armies).
- https://codehappy.net/apolyton/threads/35557-1.htm : nothing new for section 7 (Recycling Center scope, section 2).
- https://codehappy.net/apolyton/threads/60600-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/33565-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/52277-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/34565-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/79678-1.htm : nothing new.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/120973-absorbing-initial-worker : nothing new.
- https://codehappy.net/apolyton/threads/52270-1.htm : nothing new (no barbarian rules).
- https://codehappy.net/apolyton/threads/101612-1.htm : nothing new (culture-linked starts, opinion).
- https://codehappy.net/apolyton/threads/46606-1.htm : nothing new (1.17f start observations).
- https://codehappy.net/apolyton/threads/62455-1.htm : nothing new.
- http://www.civforum.de/showthread.php?t=66671&page=3 : nothing new (city finds only for Expansionist civs, already in §12).

Still unread for section 7: `Other uses for workers` 13744, `Custom Map - Raging Barbarians` 460704 (scenario maps), `Settle the Land` 396622, the seed threads 703383 / 703324 / 264492, `Resources` 158705, `Oil` 99693, `Needing more Strategic Resources` 361762, `Never getting any strategic resources` 9405, `Forest chop diagram` (image), `Today I Learned` pages 2-9, `Useful Patches` pages 3-12.

## Third reading round (code readings)

C3X source read from a local clone of https://github.com/maxpetul/C3X (commit d4e71c4, 12 Oct 2026). Only its statements about what the unmodded 1.22 game does are used; mod features are out of scope.

- https://github.com/maxpetul/C3X/blob/master/Trade%20Net%20X/TradeNetX.cpp : contribution: rivers do not carry trade (open question resolved).
- injected_code.c (camp and hut location checks, eruption damage, the barbarian production count): nothing new (wrappers only).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-3 to page-12 (re-read with all sections in view): nothing new for section 7 (#134: two land and one naval barbarian unit type, already noted).
- https://forums.civfanatics.com/threads/minimum-city-distance.661984/ : nothing new.
- https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/ (192 pages): read by helper agents in this round; their results were not merged before hand-back (see the round report).
