# Reading log: section 5 (Governments) deep pass

Each line gives a URL or local path that was read, and what it added to `05-governments.md`.

## Local first-hand sources

- `Conquests/Text/Civilopedia.txt` (GCON_Revolution, GCON_Unit_Support, GCON_Governments, GCON_Mobilization, GCON_Draft, GCON_Nationality, GCON_Resistance, GCON_Assimilation, GCON_War_Weariness, GCON_We_Love_The_King_Day, GCON_Disorder, GCON_Hurry_Production, GCON_Strengths, BLDG_Palace): the draft limit is per city; the assimilation precondition; foreign nationals at war; WLTKD effects (no despotism-penalty removal); no civil-war text.
- `civ3PTW/Text/Civilopedia.txt` and `Text/Civilopedia.txt` (GCON_Unit_Support, GCON_War_Weariness): the PTW/vanilla support tables (Communism 2/4/8, Republic 0/0/0) and WW for Republic and Democracy only.
- `Conquests/Text/script.txt`: no hurrying in civil disorder or resistance (`#HURRY_CIVIL_DISORDER`, `#HURRY_RESISTANCE`); no civil-war or capital-loss popup.
- `README.txt`, `Conquests/readme.txt`, `civ3PTW/README.txt`: nothing new beyond what was already cited. The 1.17f lines on spaceship and capital and on civs with no capital were used in 5.14.
- `Conquests/conquests.biq` PRTO `RequireSupport`, dumped with QueryCiv3 (scratchpad `prtodump`): 110 unit records need support, including Settler, Worker, Explorer, Leader and Army. The 31 King units do not.
- `Conquests/conquests.biq` GOVT (existing scratchpad `govdump/c3c.txt`): `RequiresMaintenance` is 1 for all governments except Anarchy (this settles the Feudalism maintenance question). `TilePenalty` and `TradeBonus` flags.
- `conquests.biq` DIFF (existing scratchpad `biq.txt`): rechecked `MaxGovernmentTransitionTime` per level for the comparison with justanick.
- EDHELP `Requires_Support_Unit_Statistics_.htm`, `Free_Units_Government_Properties_.htm`, `Cost_per_Unit_Government_Properties_.htm`, `Flags_Government_Properties_.htm`, `Draft_Limit_Government_Properties_.htm`, `War_Weariness_Government_Properties_.htm`, `Hurrying_Production_Government_Properties_.htm`, `Immune_To_Government_Properties_.htm`, `Resistance_Selector_Government_Properties_.htm`, `Reduces_War_Weariness_Improvement_Flag_.htm`: the Requires Support flag and the per-turn draft limit. The others had nothing new.

## Web sources

- https://forums.civfanatics.com/threads/governments-civilization-3-complete-v1-22.276379/ : shows CFC-GOV is this guide (Theov, 2008). Feudalism maintenance dispute (Mursi lives vs Spoonwood/CKS).
- https://forums.civfanatics.com/threads/governments-civilization-3-complete-v1-22.276379/page-2 : support covers all units except foreign ones (justanick, Lanzelot, Theov); Anarchy cost 0; "2 to 9 turns".
- https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/ : Jesse Smith (Firaxis) government table; Tavis on Feudalism no-maintenance being dropped; trade bonus only for Republic/Democracy; Roundman on Fascism forced labour.
- https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/page-2 : Tavis "Fascism DOES require maintenance"; Tavis's favourite/shunned Fascism list.
- https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/page-3 : nothing new (forum restatements).
- https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/page-4 : playshogi's town/city/metro sizes; otherwise nothing new.
- https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/page-5 : nothing new (necromancer's "no maintenance" claim contradicts the BIQ).
- https://forums.civfanatics.com/threads/c3c-government-breakdown.67419/page-6 : nothing new.
- https://forums.civfanatics.com/threads/compiled-list-of-bugs-and-non-bugs-not-a-dicussion-thread.70063/ : C3C 1.00 bugs: SPHQ after leaving Communism; unset matrices vs Fascism/Feudalism; AI WW turn order; anarchy research via riots; the German Feudalism hearsay; "Religious civs now have up to 2 turns" (not a bug).
- https://forums.civfanatics.com/threads/how-does-unit-support-work.76197 : workers and settlers count (Speedo).
- https://forums.civfanatics.com/threads/allowed-units.191722 : MAS's per-version support table, including Anarchy 0 gpt.
- https://forums.civfanatics.com/threads/governments.89933/ : nothing new (opinions; page 1 of 2 read).
- https://steamcommunity.com/app/3910/discussions/0/1480982971158365388 : nothing new.
- https://forums.civfanatics.com/threads/question-about-drafting-in-civ3.107429/ : draft unhappiness lasts 20 turns; the wrong Communism-3 claims.
- https://codehappy.net/apolyton/threads/53252-1.htm : nothing new (another wrong Communism-3 claim; drafting foreigners is anecdotal).
- https://codehappy.net/apolyton/threads/62518-1.htm : a second-hand Soren relay that WW accumulates under any government (superseded by 55847-2); otherwise estimates only.
- https://codehappy.net/apolyton/threads/55847-1.htm : per-civ WW pools; peace lowers WW (forum observations).
- https://codehappy.net/apolyton/threads/55847-2.htm : **Soren Johnson's first-hand post**: the WW factor is kept under all governments, only affects Republic/Democracy, and decays hidden.
- https://codehappy.net/apolyton/threads/50455-1.htm : nothing new (anecdotes; page 1 of 2).
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/ : CFC-WW is vanilla research by save-file inspection; the AI-AI turn-order observation.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-2 : Oystein's C3C retest ("Everything seems to be the same"); alexman's retraction; the 100% cap; the combat rule.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-3 : the 31-point threshold; rounding down; per-civ storage; flak kills; the 999 overflow; Democracy revolt wording.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-4 : points are independent of government; war happiness carries over; phoney war; no WW from flips.
- https://forums.civfanatics.com/threads/how-does-war-weariness-work.61628/page-5 : recapturing a flipped city adds WW; otherwise restatements.
- https://codehappy.net/apolyton/threads/57182-1.htm : assimilation "LONGER than their own civ" (Jaybe).
- https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/81314-nationality-assimilation : assimilation rates match the BIQ; ownership-time precondition (Mad Bomber, Traelin).
- https://forums.civfanatics.com/threads/assimilation-resistance-is-futile.10377 : nothing new (anecdotes).
- https://forums.civfanatics.com/threads/civil-war.131672 : Civ2 thread; nothing for Civ3.
- https://forums.civfanatics.com/threads/splitting-civilizations.6857 : Civ2 thread; nothing for Civ3.
- https://forums.civfanatics.com/threads/civil-war.84798/ : "Missed them in Civ III" (RegentMan), supporting no civil war.
- https://codehappy.net/apolyton/threads/33666-1.htm : Civ1/Civ2 discussion; nothing for Civ3.
- https://forums.civfanatics.com/threads/empire-split-by-fall-of-the-capital-when-playing-with-less-than-7-civs.674921/ : Civ1 (quotes the *Rome on 640k a Day* guide); not Civ3.
- https://codehappy.net/apolyton/threads/34719-1.htm : capital capture moves the palace free, with no other effect (2001 reports).
- https://forums.civfanatics.com/threads/looking-for-the-formula-on-how-the-palace-gets-relocated.703374/ : the palace-jump formula restated; an unresolved discrepancy.
- https://civfanatics.com/civ3/strategy/game-mechanics/free-palace-jump/ : palace-jump scoring and tie-break.
- https://forums.civfanatics.com/threads/duration-of-anarchy.683384/ : C3C anarchy bands; Religious 2 turns; AI caps by level; the new-tech turn counts.
- https://forums.civfanatics.com/threads/anarchy-question.452205 : re-revolt behaviour (CKS); 9 turns in C3C.
- https://forums.civfanatics.com/threads/how-to-define-time-of-anarcy.440076 : 9 turns in C3C (vmxa); Lanzelot's 2-9 with a difficulty claim.
- https://forums.civfanatics.com/threads/you-say-you-want-a-revolution.116180 : Golden Age halving anarchy (anecdote).
- https://forums.civfanatics.com/threads/anarchy-when-changing-governments.117324 : nothing new.
- https://forums.civfanatics.com/threads/anarchy-gamble.62465/ : specialists work during anarchy; the popup revolt counts as last turn (Qitai); 9 turns in C3C.
- https://forums.civfanatics.com/threads/cry-anarchy.80100 : Civ2 thread; nothing new.
- https://forums.civfanatics.com/threads/surviving-anarchy.308243 : the "no MP, no luxury slider" claim.
- https://forums.civfanatics.com/threads/quick-answers.100914/page-5 : C3C Religious anarchy timing (Turner).
- https://codehappy.net/apolyton/threads/42739-1.htm : **Soren Johnson's first-hand anarchy formula** (1-5 random plus 0-3 by city count; Religious always 1), Feb 2002.
- https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ : C3C anarchy +1 turn (SirPleb); Feudalism WW "like Republic".
- https://forums.civfanatics.com/threads/a-quantitative-comparison-of-governments.131546/ : nothing new (support formulas restated; opinions; page 1 of 2).
- https://forums.civfanatics.com/threads/fascism-vs-communism.81587/ : nothing new (opinions; page 1 of 3).
- https://forums.civfanatics.com/threads/the-joys-of-whipping-communism.112249/ : nothing new.
- https://forums.civfanatics.com/threads/the-benefits-of-whipping-citizens.76152/ : the whip-order guess (Ision).
- https://civfanatics.com/?p=128 : this is the Civ1 FAQ; its hurry formula does not apply to Civ3 (noted in Disputed).
- https://forums.civfanatics.com/threads/anyone-know-the-formulae-for-rush-building.18881 : Civ2 forum thread; the formula already cited from it is discarded (Disputed).
- https://codehappy.net/apolyton/threads/73546-1.htm : half-pop whip cap (Dominae); empty-box doubling for both rush types (Bluefrog).
- https://forums.civfanatics.com/threads/wealth.24405 : nothing new (Wealth conversion only).
- https://forums.civfanatics.com/threads/how-to-wage-an-effective-war-with-a-democratic-government.22759/ : nothing new (strategy anecdotes; page 1 of 5).
- https://civfanatics.com/civ3/strategy/game-mechanics/fundamentals-of-mobilization-revealed/ : Mobilization rules. These belong to the city-economy section and were not added to section 5.
- https://codehappy.net/apolyton/threads/72862-1.htm : nothing new (AU scenario rules only).
- https://codehappy.net/apolyton/threads/32558-1.htm : the Democracy worker bonus applies to Workers only.
- https://codehappy.net/apolyton/threads/41330-1.htm : nothing new (page 1 of 3, opinions).
- https://codehappy.net/apolyton/threads/63161-1.htm : whip unhappiness moving with a settler (Catt, relayed).
- https://codehappy.net/apolyton/threads/55285-1.htm : alexman (Firaxis) on Communism corruption by city count; Communism draft 2 checked in the editor.
- https://codehappy.net/apolyton/threads/84801-1.htm : "there is no senate and no penalisation for having your units out of your cities" (Kramsib).
- https://forums.civfanatics.com/threads/some-thoughts-on-despotism.69616/ : wilbill's claims on favourite/shunned governments.
- https://codehappy.net/apolyton/threads/25003-1.htm : relayed Dan Magaha on WLTKD vs Golden Age; nothing on the despotism penalty.
- https://forums.civfanatics.com/threads/drop-a-civ-from-democracy.68817/ : Civ2 thread; nothing new.
- https://forums.civfanatics.com/threads/when-to-get-out-of-despotism.430084/ : nothing new (page 1 of 3).
- https://forums.civfanatics.com/threads/monarchy-or-feudalism.114078/ : Feudalism WW same as Republic (gunkulator).
- https://forums.civfanatics.com/threads/fuedalism.76928/page-2 : nothing new (opinions).
- https://forums.civfanatics.com/threads/what-is-standard-tile-penalty.108487/ : the tile penalty is -1 above 2, includes commerce, and the help file is inaccurate.
- https://civfanatics.com/civ3/faq/ (first 100k characters) : the full CFC-FAQ anarchy formula including the size term; trade bonus scope; tile penalty; draft per city per turn.
- https://civfanatics.com/civ3/strategy/game-mechanics/a-study-of-espionage-missions/ : nothing new; the matrix direction is not explained.
- https://forums.civfanatics.com/threads/help-martial-law.86270 : Civ2 thread; nothing for Civ3.
- https://forums.civfanatics.com/threads/martial-law-not-working.33328 : Civ2 thread; nothing for Civ3.
- https://forums.civfanatics.com/threads/few-questions-about-the-game.284761/ : Civ2 thread; nothing for Civ3.

No fetch failed. No HTTP 429 was hit; WebFetch worked for every CFC URL. Plain curl gets HTTP 403 from CFC's bot protection.

## Section-5 sources still unread

- Government comparison threads: monarchy-vs-republic 54897, monarchy-or-republic 205540, democracy-versus-republic 332136, monarchy-or-communism 47265, governments 15175, whats-the-best-government 67393, best-government-type 654364, democracy-republic 703950. These are likely opinion.
- Later pages of: 131546 (p2), 81587 (p2-3), 22759 (p2-5), 89933 (p2), 41330 (p2-3), 63161 (p2), 72862 (p2), 50455 (p2), 114078 (p2-3), 430084 (p2-3).
- `Massive Despot Rush strategy` 10093; Apolyton 43034 (Fascism in 1.18, probably a mod), 31350 (Despotism forever?).
- Apolyton `No culture for you, revolution calling...` 82952 and `Various questions (civ improvements)` 116300 (anarchy).
- Apolyton WW threads 36705, 38373, 92121; `War happines works!` 246590; Apolyton `Assimilation`-related 58829, 32096, 97150; `Immigration` 332756.
- Mobilization Apolyton original (codehappy 51788). Patch v1.21f ReadMe thread 20502: the local README already holds the text.
- The rest of the CFC FAQ page (offset 100000).
