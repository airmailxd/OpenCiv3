# 6. Diplomacy, espionage and AI attitude

Scope: the base game (1.29f), Play the World (1.27f) and Conquests (1.22).
Scenarios, mods and the special Conquests game modes are excluded.

How to read this section:

- Every rule has a source and a short verbatim quote. "Local install" means
  the files of a Steam *Civilization III Complete* install (Conquests 1.22),
  read directly: `Conquests/Text/Civilopedia.txt`, `Conquests/Text/script.txt`,
  `Conquests/Text/diplomacy.txt`, `Conquests/Text/labels.txt`, the readmes,
  and `Conquests/conquests.biq` (read with this repo's QueryCiv3 parser).
- Vanilla-only text comes from `Text/Civilopedia.txt` in the same install.
- Community findings are labelled with the tester, version and date where the
  source gives them. CivFanatics studies use a sign convention where a
  negative attitude number is good for you.
- Anything without a source is marked **UNVERIFIED**.

---

## 6.1 Contact and the diplomacy screen

- **The Diplomacy button needs first contact.** Source: vanilla `README.txt`
  (manual errata, local install). Quote: "The Diplomacy button becomes enabled
  after your first contact with another civilization."
- **Foreign Advisor lines need contact with both civs.** Source: vanilla
  `README.txt`, Fixes v1.17f. Quote: "Relationship lines are no longer
  displayed on the Foreign Advisor unless the player has contact with both
  civilizations."
- **There are five attitude states**, which the advisor mouse-over shows.
  Source: vanilla `README.txt` (1.17f). Quote: "The five states an AI can be
  in on the Diplomacy screen, can now be seen on the mouse over advisor
  messages". Their names are in Conquests `labels.txt`: "gracious", "polite",
  "cautious", "annoyed", "furious".
- **Communications (contacts) can be traded.** They are introductions to a
  third civ, and Printing Press unlocks the trade. Sources: Conquests
  Civilopedia `GCON_Agreements`, quote: "{Communications}. These are
  introductions to a third civilization you can grant or receive from a
  rival." Also `conquests.biq` TECH flags, where Printing Press has "Enables
  Communication Trading". Vanilla Civilopedia `TECH_Printing_Press`, quote:
  "Can now trade communications between civilizations."
- **PTW fix: the AI cannot trade communications before it has the tech.**
  Source: `civ3PTW/readme.txt`. Quote: "Fixed bug that allowed the AI to trade
  communications before they had the tech that allowed it."
- **A world map does not give contact.** Source: CivFanatics, "Basic trading
  tips" (https://forums.civfanatics.com/threads/basic-trading-tips.71236/).
  Quote (as surfaced by search): "trading World Maps will show you the world
  known by the other civ but will not give you contact with civs you have".
- **What triggers first contact is UNVERIFIED.** It could be units adjacent,
  units seeing each other, or borders touching. No primary source was found
  (see open questions).

## 6.2 Embassies

- **Writing enables embassies.** An embassy is built in the rival's capital
  and costs gold. Source: Conquests Civilopedia `GCON_Embassies`. Quotes:
  "When a civilization learns $LINK<Writing=TECH_Writing>, it can begin to
  build its intelligence network. The first step is to build [embassies] in
  rival capital cities." and "Embassies cost gold from your treasury to build,
  and diplomatic missions cost gold to execute." In `conquests.biq`, Writing
  has the flag "Enables Diplomats".
- **You can make agreements when either side has an embassy.** Source:
  Conquests Civilopedia `GCON_Agreements`. Quote: "When your civilization
  builds an $LINK<embassy=GCON_Embassies> in a rival's capital city, or when a
  rival builds one in your capital, you can negotiate [Diplomatic
  Agreements]".
- **An embassy shows the other civ's wars and peace.** Source: CivFanatics
  Civ3 FAQ (https://civfanatics.com/civ3/faq/). Quote: "you can see what
  civilizations they are at war/peace with on the F4 Foreign Advisor screen."
- **Establishing an embassy always succeeds and costs gold by formula** (see
  6.11). The `conquests.biq` ESPN base cost is 20. Source: CivFanatics, "A
  Study of Espionage Missions"
  (https://civfanatics.com/civ3/strategy/game-mechanics/a-study-of-espionage-missions/).
  The study says Build an Embassy and Investigate City always succeed. A
  local `conquests.biq` dump gives "Build an Embassy | diplomat=True base=20".
- **War closes the embassy automatically.** Source: Civ3.com FAQ, quoted on
  Apolyton
  (https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/41397-espionage-you-can-t-steal-techs-from-civs-you-are-at-war-with).
  Quote: "your embassy in their capitol is automatically closed".
- **Wonder pop-ups need an embassy or a spy.** Source: vanilla `README.txt`.
  Quote: ""beginning wonder" pop-ups now only appear if you have an embassy or
  spy with the corresponding civ." Also: "Spaceship parts pop-ups now only
  appear if you have a spy with the corresponding civ."

## 6.3 Diplomatic agreements: kinds and the techs that enable them

- **There are four agreement kinds.** Source: Conquests Civilopedia
  `GCON_Agreements`. Quotes:
  - Military Alliance: "You agree to declare war on a despised third party."
  - Right of Passage: "You agree to allow his or her military units to move
    through your $LINK<territory=GCON_Territory> and use your transportation
    network (roads and railroads)."
  - Trade Embargo: "An agreement that neither of you will
    $LINK<trade=GCON_Trade> with or [allow] trade with a third party."
  - Mutual Protection Pact: "An agreement to declare war upon any
    civilization that attacks your partner."
- **Tech gates in Conquests (`conquests.biq` TECH flags, local dump):**
  - Writing: Diplomats, Military Alliance.
  - Map Making: Right of Passage.
  - Printing Press: Communications.
  - Navigation: Map Trading.
  - Nationalism: Mutual Protection Pact, Trade Embargo.

  The Conquests Civilopedia agrees. `TECH_Writing`: "Can now negotiate
  military alliance." `TECH_Map_Making`: "Can now negotiate right of passage".
  `TECH_Navigation`: "Can now trade $LINK<world maps=GCON_Maps> in
  diplomacy." `TECH_Nationalism`: "Can now negotiate mutual protection pact
  and trade embargo". CivFanatics FAQ, quote: "you cannot trade
  World/Territory Maps until you research Navigation."
- **Vanilla differs (see Disputed).** In vanilla, Writing also gives Right of
  Passage and Map Making gives map trading. Source: vanilla `Text/Civilopedia.txt`.
  `TECH_Writing`: "Can now negotiate military alliance and right of passage".
  `TECH_Map_Making`: "Can now trade $LINK<world maps=GCON_Maps> in
  diplomacy."
- **An MPP can be offered to you before you have Nationalism.** Source:
  CivFanatics search summary of "control over rop/mpp" and Apolyton "quick
  quest what else is needed for mpp". Quote (search snippet): "an AI with
  Nationalism can offer you an MPP even if you lack the tech". Weak source;
  treat as UNVERIFIED.
- **Locked alliances are scenario-only, so out of scope.** Members have a
  permanent MPP. Source: Conquests Civilopedia `GCON_Locked_Alliances`.
  Quote: "A pre-designed scenario (only) might begin with some (or all)
  civilizations grouped into several "Locked Alliances"."

## 6.4 Trade routes, resources and embargoes

- **Resource trade needs connected capitals.** Source: Conquests Civilopedia
  `GCON_Trade`. Quote: "Excess resources may be traded to other civilizations
  if the capital cities of both civilizations are connected."
- **Connection rules.** Source: same entry. Quote: "there is a road or
  railroad running between them, there are harbors in both cities and a
  visible water route between them, or both cities contain an airport"
  (paraphrased list; the original is three bullet lines). A road through
  enemy territory or a blockaded harbor does not count. Quote: "two cities are
  not connected if the only road/rail that connects them passes through enemy
  territory, or if a harbor is blockaded".
- **An embargo blocks resource and luxury trade.** Source: Conquests
  `script.txt` `DIPLOADVICETRADE_THEIREMBARGO`. Quote: "We cannot trade
  resources or luxuries with the $CIVNOUN0 -- they have a trade embargo
  against us." Also `DIPLOADVICETRADE_OUREMBARGO`: "We have agreed not to
  trade with the $CIVNOUN0 -- we have a trade embargo against them."
- **The embargoed civ is told when an embargo starts and ends.** Source:
  `script.txt` `TRADEEMBARGO` / `TRADEEMBARGOENDS`. Quotes: "$CIVNAME0 and
  $CIVNAME1 have signed a trade embargo against us." and "The trade embargo
  against us between $CIVNAME0 and $CIVNAME1 has ended."
- **The AI rarely trades its only copy of a luxury.** Source: CivFanatics,
  "Civs and their luxuries", simwiz2 (Civ3 General Discussions)
  (https://forums.civfanatics.com/threads/civs-and-their-luxuries.15068/).
  Quote: "An AI will almost NEVER trade a luxury that it only has one of."
  The "never trades its only resource" rule found elsewhere is a **Civ4** rule
  (https://forums.civfanatics.com/threads/question-about-trading-resources.368926),
  so it is not used here.

## 6.5 Deal durations, renegotiation and peace treaties

- **Ongoing deals run for 20 turns.** Source: Conquests `labels.txt`.
  Quotes: "This deal may be canceled peacefully after 20 turns." and "This
  deal will last for 20 turns."
- **"Always Renegotiate Deals" (1.29f) forces renegotiation every 20 turns.**
  Peace treaties are exempt. Source: vanilla `README.txt`, Additions v1.29f.
  Quote: "Added "Always Renegotiate Deals" prefs which forces a renegotiation
  of every non-peace treaty deal every 20 turns."
- **The AI asks to end or renew at expiry.** Source: Conquests
  `diplomacy.txt` `AIRENEGOTIATE`. Quote: "Friend $PLAYER0, this pact is about
  to expire. We believe that its usefulness is over." `AIRENEGOTIATEPEACE`:
  "$PLAYER0, our peace treaty is about to expire. For the sake of world
  peace, we will extend it if you desire."
- **A peace treaty lasts until someone declares war.** It has a 20-turn term,
  and breaking it inside that term costs reputation. Source: CivFanatics,
  "question about peace treaties" (C3C 1.15)
  (https://forums.civfanatics.com/threads/question-about-peace-treaties.84591).
  marioh, quote: "If you sign a peace-treaty with a civ it will last until you
  or the other declares war." Taé Shala, quote: "there will be a reputation
  penalty if you break the treaty within the remaining turns."
- **A bare treaty renews itself; one with attachments does not.** Source:
  same thread (marioh), as summarised in search. Quote: "If you don't have
  GPT/ROP/MPP/luxuries/resources attached with the peace treaty, then it will
  automatically renew until war is re-declared by either party."
- **Mobilization lasts until peace.** Source: Conquests Civilopedia
  (Mobilization). Quote: "May not return to Normalcy until a Peace Treaty is
  signed."
- **Ceasefire length is UNVERIFIED for Civ3.** The often-quoted "16 turns"
  comes from the *Civ2* Official Strategy Guide, quoted in a Civilization II
  forum thread
  (https://forums.civfanatics.com/threads/how-long-does-a-cease-fire-last.55033).
  Quote: "a cease-fire lasts only 16 turns." That is not a Civ3 source.
  `diplomacy.txt` and `labels.txt` have no ceasefire strings (only
  `AIPEACETREATY`, `AIGIVEPEACETREATY`, `USERPEACETREATY`). The labels list
  "Embargo / Against / War / Peace / Mutual Protection / Right of Passage".
  That suggests Civ3 relations are War or Peace only, with no ceasefire state
  (UNVERIFIED).

## 6.6 Gold-per-turn deals and reputation

- **What breaks a 20-turn GPT deal.** Source: CivFanatics article "Trading
  Reputation" (https://civfanatics.com/?p=280). Quote: "declare war on that AI
  (including MA/MPP)", "break an MA or MPP", "the trade route between you and
  the AI is cut".
- **No reputation hit if the AI ends the deal.** Source: same article.
  Quote: "Of course you get no rep hit if the payee decides to end the deal by
  itself." Destroying a third party under an MA also ends the deal harmlessly.
  Quote: "Your rep isn't affected."
- **Reputation is per AI, but news of a betrayal spreads.** Source: same
  article. Quote: "Each AI has a record of your reputation separately." An AI
  may also treat you as untrustworthy if it knows you broke a deal with
  another AI it is not at war with. Confirmed by Conquests `diplomacy.txt`
  `AIREJECTINFORMEDOFBROKENDEAL`. Quote: "$CIVNAME5 has informed me that you
  have broken deals with them in the past."
- **A ruined reputation is effectively permanent for GPT deals.** Sources:
  "Trading Reputation", quote: "once you break a gpt deal you'll never be able
  to make such deal again". CivFanatics Civ3 FAQ, quote: "will never again for
  the game accept per-turn payments or deals from you." The article names two
  workarounds: renegotiating peace, or signing an MA against the betrayed AI
  ("B will forgive your betrayal to A").
- **An AI war declaration during a deal does not hurt your reputation.**
  Source: CivFanatics Civ3 FAQ. Quote: "the AI declares war on you when you
  have a 20-turn deal going on" (not counted against you).
- **The AI has specific refusal lines for each kind of bad record.**
  Source: Conquests `diplomacy.txt`.
  - `AIREJECTBROKENDEAL`: "My people remember too well the deals you have
    broken with us in the past."
  - `AIREJECTPASSAGEVIOLATION`: "the last time we had a right of passage
    treaty with $CIVNAME1, you sort of attacked us, didn't you?"
  - `AIREJECTMILITARYRETRACT`: "You have failed to fully honor diplomatic
    agreements with us in the past".

  So the engine tracks at least three kinds of record: broken deals, ROP
  violations, and alliance/treaty retractions. Each also has an "informed of"
  version for third parties.
- **The advisor flags liars and betrayers.** Source: `script.txt`
  `DIPLOADVICEFOREIGN_LIAR` / `_BETRAYAL`. Quotes: "$LEADER0 is a [known liar
  and cheat.]" and "We know that $LEADER0 has [betrayed our friends] the
  $CIVNOUN1."
- **Sneak attacks.** Declaring war with any of your units inside the target's
  territory counts as breaking an implicit Right of Passage. Source:
  CivFanatics, "Reputation for sneak attacks" (Civ3, July 2002)
  (https://forums.civfanatics.com/threads/reputation-for-sneak-attacks.28206).
  Catt, quote: "Reputation hits come not because of war, but because of
  declaring war with troops in enemy territory." Lt. 'Killer' M., quote:
  "having a Scout/worker/any other unit in enemy territory when declaring war
  is enough to be a ROP-breaker".
- **Whoever declares takes the hit, and a lapsed treaty means war.** Source:
  CivFanatics, "How does the AI calculate reputation?" (Civ3, Jun 2002),
  Bamspeedy
  (https://forums.civfanatics.com/threads/how-does-the-ai-calculate-reputation.26122).
  Quotes: "The rep hit goes to whoever actually does the declaration of war."
  and "No peace treaty = war."
- **Razing ruins reputation.** Source: "The Road to Diplomatic Victory"
  (https://civfanatics.com/civ3/strategy/empire-management/the-road-to-diplomatic-victory/).
  Quote: razing an AI city "will destroy your reputation." Also: "if you trash
  your reputation no amount of gifts and bribery can fix it."
- **Failed espionage hurts reputation.** Source: Conquests Civilopedia
  `GCON_Espionage`. Quote: "Remember though, failed missions can seriously
  damage your reputation."

## 6.7 AI attitude: levels and modifiers

Primary source: CivFanatics "AI Attitude Study"
(https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/),
tested on PTW 1.01. Quote: "This test was with the 1.01 version of PTW".
In this study a negative number is good.

- **Bands.** Quotes: "-11 and lower = Gracious", "-1 through -10 = Polite",
  "0 = Cautious", "1-10 = Annoyed", "11 through 100 = Furious".
- **Starting value.** The baseline is 0. Sharing a culture group gives -1.
  Aggression level adds a difficulty-dependent amount. For example, on
  Regent-Monarch AL5 gives +3 and AL1 gives -1, and on Emperor-Deity AL5
  gives +3 and AL1 gives 0 (study, summarised). Conquests Civilopedia
  `GCON_Culture` agrees on culture groups. Quote: "nations of the same global
  culture tend to start near each other and are more tolerant in diplomacy."
- **Government.** Same government: -1 (study, summarised). Quotes: "-5
  Favorite government (only applies if both you and the AI are in this
  government)" and "+4 Shunned government (only applies if you are in this
  government and the AI is not)".
- **Agreements.** Quotes: "-5 If you have a Right of Passage with the AI."
  and "-10 If you have a Mutual Protection Pact with the AI." A common enemy
  in war gives -3, an alliance -2, and both together -5 (study, summarised).
  Damage done to a common enemy gives -1 to -3 per act, and razing an enemy
  city -7, capped near -15. This decays by 1 per turn.
- **Trade.** A recent map or tech trade, or an active 20-turn deal, gives -1.
  Trading or giving a resource gives -5. Signing an embargo gives -1 with the
  partner and +10 with the victim (study, summarised).
- **Gifts.** About -1 per 10 gold of gifted gold or techs, capped at -10.
  Quote: "This bonus decays at about 10 gold/turn."
- **Culture lead.** -1 if you lead in culture, +1 if the AI leads (study,
  summarised).
- **Tribute.** Quotes: "-1 You pay tribute to the AI.", "+2 You demand tribute
  from the AI. Whether they actually pay the tribute or not." and "+1 If you
  call the AI's bluff when he demands tribute".
- **Units in territory.** Being asked to leave gives +1. Being told to leave
  or declare war gives +4. Both are temporary and reverse when you comply
  (study, summarised).
- **Permanent penalties** (study). Quotes: "+4 for breaking a peace treaty
  with that civ.", "+1 if you raze the city of another AI.", "+12 if you raze
  a city of that civ.", "Nukes: Victim of the nuke adds +32!", "Other civs
  will add on +16". Summarised from the study: breaking an alliance +12
  (others +2); breaking an ROP +4, or +6 with units inside; selling a worker
  +1; accepting a culture flip +1 and refusing one -1.
- **Failed espionage.** Quotes: "+4 for failing at an espionage mission (any
  mission or attempting to plant a spy)." (temporary) and "+1 for failing at
  an espionage mission" (permanent).
- **Power lead halves the good effects.** Quote: "Power lead: If you have a
  power lead, most of the good effects (negative numbers) are halved." The
  study says ROP is the exception.
- **Things with no measured effect** (study, summarised): owning resources
  the AI lacks, sharing a landmass, and culture pressure on border cities.
- **BIQ personality fields (`conquests.biq` RACE, local dump).** Each civ has
  `AggressionLevel` (stored -2..2, shown as 1..5 in the editor),
  `FavoriteGovernment`, `ShunnedGovernment` and `CultureGroup`. It also has
  the AI build flags "Never"/"Often" for offensive/defensive/artillery land
  units, settlers, workers, naval, air, growth, production, happiness,
  science, wealth, trade, explore and culture, plus NoWonders/NoSmallWonders
  and the Manage/Emphasize governor flags. Examples from the dump: "Germany
  aggr=2 fav=4 shun=3", "France aggr=-2 fav=4 shun=6", "Mongols aggr=2 fav=1
  shun=5". Government indices: 0 Anarchy, 1 Despotism, 2 Monarchy,
  3 Communism, 4 Republic, 5 Democracy, 6 Fascism, 7 Feudalism.
- **Barbarians are a RACE entry with no personality.** Dump: "A Barbarian
  Chiefdom aggr=0 fav=-1 shun=-1 culture=-1". There is no diplomacy with
  barbarians: the Civilopedia `GCON_Barbarians` describes only villages and
  raiders, and no diplomacy text targets them. UNVERIFIED as an explicit rule.

## 6.8 AI trade valuation

- **The AI-human trade markup is hard-coded.** It depends on difficulty, the
  AI's aggression level and attitude. The editor's AI-AI trade rate does not
  apply to it. Source: TheNiceOne, "AI-human trade revisited" (1.29, Jan 19
  2003) (https://forums.civfanatics.com/threads/ai-human-trade-revisited.42110;
  mirror
  https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/86979-ai-human-trade-revisited).
  The summarised table:

  | Difficulty | Markup by AL1..AL5 |
  |---|---|
  | Chieftain | 0% at every AL |
  | Warlord | 0, 0, 1, 2, 3% |
  | Regent | 2% from AL2, +2% per AL (8% at AL5) |
  | Monarch | 3% from AL2, +3% per AL (12% at AL5) |
  | Emperor | 4% from AL1, +4% per AL, capped at 16% |
  | Deity | 5% from AL1, +5% per AL, capped at 20% |

  Buying: price = base x (1 + bonus). Selling: price = base / (1 + bonus).
  Quote: "the AI-human trade bonus may be reduced by 1 or 2 %" (at Polite or
  Gracious).
- **Tech price base.** The human's research cost is divided by the AI cost
  factor on Monarch and above. Quote: "Monarch 0.9, emperor 0.8, deity 0.6."
  and "the human player's research cost is divided by the AI's cost factor".
  These match `conquests.biq` DIFF CostFactor (local dump: "Monarch
  costFactor=9", "Emperor costFactor=8", "Deity costFactor=6", "Demigod
  costFactor=7", "Sid costFactor=4", against Regent's 10).
- **The tech price falls with known civs and with civs that have the tech.**
  Source: same post. Quote: "number of known civs and number of civs that
  already has the tech". The exact function is UNVERIFIED. The commonly
  cited "x1.5 to x2 depending on how many civs know it" formula is **Civ4**
  (`CvTeamAI::AI_techTradeVal`;
  https://forums.civfanatics.com/threads/ai-valuation-of-techs.658224), so it
  is not used here.
- **Things that do not change the price** (TheNiceOne, summarised): relative
  culture, relative military, culture group, and favourite or shunned
  government.
- **Attitude does change the asking price.** Source: Zwingli, "AI Attitude
  and Trade" (Civ3 1.29, Jul 18 2003), Persia at Deity buying monopoly Radio
  (https://forums.civfanatics.com/threads/ai-attitude-and-trade.59079/).
  Quotes: "Gracious** - 681 gpt (620 gpt)", "Cautious - 705 gpt (641 gpt)",
  "Furious (unlimited demands) - 939 (854)", and "For each attitude point
  drop below Cautious, Persia charged about 0.32% more for the tech". This
  conflicts with TheNiceOne (see Disputed).
- **AI-AI trade rate is a BIQ difficulty field.** Source: `conquests.biq`
  DIFF `AIToAITradeRate` (local dump: Chieftain 110 rising by 10 per level to
  Deity 170, Sid 200). Vanilla `README.txt`, quote: "Editor: Exposed AI to AI
  trade rate."
- **Exploits removed by patches.** Source: vanilla `README.txt`. Quotes: "You
  can no longer demand very large amounts of gold in diplomacy.", "Fixed AI
  exploit with regards to giving cities away in diplomacy." and "The AI no
  longer trades during the players turn."
- **The advisor grades deals on five levels.** Source: `script.txt`
  `DIPLOADVICETRADE_DEAL_*`, which match `diplomacy.txt`
  `AIWEAKREJECT`/`AINEUTRALREJECT`/`AISTRONGREJECT`/`AITOTALREJECT`. Quotes:
  "This deal will probably be acceptable.", "We're getting close to a deal
  here.", "I doubt they will accept this proposal.", "They will probably be
  insulted by this deal.", "They would never accept such a deal."
- **Selling techs for large GPT can drain the AI economy for 20 turns.**
  Source: "Crippling the AI's Research Capability"
  (https://civfanatics.com/?p=170). Quotes: "There is a limit to how much the
  AI will pay you" and "cripple their economic and scientific growth for 20
  turns".
- **The AI offers loans.** Source: `diplomacy.txt` `AILOANOFFER`. Quote: "We
  are always happy to lend you money, $PLAYER0 - at surprisingly high
  interest rates, that is." Loan terms are UNVERIFIED.

## 6.9 Tribute and demands

- **The AI demands tribute and threatens.** Source: `diplomacy.txt`
  `AIDEMANDTRIBUTE`. Quote: "Think well, $PLAYER0: if you give us tribute, our
  friendship will continue. If not, we will have to seriously re-evaluate our
  relationship." If you refuse ("call the bluff"), the outcome is
  `AIBLUFFISCALLED` or war. Quote: "Very well, then. We shall leave you to
  think about the future".
- **The AI can also give in to your demands.** Sources: `diplomacy.txt`
  `AIGIVETRIBUTE`, quote: "We will give you $GET3 as you request. But we do
  this not out of fear of you and your minions, but out of a love of peace."
  The user side, `USERWILLGIVETRIBUTE`: "We will give you what you want - this
  time."
- **Attitude effects of tribute:** see 6.7 (+2 for demanding, +1 for calling
  a bluff, -1 for paying).
- **When the AI gives in is UNVERIFIED.** The only formulas found
  ("boldness x 10%", "fear", "dominance") are from Civ1, Civ2 or Civ4 threads
  (https://forums.civfanatics.com/threads/some-notes-about-diplomacy.678722/
  is Civ1; https://forums.civfanatics.com/threads/foreign-relations.76336 is
  Civ2), so they are not used here.

## 6.10 Units in foreign territory

- **The AI warns, then demands withdrawal or war.** Sources: `diplomacy.txt`
  `AIBORDERWARNING`, quote: "it appears that some of your troops have -
  inadvertently, I'm sure - wandered into our territory, near $CITY3. Kindly
  remove them." `USERBORDERWAR` (the human's version of the same threat),
  quote: "Remove your forces from our territory or declare WAR!"
- **A promise to withdraw creates a temporary implicit ROP.** Breaking that
  promise counts as backstabbing. Source: Evie, "Reputation for sneak
  attacks" (Civ3, 2002). Quote (summarised from the page): a "We'll move our
  troops out asap" reply without a withdrawal creates a temporary ROP.
- **Diplomacy can remove units from territory.** PTW 1.27-era readme, quote:
  "Hidden Nationality units are no longer forcibly removed from enemy
  territory via diplomacy." This implies other units are forcibly removed
  (`civ3PTW/readme.txt`). The exact teleport rule is UNVERIFIED.

## 6.11 Espionage

- **Two tiers.** Diplomatic missions open with Writing. Spy missions need
  Espionage and the Intelligence Agency. Source: Conquests Civilopedia
  `GCON_Espionage_Missions`. Quote: "You can launch certain diplomatic missions
  at any time after the discovery of $LINK<Writing=TECH_Writing>." Also
  "Spy missions become available after you have discovered
  $LINK<Espionage=TECH_Espionage>." `TECH_Espionage`: "Build an
  $LINK<Intelligence Agency=BLDG_Intelligence_Center> and you can conduct
  $LINK<Espionage Missions=GCON_Espionage>." `conquests.biq` BLDG has an
  "AllowsSpyMissions" flag.
- **There are no spy units.** Source: CivFanatics Civ3 FAQ. Quote: "Spies are
  not units in the game." Civ3.com FAQ on Apolyton, quote: "will no longer
  require you to use spy units".
- **The mission list (C3C).** Source: Conquests Civilopedia `GCON_Espionage`.
  Missions: Establish Embassy, Plant Spy, Steal Technology, Steal Military
  Plans ("reveals the locations of all their military units"), Initiate
  Propaganda ("If successful, the people will revolt and
  $LINK<defect=GCON_Conversion> to your side."), Expose Enemy Spy,
  Investigate City, Steal World Map, and Sabotage Production ("destroy all
  the $LINK<shields=GCON_Shields> in the production box of a rival city").
- **Plant Disease was removed in vanilla.** Source: vanilla `README.txt`.
  Quote: "Plant Disease: This espionage mission has been removed."
- **Steal Plans shows units on the Military Advisor for one turn.** Source:
  vanilla `README.txt`. Quote: "On the turn a Steal Plans mission is
  successful, when you select the civilization ... you can mouseover a unit
  and it's location will be marked on that screen's mini-map."
- **Which agent can do what (`conquests.biq` ESPN, local dump).** Each line
  gives the diplomat flag, the spy flag and the base cost:
  - Build an Embassy: diplomat, not spy, base 20.
  - Investigate City: diplomat and spy, base 10.
  - Steal Technology: diplomat and spy, base 10.
  - Steal World Map: spy only, base 1.
  - Plant Spy: spy only, base 60.
  - Steal Plans: spy only, base 10.
  - Initiate Propaganda: spy only, base 100.
  - Sabotage Production: spy only, base 10.
  - Expose Enemy Spy: spy only, base 80.
- **Requirements.** Investigate City and Steal Technology need only an
  embassy. The other five need the Intelligence Agency, an embassy and a
  planted spy. Source: CivFanatics, "Successful Espionage"
  (https://forums.civfanatics.com/threads/successful-espionage.422226). Civ3.com
  FAQ on Apolyton, quote: "only against Civs with whom you've established an
  embassy and planted a spy".
- **War.** Embassy missions stop at war because the embassy closes. A planted
  spy keeps working. Source: Civ3.com FAQ on Apolyton. Quote: "Even if you go
  to war and your embassy is closed, your spy will continue to function as
  long as it is not caught."
- **Cost formulas.** Source: "A Study of Espionage Missions". Here dist is
  the distance to your nearest city, level is 1 for a town, 2 for a city and 3
  for a metropolis, and the leading number is the BIQ base:
  - Embassy: "20 dist*level + base + pop".
  - Investigate: "10 dist*level + base * pop".
  - Steal Tech: "10 dist*level + base * techs*techrate/100".
  - Steal World Map: "1 dist*level + base * mapsize".
  - Plant Spy: "60 dist*level + base".
  - Steal Plans: "10 dist*level + base * unit".
  - Propaganda: "100 dist*level + base * pop * (1 + opp_cult/your_cult)".
  - Sabotage: "10 dist*level + base * shield".
  - Expose: "80 dist*level + base".

  The study says "Carefully" halves the cost and "Safely" doubles it. The
  readme says the cost was exposed in the editor. Quote: "Editor: Added
  mission cost to Diplomats and Spies page."
- **Success chances** (same study):
  - Chance of escaping (Immediate/Carefully/Safely): Embassy 40/55/65,
    Regular spy 60/75/85, Veteran spy 70/85/95.
  - Success is 80% if not caught, giving Embassy 32/44/52, Regular 48/60/68,
    Veteran 56/68/76.
  - Plant Spy: Embassy 50%, Conscript 20%, Regular 50%, Veteran 60%,
    Elite 75%.
  - Embassy and Investigate City always succeed.
  - Expose Enemy Spy always fails if there is no spy.

  The escape table is confirmed by Kool Keith and Jazzmail in "Steal
  Technology"
  (https://forums.civfanatics.com/threads/steal-technology.232487/post-5709659).
  Jazzmail, quote: "You have to multiply the values in the table by 0.8."
- **Spy experience comes from government.** `conquests.biq` GOVT "SpiesAre":
  Communism and Fascism give veteran (2), the others regular (1).
  "DiplomatsAre" is regular for all. CivFanatics FAQ, quote: "the chances to
  plant a spy depend on your government, but it is around 50-60%."
- **Failure lockout.**
  - Source: `civ3PTW/readme.txt`. Quote: "If you fail a spy mission, you will
    automatically fail any spy mission against that same civilization for
    next 2-4 turns."
  - The study (C3C) differs: a failed Plant Spy sets a flag that clears with a
    1/3 chance each turn, and other failures do not set it (see Disputed).
  - MAS (Steal Technology thread), quote: "its more difficult to succeed a
    second time, as your opponent is weary."
- **A caught spy can lead to war.** Source: CivFanatics FAQ. Quote: "If it is
  not successful, the AI will become mad at you and might declare war." The
  attitude cost is in 6.7.
- **Propaganda** (study, summarised). Your spy is never caught. Each
  citizen's chance of being persuaded is:

  spy bonus (veteran 10) + government modifier + culture factor
  - 5 x garrison - courthouse (20) - capital (40) + 20 for your own
  nationals

  The city flips when at least 3/4 of its citizens are persuaded. Otherwise
  the persuaded citizens become "confused" for some turns.
  - **Democracy is immune.** `conquests.biq` GOVT Democracy has ImmuneTo=6,
    the Initiate Propaganda mission. `script.txt`, quote: "The $CIVADJ1
    people are immune to propaganda."
  - **Government modifiers** (`conquests.biq` GOVT_GOVT BriberyModifier;
    rows are the sender's government, columns the target's, in the order
    Anarchy, Despotism, Monarchy, Communism, Republic, Democracy, Fascism,
    Feudalism):
    - Despotism 15/0/-10/-15/-20/-30/0/0
    - Monarchy 20/15/0/-5/-10/-25/0/0
    - Communism 25/20/5/0/-10/-20/0/0
    - Republic 30/25/10/8/0/-5/0/0
    - Democracy 35/30/20/10/5/0/0/0
    - Fascism 0/0/-5/-20/-10/-10/0/0
    - Feudalism 20/15/0/-10/-10/-20/-25/0
    - Anarchy 0 across the row.
  - **Culture factors** (`conquests.biq` CULT "chance of successful
    propaganda"): in awe of 30, admirers of 25, impressed with 20,
    unimpressed by 10, dismissive of 5, disdainful of 3.
  - **Courthouse.** Conquests Civilopedia, quote: "The effectiveness of enemy
    spy propaganda missions against the city is reduced."
- **Steal Technology is blocked against civs you are at war with.** Source:
  rysingsun in the "Steal Technology" thread, and the Apolyton thread title
  "espionage: you can't steal techs from civs you are at war with". Combined
  with the war closing the embassy, this follows from 6.2. Whether a planted
  spy can steal tech during a war is UNVERIFIED.

## 6.12 United Nations (brief)

- **Periodic majority votes.** Source: Conquests Civilopedia (Victory,
  Diplomacy). Quote: "After the $LINK<United Nations=BLDG_United_Nations> is
  built, there are periodic votes by the council to elect a Leader. To be
  elected, a candidate must win a majority of all votes."
- **Candidates.** Quote: "There are always two, and sometimes three,
  candidates." A civ qualifies if it "have built the UN", or controls "at
  least 25% of the world's territory", or "at least 25% of the world's
  population". If only one civ is eligible, "the second candidate is the
  civilization with the largest population."
- **The UN owner decides whether to hold the vote, every 11 turns.** Source:
  CivFanatics FAQ. Quote: the choice "comes around every 11 turns". Also
  `script.txt` `DIPLOVICTORYVOTEOPTION`: "Would you like to hold elections for
  UN Secretary-General?" and "Election was inconclusive; no majority was
  found."
- **An AI must be Polite or better to vote for you.** Source: AI Attitude
  Study (summarised).

---

## Disputed

- **Right of Passage tech.** Vanilla Civilopedia says Writing ("Can now
  negotiate military alliance and right of passage"). Conquests BIQ and
  Civilopedia say Map Making. The C3C `GCON_Embassies` text still says Writing
  ("That done, you can sign Right of Passage agreements and Military
  Alliances"), a leftover from vanilla. **Map trading:** Map Making in vanilla,
  Navigation in C3C. For a C3C ruleset, follow the BIQ tech flags.
- **Does attitude change trade prices?** TheNiceOne (1.29) found that
  attitudes below Polite give no extra markup ("no AI will demand more if
  falling below catious"). Zwingli (1.29) measured +0.32% per attitude point
  below Cautious, and Furious cost about 38% more than Gracious when combined
  with demands.
- **Spy failure lockout.** The PTW readme says any spy mission fails
  automatically for 2-4 turns after a failure. The espionage study says only
  a failed Plant Spy sets a flag, cleared with a 1/3 chance per turn.
- **Embassy attitude sign.** The AI Attitude Study lists "-2 If you have an
  embassy with the AI" (good) in one place, but its permanent-effects list
  reads it as +2.
- **AI reputation.** Players disagree on whether AIs suffer reputation for
  their own treaty breaks. Myartar, quote: "There seems to be NO penelty to
  the AI on reputaion". Others are unsure.
- **Embassy and alliance gate.** The CivFanatics FAQ and the Civilopedia say
  Military Alliance needs Writing. A forum post (search result) adds that a
  road or sea link to the AI capitals is needed for ROP and MA. That link
  requirement is likely confused with resource trade (UNVERIFIED).

## UNVERIFIED / open questions

1. **First contact trigger.** Adjacency, sight, or borders? Does meeting one
   civ ever give contact with its partners automatically?
2. **Ceasefire.** Does Civ3 have one, and how long does it last? The "16
   turns" figure is Civ2. The Civ3 text files have no ceasefire strings.
3. **Exact tech-trade valuation function in Civ3**: how the number of known
   civs and civs owning the tech scale the price, and the wonder value term.
4. **When the AI gives in to tribute demands**, and when it starts making
   its own demands (power ratio, aggression, attitude).
5. **When the AI agrees to or refuses ROP, MPP, MA and embargo**
   (`AINORIGHTOFPASSAGE`: "Our bond is not sufficient to allow trust of this
   magnitude.") The attitude thresholds are unknown.
6. **How long attitude penalties last**, beyond the study's decay rates
   (gifts at about 10 gold/turn, war damage at 1/turn). The temporary
   espionage +4 recovers over about 10-20 turns per the study, untested.
7. **Whether reputation ever recovers** without the "renegotiate peace / ally
   against the victim" workarounds.
8. **Withdrawal on agreeing to leave territory**: are units teleported out
   at once?
9. **Whether a planted spy can steal tech during a war.**
10. **Loan terms** offered by the AI (`AILOANOFFER`).
11. **AI pricing of cities, workers and maps in trades.** There are no
    sources beyond "the AI values its towns highly" anecdotes.
12. **AI "won't trade techs with someone at war with its friend"** or "won't
    trade while you're in a war you started". No Civ3 source was found. Such
    rules are attested for Civ4 only.
13. **Embassy-granting wonder** (Marco Polo's Embassy): its presence and
    effect per version were not checked here. It belongs to the wonders
    section.
