# Reading log, section 3 (Happiness, culture and borders)

Deep pass of 2026-10-20. One line per URL or local source read: what it contributed to `03-happiness-and-culture.md`, or "nothing new", or "fetch failed".

## Local first-hand sources

- `Conquests/conquests.biq`, read with the repo's QueryCiv3 reader through a scratch dump program. Gave the C3C defaults for: born-content, `MilitaryLaw` (1), MP limits, `WarWeariness` (Feudalism = Republic), `DraftLimit`, `Hurrying`, `AssimilationChance` (1/1/2/4/2/4/2/3), the per-government-pair `ResistanceModifier` matrix, `Xenophobic`, CTZN outputs (Entertainer lux 1), CULT table, RULE riot/draft/hurry/WLTKD/border values, building and wonder content faces, culture values (all wonders and small wonders), the Police Station, Universal Suffrage and Courthouse flags, no "+50% Luxury" building, and the tourist-attraction flags.
- `Conquests/Text/Civilopedia.txt`: `#GCON_Commerce`, `#GCON_Moods`, `#GCON_Disorder`, `#GCON_Draft` (conscript unit list), `#GCON_Conversion`, `#GCON_Assimilation` (the "minority population longer than not" precondition), `#GCON_Nationality`, `#GCON_Resistance`, `#GCON_War_Weariness`, `#BLDG_Marketplace`, `#BLDG_Universal_Suffrage`, `#BLDG_Mausoleum`, `#BLDG_Cure_For_Cancer`, and the tourist-attraction lines (they match the BIQ flags).
- `Conquests/Text/script.txt`: `#CIVIL_DISORDER_INTENSIFIES` (one improvement destroyed per riot), `#REVOLUTION`, propaganda success, failure and "upset its citizens" messages, `#WE_CONVERT_CITY` ("Rebuff the rebels"), `#THEY_REFUSED_TO_CONVERT_CITY`, `#WE_CONVERT_CITY_WITH_WONDER`, and the `$CULTURAL_LEVEL` uses.
- Editor help (decompiled CHM): `Number_of_Citizens_Quelled_by_Military`, What's New ("Difficulty Level Page … defaults to 1"), `Assimilation_Chance`, `Chance_of_Rioting`, `Border_Expansion_Multiplier`, `Citizen_Luxury_Bonus`, `Culture_Ratio`, `Doubles_Happiness_Of`, `Happy/Unhappy_Faces*`, `Reduces_War_Weariness`, `Resistance_Modifier`, `Resistant_to_Propaganda`, `War_Weariness`, `_50_Luxury_Output`, `Military_Police_Limit`, `Resistance_Chance`, `Continued_Resistance`, `Hurrying_Production`, `Draft_Limit`. These gave definitions quoted in §3.4, §3.12 and §3.13.
- Manual text (scratch extract of the Complete manual): civil disorder, restoring order, WLTKD and entertainment passages. These were already cited; nothing new, and no Marketplace-luxury statement was found.
- Civ3 Strategy Compilation (scratch extract): nothing new on riots or flips.

## CivFanatics

- https://forums.civfanatics.com/threads/mechanics-of-happy-content-faces.701169/ : two faces per unhappy-to-happy citizen, the worked order of application, "During peace WW is suspended", and 1 luxury point = 1 face.
- https://civfanatics.com/civ3/strategy/game-mechanics/the-inner-workings-of-resistance-revealed/ : the garrison quelling cap, excluded units, the ±5 government modifier and its exceptions, the Mike B. (Firaxis) credit, and resisters starving first.
- https://forums.civfanatics.com/threads/the-inner-workings-of-resistance-revealed.56487/ : author WillJ (Jun 2003); the setting is on the Difficulty tab; MadHatter's flip correction.
- https://forums.civfanatics.com/threads/the-inner-workings-of-resistance-revealed.56487/page-2 : KrikkitTwo's test (quelling depends only on units), and WillJ's "no point in more units than resistors".
- https://forums.civfanatics.com/threads/the-eight-laws-of-border-dynamics.106882/ : the eight border laws (rank claims, tie-breaks, ocean rank 2, gap filling), plus the disputes.
- https://forums.civfanatics.com/threads/the-eight-laws-of-border-dynamics.106882/page-2 : the distance metric (1 and 1.5, rounded down), from punkbass2000.
- https://forums.civfanatics.com/threads/indispensible-culture-reference-guide.37681/ : nothing new. The data is only in a zip attachment, which wasn't read.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/ (page 1): the original rules, MadScot's "total active WWP" objection, and the AI bug.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-2 : caps, the 125% Police Station note, combat rules, war happiness of 1 per 4 citizens, and alexman's retraction.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-3 : round-down, the 31-point threshold, "20 turns of peace does NOT mean that ww is gone", and LordKestrel on Democracy's revolt.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-4 : points build up under any government, carry-over to later wars, US "1 unhappy content", and no WW from flips.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-5 : Lord Emsworth (points under Monarchy, "Overall" per turn), and justanick's 31-point calculation.
- https://forums.civfanatics.com/threads/war-happines-works.246590/ : war happiness is rounded per enemy, and alliance-triggered declarations give none.
- https://forums.civfanatics.com/threads/help-martial-law.86270 : a Civ2 thread, flagged in §3.4. The first try got HTTP 429, and the retry worked.
- https://forums.civfanatics.com/threads/martial-law-not-working.33328/ : a Civ2 thread, flagged in §3.4.
- https://forums.civfanatics.com/threads/happiness-and-avoiding-civil-disorder.22210 : a Civ2 thread, so nothing for Civ3.
- https://forums.civfanatics.com/threads/few-questions-about-the-game.284761/ : a Civ2 thread, so nothing for Civ3.
- https://civfanatics.com/?p=396 ("Ways into Happiness"): a Civ4 page, so nothing for Civ3.
- https://forums.civfanatics.com/threads/drafting-for-fun-and-profit.216509 : a Civ4 thread, so nothing for Civ3.
- https://forums.civfanatics.com/threads/question-about-drafting-in-civ3.107429/ : conscript units, the 20-turn penalty, and wrong limits of 3 (contradicted by the BIQ).
- https://forums.civfanatics.com/threads/how-do-i-draft-military.13697/ : Toasty's "only YOUR nationality" claim (base game), and early wrong draft limits.
- https://forums.civfanatics.com/threads/invading-when-youre-low-on-culture.61562/ : a city cut off from the capital flips back with a weak defender.
- https://forums.civfanatics.com/threads/cultural-conversion-question.87425/ : re-fetched to verify the free-defender quote (Halcyon #2), and units outside the city are kept (Grille #3).
- https://forums.civfanatics.com/threads/stopping-culture-flip.69295/ : nothing new (anecdotes; page 1 of 2 only).
- https://forums.civfanatics.com/threads/culture-flip-solution-by-zachriel.77672 : nothing new (garrison rules of thumb).
- https://forums.civfanatics.com/threads/chance-to-culture-flip.67626/ : nothing new.
- https://forums.civfanatics.com/threads/no-more-culture-flips.61190/ : nothing new.
- https://forums.civfanatics.com/threads/how-do-i-prevent-culture-flips.89878 : nothing new.
- https://forums.civfanatics.com/threads/tips-for-avoiding-a-flip-after-capturing-an-enemy-city.453267/ : nothing new.
- https://forums.civfanatics.com/threads/ridiculous-culture-flip-a-deal-breaker.491546/ : nothing new.
- https://forums.civfanatics.com/threads/babylon-ii-civ3-culture-flip-calc.53181/ : the calculator formula (Chieftess) and the Sirp/sgrig/anarres notes. Cited in the §3.15 table.
- https://forums.civfanatics.com/threads/53228/ (HTML Flip Calc): nothing new. The code is in an attachment, not read.
- https://forums.civfanatics.com/threads/offensive-border-manipulation.84761/ : nothing new (razed-city borders anecdote).
- https://forums.civfanatics.com/threads/how-to-make-strong-borders.160678/ : nothing new (military strategy).
- https://forums.civfanatics.com/threads/problem-culture-border-of-city-doesnt-expand.537508/ : Fascist captured cities don't expand borders until your nationals are the majority.
- https://forums.civfanatics.com/threads/assimilation-resistance-is-futile.10377 : nothing new (anecdotes).
- https://forums.civfanatics.com/threads/immigration.332756 : not Civ3 (a general ideas thread), so nothing new.
- https://forums.civfanatics.com/threads/a-small-trick-to-keep-ai-happy.57086/ : nothing for section 3 (AI gifts).
- https://forums.civfanatics.com/threads/anarchy-when-changing-governments.117324 : not Civ3-specific (a series-wide ideas thread), so nothing new. The first try got HTTP 429, and the retry worked.
- https://forums.civfanatics.com/threads/cant-stop-my-descent-into-anarchy.28256 : Democracy collapsing from war weariness without disorder (camens #1).
- https://forums.civfanatics.com/threads/civ3-governments-and-why-its-good.552047 : nothing new.
- https://forums.civfanatics.com/threads/problem-with-democracy.266398 : fetch failed (HTTP 429). Not retried.
- https://civfanatics.com/civ3/civilopedia/improvements/ : the Marketplace text mentions luxury resources but no +50% luxury.

## Apolyton (codehappy mirror and apolyton.net archive)

- https://codehappy.net/apolyton/threads/97150-1.htm ("formula for defection chance?"): nothing new (it links to 39825).
- https://codehappy.net/apolyton/threads/57182-1.htm ("Assimilation formula?"): whosurdaddy's 90% editor test (no assimilation), player1's majority hearsay, and dunk on foreign citizens' war weariness.
- https://codehappy.net/apolyton/threads/39825-1.htm (Culture-Flipping Exposed, Apolyton copy, page 1): Magaha's ranking of the factors.
- https://codehappy.net/apolyton/threads/39825-2.htm : "I would assume this is measured in tiles", no return of dead civs (vmxa1), and garrison anecdotes.
- https://codehappy.net/apolyton/threads/39825-3.htm : courthouses don't affect flips (Purest Warrior), plus anecdotes.
- https://codehappy.net/apolyton/threads/39825-4.htm : 1.17f garrison suppression report, plus anecdotes.
- https://codehappy.net/apolyton/threads/39825-5.htm : no flips to destroyed civs (Explorer579).
- https://codehappy.net/apolyton/threads/49417-1.htm (culture flipping in 1.21): Soren's 500–8000 die range, as relayed by Fitz.
- https://codehappy.net/apolyton/threads/51825-1.htm : nothing new (page 1 of 2).
- https://codehappy.net/apolyton/threads/56774-1.htm to https://codehappy.net/apolyton/threads/56774-5.htm (Culture Flipping:, 5 pages): nothing new. Jon Shafer (Firaxis) posts only opinion and moderation.
- https://codehappy.net/apolyton/threads/51715-1.htm : re-checked for capitals, refusal and truncation. Nothing beyond what was already cited.
- https://codehappy.net/apolyton/threads/52796-1.htm : nothing new (page 1 of 3).
- https://codehappy.net/apolyton/threads/58488-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/39361-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/53387-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/62844-1.htm : flips only to civs with nationals or overlapping territory, and "never heared of a capital flipping" (DeepO).
- https://codehappy.net/apolyton/threads/68944-1.htm : nothing new (contradictory anecdotes).
- https://codehappy.net/apolyton/threads/33594-1.htm : nothing new (anecdotes).
- https://codehappy.net/apolyton/threads/33752-1.htm : nothing new for section 3 (border opinions).
- https://codehappy.net/apolyton/threads/28079-1.htm : nothing new (a pre-release thread).
- https://codehappy.net/apolyton/threads/33038-1.htm : nothing new (a question with no answers).
- https://codehappy.net/apolyton/threads/76110-1.htm : The Mad Monk's test of the 1000-year doubling.
- https://codehappy.net/apolyton/threads/58829-1.htm : guesses on how armies count for quelling (left open).
- https://codehappy.net/apolyton/threads/32096-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/53252-1.htm : the unresolved question of drafting foreign citizens.
- https://codehappy.net/apolyton/threads/61665-1.htm (The Power of the Luxury Slider, page 1): 1 gold = 1 citizen step, and the Marketplace doesn't boost the slider (Rhothaerill).
- https://codehappy.net/apolyton/threads/61665-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/28755-1.htm : nothing new (a pre-release luxury table, matching the Civilopedia).
- https://codehappy.net/apolyton/threads/38098-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/35096-1.htm (What exactly does WLT*D do?): Thoth's "nowhere near as powerful as it was in Civ I/ II" and the corruption claim.
- https://codehappy.net/apolyton/threads/44579-1.htm (WLTK Days…): Dan Magaha's (Firaxis) conditions and benefits, plus players' "happy >= content" reports.
- https://codehappy.net/apolyton/threads/83368-1.htm : the carry-over order (Spiffor, bongo), and the Marketplace not boosting the slider (vulture).
- https://codehappy.net/apolyton/threads/43251-1.htm (more disorder in 1.17?): nothing new (governor behaviour only).
- https://codehappy.net/apolyton/threads/31220-1.htm : specialists get no building multipliers (Sarxis, 2001).
- https://codehappy.net/apolyton/threads/35021-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/50455-1.htm (Expertise on War Weariness, page 1): nothing new (secondhand Soren on traditional enemies; no tests).
- https://codehappy.net/apolyton/threads/55847-1.htm (War weariness carryover, page 1): nothing new beyond the per-civ pools. jabberwockysr's assimilation recollection matches the BIQ.
- https://codehappy.net/apolyton/threads/62518-1.htm : Soren (relayed by Jaybe), "WW accumulates even if you are in a government with no WW".
- https://codehappy.net/apolyton/threads/36705-1.htm : nothing new (page 1 of 2).
- https://codehappy.net/apolyton/threads/38373-1.htm : nothing new.
- https://codehappy.net/apolyton/threads/92121-1.htm : alexman (Firaxis), WW exists in PBEM but rises more slowly.
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/81314-nationality-assimilation : WarpStorm's 4/2/1% (matches the BIQ), and Mad Bomber's and Traelin's precondition.
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/44876-holding-onto-conquered-cities : Hondo's editor test (the assimilation setting has no visible effect).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/116300-various-questions-civ-improvements : Egbert's hedged "anarchy may last longer" with disorder.

## Other

- https://web.archive.org/web/2020/https://forums.civfanatics.com/threads/help-martial-law.86270/ : fetch failed (WebFetch can't reach web.archive.org).
- Two web searches (government collapse from disorder; "Stop the aggression…" counts) found no Civ3 source beyond those above. The "disorder for more than 1 turn" snippet reads as Civ2 rules, and its thread wasn't identified.

## Second reading round (2026-10-20)

Local:
- `civ3mod.bic` (1.29f) and `civ3PTW/civ3X.bix` (1.27f), decompressed with QueryCiv3's `Util.ReadFile` and read raw at the C3C BLDG/GOVT/DIFF offsets: wonder and building culture identical to C3C (Disputed, wonder culture); no "+50% Luxury Output" flag on any building in any version (§3.3); draft limits and assimilation chances identical (§3.6); Deity content citizens 1 in 1.29f (Disputed, content base at Deity).

Web:
- https://forums.civfanatics.com/threads/problem-with-democracy.266398/ : not Civ3 (Civ2 General Discussions).
- https://forums.civfanatics.com/threads/stopping-culture-flip.69295/page-2 : anarres #24 restates the calculator form with 2000*D and "Government type is not relevent at all" (§3.15). Otherwise nothing new.
- https://codehappy.net/apolyton/threads/51825-2.htm : nothing new (flip anecdotes).
- https://codehappy.net/apolyton/threads/52796-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/52796-3.htm : nothing new (DeepO's untested "10 times the culture, one defender will silence 10 citizens").
- https://codehappy.net/apolyton/threads/36705-2.htm : nothing new (one reload anecdote on war weariness).
- https://codehappy.net/apolyton/threads/50455-2.htm : nothing new.
- https://codehappy.net/apolyton/threads/55847-2.htm : contribution: Soren Johnson #33, the war weariness factor is kept all game and shows only at war under Republic/Democracy (§3.5).
- https://codehappy.net/apolyton/threads/37559-1.htm : contribution: unhappiness from several pop-rushes adds up (untested models) (§3.6).
- https://forums.civfanatics.com/threads/we-love-the-king-day-defeats-corruption.13177/ : contribution: T-hawk, WLTKD acts on shields only, like an extra courthouse (Disputed, WLTKD corruption vs waste).
- https://forums.civfanatics.com/threads/53228/ (HTML Culture Flip Calc): attachment located.
- https://forums.civfanatics.com/attachments/flipcalc1-0-html.34465/ : contribution: DaveMcW's calculator code (real-valued culture ratio, distance factor 4/2/1/.5/.25, per-turn compounding) (§3.15 table).
- http://www.danwilliamson.net/FlipCalc.html : fetch failed (empty page).
- https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini (stock-behaviour descriptions only): contribution: tourism and building-culture doubling counted in years (1000 and 1500) (§3.10).
- https://forums.civfanatics.com/threads/indispensible-culture-reference-guide.37681/ (the culturereference2.zip attachment): not fetched; its building culture values are superseded by the raw BIQ-V/BIQ-PTW read.

## Still unread (section 3 entries)

- All the items listed after the first pass have now been read or settled (see the second round above). The base-game and PTW BIQs were read raw rather than through the repo's reader.
- Remaining low-value inventory entries in sources.md for this section (CFC ?p=396, probably Civ2; 216509, probably Civ4) were not checked.
