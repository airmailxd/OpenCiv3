# Reading log, section 4 (Science, techs and wonders)

Deep pass, 2026-10-20. One line per source: contribution / nothing new / fetch failed / not Civ3.

## Local first-hand sources
- `Conquests/Civ3ConquestsEdit.chm` (EDHELP; pages AI_to_AI_trade_rate, Cost_Civilization_Advance, Cost_Factor, Future_Tech_Cost, Min/Max Research Time, Era, Free_Techs, Miscellaneous_Effect_Flags, Rendered_Obsolete_By, Required_Goods_Must_Be_Within_City_Radius, Tech_Rate) — contribution: trade-rate meaning, CF applies to the AI, Future Tech "each", ToE "randomly selected", tourist attraction in turns, free techs wait for prerequisites, FP city count = OCN/2.
- `Conquests/conquests.biq` via QueryCiv3 (TECH, GOOD, BLDG) — contribution: Iron requires Iron Working, Coal requires Steam Power; Iron Works needs Iron + Coal; tech flavors all 0.
- `Conquests/Text/Civilopedia.txt` (GCON_Research, BLDG_* entries) — nothing new (already cited).
- `README.txt`, `civ3PTW/readme.txt`, `Conquests/readme.txt` — nothing new beyond what was cited (re-checked research/wonder lines).
- Steam manual PDF https://cdn.akamai.steamstatic.com/steam/apps/3910/manuals/manual.pdf (text extract) — contribution: switching research loses work; receiving the tech in progress; era gating; Future Technology; no production-switch penalty; rival completes wonder = excess lost; obsolete wonders keep culture; capture/raze rules; small wonders destroyed on capture.

## Web sources
- https://forums.civfanatics.com/threads/ais-favorite-techs.123345/ — contribution: C3C model, random element, Doc Tsiolkovski worked values.
- https://forums.civfanatics.com/threads/ais-favorite-techs.123345/page-2 — nothing new (2022 anecdotes).
- https://forums.civfanatics.com/threads/how-do-ais-value-techs-in-market.126379/ — contribution: player-observed trade-value factors, no formula.
- https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/page-2 — contribution: alexman #31 (AI cost constant across levels), random choice.
- https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/page-4 — contribution: C3C update ("Cannot be traded: 149/turns"), "other tech" = 1 per direct dependant, flavors excluded.
- https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/page-5 — nothing new.
- https://civfanatics.com/civ3/strategy/game-mechanics/what-will-the-ai-research-next/ — contribution: original list (no C3C entries).
- https://forums.civfanatics.com/threads/technology-list-and-cost.193586/ — contribution: 11.43% per known civ; C3C MTDG Emperor check.
- https://forums.civfanatics.com/threads/utility-techcalc.38631/ — contribution: exact rounding form (first try HTTP 429, retried OK).
- https://forums.civfanatics.com/threads/late-game-tech-rate-confusion.696592/ — contribution: C3C, AI pays the Regent price (first try 429, retried OK).
- https://forums.civfanatics.com/threads/how-much-techs-cost.62580/ — contribution: trade cost vs research cost after a wonder is built.
- https://codehappy.net/apolyton/threads/66516-1.htm — contribution: alexman (Firaxis) on 1.29f difficulty cost change.
- https://codehappy.net/apolyton/threads/33948-1.htm — contribution: switching loses research; beakers lost if the tech is obtained elsewhere (Weldon).
- https://codehappy.net/apolyton/threads/33948-2.htm — contribution: korn469 4-turn test; explanations of 3-turn techs.
- https://codehappy.net/apolyton/threads/33056-1.htm — nothing new (early 3-turn anecdotes).
- https://codehappy.net/apolyton/threads/44479-1.htm — nothing new.
- https://codehappy.net/apolyton/threads/31243-1.htm — nothing new (1.0 era 32-turn cap anecdotes).
- https://codehappy.net/apolyton/threads/62071-1.htm — contribution: ToE picks are chosen by the player (anecdote).
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/117372-50-turn-techs — contribution: alexman (Firaxis) on scientist waste at 0%, Philosophy free tech.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/121690-early-game-research-on-monarch — nothing new.
- https://forums.civfanatics.com/threads/the-role-of-the-specialist-citizen.108789/ — contribution: one beaker a turn finishes any tech at the cap (1.29f/PTW).
- https://forums.civfanatics.com/threads/science-slider-when-1-turn-from-tech-change-it-or-not.24350/ — contribution: T-hawk tests (no overflow; half kept on switch, disputed).
- https://forums.civfanatics.com/threads/science-slider-when-1-turn-from-tech-change-it-or-not.24350/page-2 — contribution: Jim overflow test; switch-back same turn; beakers kept on trade (claim).
- https://forums.civfanatics.com/threads/switching-research-in-progress.301893/ — not Civ3 (Civ4 forum).
- https://forums.civfanatics.com/threads/research-beaker-count.343211/ — not Civ3 (Civ4/BTS).
- https://forums.civfanatics.com/threads/struggling-research-tactics.346706/ — nothing new (tech buying strategy).
- https://forums.civfanatics.com/threads/oscillating-research-expenditure.13203/ — nothing new.
- https://forums.civfanatics.com/threads/crippling-the-ais-research-capability.9332/ — nothing new.
- https://forums.civfanatics.com/threads/changes-from-civ-to-ptw.52591 — nothing new.
- https://forums.civfanatics.com/threads/scoring-of-future-tech.31261/ — nothing for section 4 (score value of future techs; belongs to victory/score).
- https://civfanatics.com/civ3/faq/ (both halves) — nothing new.
- https://forums.civfanatics.com/threads/frequently-asked-questions-civ3-play-the-world-conquests.170282/ — contribution: Age of Science "is bugged" (first 100k chars only).
- https://forums.civfanatics.com/threads/editor-rules-explained.210035/ — nothing new beyond EDHELP (first 100k chars).
- https://forums.civfanatics.com/threads/patch-framework-for-c3c-v1-22.526966/ — contribution: Science Age code is NOPs in 1.22 (page 1 only).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285 — contribution: "Fix of the science age bug" (page 1 of 12 only).
- https://codehappy.net/apolyton/threads/42514-1.htm — contribution: Gramphos: obsolescence needs the owner's tech.
- https://codehappy.net/apolyton/threads/74795-1.htm — contribution: GL one-turn delay; contact = F4 screen; obsolete on owner's Education.
- https://codehappy.net/apolyton/threads/94996-1.htm — contribution: gifted GL city gave techs up to the Industrial Age.
- https://codehappy.net/apolyton/threads/84522-1.htm — contribution: Soren on AI wonder preferences; Bamspeedy tests.
- https://forums.civfanatics.com/threads/my-gl-vs-science-test-on-regent-level.73210/ — nothing new.
- https://forums.civfanatics.com/threads/scientific-leader.67636/ — nothing new (conditions only).
- https://forums.civfanatics.com/threads/scientific-leader-probability-generator-excel.69141/ — contribution: 3%/5% "hardcoded"; same in 1.00 and 1.22.
- https://forums.civfanatics.com/threads/2-x-sci-leaders.292238 — contribution: two SGLs from a Philosophy slingshot; odds arithmetic.
- https://forums.civfanatics.com/threads/how-do-you-get-a-scientific-leader.211436 — contribution: 3%/5%; SGL only in C3C; Philosophy free tech counts; appears in capital.
- https://codehappy.net/apolyton/threads/65178-1.htm — contribution: Mike Breitkreutz (Firaxis) on the Scientific free-tech algorithm.
- https://codehappy.net/apolyton/threads/65178-2.htm — nothing new (repeats the Firaxis quote).
- https://codehappy.net/apolyton/threads/102197-1.htm — contribution: Philosophy free tech is the player's pick.
- https://codehappy.net/apolyton/threads/102197-2.htm — contribution: examples of free picks.
- https://codehappy.net/apolyton/threads/102197-3.htm — contribution: pick is the next research choice after the popup; trade-before-pick trick.
- https://codehappy.net/apolyton/threads/68856-1.htm — contribution: trade rate is AI-only; Soren "deficit spending only" (secondhand).
- https://codehappy.net/apolyton/threads/42905-1.htm — contribution: Soren on AI-AI tech sales (half value only if nothing else to give).
- https://codehappy.net/apolyton/threads/42905-2.htm — nothing new (player claims).
- https://codehappy.net/apolyton/threads/61744-1.htm — nothing new (anecdotes on AI-avoided techs).
- https://codehappy.net/apolyton/threads/43622-1.htm — nothing new (design opinions on prebuilding).
- https://codehappy.net/apolyton/threads/30796-1.htm — nothing new (wonder changes from Civ2, early impressions).
- https://codehappy.net/apolyton/threads/49979-1.htm — nothing for section 4 (auto-raze and culture; section 3).
- https://codehappy.net/apolyton/threads/33686-1.htm — nothing new.
- https://forums.civfanatics.com/threads/a-guide-to-great-wonders-and-how-to-use-them.97063/ — contribution: research-wonder stacking claim; Hoover no pollution.
- https://forums.civfanatics.com/threads/a-guide-to-great-wonders-and-how-to-use-them.97063/page-2 — contribution: GL give-away-and-retake trick; ToE timing.
- https://forums.civfanatics.com/threads/obsolete.100124/ — contribution: obsolete per owner; culture kept; effects stop.
- https://forums.civfanatics.com/threads/how-can-i-tell-if-a-wonder-is-obsolete.107358/ — nothing new.
- https://forums.civfanatics.com/threads/captured-great-wonders-where-are-the-culture-points.72979/ — contribution: captor gets effects, not culture.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/46283-getting-golden-age-from-wonders — nothing new (2001 confusion).
- https://sullla.com/Civ3/strat9.html — nothing new.
- https://forums.civfanatics.com/threads/strange-triggering-of-golden-age-with-wonders.86585/ — nothing for section 4 (Vanilla: GA is checked when you build a GW and counts captured wonders; obsolete wonders not addressed; Golden Age section).
- https://sullla.com/Civ3/conquestsed.html — contribution: "2-5%" SGL chance (beta tester).

## Second reading round (2026-10-20)

Local:
- `civ3mod.bic` (1.29f) and `civ3PTW/civ3X.bix` (1.27f), BLDG read raw at the C3C offsets: vanilla/PTW wonder costs and culture match C3C except the Great Wall (200) and Shakespeare's Theatre (400) (4.9).

Web:
- https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini (stock-behaviour descriptions only): contribution: tourism thresholds in years (resolves the years/turns question); the Science Age bug mechanism; MGLs can't hurry wonders in stock, and the AI leader routine's rush bug (4.8, 4.9, Disputed 5).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-2 to page-12 : nothing new for this section (logged in full in reading-log-01).
- https://forums.civfanatics.com/threads/patch-framework-for-c3c-v1-22.526966/page-2 : nothing new (single-page thread).
- https://codehappy.net/apolyton/threads/74795-2.htm : nothing new (Great Wall anecdotes).
- https://codehappy.net/apolyton/threads/94996-2.htm : nothing new (Great Library opinions).
- WebSearch "civ3 conquests changing research lose beakers switch tech keep beakers": led to the three below.
- https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/117372-50-turn-techs : nothing new (pre-release reactions to the 50-turn cap; nothing on what happens at the cap).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/63400-sciences-surplus-or-science-waste : contribution: kring's switch-and-back test (vanilla, 2002) (Disputed 9).
- https://forums.civfanatics.com/threads/gain-beakers-from-conquest.390786 : not Civ3 (Civ5).
- https://forums.civfanatics.com/threads/great-scientists.270481/ : contribution: Chamnix's ~100-hut test with no SGL (disputed in the thread), SGLs can be hoarded, the 3%/5% recollection (4.8).
- https://forums.civfanatics.com/threads/scientific-trait-question.448033 : contribution: CommandoBob, a random tier-1 pick in C3C vs a fixed one in vanilla (4.8).

## Left unread (section 4 inventory and related)
- https://forums.civfanatics.com/threads/how-do-i-speed-up-my-science.277970/ ; https://forums.civfanatics.com/threads/how-to-advance-fast.413221/
- codehappy 24466, 42643 (Vanilla research anecdotes)
- https://forums.civfanatics.com/threads/tech-trade-valuation-help.217576 ; https://forums.civfanatics.com/threads/trade-is-difficulty-with-ai-civs.115977
- codehappy 70620, 67912, 48219, 40548, 49524, 40489 (trade valuation, diplomacy section)
- https://forums.civfanatics.com/threads/great-leader-strategies.132684/ ; https://forums.civfanatics.com/threads/early-sgl-please-help.690240/
- https://forums.civfanatics.com/threads/a-guide-to-great-wonders-and-how-to-use-them.97063/page-3 and page-4
- https://forums.civfanatics.com/threads/wich-are-the-best-wonders.9224/ ; https://forums.civfanatics.com/threads/the-4-rules-of-wonder-addiction.71238/ ; https://forums.civfanatics.com/threads/late-super-wonder-city.75104/ ; https://forums.civfanatics.com/threads/best-small-wonder.91398/ ; https://forums.civfanatics.com/threads/noob-question-of-wonders.215788
- Apolyton Heroic Epic thread (121330); codehappy 58814, 33007, 33343, 69775, 86087
- https://forums.civfanatics.com/threads/informative-tech-tree.9805/ ; Nethog PDFs; CFC interactive tech tree (Vanilla data)

## Third reading round (code readings)

C3X source read from a local clone of https://github.com/maxpetul/C3X (commit d4e71c4, 12 Oct 2026). Only its statements about what the unmodded 1.22 game does are used; mod features are out of scope.

- injected_code.c (`patch_Leader_update_great_library_unlocks`): contribution: the Great Library needs contact both ways (4.8).
- injected_code.c (`patch_Unit_can_hurry_production`), Civ3Conquests.h (`LeaderKind`), default.c3x_config.ini: contribution: MGLs are barred from spaceship parts as well as Great Wonders (Disputed 5).
- https://forums.civfanatics.com/threads/infinite-golden-age-and-science-age.526292/ : contribution: the Science Age status and end turn (+20), the multiplier code and the order-of-operations bug (4.8).
- Civ3Conquests.h (wonder and small-wonder flag enums): nothing new (the names match the BIQ flags).
- https://forums.civfanatics.com/threads/useful-patches-for-civ-3-conquests-v1-22.526285/page-3 to page-12 (re-read with all sections in view): nothing new for section 4.
- https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/ (192 pages): read by helper agents in this round; merged in the "C3X forum thread (666881)" round below.

## C3X forum thread (666881)

https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/ (192 pages, read in full for stock-game statements by four helper agents, notes in `c3x-thread-notes-1.md` to `-4.md`; pages 65-66, missing from the notes, fetched in this round). Quotes were re-checked against the posts with a summarising fetch tool, one page at a time; quotes that could not be re-checked (CivFanatics rate limits) are marked "(paraphrase from a summarised fetch)" in the section.

- Pages used for this section: 6, 52, 107, 109, 123, 139, 142, 158.
- Posts cited: post-16058309 (p6 #105), post-16346025 (p52 #1029), post-16624227 (p107 #2127), post-16638420 (p109 #2167), post-16719849 (p123 #2448), post-16829069 (p139 #2774), post-16839866 (p142 #2840), post-16884982 (p158 #3147), post-16345754.
- Pages 65-66: nothing new for section 4.
