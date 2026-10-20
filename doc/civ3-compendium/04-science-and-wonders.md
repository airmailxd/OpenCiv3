# 4. Science, techs and wonders

This section covers research cost, beakers, eras, tech trading, the AI's research choices, free-tech sources, and all Great and Small Wonders in Conquests (C3C 1.22, with Vanilla 1.29f / PTW 1.27f differences where sources give them). Scenarios, mods and the special C3C game modes are out of scope.

Source key. Local files read for this section, from the Steam "Civilization III Complete" install (`.../Sid Meier's Civilization III Complete/`):
- **BIQ** = `Conquests/conquests.biq` (C3C 1.22 default rules), read with this repo's `QueryCiv3` (BLDG, TECH, RULE, WSIZ, DIFF sections). Building costs are stored at 1/10 scale ("In Civ3 files, building costs are stored at 1/10th of their actual value", `C7Engine/C7GameData/ImportCiv3.cs`). Shield costs below are BIQ x 10.
- **Pedia** = `Conquests/Text/Civilopedia.txt` (Firaxis' C3C Civilopedia). This is not OpenCiv3's `C7/Civilopedia/Text/Civilopedia.txt`, which is our own text.
- **README-V** = `README.txt` (Vanilla patch notes up to 1.29f). **README-PTW** = `civ3PTW/readme.txt`. **README-C3C** = `Conquests/readme.txt`. **EdSupp** = `Conquests/EditorSupplement.txt`.

---

## 4.1 Research cost

- **Formula (Vanilla 1.29f, reverse-engineered).** Source: https://forums.civfanatics.com/threads/research-cost-formula-v1-29f.29485/ — "Research Cost = [MM * [10*COST * (1 - N/[CL*1.75])]/(CF * 10)] - Research done so far". The thread also gives a simplified form: "Research cost = DOWN{MM*DM*(COST/100) *[1 - (N/(CL*1.75))]} - Research done so far." The CivFanatics FAQ repeats the first form: https://civfanatics.com/civ3/faq/ — "Research Cost = [MM * [10*COST * (1 – N/[CL*1.75])]/(CF * 10)] – Research done so far".
  - **MM** (map size factor) — "MM = map modifier(tech rate on world sizes tab in the editor)". BIQ WSIZ tech rate: Tiny 160, Small 200, Standard 240, Large 320, Huge 400. These match the thread's values.
  - **COST** — "COST = technology cost as on the civilization advances tab in the editor." The BIQ cost is the base (for example Bronze Working 3, Integrated Defense 360).
  - **Check on a Standard map.** https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/ — "For a standard map (tech rate = 240), the AI needs 24 times the base cost of each technology in beakers." alexman's example: "So if Bronze Working were an empty tech, it would have a value of 256/18=14", where 18 = 72 beakers / 4 per turn.
  - **N, CL** ("civs that know it" discount) — "N = number of civs on the diplomacy screen that have discovered the tech." "CL = number of civs left in the game". Only contacted civs count toward N. Version note from the same thread: "CL*1.5 in 1.21f is now CL*1.75 in 1.29f." The 1.21f thread gives a different rounding form: https://forums.civfanatics.com/threads/ai-trading-valuation.21925/page-2 — "Research cost = MM*COST/10 - UP{MM/100 * UP[COST*10N/DOWN(CL*1.5)]} - Research done so far".
  - **CF / DM** (difficulty) — "CF = AI cost factor(as on the difficulty tab in the editor)" and "For the purposes of the research cost formula, CF has a maximum value of 10." Qitai's correction in the same thread: "Cost factor = {20, 12, 10, 9, 8, 6} for the various difficulty level", which gives DM {10, 10, 10, 11.1, 12.5, 16.7}. The author confirms: "the Monarch figure is 11.1". BIQ DIFF cost factors in C3C: Chieftain 20, Warlord 12, Regent 10, Monarch 9, Emperor 8, Demigod 7, Deity 6, Sid 4. Implementation reading: research cost x 10/min(CF,10), so x1.0 at Regent and below, x1.11 Monarch, x1.25 Emperor, x1.43 Demigod, x1.67 Deity, x2.5 Sid. The Demigod and Sid values are an UNVERIFIED extrapolation, because the thread predates PTW's extra levels.
  - **Whom it applies to.** Same thread: "This formula is relevant for research cost of the AI civs, not just the human player" and "techs are now more expensive for the human player on Monarch, Emperor and Deity." See Disputed, item 1.
- **Future techs.** BIQ RULE: Future Tech Cost = 400. The editor field was added in Vanilla (README-V: "Editor: On General Settings, added Future Tech Cost."). How 400 enters the formula is UNVERIFIED.
- **Integer rounding.** Commerce-to-beaker conversion floors, so some beakers are lost. https://forums.civfanatics.com/threads/the-research-slider.662300 (vorlon_mi) — "Some of those calculations use the "FLOOR" function, to produce an integer result that drops the fractional parts."

## 4.2 Minimum and maximum research time

- **C3C: 4 to 50 turns.** BIQ RULE: MinimumResearchTime = 4, MaximumResearchTime = 50. https://civfanatics.com/civ3/faq/ — "For all three games, the minimum research length is 4 turns." and "For **Conquests**, that was upped to 50 turns as the maximum."
- **Vanilla history.** README-V (1.16f): "The maximum number of turns it can take to research an advance has been increased from 32 to 40." README-V (1.29f): "Editor: On General Settings, added Min. Tech Turns." / "added Max. Tech Turns."
- **What the maximum means.** https://forums.civfanatics.com/threads/the-research-slider.662300 (justanick) — "The time for researching a tech is limited to 4 turn as the minimum and 50 turns as the maximum." and "You cannot use up more than 50 turns of actual research, no matter how little you spend". jarred! in the same thread: "it'll still take the 50 turn max to finish a 200 beaker tech". Reading: as long as at least some research is funded, the tech completes on turn 50 at the latest.
- **What the minimum means.** https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes?p=953406 (David Weldon) — "Yes, there is a minimum of 4 turns for a tech advance, no matter how much overkill you use." See Disputed, item 2. Whether beakers above the 4-turn pace are lost or banked is UNVERIFIED.
- **Slider floor.** https://forums.civfanatics.com/threads/the-research-slider.662300 (vmxa) — "at the start you cannot drop below 20% and research, unless you have a scientist running." Treat this as UNVERIFIED as a rule. It probably reflects the 50-turn cap, since research has to be fast enough to beat 50 turns.

## 4.3 Beaker overflow and loss

- **Excess from the completing city is lost; later cities feed the next tech.** https://forums.civfanatics.com/threads/a-few-questions.225457 (TimTheEnchanter) — "The cities are processed one at a time in reverse order of their creation." and "Any excess beakers from the city that gets you to the next tech are lost." ElephantU, same thread: cities processed after the completing one add their research to the new tech.
- **Simpler player statements (all extra lost).** https://forums.civfanatics.com/threads/are-extra-science-beakers-wasted.63391 (Rik Meleet) — "When a tech is researched all extra beakers are lost." https://civfanatics.com/civ3/faq/ — "there is no shield or food carryover for cities, just like there is no beaker carryover when researching a technology." See Disputed, item 3.
- **Measured loss.** The overflow mod's tracker found about 5% of beakers lost: https://forums.civfanatics.com/threads/mod-tech-overflow.398296/latest — "generated 7,562 research and lost 375 (4.96%)" (as reported in search results; not re-fetched).
- **Theory of Evolution discards stored beakers.** https://forums.civfanatics.com/threads/theory-of-evolution-toe.60765 (Gengis Khan) — "All beakers stored when the ToE is completed are lost."
- **Research is applied at turn start.** Pedia GCON_Research — "At the start of each turn, the science output of each city is added to the research project currently in progress".
- **C3C scientists.** https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ (Arathorn) — "Scientists now produce 3 beakers per turn instead of 1."

## 4.4 Eras: required vs optional techs

- **Four eras.** BIQ ERAS: Ancient Times, Middle Ages, Industrial Ages, Modern Times.
- **Every required tech of the current era must be known before advancing.** https://forums.civfanatics.com/threads/revolutionizing-the-tech-tree-mechanics.102803 (Plotinus) — "a civ cannot progress from one era to another without first researching all the non-essential techs from the earlier one". "Non-essential" is evidently a slip for "required". https://forums.civfanatics.com/threads/certain-clarifications.22543 (Mr Spice) — "Yes, you have to possess them, but there are plenty of other ways to accuire techs than to research them on your own."
- **You cannot research into the next era early.** The structural evidence is in the BIQ. The first-tier techs of each later era have no prerequisites in the BIQ: Monotheism, Feudalism and Engineering (Middle); Nationalism, Steam Power and Medicine (Industrial); Rocketry, Fission, Computers and Ecology (Modern). The editor forbids cross-era prerequisites. https://forums.civfanatics.com/threads/revolutionizing-the-tech-tree-mechanics.102803 — GRM7584: "Era-specific techs can only have prereqs in their era, or NONE-era"; Paasky quotes the editor error "The required tech must be in the same era". So the era gate, not prerequisites, is what keeps next-era techs closed. No source says in so many words that you cannot research a next-era tech early, so the exact UI behaviour is an inference.
- **Trading and the Great Library cross eras.** https://forums.civfanatics.com/threads/tech-prerequisites-in-previous-eras.472427/ (Quintillus) — "tech trading and the Great Library techs are always across eras." README-V (1.16f): "Traded advances that cause an era transition, now allows the player to select the advance to research in the new era."
- **Optional techs never block advancement.** BIQ flag `NotRequiredForEraAdvancement`. The editor checkbox is "Not Needed for Era Advancement", and the F6 chart marks these techs: https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/73736-what-technologies-don-t-you-need (vondrack) — "the optional techs have a slashed circle in the upper right corner."
- **Era-None techs.** README-V: "Editor: Updated ERA_NONE techs to work as expected (you can never research them and they don't impede era advancement but they can be assigned as free techs)." README-PTW: if a civ cannot reach a non-era prerequisite, the techs sprouting from it "will not be required for advancement to the next era."
- **C3C optional techs (BIQ).**

| Era | Techs | Optional (not required) |
|---|---|---|
| Ancient | 21 | Literature, The Republic, Monarchy |
| Middle Ages | 22 | Chivalry, Printing Press, Music Theory, Democracy, Economics, Navigation, Free Artistry, Military Tradition |
| Industrial | 23 | Nationalism, Communism, Sanitation, Espionage, Amphibious War, Advanced Flight, Ironclads, Fascism |
| Modern | 17 | none |

## 4.5 Conquests tech-tree differences (vs Vanilla/PTW)

- **Radio removed; Ironclads and Fascism added.** README-C3C (1.20): "Radio Tech removed", "Electronics requirement removed from Motorized Transportation", "Civil Defense requires Electronics", "Advanced Flight now requires Electronics", "Radar Towers require Advanced Flight". https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ (Arathorn) — Ironclads: "requires Steam Power, required for nothing, allows Ironclads unit, optional tech"; Fascism: "requires Nationalism, required for nothing, allows Fascism Government, optional tech". The BIQ confirms both (Industrial, optional, costs 100 and 130).
- **Philosophy became a flagged bonus tech.** Same thread: "Philosophy now gives a bonus tech to the first civ to research it." BIQ: Philosophy has `BonusTechToFirstCivThatResearches`.
- **Diplomatic tech moves.** Same thread (Arathorn): "Map trading moved from available with Map Making to Printing Press." and "Communications trading moved from available with Map Making to Navigation." README-C3C notes MPTournament rules moved map trading back ("MPTournament: Map Trading moved back to Map Making").
- **Tech count.** Thunderfall, same thread: "it is now 81 with the removal of Radio". The C3C BIQ has 83 era techs (21+22+23+17). The 81 figure is UNVERIFIED and may be pre-1.22 or may exclude the two new optional techs.
- **Tech purchase price.** Same thread (Arathorn): "Changed calculation on tech prices, in terms of gold required to buy." The new formula is UNVERIFIED.

## 4.6 Tech trading and tech brokering

- **Who can trade a tech.** https://forums.civfanatics.com/threads/revolutionizing-the-tech-tree-mechanics.102803 (Hugin) — general techs "can be traded with any other civilization, as long as that civ has the prerequisite technologies". The BIQ has a `CannotBeTraded` tech flag; no C3C default tech sets it.
- **AI-to-AI trade rate.** BIQ DIFF "AI to AI trade rate", exposed in Vanilla (README-V 1.21f: "Editor: Exposed AI to AI trade rate."). C3C values: Chieftain 110, Warlord 120, Regent 130, Monarch 140, Emperor 150, Demigod 160, Deity 170, Sid 200. Exact semantics are UNVERIFIED; it is presumably the AIs' price discount or willingness among themselves.
- **AI price drops as more civs know a tech; monopolies are dear.** https://civfanatics.com/civ3/faq/ — "techs are very expensive to buy or research when no one knows them". The FAQ also names the classic brokering trade: "called a '2fer' or 'xfer' (2 for 1)."
- **Brokering.** Buy a tech from one AI and resell it to the others. https://civfanatics.com/?p=171 ("Zero Percent Science") — the author calls himself a "Day Trader" and sells a tech for "a few gold per turn for 20 turns". https://forums.civfanatics.com/threads/science-broker-analysis.10012 (Baron23) — "The AI also plays tech broker at times".
- **AIs pass techs on among themselves.** https://civfanatics.com/?p=170 — "they'll trade it amongst themselves if you give it to one so give it to all of them at the same time."
- **The AI's trade value differs from its research value.** https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/page-3 (alexman) — "The AI places a value on each tech for trading purposes, which (according to Soren) is actually different". Vanilla also changed this: README-V (1.21f) "Adjusted advance trading rates for AI." The full trade-valuation formula is UNVERIFIED and belongs to the diplomacy section.

## 4.7 AI research choice (alexman) — already in `C7Engine/AI/DevelopmentAI.cs`

- **Method.** https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/ — "The AI places a value on each technology it can research next. The values depend on the various things allowed by each technology, as well as on the turns needed by that AI civ". It was found "by stripping all technologies from their benefits, adding them back one by one". "These values are cumulative." The values feed a weighted random choice: "techs with higher value were chosen more often" (alexman, quoted in https://forums.civfanatics.com/threads/manipulating-ai-tech-research.657856/), and tjs282 there: "still basically just a weighting-factor for a pRNG dice-roll".
- **Values.** "/turns" means divided by the turns the AI needs for the tech. Per turn: Empty tech 256, SS component 261, Government 259, Conscription 259, Mobilization 259, No trade 149. Flat: Defender (no resource) 198, Attacker (no resource) 134, Defender (resource required) 70, Naval transport 34, Resource 16, Double worker speed 8, Diplomats 8, MPP 8, ROP 8, Alliances 8, Embargoes 8, Trade over ocean 8, Attacker (resource required) 6, Wonder 6, Irrigation 4, No disease 4, Trade over sea 4, Bridges 2, Double wealth 2, Map trades 2, Communications trade 2, Small wonder 2, Specialist 2, Unit (no A/D/NT) 2, Improvement 2, Other tech 1, Recycling 1, Precision bombing 1, Worker job 1, Bonus tech 0, Reveal map 0, Sacrifices 0. "Optional: divide by 1.5".
- **Worked checks.** "Iron Working allows swordsmen and iron, so it has a value of 22 more than if it were just an empty tech". That is Resource 16 + Attacker-needing-resource 6. And "for the Romans who build Legionaries (attacker and defender), Iron Working is worth an additional 70 points!", which is Defender-needing-resource 70.
- **Version.** The list dates from Vanilla 1.29f (Bamspeedy cites "Civ3 1.29f"; Qitai on page 2: "the research cost changes is effective vanilla civ3 1.29, not PTW"). Yet it lists Bonus Tech, Reveal Map and Sacrifices, which are PTW-era editor flags. So it was most likely measured with a PTW-capable editor or game. C3C re-validation: UNVERIFIED.
- **"No trade: 149/turns" — best reading.** The thread never explains it (WebFetch of pages 1–3: "The thread never explains either entry"). Its place in the list next to the other tech flags suggests the editor's **"Cannot Be Traded"** tech flag, which exists in the BIQ (`TECH.CannotBeTraded`, flag byte 2 bit 3). The comment in DevelopmentAI.cs that says "no BIQ tech flag found" should be corrected accordingly. No default C3C tech sets this flag, so leaving it out changes nothing in standard games. It only matters for mods. Interpretation UNVERIFIED.
- **"Other tech: 1" — unresolved.** It is not explained anywhere in the thread. It cannot mean "is a prerequisite of another tech": Iron Working is a prerequisite of Construction, yet alexman's Iron Working total is exactly 22 (16 + 6) with no +1. Possible readings: something the tech enables that has no other category, or a tech received for free (Bonus Tech is listed separately at 0). UNVERIFIED. It is worth 1 point, so it is negligible.
- **Other nuances in the thread.** Unique units count only when they differ from the unit they replace ("Bowmen... count as both attacker and defender"). Only direct effects count (Sanitation gets nothing for Battlefield Medicine). A benefit counts even if the civ already has it (Magnetism still gets Trade over Ocean). Flavors: Ozymandias, https://forums.civfanatics.com/threads/manipulating-ai-tech-research.657856/ — "I'm 99% certain that the effect of a Flavor is 5%." This is UNVERIFIED and not in DevelopmentAI.cs. Vanilla also fixed an AI bug: README-V "Fixed AI bug giving value to worthless techs."

## 4.8 Free techs

### Great Library
- **Effect.** Pedia BLDG_Great_Library — "The civilization that builds the [Great Library] gets any Civilization Advance already discovered by two other known civilizations for free." BIQ flag `GainAnyTechKnownByTwoCivs`; cost 400, Literature, obsolete with Education.
- **Only contacted civs count.** https://forums.civfanatics.com/threads/great-library-question.78293 (sealman) — "You get all the techs that you do not have that are known to at least 2 civs that you have contact with." https://forums.civfanatics.com/threads/how-does-the-great-library-work.26689 (Catt) — "If you only know one civ, the Great Library will do nothing for you."
- **Timing.** Same thread (robinm) — "The Library checks all civs you have met at the beggining of each of your turns".
- **Obsolescence.** Vanilla: README-V "Great Library: This Great Wonder is now obsolete when Education is researched." Obsolescence is checked after the turn's grants. https://forums.civfanatics.com/threads/strange-tech-glitch.112341 (Renata) — "it is possible to gain techs long past education in circumstances like yours". robinm (26689) — "you continue to recieve all of the other techs the GL made available to you for that turn only."
- **Prerequisites and editor order.** https://forums.civfanatics.com/threads/quick-answers-newbie-questions.318752/page-275 (justanick) — "You can only gain those techs that you have the prerequisite for." "Music theory seems to be one of them, amphibic warfare is another one and possibly space flight,too." "As i recall it the techs are checked in the order in which they are listed in the editor." So within one turn, a tech whose prerequisite is granted later in the list waits a turn. PTW fix (README-PTW): "Fixed a bug with non-era techs that allowed players to potentially receive them for free from the Great Library (NOTE: this also fixes a similar bug that would allow players to receive techs that they do not have the prerequisites for if the order of the techs in the editor was changed)."

### Theory of Evolution
- **Effect.** Pedia BLDG_Theory_of_Evolution — "Grants two Civilization Advances as soon as completed." BIQ flag `TwoFreeAdvances`; 600 shields, Scientific Method, never obsolete.
- **Which techs.** https://forums.civfanatics.com/threads/theory-of-evolution-toe.60765 (Yndy) — "First tech is the one you were currently researching." The second is the next one you pick. Turner: "Then you get a pop up box asking you which tech you'd like to research next." Stored beakers are lost (Gengis Khan, quoted in 4.3). See Disputed, item 6 for "random techs".
- **Patch fixes.** README-V: "Fixed bug which sometimes caused you to not get any techs from Theory of Evolution." README-PTW: "Fixed bug with free tech popups from Theory of Evolution."

### Philosophy (C3C)
- **Effect.** Pedia TECH_Philosophy — "{Bonus Tech} The first civilization to research this tech is awarded a free tech." BIQ: only Philosophy carries the bonus flag.
- **The player picks the free tech, from what is researchable at that moment.** https://forums.civfanatics.com/threads/free-tech-tricks.90177 (DaveMcW) — "Finish research or pop Philosophy from a hut, and you can pick any of the Middle Age techs for free!" This is the case where Philosophy was the last required Ancient tech, so the era advanced first. https://civfanatics.com/?p=152 — "If you manage to get Philosophy first, you get Republic as your free tech!"
- **Version note.** https://civfanatics.com/?p=152 — "none of them work in PTW." The free-tech tricks are C3C (Philosophy) and Vanilla/C3C (Scientific). The CivFanatics FAQ lists the Philosophy rule among Conquests changes: "Philosphy now grants a free technology if researched first".

### Scientific trait
- **Effect.** Pedia GCON_Strengths — "Scientific city improvements (like research labs) are easier to build and the civilization receives a free Civilization Advance at the start of every era." https://civfanatics.com/civ3/faq/ — "Upon entering a new age, they receive a free technology (one of the starting technologies for that age)."
- **Selection is random among the new era's researchable techs, and seems to avoid techs AIs already have.** https://forums.civfanatics.com/threads/free-tech-tricks.90177 (DaveMcW) — the trick yields "a _different_ free tech that none of the AIs have"; with the Russian triple slingshot there is "a 33% chance to learn Education or Gunpowder as your scientific tech". README-PTW: "Improved random free tech selection for the Scientific civ trait." The exact selection rule is UNVERIFIED.
- **Timing.** https://civfanatics.com/?p=152 — after the "new era" popup, the free tech shows up once the turn is processed ("check your research screen").

### Scientific Great Leaders (C3C)
- **Spawn.** Pedia GCON_Leaders — "Each time a technology is researched for the [first] time by any civilization there is a chance that a Scientific leader will arise (this chance is increased slightly if the researching civilization has the "scientific" strength." Rates: https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ (Arathorn) — "has a 3% chance of appearing if a Civ is the first to discover a tech (5% for Scientific civs)." See Disputed, item 4.
- **Uses.** Pedia — "A Scientific Leader can be used to increase the scientific output of all cities of his civilization, thus starting an Age of Science" and "can also be used to hurry the construction of any [improvement] (including a Great Wonder)". Pedia GCON_Age_of_Science — "a 20-turn period in which the scientific research of each city in that civilization's empire is 25% more productive."
- **Age of Science is broken in the shipped game.** https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ (Arathorn) — "this functionality does not work. The button is there, but it does nothing." Post 2 of that thread says the same about 1.22.
- **Rule switch.** README-C3C: "Implemented Allow Scientific Leaders Rule". EdSupp: "Allow Scientific Leaders Rule has been added and is active as a Default Rule. With this disabled only militaristic leaders are allowed."

## 4.9 Great Wonders (C3C 1.22)

Cost = BIQ cost x 10 (shields, before any production modifiers). Culture = BIQ culture per turn; Pedia GCON_Culture: "The amount of culture each Wonder or improvement contributes doubles after 1000 years." Traits = the BIQ strengths that can trigger a Golden Age. TA = can become a Tourist Attraction (BIQ flag, matching the Pedia GCON_Tourist_Attraction list). Effect quotes are from the Pedia BLDG_* entries.

| Wonder | Cost | Prereq tech | Obsolete | Culture | Traits | TA | Effect (Pedia) | Notes (BIQ) |
|---|---|---|---|---|---|---|---|---|
| The Pyramids | 400 | Masonry | — | 4 | Rel/Ind/Agr | yes | "Puts a Granary in every city on the same continent." | |
| The Colossus | 200 | Bronze Working | Flight | 3 | Com/Exp/Sea | yes | "one extra Commerce in every square that already produces at least one." | coastal only |
| The Great Lighthouse | 300 | Map Making | Magnetism | 2 | Com/Exp/Sea | yes | "Galleys and other early naval units can move safely in and out of sea squares, and all naval unit movement rates are increased by one." | coastal only |
| The Oracle | 300 | Mysticism | Theology | 4 | Rel | yes | "Doubles the effect of all Temples in the Empire" | |
| The Great Library | 400 | Literature | Education | 6 | Sci | yes | "any Civilization Advance already discovered by two other known civilizations for free." | |
| The Hanging Gardens | 300 | Monarchy | Steam Power | 4 | Ind/Agr | yes | "three unhappy citizens content in its city and one unhappy citizen content in all other friendly cities." | |
| The Great Wall | 300 | Construction | Metallurgy | 2 | Mil/Ind | yes | "Doubles unit combat values versus Barbarians. Places a Wall in each city of the continent" | |
| The Temple of Artemis | 500 | Polytheism | Education | 4 | Rel | yes | "Puts a Temple in every city on the same continent." | new in C3C |
| The Statue of Zeus | 200 | Mathematics | Metallurgy | 4 | Mil/Rel | yes | "produces an Ancient Cavalry every 5 turns." "A city must have access to Ivory" | new in C3C; needs Ivory |
| The Mausoleum of Mausollos | 200 | Philosophy | — | 2 | Sci/Sea | yes | "makes three unhappy citizens content in its city." | new in C3C |
| Sun Tzu's Art of War | 600 | Feudalism | — | 2 | Mil | no | "Puts a barracks in every city on the same continent." | |
| Knights Templar | 300 | Chivalry | Steam Power | 2 | Mil/Rel | no | "produces a Crusader every 5 turns." | new in C3C |
| Sistine Chapel | 600 | Theology | — | 6 | Rel | yes | "Doubles the effects of all Cathedrals in your cities." | |
| Leonardo's Workshop | 600 | Invention | — | 2 | Mil | yes | "Obsolete military units can be upgraded at half price." | |
| Copernicus' Observatory | 400 | Astronomy | — | 4 | Exp | yes | "Doubles scientific research in the city where it is built." | |
| Magellan's Voyage | 400 | Navigation | — | 3 | Com/Exp/Sea | no | "The movement rate of all naval units is increased by one." | coastal only |
| JS Bach's Cathedral | 600 | Music Theory | — | 6 | Rel | yes | "Decreases the number of unhappy citizens on the continent by two per city." | continental |
| Shakespeare's Theater | 450 | Free Artistry | — | 8 | (none) | yes | "Eight unhappy citizens in the city are made content." | also lets the city grow past size 12 (BIQ AllowsCitySize3) |
| Newton's University | 400 | Theory of Gravity | — | 6 | Sci | yes | "Doubles scientific research in the city where it is built." | |
| Smith's Trading Company | 600 | Economics | — | 3 | Com/Sea | no | "Pays maintenance costs for all trade-based city improvements (Harbors, Marketplaces, Banks, Airports, Commerical Dock and Stock Exchanges)." | |
| Universal Suffrage | 800 | Industrialization | — | 4 | Agr | no | "Reduces war weariness in all cities." | |
| Theory of Evolution | 600 | Scientific Method | — | 3 | Sci | no | "Grants two Civilization Advances as soon as completed." | |
| Hoover Dam | 800 | Electronics | — | 2 | Ind/Agr | yes | "Brings the effects of a hydro plant to all cities on the same continent." "a city must have a river within its radius." | |
| The United Nations | 1000 | Fission | — | 4 | Com | yes | "Initiates the possibility of a Diplomatic Victory." | |
| The Manhattan Project | 800 | Fission | — | 2 | Mil/Ind | no | "Allows the construction of nuclear weapons by {all} civilizations." | needs Uranium |
| SETI program | 1000 | Computers | — | 3 | Exp | no | "Doubles scientific research in the city where it is built." | |
| The Internet | 1000 | Miniaturization | — | 4 | all 8 | no | "Puts a Research Lab in every city on the same continent." | new in C3C |
| Cure for Cancer | 1000 | Genetics | — | 3 | Sci/Agr | no | "Makes one unhappy citizen content in every city." | |
| Longevity | 1000 | Genetics | — | 3 | Sci | no | "All cities get a population increase of two instead of one when the Food Storage box is full." | |

Cross-checks:
- https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/ (Arathorn) agrees with the BIQ on the new wonders: Statue of Zeus "200 shields, Mathematics, requires Ivory, 4 cpt, Rel and Mil, produces Ancient Cavalry unit every 5 turns, expires with Metallurgy"; Mausoleum "200 shields, Philosophy, 2 cpt, Sci and Sea, 3 unhappy citizens made content, never expires"; Temple of Artemis "500 shields, Polytheism, 4 cpt, Rel"; Knights Templar "300 shields, Chivalry, 2 cpt, Rel and Mil ... expires with Steam Power". Thunderfall: "There are now 11 SWs and 29 GWs". The BIQ also has 29 GWs and 11 SWs.
- **C3C changes to existing wonders** (Arathorn, same thread). Shakespeare's "also allows a city to grow beyond size 12 and now costs 450 shields". Colossus "traits from Expansionist, Commercial, Religious to Expansionist, Seafaring, Commercial". Universal Suffrage "trait from Militaristic to Agricultural". Pyramids and Cure for Cancer "add trait Agricultural". Leonardo "still halves the upgrade cost". SirPleb: "The Great Wall has the new feature of putting walls in every town on the same continent."
- **Vanilla wonder patches** (README-V 1.16f). "United Nations Wonder: This Great Wonder is now enabled by Fission." "Art of War: The effects of this Great Wonder are now restricted to the continent it is built on." "Adam Smith's Trading Company: This wonder now pays the maintenance costs for all trade-based city improvements ... instead of all improvements with a maintenance cost of one gold."
- **Tourist attraction income** (Pedia GCON_Tourist_Attraction). A wonder qualifies after it has "stood the test of time" and then earns gold: 1000–1500 years +2, 1501–1750 +4, 1751–1875 +6, 1876–2000 +8, 2001–2250 +10, 2251–2500 +12, 2501+ +14. Arathorn: "It starts at 2 gpt after 1000 years and can grow as high as 14 gpt."

## 4.10 Small Wonders (C3C 1.22)

| Small Wonder | Cost | Prereq tech | Other requirement | Culture | Effect (Pedia) |
|---|---|---|---|---|---|
| Heroic Epic | 200 | — | "The civilization must have had a victorious army." | 4 | "Increases the chances of the appearance of leaders from victorious combat." |
| Iron Works | 300 | — (BIQ none) | "must have Iron and Coal in its city radius" | 2 | "Production increased by 100% in the city where it's built." (BIQ also has pollution 4) |
| Forbidden Palace | 200 | — | "at least ten cities (on a standard size map)" | 2 | "Gives similar benefits as the Palace but still suffers some corruption." It also raises the optimal city count. |
| Military Academy | 400 | Military Tradition | "must have had a victorious army." | 1 | "can build armies without the benefit of a leader. Armies from this civilization are more effective in combat." |
| The Pentagon | 400 | — | "at least three armies in the field." | 1 | "increases the unit capacity of all the civilization's armies from three to four." |
| Wall Street | 300 | — | "at least five Stock Exchanges" | 2 | "5% interest every turn with a maximum cap of 50 gold per turn." |
| Apollo Program | 500 | Space Flight | Aluminum (BIQ) | 2 | "enables the civilization to begin construction of the Alpha Centauri Spaceship." |
| Strategic Missile Defense | 500 | Integrated Defense | "at least five SAM Batteries" | 1 | "Offers a 75% chance of intercepting enemy ICBMs." |
| Intelligence Agency | 400 | Espionage | — | 1 | "allows the player to undertake Espionage missions." |
| Battlefield Medicine | 500 | — | "at least five hospitals" | 1 | "Allows military units to heal in enemy territory." |
| Secret Police HQ | 200 | Espionage | Communism only (BIQ required government) | 0 | Second Forbidden-Palace-style anti-corruption centre; "Changing from Communism will cause the Secret Police HQ bonuses to stop." |

- Small wonders cannot trigger Golden Ages: https://civfanatics.com/?p=151 — "Note that small wonders cannot trigger golden ages".
- **Iron Works tech.** The BIQ gives no required tech. In practice it is gated by seeing and using Coal and Iron. The coal tech is UNVERIFIED here and belongs in the resources section.
- **Secret Police HQ fix.** README-C3C 1.20: "Secret Police HQ no longer gives corruption OR OCN bonus when player leaves Communism."

## 4.11 Wonder rules

- **One per world, never rebuilt.** https://civfanatics.com/civ3/faq/ — "Great Wonders can only be built once, by one civilization, and only they get the benefit of that wonder." "However, Small Wonders can be built by each civilization". https://forums.civfanatics.com/threads/quick-answers.117535/page-32 (Turner) — "once a great wonder is gone, no one can rebuild it." Same thread: small wonders can be rebuilt (paraphrase in the fetch result).
- **Only one city per civ may build a given wonder at a time.** https://civfanatics.com/civ3/faq/ — "First off, you can only build a wonder in one city."
- **Wonders cannot be hurried with population, gold or forest chops; a city with rushed shields cannot switch to a wonder.** https://civfanatics.com/civ3/faq/ — "you can’t rush a wonder by sacrificing population (Despotism, Communism, Fascism)," / "by rushing it with gold (other governments), or by forest chops." https://forums.civfanatics.com/threads/partial-rush-build-of-wonders.8145 (Ferd) — "Once you rush something, the wonders are greyed out for that city and you can't switch to them." https://forums.civfanatics.com/threads/disbanding-question.18365 (Bamspeedy) — with forest or disband shields in the box, "that particular city won't be able to switch to a wonder." Pedia GCON_Hurry_Production only mentions "improvements or units".
- **Switching shields between wonders is free.** https://forums.civfanatics.com/threads/building-wonders.8756 (Ruffin) — "switch all the production over to the new wonder (no penalty in Civ3)". This is the standard "prebuild" (Palace or another wonder as placeholder): https://forums.civfanatics.com/threads/quick-answers.117535/page-32. Apolyton tip on the timing limit: https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes?p=953406 (Travathian) — "If it gets to one turn left you can switch to other units/improvements, but NOT wonders."
- **Switching between categories (unit ↔ improvement ↔ wonder).** No Civ3 source found for a percentage penalty. The 50% "categories" rule found online (https://forums.civfanatics.com/threads/help-build-a-wonder.174050/) is a Civ2 forum thread, so it is excluded. Shields carry over to a different item: https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/57954-solution-to-avoid-micromanagement (Franses) — "Shields are carried over in case you change production to something that costs more (or less) than the shields produced." UNVERIFIED (see open questions).
- **A rival completes the wonder you are building.** Vanilla added notices: README-V "Players now receive a message when a wonder is made obsolete." / "\"beginning wonder\" pop-ups now only appear if you have an embassy or spy with the corresponding civ." Shields: https://forums.civfanatics.com/threads/wonder-building.27531 (Archullus) — "Switch to a less favourable wonder; or Get abortive production cost." https://forums.civfanatics.com/threads/building-wonders.8756 — after switching to a Bank the original poster was "wasting about 400 shields". Reading: the shields stay in the box, a free switch to another wonder keeps them, and a switch to something cheaper wastes the excess on completion. Exact rule UNVERIFIED. The "reimbursed in cash" claim (forums.civfanatics.com/threads/list-of-bugs-ive-experienced.139564) is from a Civ4/BtS thread and is excluded.
- **Obsolete wonders.** Obsolete wonders drop out of the build list. Their effects stop, but their culture and Golden Age triggers are presumably unaffected (UNVERIFIED). README-V: "Players now receive a message when a wonder is made obsolete."
- **Great Leader hurry.**
  - Vanilla/PTW: military leaders could rush Great Wonders. https://civfanatics.com/civ3/faq/ — "In **Conquests** the rushing ability is limited to Small Wonders, but in **Vanilla** or **Play the World** it can rush a Great Wonder."
  - C3C, military leader: Pedia GCON_Leaders — "A Military Leader can be used to either create an army or to hurry production of an Improvement or Small Wonder in a city." Arathorn — "Military Great Leaders (MGLs) can no longer rush Great Wonders."
  - C3C, scientific leader: Arathorn — "An SGL can rush a wonder (either Great or Small)."
- **Golden Age from wonders.** Pedia GCON_Golden_Age — "If a civilization builds a Great Wonder (or Wonders) that corresponds to [both] its unique strengths, its Golden Age can be triggered." BIQ RULE Golden Age duration = 20. Details belong in the Golden Age section.
- **Coastal, river and resource requirements** (BIQ). Colossus, Great Lighthouse and Magellan's need a coastal city (CoastalInstallation). Hoover Dam needs a river (MustBeNearRiver). Statue of Zeus needs Ivory, Manhattan Uranium, Apollo Aluminum, and Iron Works Iron + Coal.
- **Wonder victory** (C3C option, only if enabled). README-C3C — "The "Wonder Victory" is actually a Time Limit type of victory condition; i.e., when the Wonder Victory is in effect, the game ends immediately upon completion of the last Great Wonder." Victory-point games are out of scope.

---

## Disputed

1. **Does the difficulty factor scale the AI's research or only the human's?** The 1.29f thread says the formula "is relevant for research cost of the AI civs, not just the human player" and that techs "are now more expensive for the human player on Monarch, Emperor and Deity", with CF capped at 10. The CivFanatics FAQ says "On Chieftain, the cost factor ... is 2 for the AI – meaning everything takes twice as long to do, from researching a tech, to building a worker." That reads as AI cost x CF/10, uncapped. The two readings conflict for the AI below Regent.
2. **Minimum research time.** 4 turns (BIQ RULE; FAQ "For all three games, the minimum research length is 4 turns"; David Weldon) vs Special_K, https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/42626-distilled-civ3-tips-and-notes?p=953406 — "You can research an advance in 3 turns, it just takes a ton of science." The maximum is 32 then 40 in Vanilla (README-V 1.16f) and 50 in C3C (BIQ/FAQ). A Steam post cited in searches claimed "4/40" for the default game, which is probably Vanilla/PTW.
3. **Overflow.** "all extra beakers are lost" (Rik Meleet; FAQ "no beaker carryover") vs "excess from the completing city is lost, later cities' beakers go to the next tech" (TimTheEnchanter / ElephantU, city order = reverse founding order). The second is more specific and consistent with the first as a simplification.
4. **Scientific leader chance.** 3% / 5% for Scientific civs (Arathorn; Turner in https://forums.civfanatics.com/threads/great-leader-abilities.88505/latest) vs 2% / 5% (Ginger_Ale, https://forums.civfanatics.com/threads/leaders.102561 — "you get a 5% chance of getting one", with 2% for non-scientific civs). The Pedia says only "increased slightly".
5. **Can a C3C military leader rush anything?** Pedia, FAQ and Arathorn: yes, improvements and Small Wonders. EdSupp: "NOTE: Military leaders can not rush." HellboundHeathcliff, https://forums.civfanatics.com/threads/great-leader-abilities.88505/latest — "Conquests MGLs can only rush improvements and small wonders or create an army." EdSupp is the outlier.
6. **Theory of Evolution techs.** Current research plus next pick (Yndy, Catt 1.21f) vs philippe, https://forums.civfanatics.com/threads/how-does-the-great-library-work.26689 — "thoery of evolution picks random techs." Turner describes three techs in one well-timed turn (current research, then two picks).
7. **Wonder data from the CivFanatics wonder guide** (https://civfanatics.com/?p=151, as extracted by WebFetch; not re-verified line by line) differs from the BIQ on Copernicus' Observatory (Invention vs BIQ Astronomy). It also lists Universal Suffrage under "Industrialisation" and spells several names differently. The BIQ is authoritative for C3C 1.22.
8. **Shields after losing a wonder race** (see 4.11): kept for a free wonder switch vs partly wasted ("abortive production cost"). No authoritative source.

## UNVERIFIED / open questions

- Whether the 1.29f research formula (CL*1.75, DM table) is unchanged in PTW 1.27f and C3C 1.22. alexman says the difficulty scaling "differs between PTW and vanilla" (page 2 of the AI-research thread, paraphrased by the fetch). DM for Demigod (CF 7) and Sid (CF 4) is extrapolated.
- How Future Tech Cost (400) enters the cost formula.
- What happens to beakers beyond the 4-turn minimum pace (lost vs banked), and the exact mechanism of the 50-turn cap (forced completion vs cost reduction).
- Era gating UI. It is inferred that next-era techs simply are not offered until the era advances. No source states this outright.
- alexman's "No trade: 149/turns": probably the BIQ "Cannot Be Traded" flag, unused in default C3C. "Other tech: 1" is unexplained, and the Iron Working total shows it is not "leads to another tech". Flavor weight "5%" (Ozymandias) is unconfirmed. How the values weight the random roll is unknown (DevelopmentAI.cs assumes linear).
- The AI-to-AI trade rate's exact meaning, and the C3C "changed calculation on tech prices" formula (diplomacy section).
- The Scientific trait free-tech selection rule (random among researchable new-era techs, possibly excluding ones the AIs know).
- The Philosophy free tech: confirm that it is the player's next pick and is limited to currently researchable techs (it can be a Middle Ages tech only once the era has advanced).
- The Civ3 production-switch penalty between units, improvements and wonders (none found for Civ3), and exactly what happens to wonder shields when a rival completes the wonder.
- Whether obsolete wonders keep culture and Golden Age eligibility.
- Iron Works' effective tech gate (Coal visibility).
- Search budget note: the session's web-search budget ran out before the Philosophy-pick and production-penalty questions could be searched further; follow-up searches may resolve them.
