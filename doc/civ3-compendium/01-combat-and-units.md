# 1. Combat and units

Scope: base game, Play the World (PTW) and Conquests (C3C), patched (1.29f / PTW 1.27f / C3C 1.22).
Scenarios, mods and the special C3C game modes (Regicide, Elimination, Capture the Princess, victory-point
games) are out of scope. C3C is the target ruleset. Where vanilla or PTW differ, the difference is called out.

## Sources used

Local files are from a Steam *Civilization III Complete* install (`.../Sid Meier's Civilization III Complete/`).
Quotes from local files are exact. Quotes from web pages were pulled out with a fetch tool that summarizes pages,
so short web quotes should be spot-checked against the page before anything leans on their exact wording.

| Key | Source |
|---|---|
| **PEDIA** | `Conquests/Text/Civilopedia.txt`, the C3C 1.22 in-game Civilopedia. Cited by entry key (`GCON_Combat`, `PRTO_Galley`, ...). |
| **PEDIA-V** / **PEDIA-PTW** | `Text/Civilopedia.txt` (vanilla 1.29f) and `civ3PTW/Text/Civilopedia.txt` (PTW 1.27f). |
| **EDHELP** | `Conquests/Civ3ConquestsEdit.chm`, the official C3C editor help (decompiled). Cited by page name. |
| **BIQ** | `Conquests/conquests.biq` (C3C 1.22 default rules), as imported into this repo's `C7/Lua/civ3/ruleset.json` by `ImportCiv3` (`_Console/RegenerateRuleset`). Only fields that come from the BIQ are used. |
| **README-V** | `README.txt`, the vanilla patch notes up to 1.29f, plus a "Changes" list of differences from the printed manual. |
| **README-PTW** | `civ3PTW/readme.txt` (PTW patch notes up to 1.27f). |
| **README-C3C** | `Conquests/readme.txt` (C3C 1.20 to 1.22). |
| **CFC-ARMY** | https://civfanatics.com/civ3/strategy/introductory/the-basics-of-armies/ (C3C only) |
| **CFC-LEAD** | https://civfanatics.com/civ3/strategy/military/creating-and-using-leaders/ |
| **CFC-SINK** | https://civfanatics.com/civ3/strategy/game-mechanics/sinking-chances-of-seafaring-and-non-seafaring-civilizations/ ("All tests done with Conquests, v1.22.") |
| **CFC-RULES** | https://forums.civfanatics.com/threads/editor-rules-explained.210035/ (Bluemofia's C3C editor field guide) |
| **CFC-CSE** | https://forums.civfanatics.com/threads/combat-system-explained.7679/ (Valant2, 2001-2002, vanilla) |
| **CFC-RET** | https://forums.civfanatics.com/threads/how-do-retreats-work.100878 and https://forums.civfanatics.com/threads/retreating-units.104978/ |
| **APO-ZOC** | https://apolyton.net/forum/civilization-series/civilization-iii/124182-zoc-defensive-bombardment (Jan 2004, includes **alexman, Firaxis**) |

Note: the repo file `C7/Civilopedia/Text/Civilopedia.txt` is OpenCiv3's own text ("It is not Civ3's
Civilopedia"), so it is **not** used as a source. The repo's ruleset also carries a `promotionChance` per
experience level; that field is **not** in the BIQ (the BIQ EXPR record holds only a name, base hit points and a
retreat bonus, see `QueryCiv3/BiqSections/Expr.cs`), so it is not a source either.

---

## 1.1 Combat resolution

- **Four kinds of combat.** Ground/naval combat, bombardment, missile attacks and air combat.
  PEDIA `GCON_Combat`: "There are four types of combat: ground/naval combat, bombardment, missile attacks, and air combat."
- **A battle is fought in rounds until one side has no hit points left; the loser dies, the winner may be damaged.**
  PEDIA `GCON_Combat`: "The two units trade blows until one of them loses its health bar and is destroyed.
  The winner may be damaged (its health bar is reduced)."
- **Each round costs the loser exactly 1 hit point. Civ3 has no firepower.** Every round removes one HP.
  - CFC-CSE (Valant2): "During battle each time the unit loses on a battle roll they loose 1 hp." and
    "If the unit wins the turn, it causes 1hp of damage to the opponent."
  - https://forum.freeciv.org/f/viewtopic.php?p=97232 (Akechi, from memory): "There is no Firepower status in Civ 3. all combat round deals 1 damage to each round loser."
  - Pre-release Firaxis statements on firepower are not available; the Apolyton thread
    https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/45717-probability-and-combat (Grunthex)
    says: "Uh, units have between 2 and 5 hp, not 10. Depending on their veterancy."
- **Per-round odds are A / (A + D)**, with A the attacker's modified attack and D the defender's modified defense.
  Community-derived, never confirmed by Firaxis in the sources found.
  - CFC-CSE (Valant2): "Crude Turn Win % = (attackers offensive rating)/ ((attackers offensive rating)+(defenders defensive rating))"
  - https://forums.civfanatics.com/threads/made-attacker-win-probability-page.7886/page-2 (Grendel): "the probability of the attacker winning one round (causing a HP loss in the defender) is a/(a+x)."
  - The same thread gives the whole-battle odds as a negative binomial: "(a / (a+x))^y * (x / (a+x))^z * nCr(y+z-1, y-1)", summed over z = 0 .. b-1 (b = attacker HP, y = defender HP, z = rounds the attacker loses).
- **Bombard odds use the same ratio, with bombard strength in place of attack.** EDHELP's own example shows a 50%
  chance at equal values. EDHELP *Building (Defensive Bonuses)*: "a unit with a bombard strength of 4 would have a
  50% chance of destroying a building with a defense rating of 4."
  https://forums.civfanatics.com/threads/bombard-question.19648 (dunk): a bombard-8 cannon against a defense-3 cavalry has "an 8 out of 11 chance of hurting the cavalry."
- **Random seed is saved with the game** (reloading replays the same results) unless the rule is turned off.
  README-V 1.21f: "Added Preserve Random Seed rule. Turning this off will allow random seeds to be changed when a saved game is reloaded (which makes combat different...)."
  Dan Magaha (Firaxis), https://forums.civfanatics.com/threads/as-promised-a-civ-iii-combat-test-map.15208/: "the game was designed so that save + reload wouldn't be a very effective or efficient way to cheat"
- **Defender choice.** The game defends a stack with its strongest available defender. UNVERIFIED (no source found
  that states the exact rule, for example whether HP or experience break ties).
- **Units without attack can't attack; units without defense are captured, not fought** (see 1.13).
- **Stealth attack lets the attacker pick its target in a stack** (C3C). PEDIA `GCON_Stealth_Attack`: "Stealth
  attack allows a unit to specify which enemy unit -- in a stack -- it will attack." Submarines have it
  (PEDIA `PRTO_Submarine`: "Submarines have $LINK<Stealth Attack=GCON_Stealth_Attack>").
- **Collateral damage** (C3C ability). EDHELP *Create Craters and Collateral Damage*: if the defender "takes damage,
  then: If the defending unit is not in a city, the artillery/bombard value of the attacking unit is used to conduct
  a bombard attack on that space to determine if any terrain improvements are also destroyed. If the defending unit is
  in a city, the the artillery/bombard value of the attacking unit is used to conduct a bombard attack on that city".
- **Craters** (C3C). PEDIA `GCON_Craters`: "When a bombardment attack which eliminates the last unit or terrain
  improvement in a square, a crater may be formed. Like pollution, the presence of craters reduces a square's
  production, commerce, and food output by one."
- **"Starts Golden Age" units.** EDHELP *Unit Abilities*: "If victorious in combat, this unit triggers a golden age".

### Healing

- **1 HP per turn, only in friendly or neutral territory, only if the unit does not move.**
  PEDIA `GCON_Combat`: "adding one $LINK<block=GCON_Experience> per turn back to its health bar. To do so, it
  ^* must be in friendly or neutral territory. ^* must not move for a complete turn."
- **Full heal in one turn:** ground units in a city with Barracks, ships with a Harbor, air units with an Airport.
  PEDIA `GCON_Combat`: "A city with a $LINK<barracks=BLDG_Barracks> heals ground units completely in one turn." (same wording for Harbor and Airport).
- **Battlefield Medicine** (small wonder, needs 5 Hospitals) allows healing in enemy territory.
  PEDIA `BLDG_Battlefield_Medicine`: "Allows military units to heal in enemy territory. ^{Requires:} The civilization must have at least five hospitals".
- BIQ heal-rate table: `friendly_field 1, neutral_field 1, hostile_field 0, city 2`. (The "city 2" value means a city
  without Barracks heals 2 HP per turn if the import is correct. Not confirmed by a text source.)

---

## 1.2 Experience and hit points

- **Hit points by experience level.** BIQ EXPR and PEDIA agree.

  | Level | HP (blocks) | Retreat bonus (BIQ) |
  |---|---|---|
  | Conscript | 2 | 34 |
  | Regular | 3 | 50 |
  | Veteran | 4 | 58 |
  | Elite | 5 | 66 |

  PEDIA `GCON_Experience`: "Conscripts 2 ^Regulars 3 ^Veterans 4 ^Elite units 5".
- **Unit HP bonus** (editor, since 1.21f): added to the level's base HP, from -20 to +20, never below 1 HP. All stock
  units have 0 (UNVERIFIED for every unit; the BIQ `hpBonus` field is 0 for the units checked).
  EDHELP *HP Bonus*: "This bonus is added to the base hitpoint value (determined by the unit's experience level) ...
  the unit's total number of HP will never drop below 1."
- **What produces each level.** Cities build regulars; a Barracks (Harbor for ships, Airport for planes) builds
  veterans; draft and barbarian-hut units are conscripts; elite only comes from combat.
  PEDIA `GCON_Experience`: "Normally, cities produce regulars. But if a city has a $LINK<barracks=BLDG_Barracks>, it
  produces [veterans]. ... [Elite units] cannot be built: they gain this status only through actual combat experience."
  PEDIA `BLDG_Harbor` / `BLDG_Airport`: "produce $LINK<veteran=GCON_Experience> naval units" / "air units instead of regulars."
  Sun Tzu's Art of War gives a Barracks in every city on its continent (PEDIA `BLDG_Art_of_War`: "Puts a $LINK<barracks=BLDG_Barracks> in every city on the same continent.").
- **Battlefield promotion after a win.** PEDIA `GCON_Experience`: "When one of your units is victorious in battle,
  there is a chance it will gain combat experience and be promoted to the next level." The chances are not in the
  Civilopedia or BIQ. Community figures:

  | Promotion | Non-militaristic | Militaristic |
  |---|---|---|
  | Conscript to Regular | 1/2 | doubled |
  | Regular to Veteran | 1/4 | 1/2 |
  | Veteran to Elite | 1/8 | 1/4 |

  - https://forums.civfanatics.com/threads/barbarians-gaining-experience.115547 (Theoden, #18): odds roughly 1 in 2,
    1 in 4 and 1 in 8, "Double the chances for militaristic civs and halve them when fighting barbarians."
  - https://forums.civfanatics.com/threads/heroic-epic-militaristic-civs.26455 (Catt, #2): "A veteran militaristic
    unit has a 1 in 4 chance of being promoted to elite after a victorious battle against an AI civ." and "A veteran
    non-militaristic unit has a 1 in 8 chance." Catt (#6) credits this to a Soren Johnson post that is not reproduced.
  - PEDIA `GCON_Strengths` (Militaristic): "combat $LINK<experience=GCON_Experience> is gained more quickly."
- **Barbarians do not promote** (C3C 1.22 test). Same thread 115547 (Theoden, #14, C3C 1.22): "all dying without any of the mech infs gaining any experience." This is about barbarian units gaining levels, not about your units promoting while fighting barbarians (see Disputed).
- **A damaged unit keeps its level.** Experience is the level, not the current HP. A veteran at 2/4 HP is still a
  veteran for retreat and promotion. Apolyton (Ethelred) https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/75784-fast-units-no-longer-retreating-at-low-health: "The editor's retreat setting is for the experience level not the hit points."
- **Units that made a leader are marked.** README-V 1.29f: "Added an '*' next to experience level name for units that have produced a leader." Whether such a unit can make a second leader is UNVERIFIED.

---

## 1.3 Great Leaders (military)

- **A leader can appear when an elite unit wins a battle.** PEDIA `GCON_Leaders`: "A {Military leader} is born in
  combat. This [can] occur when an $LINK<elite unit=GCON_Experience> wins a battle."
- **Chance per elite win: 1/16; 1/12 with the Heroic Epic; half that when the win is on defense.** Not in the BIQ or
  Civilopedia. Community figures, consistent across sources:
  - CFC-LEAD: "Each time one of your elite units wins a fight against a rival, there is a 1 in 16 chance that a leader will appear." / "your chance of getting a leader improves to 1 in 12 when you win a fight with an elite." / "The chances of getting a leader from a successful defense are 1/2 the chances while attacking."
  - https://apolyton.net/forum/miscellaneous/archives/civ3-play-the-world/80836-how-a-leader-is-born (Catt): "The chance of a leader generation is 1/12, 1/16, 1/24, or 1/32, depending on circumstances".
  - PEDIA `BLDG_Epic`: "Increases the chances of the appearance of $LINK<leaders=GCON_Leaders> from victorious combat."
- **No leaders from fighting barbarians.** CFC-LEAD: "You can not get a leader by fighting a barbarian."
- **The militaristic trait does not change leader odds** (it only raises promotion odds). Thread 26455 (Catt): "The
  odds of leader generation for ALL civs, militaristic or not, is 1/16 without the Heroic Epic".
- **Armies do not generate leaders.** Thread 80836 (Catt): "Armies cannot generate leaders." Killing an army can
  (Theseus, same thread: "You can, however, generate a GL by an elite killing an Army.").
- **Only one military leader alive at a time, but several can appear in one turn.** CFC-LEAD: "You can only have one
  leader at a time." / "You can however produce more than one leader in one game turn." The second statement looks
  inconsistent with the first; how it works (whether the limit is checked at turn start) is UNVERIFIED.
- **Leader unit.** BIQ: Leader costs 0, A/D 0/0, movement 3. The unit the editor spawns is the "Battle Created Unit"
  (CFC-RULES: "Determines which unit is spawned when an elite unit wins a battle."; BIQ `battleCreatedUnit: Leader`).
  A leader cannot fight and is captured or destroyed if caught alone (see 1.13).
- **Uses (C3C): make an army, or finish an improvement or small wonder in the city it is in. It cannot hurry a Great Wonder.**
  PEDIA `GCON_Leaders`: "A Military Leader can be used to either create an $LINK<army=PRTO_Army> or to
  $LINK<hurry=GCON_Hurry_Production> production of an Improvement or Small Wonder in a city. When a military leader
  does either of these actions he disappears."
- **Vanilla and PTW: leaders hurry anything, including Great Wonders.** PEDIA-V / PEDIA-PTW `GCON_Leaders`: "Leaders
  can be used to either create an $LINK<army=GCON_Armies> or to $LINK<hurry=GCON_Hurry_Production> production in a city."
  https://forums.civfanatics.com/threads/what-is-a-leader-and-how-do-i-get-one.21132 (Fez_Monk): "Using the leader is
  the only way of hurrying a wonder."
- **C3C scientific leaders** (from being first to a tech) can hurry Great Wonders. They belong with science, not
  combat. PEDIA `GCON_Leaders`: "A Scientific Leader can also be used to $LINK<hurry=GCON_Hurry_Production> the
  construction of any [improvement] (including a Great Wonder)".
- **A unit that makes a leader can be renamed.** README-V: "Added the ability to rename units if they produce a great leader during combat."

---

## 1.4 Armies

- **Capacity 3 units, 4 with the Pentagon. The Pentagon needs 3 armies in the field.**
  PEDIA `BLDG_Pentagon`: "[The Pentagon] increases the unit capacity of all the civilization's armies from three to
  four. ^{Requires:} The civilization must have at least three $LINK<armies=GCON_Armies> in the field." (BIQ Army `capacity 3`.)
- **Built by a leader in any city, or built (400 shields in C3C) in the city with the Military Academy.**
  PEDIA `GCON_Armies`: "Armies can be built in cities that have the $LINK<Military Academy=BLDG_Military_Academy>
  or a $LINK<Leader=GCON_Leaders> in them." BIQ: Army `shieldCost 400`.
  The CivFanatics FAQ v3.0 (https://civfanatics.com/civ3/strategy/misc/civilization-iii-faqstrategy-guide-v3-0/)
  says "you can use 200 shields to construct an army". That is probably the vanilla cost (see Disputed).
- **At most one army per 4 cities.** PEDIA `GCON_Armies`: "you must have at least four cities for every army you
  create." EDHELP *Cities Needed to Support an Army*: "Default is 4 cities." CFC-ARMY: dropping below the ratio keeps
  existing armies but blocks new ones.
- **Military Academy and Heroic Epic both need a victorious army first.** PEDIA `BLDG_Military_Academy`: "The city with
  the [Military Academy] can build $LINK<armies=GCON_Armies> without the benefit of a $LINK<leader=GCON_Leaders>.
  Armies from this civilization are more effective in combat. ^{Requires:} The civilization must have had a
  victorious $LINK<army=GCON_Armies>." PEDIA `BLDG_Epic` has the same requirement.
- **Units in an army pool their hit points; the whole army must be killed to kill any unit in it.** README-V
  ("Changes" from the manual): "Armies now literally share the hit points of the units within it. For example, an
  army of 3 regular spearman would have hit points equal to 9. To kill any unit in the army, the entire army must be
  eliminated."
- **Loading.** Units can be loaded or unloaded only while the army is still in the city that made it; after it leaves,
  the contents are fixed. README-V: "Once that army leaves the city of it's construction, you cannot add or remove
  units from the army." BIQ `allowUnloadFromArmy: false`. Units in an army cannot be upgraded (CFC-ARMY: "Units in
  armies cannot be upgraded."). Only units with both attack and defense can be loaded (CFC-ARMY: "Only combat units
  (those with an attack and defense) can be loaded into an army.").
- **Abilities.** Armies have zone of control and blitz (vanilla 1.17f on), movement = slowest unit + 1 (C3C),
  pillage (PTW 1.04f on), and see 2 tiles.
  - README-V 1.17f: "Armies now have the zone of control and blitz ability."
  - README-C3C: "an Army unit also has the "Blitz" capability (it can attack twice in the same turn), its movement
    rate has been increased by one, and units in an Army heal faster than they would if they were not within the Army unit."
  - README-V: "The only unit abilities that an army can inherit is mobility, and only when all units in that army have that ability."
  - README-PTW 1.04f: "Added the Pillage ability to Armies."
  - PEDIA `GCON_Armies`: "An army can "see" up to two squares away, due to its intrinsic scouts."
  - CFC-ARMY: "When no units have yet loaded into the army, the army will only have a movement of one."
- **Army combat strength (C3C).** The fighting unit gets a share of the others' strength:
  `Attack = A + (TA / N)`, N = 4 with the Military Academy, else 6, rounded down. Defense is the same with defense values.
  CFC-ARMY: "A is the attack of the unit currently doing the fighting." / "TA is the attacks of all the units added
  together." / "N is 4 if you own the military academy, else just 6." / "This formula is rounding fractions down."
  Whether TA includes the fighting unit itself is as written in the article ("all the units").
- **Which unit fights.** PEDIA `GCON_Armies`: "An army uses one, some, or all its internal units to fight a battle,
  depending upon the course of the fight." CFC-CSE (Valant2, vanilla): "When an army attacks or defends the first unit
  continues to fight/defend until the unit is down to 1hp."
- **Army healing.** CFC-ARMY: "Armies heal at the rate of one HP per unit in the army." and "armies are also able to
  heal in one turn in a city containing barracks." CFC-ARMY also says armies heal in enemy territory without
  Battlefield Medicine ("This can also be done in enemy territory even before battlefield medicine.").
- **Army retreat.** Whether armies can withdraw is disputed (see Disputed).

---

## 1.5 Retreat (withdrawal)

- **Fast units withdraw when losing, unless the opponent is also fast.** PEDIA `PRTO_Horseman`: "move fast and will
  [withdraw] from combat if losing (unless fighting another fast unit)." (`PRTO_Cavalry` has the same wording.)
- **Since 1.17f, withdrawal is a die roll that depends on experience.** README-V 1.17f: "Mobile units now have to make
  a die roll to determine if they withdraw. Success is also modified by their experience level." Before that, fast
  units always withdrew (thread 100878, The Last Conformist: "In Original Release Vanilla, units always retreated if able.").
- **Retreat bonus by level (BIQ, editable since 1.21f):** conscript 34, regular 50, veteran 58, elite 66.
  README-V 1.21f: "Editor: Added Retreat Bonus controls to Combat Experience page." CFC-RULES: "Determines the ratio
  of retreating when a fast unit is fighting a slow unit." EDHELP *Retreat Bonus*: "The higher the number listed for
  the retreat bonus, the more likely it is that the unit will successfully retreat when losing a fight."
- **"Fast"** means more movement points than the opponent. CFC-CSE (Valant2): retreat requires higher movement and
  unused movement points. The exact comparison (base movement or moves left) is UNVERIFIED.
- **When the roll happens (community-tested):** when the losing fast unit is about to drop to its last HP, the opponent
  has 2 or more HP, and the fast unit has movement left.
  - CFC-RET 104978 (Offa, #9): "Retreat possible if attacking unit drops from 2 HP to 1 HP, and is implemented before
    the attacker attacks with 1HP left." / "Zero chance if defender has 1 HP, or fast defender." / "Only one chance per turn to retreat, barring odd circumstances."
  - CFC-RET 100878 (boogaboo): the roll applies only if the opponent has 2HP or more, the retreating unit has 1HP, and
    it would have at least 1/3 movement point left after the attack.
  - A retreating unit survives with 1 HP and does not move (IbnSina, 100878: "Retreat does not require any special ground - no movement actually takes place.").
- **Retreat formula (best guess, not confirmed):** P(retreat) = attacker retreat bonus / (defender retreat bonus + 50).
  CFC-RET 104978 (Offa): "prob of retreat = (attacker retreat bonus) / (defender retreat bonus + 50)", described as
  "thought to be". BomberEscort (quoted in 100878): "(100*Attacker Retreat Bonus)/(50+Defense Retreat Bonus)" and
  "When the Attackers retreat value is 50 percentage points greater than the Defender, the Attacker always retreats."
  Measured success ranges for fast attackers in valid cases (same post): "elite fast-attacker is 55-75% retreat";
  veteran 50-66%; regular 43-56%; conscript 29-38%.
- **Defending fast units can also withdraw** when attacked by a slower unit. CFC-RET 100878 (boogaboo): the retreat
  bonus applies "both to retreating when failing to attack and to retreaing when failing to defend." Disputed by
  Offa's "fast defender" zero-chance line (see Disputed).

---

## 1.6 Defense modifiers

- **Terrain bonuses (C3C).** PEDIA `GCON_Terrain_Combat` and BIQ TERR agree:

  | Terrain | Defense | Move cost (BIQ) | Wheeled can enter (BIQ) |
  |---|---|---|---|
  | Grassland, Plains, Desert, Floodplain, Tundra | +10% | 1 | yes |
  | Coast, Sea, Ocean (and Fresh Water Lake) | +10% | 1 | n/a |
  | Marsh (C3C) | +20% | 2 | no |
  | Forest | +25% | 2 | yes |
  | Jungle | +25% | 3 | no |
  | Hills | +50% | 2 | yes |
  | Volcano (C3C) | +80% | 3 | no |
  | Mountains | +100% | 3 | no |

  PEDIA `GCON_Terrain_Combat`: "^Marsh 20% ^River 25%* ^Forest 25% ^Jungles 25% ^Hills 50% ^Volcano 80% ^Mountains 100%".
  Vanilla and PTW have no Marsh or Volcano rows (PEDIA-V and PEDIA-PTW tables stop at the 14 terrain rows plus River).
- **River: +25% for a defender attacked across a river.** PEDIA `GCON_Terrain_Combat`: "* This bonus applies to the
  defender if attacked from the other side of a river." BIQ/EDHELP *River*: "Default is 25."
- **Structural bonuses (C3C).** PEDIA `GCON_Terrain_Combat`:

  | Source | Bonus |
  |---|---|
  | Fortified | +25% |
  | Fortress | +50% |
  | Barricade (C3C) | +100% |
  | Town Walls (town only) | +50% |
  | Civil Defense | +50% |
  | City (size 7-12) | +50% |
  | Metropolis (size 13+) | +100% |

  "^Fortified Units +25% ^Fortress +50% ^Barricade +100% ^Town Walls +50% ^Civil Defense +50% ^City +50% ^Metro +100%".
  EDHELP gives the defaults for the General-page fields: *Town*: "Default is 0 (no bonus)." *City*: "Default is 50."
  *Metropolis*: "Default is 100." *Fortifications*: "Default is 25." *Fortress*: "Default is 50."
  City size levels (BIQ): town up to 6, city up to 12 (`maximumLevel1CitySize 6`, `maximumLevel2CitySize 12`).
  Vanilla's table has no Civil Defense row (PEDIA-V); PTW adds it (PEDIA-PTW).
- **Bonuses add up (they are not multiplied).** EDHELP *Fortifications*: "Note that this bonus is cumulative with
  other terrain and fortification-related defensive bonuses." EDHELP *Defense Bonus (Improvement Properties)*: "This
  bonus is cumulative with other improvement and general defense bonuses". CFC-CSE (Valant2): "Bonus percentages add,
  so 2 50% bonuses result in a 100% total bonus." See Disputed for the multiplicative view.
- **Walls: +50% in towns only, and land bombard defense 8.** PEDIA `BLDG_Walls`: "[Walls] can be built only in
  town-size settlements and provide a land bombardment defense of eight, and a 50% defensive bonus to units defending
  in the town. When a town becomes a city, the walls no longer provide a defensive bonus." BIQ: Walls `combatDefenseBonus 0.5`, `bombardDefense 8`, flag `canOnlyBeBuiltInTowns`.
  Walls do not add to the size 7+ bonus (Mike B., Firaxis, quoted by Catt in
  https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/70027-question-about-great-wall: "It has no
  effect on Cities or Metropolises (since walls are ineffective for these pop. levels)."). README-PTW: "Fixed bug which
  caused walls not to be obsoleted by growing from a town to a city."
- **Civil Defense: +50% at any size.** PEDIA `BLDG_Civil_Defense`: "[Civil Defense] can be built in any size settlement
  and provides a 50% defensive bonus to units defending in the town. Civil Defense can only be built in a city that has
  a barracks." README-C3C: "Civil Defense requires Electronics".
- **Great Wall.** C3C: a free Wall in every city on its continent, and double combat values against barbarians.
  PEDIA `BLDG_Great_Wall`: "Doubles unit combat values versus $LINK<Barbarians=GCON_Barbarians>. ^Places a
  $LINK<Wall=BLDG_Walls> in each city of the continent on which it is built."
  Vanilla/PTW: doubles walls instead. PEDIA-V `BLDG_Great_Wall`: "The [Great Wall] doubles the effects of
  $LINK<Walls=BLDG_Walls> in cities that have them." Mike B. (Firaxis, via thread 70027): "The Great Wall doubles the
  defensive value of Walls in all Towns that have them."
- **Cities in resistance give no defense bonus.** PEDIA `GCON_Terrain_Combat`: "Cities with resisters do not give defensive bonuses."
- **Terrain under a city still counts.** CFC-CSE (Xerxes314): "Terrain under a city does affect its defense value."
  The printed manual (p. 110, quoted by royfurr in thread 7886): "Terrain bonuses are figured in BEFORE THE CITY SIZE
  AND WALLS TAKE EFFECT."
- **Fortify.** +25%; fortify, fortress and walls stack per EDHELP's "cumulative" wording. Some players believed the
  wall or fortress replaces fortify (https://forums.civfanatics.com/threads/defense-bonuses-question.21715). See
  Disputed. Which units cannot fortify is UNVERIFIED.
- **Fortress.** +50%, gives zone of control to units inside. PEDIA `GCON_Fortresses`: "They get a 50% defensive bonus
  if attacked. ^* They have a $LINK<zone of control=GCON_ZOC> and fire at passing enemy units without receiving return
  fire." Built after Construction (same entry).
- **Barricade (C3C).** Built on a fortress, +100%, stops enemy ground movement into the tile. PEDIA `GCON_Barricades`:
  "barricades automatically stops movement of a ground unit entering its square (unless that unit belongs to the same
  civilization currently controlling the barricade's square, or if that unit has a $LINK<Right of Passage=GCON_Agreements>
  agreement through that land." / "Barricades become a fortress if their square is pillaged."
- **Radar Tower (C3C): +25% attack and defense to friendly units within 2 tiles, once.** PEDIA `GCON_Radar_Towers`:
  "The range of the effected area is 2 tiles. Friendly units within the Radar Towers range get a +25% attack and
  defense bonus. This bonus can only be applied once, even if multiple towers overlap."
- **Coastal Fortress: naval bombard defense 8, and it bombards passing enemy ships. It gives no defense bonus to land
  units.** PEDIA `BLDG_Coastal_Fortress`: "A [Coastal Fortress] gives the city a naval bombardment defense of eight.
  ^The fortress automatically bombards passing enemy ships." README-PTW: "Fixed Civilopedia entry for Coastal Fortress
  by removing reference to, "50% defense bonus from naval attacks". There are no direct naval attacks on land units --
  only bombardment." (The BIQ still holds a naval defense bonus of 50 for it, which per this note has no target.)
- **Attacker bonuses.** Terrain gives no attack bonus (CFC-CSE: "terrain does not give any offensive bonuses what so
  ever"). The only attack modifiers found are the Radar Tower and the bonus against barbarians.
- **Rounding of modified strengths.** Unknown. https://forums.civfanatics.com/threads/civ3-combat-calculator.454569/
  (vmxa): "I think it is rounded down, after all modifiers are applied." Xerxes314 (CFC-CSE): "I don't know why
  anybody would think that there's rounding involved at any stage of the calculation." UNVERIFIED.

### Versus barbarians

- **Attack bonus against barbarians, by difficulty** (editor DIFF field, added in 1.16f). README-V 1.16f: "Editor:
  Added Barbarian Combat Bonus to difficulty level tab." Values from
  https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/ (Pyrkaige, read from the PTW/1.29f editor):
  "Attack bonus against barbarians: 800 400 200 100 50 0" for Chieftain, Warlord, Regent, Monarch, Emperor, Deity.
  Arathorn's later post in the same thread gives Demigod 25 and Sid 0 (C3C levels).

  | Chieftain | Warlord | Regent | Monarch | Emperor | Deity | Demigod | Sid |
  |---|---|---|---|---|---|---|---|
  | +800% | +400% | +200% | +100% | +50% | 0 | +25%? | 0 |

  EDHELP *Attack Bonus Against Barbarians*: "Determines the attack bonus enjoyed by any player (human or AI) against
  barbarians." Whether it is attack only or also defense is UNVERIFIED (the field name says attack).
- **Great Wall doubles combat values against barbarians** (all versions; quoted above).

---

## 1.7 Bombardment

- **Bombard stats: strength, range, rate of fire.** EDHELP *Rate of Fire*: "Rate of fire defines how many times a
  bombarding unit will attack per turn." CFC-RULES: "Determines the number of times the unit initiates a bombard check
  when bombarding. (0-10)". EDHELP *Bombard Range*: "how many squares away a unit may bombard cities or units."
- **Non-lethal bombard can't kill: it stops at 1 HP.** PEDIA `GCON_Combat`: "Bombardment inflicts losses to the
  defender's health bar but never completely destroys them." CFC-RULES: lethal bombard lets a unit "kill units when
  bombarding, and to bombard units with 1 HP."
- **A failed bombard roll hurts nobody; the bombarding unit never takes damage.** freeciv thread (Akechi): "If attacker
  lose round during this combat, attacker bombard unit does not receive damage." https://forums.civfanatics.com/threads/artillery-civ3-vs-civ4.145967 (search excerpt): a bombard "does not expose the artillery to harm."
- **Each hit removes 1 HP.** CFC-CSE (Valant2): "I assume all successful bombarbments on a military units only results
  in 1hp lost." Thread 19648 (warpstorm): "You fire twice. Each shot is tallied up individually."
- **Bombarding a city can damage a unit, kill 1 citizen, or destroy a building.** Thread 19648 (Park Ranger) lists
  "destroy an improvement," "kill 1 population point," and "damage a defending unit"; and "bombarding will not kill
  citizens if the city is only size 1."
- **Building and citizen defense against bombard: 16** (since 1.17f; BIQ 16). README-V 1.17f: "Building and citizen
  defense against bombardment increased to 16." BIQ `buildingDefensiveBonus 16`, `citizenDefensiveBonus 16`.
  EDHELP still says "Default is 4" (stale text; see Disputed). Walls add land bombard defense 8 and a Coastal Fortress
  naval bombard defense 8 (quoted above). How those 8s combine with 16 is UNVERIFIED.
- **Bombard can hit tile improvements outside cities, and PTW+ airfields, outposts and radar towers.**
  README-PTW 1.01f: "You can destroy through bombarding Airfields, Outposts and Radar Towers." README-PTW: "Fixed bug
  where auto-bombard would not auto-bomb multiple terrain improvements in neutral territory."
- **Ships in port are extra vulnerable** (C3C). PEDIA `GCON_Bombardment_in_Ports`: "naval units "in port" (i.e., in a
  coastal city square being bombarded) are especially vulnerable to bombardment attacks." The size of the effect is UNVERIFIED.
- **Lethal bombard (editor ability since 1.21f; used by stock units in C3C only).** README-V 1.21f: "Editor: Added new
  unit abilities to allow bombarding to be lethal against land and/or sea units." C3C stock units with it (PEDIA):
  Cruise Missile, Bomber, Stealth Bomber, Stealth Fighter, F-15, Hwach'a (land and sea); Fighter, Jet Fighter, Dromon
  (sea only). Examples: `PRTO_Bomber`: "The Bomber has both Lethal Land and Sea Bombardment." `PRTO_Fighter`: "The
  Fighter has Lethal Sea Bombardment." Vanilla/PTW have no lethal units in the stock rules (freeciv thread, Akechi:
  "If vanilla Civ 3, there is no Lethal Land/Sea Bombardment unit.").
- **Stock bombard units (BIQ, C3C): bombard / range / rate of fire.**

  | Unit | B | R | RoF |
  |---|---|---|---|
  | Catapult | 4 | 1 | 1 |
  | Trebuchet (C3C) | 6 | 1 | 1 |
  | Cannon / Hwach'a | 8 | 1 | 1 |
  | Artillery | 12 | 2 | 2 |
  | Radar Artillery | 16 | 2 | 3 |
  | Cruise Missile | 16 | 4 | 3 |
  | Archer / Bowman | 1 | 0 | 1 |
  | Longbowman / Musketeer | 2 | 0 | 1 |
  | Guerilla | 3 | 0 | 1 |
  | TOW Infantry | 6 | 0 | 1 |
  | Frigate | 3 | 1 | 2 |
  | Ironclad / Destroyer / AEGIS Cruiser | 6 | 1 / 1 / 2 | 2 |
  | Cruiser | 7 | 1 | 2 |
  | Battleship | 8 | 2 | 2 |
  | Bomber | 12 | 0 | 3 |
  | Stealth Bomber | 18 | 0 | 3 |

  Range 0 units can only bombard defensively (see next item). Air units use operational range instead of bombard range.
- **Defensive bombard ("Artillery Free Shot").** When a land tile holding a bombard-capable unit is attacked, each
  such unit (other than the one defending) fires once at the attacker before the battle.
  README-V ("Changes"): "Artillery Free Shot: When a land tile with a bombard capable unit in it is attacked, that
  bombard unit will take one free shot at the attacking unit."
  APO-ZOC (Harovan): "The defense bombard is used once in a turn per unit with this feature and only, if the unit isn't
  the defender itself."
  https://forums.civfanatics.com/threads/zones-of-control.682034/ (Flintlock, #20): "Yes, defensive bombard only uses bombard strength."
  In C3C, Archers and Longbowmen got a range-0 bombard for this (APO-ZOC, Willem: "As of Conquests, Archer/Longbowmen units now get one as well").
  Whether the free shot can kill (with a lethal unit) is UNVERIFIED.
- **Cruise missiles die after bombarding.** CFC-RULES: "a unit which, when bombarding, will die after it is done bombarding."
  PEDIA `GCON_Combat`: missiles "never return to its base and the attacks can destroy enemy units."
- **Artillery have no attack or defense and must be escorted.** PEDIA `PRTO_Artillery`: "Artillery has no attack or
  defense value, so they should be protected by an escort." If caught alone they are captured (see 1.13).
- **Bombard uses the attack for the turn.** freeciv thread (Akechi): "if unit does not have Blitz ability, this unit
  can only one attack per turn". Whether bombarding costs all movement is UNVERIFIED.

---

## 1.8 Zone of control

- **ZOC does not block movement; it gives a free shot at an enemy moving past.** PEDIA `GCON_ZOC`: "Such units attack
  enemies that enter the zone of control without initiating a full battle." EDHELP / CFC-RULES: "Determines if the
  unit will automatically bombard passing units." APO-ZOC (Harovan): "In Civ2 or CtP the movement would be blocked due
  to ZoC, in Civ3 it is still possible, but the other civ gets a free shot on the passing unit."
- **Units with ZOC (C3C):** AEGIS Cruiser, Armies, Cavalry, German Panzer, Mech Infantry, Modern Armor, Mongol Keshik,
  Ottoman Sipahi, Modern Paratroops, Radar Artillery, Russian Cossack, Spanish Conquistador, Tank, and any unit in a
  fortress or barricade (PEDIA `GCON_ZOC`, `GCON_Fortresses`, `GCON_Barricades`). Vanilla also lists Marines and
  (plain) Paratroops and lacks the PTW/C3C unique units (PEDIA-V `GCON_ZOC`).
- **Trigger: an enemy moves from one tile next to the ZOC unit to another tile next to it.** APO-ZOC (geniemalin):
  the shot triggers only when an enemy moves between two squares adjacent to the unit.
  https://apolyton.net/forum/civilization-series/civilization-iii/110683-how-do-the-zone-of-control-work (CerberusIV):
  "It only works when an enemy unit moves from a tile adjacent to the fortress to another tile also adjacent to the fortress".
- **Each shot takes 0 or 1 HP and never kills.** Thread 682034 (Theov): "The outcome is binary and costs one HP."
  Ozymandias (same thread, quoting civ-wiki.de): "The control zone has no effect on units that have only one health point."
- **Hit chance (reverse-engineered, C3C):** `1 - (D*16 / (D*16 + A))`, D = the moving unit's defense with no terrain
  bonus, A = the higher of the ZOC unit's attack or bombard, using the strongest ZOC unit in the stack.
  Thread 682034 (Flintlock, #3): "1 - (D*16 / (D*16 + A))"; "The game will always select the strongest available unit
  to try to do damage."; Ozymandias: "The defense value of the moving units does not enjoy bonuses from the terrain."
  This implies low odds (for example A 16 vs D 2: 16/48 = 33%).
- **Walls in a town give ZOC (claim).** Thread 682034 (Ozymandias, quoting civ-wiki.de): "All units in a settlement
  (size 1 to 6) with a city wall automatically have a control zone." UNVERIFIED.

---

## 1.9 Movement

- **Terrain costs** are in the table in 1.6 (BIQ): 1 for flat land and water, 2 for hills, forest and marsh, 3 for
  mountains, jungle and volcano. EDHELP *Movement Cost*: "Determines the base movement cost for a tile of the selected terrain type."
- **Roads: 1/3 movement point per tile regardless of terrain.** PEDIA `TFRM_Road`: "units moving along roads expend one
  third of a movement point per square, regardless of terrain type." BIQ `movementAlongRoads 3`.
- **Railroads: zero movement.** PEDIA `TFRM_Railroad`: "Units moving along a railroad expend zero movement points."
  Railroads need Steam Power plus Iron and Coal (same entry). README-C3C: "Railroads will always appear under captured cities if you have Steam Power."
- **Rivers give no movement bonus in Civ3** (rivers run between tiles). Jon Shafer, in
  https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/58801-rivers (April 2002, before he joined
  Firaxis): "Rivers are placed in between spaces in Civ III, unlike in Civ II". A claim in the same thread that roads
  across rivers need Engineering is UNVERIFIED.
- **Wheeled units can't enter mountains, volcano, jungle or marsh except along a road.** PEDIA `PRTO_Catapult`:
  "catapults are wheeled so they may not enter $LINK<mountain=TERR_Mountains>, $LINK<volcano=TERR_Volcano>,
  $LINK<jungle=TERR_Jungle> or $LINK<marsh=TERR_Marsh> terrain unless along a road." BIQ `impassableByWheeled` is set
  on exactly those four terrains.
- **"All Terrain as Roads" (Explorer) and "Ignore Movement Cost" per terrain.** PEDIA `PRTO_Explorer`: "unarmed,
  non-combat units that treat all terrain as if it were a road." EDHELP *Ignore Movement Cost*: "Units with a terrain
  movement bonus will ignore movement costs of the selected terrain types." README-V 1.21f: "Updated All Terrain As
  Roads unit ability to work with water units."
- **Air units ignore terrain costs; water units pay terrain cost.** README-V 1.29f: "Changed air units to ignore
  movement costs of terrain types". README-V 1.21f: "Updated water unit movement to take into account the cost of the terrain type."
- **Naval movement bonuses:** Great Lighthouse +1 (PEDIA `BLDG_Lighthouse`: "all naval unit movement rates are
  increased by one."), Magellan's +1 (PEDIA `BLDG_Circumnavigation`: "The movement rate of all naval units is
  increased by one."), Seafaring trait (PEDIA `GCON_Strengths`: "Ships move faster and are less likely to sink in the sea or ocean.").
- **Partial moves.** A unit with 1/3 move left can still enter any tile. UNVERIFIED (the Civ3 rule for entering a tile
  with fewer moves than its cost was not found in a source).
- **Using enemy roads.** UNVERIFIED (no fetched source states whether units can use roads in enemy territory).
- **Units that win an attack advance into the tile only if it is empty.** README-V 1.21f: "Fixed bug involving extra
  movement costs if the unit can't advance after combat." The exact advance rule is UNVERIFIED.

---

## 1.10 Naval units

- **Curragh and Galley must end turns in coast; they risk sinking in sea or ocean.** PEDIA `PRTO_Galley`: "A galley
  must end each turn in a $LINK<coastal=TERR_Coast> square -- they are in danger of sinking if they end their turn in a
  $LINK<sea=TERR_Sea> or $LINK<ocean=TERR_Ocean> square. If $LINK<The Great Lighthouse=BLDG_Lighthouse> is built,
  galleys may safely end turns at sea, but never ocean squares." (`PRTO_Curragh` has the same rule.)
- **Caravel is safe in sea, at risk in ocean.** PEDIA `PRTO_Caravel`: "may safely traverse $LINK<coast=TERR_Coast>
  and $LINK<sea=TERR_Sea> squares. If it ends a turn in an $LINK<ocean=TERR_Ocean> square, it is in danger of sinking."
- **The sink flags are per unit: "Sink in Sea" and "Sink in Ocean".** EDHELP *Unit Abilities*: "Sink in Sea Unit dies
  if it ends its turn on a sea square Sink in Ocean Unit dies if it ends its turn on an ocean square". CFC-RULES:
  "can possibly sink and die without the right technology". The techs that make sea and ocean safe are given in
  PEDIA `TERR_Sea` (Astronomy) and `TERR_Ocean` (Navigation or Magnetism) for trade and travel; whether those techs
  remove the sink check for units that have the flag is UNVERIFIED.
- **Sinking chance per turn ended in danger: 50%, or 25% for Seafaring civs (75% survive)** (C3C 1.22 test).
  CFC-SINK: "If you are Seafaring, it's 75% of survival, regardless of difficulty level, terrain, ship, turns spent in
  water." / "If you are not Seafaring, it's 50%." Tested on curraghs and caravels in ocean, galleys in sea.
- **Ships do not attack land units; they only bombard them** (PTW note above: "There are no direct naval attacks on
  land units -- only bombardment."). Whether a ship can attack a land tile that holds only non-combat units is UNVERIFIED.
- **Amphibious attack: only units with the Amphibious ability attack from a ship onto land** (Marines; Viking Berserk).
  EDHELP *Unit Abilities*: "Amphibious Unit may attack from a ship onto land". PEDIA `PRTO_Marine`: "They can unload
  from a sea $LINK<transport=PRTO_Transport> into any coastal land square, including enemy cities and enemy-occupied
  squares." PEDIA `PRTO_Berserk`: "allows them to attack enemy units or structures directly from within sea-based
  transport vessels." No amphibious attack penalty or bonus was found in any source.
- **C3C: any unit can step off a ship onto a coastal tile that holds only non-combat units and capture them.**
  https://forums.civfanatics.com/threads/plucking-workers-from-the-coast.72176/ (Fanny Brice, #1): "ALL units can
  attack enemy workers along the coast right off of transports." Chieftess (moderator, #9): "It's a new feature to
  prevent the coastal blockades with units and workers, especially in MP games."
- **Landing.** Units unloaded onto land can't move again that turn (CFC artillery article:
  https://civfanatics.com/civ3/strategy/military/how-to-use-artillery-defensively/ says landed enemy units "cannot
  move until the next turn"). The exact rule is UNVERIFIED.
- **Submarines are invisible** except to subs, AEGIS Cruisers and Destroyers. PEDIA `PRTO_Submarine`: "They remain
  invisible until they attack a surface ship ... other submarines and $LINK<AEGIS Cruisers=PRTO_AEGIS_Cruiser> and
  $LINK<Destroyers=PRTO_Destroyer> can see them plainly." README-V: "You no longer declare war by accidentally passing over a hidden submarine."
- **Privateers hide their nationality** (attack without declaring war). PEDIA `PRTO_Privateer`: "allowing them to
  attack and be attacked [without revealing the nationality] of the ship."
- **Transports.** Ships are boarded by moving onto them from shore or loaded in a coastal city (PEDIA `GCON_Transport`).
  Transport units can't carry other transports (Mike Breitkreutz, Firaxis,
  https://apolyton.net/forum/civilization-series/civilization-iii/53195-some-questions-for-the-developers-on-editing-and-the-civ3mod-file:
  "No. Transport units cannot carry other transport units.").
- **What happens to carried units when a ship sinks or loses** is UNVERIFIED (presumably they die with it).

---

## 1.11 Air units

- **Air units stay in cities, airfields or carriers and fly missions within operational range.** PEDIA
  `GCON_Air_Missions`: "Air units are not moved around the map like other units. Instead, they are stationed in a city,
  and assigned missions which they execute immediately against targets within their operational range."
- **Missions (C3C):** Air Superiority, Bombing, Precision Strike, Recon, Re-Base.
  - Air Superiority. PEDIA: "The plane attempts to intercept enemy aircraft that attack targets within the inner half of its operational range."
  - Bombing. PEDIA: "The plane flies to the target and attacks, sometimes affecting city improvements, and sometimes population."
  - Precision Strike. PEDIA: "Performed by $LINK<stealth aircraft=PRTO_Stealth_Fighter>, precision strikes destroy
    city improvements only, unless there are none in which case population is affected." README-V ("Changes"): needs
    the Smart Weapons advance; "Useful only against cities, this mission targets improvements."
  - Recon. PEDIA: "The plane flies to the target and reveals the map." It can't be intercepted (CFC-RULES: "(Recon and Rebase can not be intercepted)").
  - Re-Base. PEDIA: "a distance equal to up to Six times the plane's operational range."
- **Interception chance: 50% per interceptor, 5% against stealth.** EDHELP *Chance to Intercept Enemy Air Missions*:
  "Default is 50%." *... Stealth Missions*: "Default is 5%."
  alexman (Firaxis), APO-ZOC #11: "A fighter on air superiority has a 50% chance to intercept a bomber (5% if the bomber
  is stealth)." / "If it fails to intercept, it can try to intercept the next incoming bomber." / "If it succeeds, it's
  done intercepting for that turn."
- **Flak and Mobile SAM defend only their own tile.** alexman (APO-ZOC #11, as summarized): Flak and Mobile SAMs only
  defend against aircraft attacking their own tile. SAM Battery: PEDIA `BLDG_SAM_Missile_Battery`: "[SAM Missile
  Batteries] attack enemy air units that attempt to attack the SAM site's city." Anti-air stat, BIQ: Flak 2, Mobile
  SAM 4, AEGIS Cruiser 3, Battleship 2, Destroyer/Cruiser/Carrier 1. How the anti-air stat is rolled is UNVERIFIED.
- **Air units on interception stay on it when their carrier moves** (README-V 1.21f).
- **Air and sea units in a captured city are destroyed.** CFC-CSE (Xerxes314): "air and sea units caught in a city are
  automatically destroyed. They have no combat value." README-PTW: "Fixed bug that caused air units to not be destroyed
  if they are not in a city." Which units this covers exactly is UNVERIFIED.
- **Airfields (PTW+)** work like an airport for air units; one use consumes a worker; pillaging destroys them.
  PEDIA `GCON_Airfields`: "Airfields function in the same manner that a city with an airport would for air units." /
  "Airfields can be pillaged like a terrain improvement, which also destroys the airfield."
- **Helicopters carry foot soldiers only, 3 at a time (BIQ capacity 3).** Other capacities (BIQ): Transport 6,
  Galleon 4, Carrier 4 air units, Nuclear Submarine 1 tactical nuke.
  PEDIA `PRTO_Helicopter`: "Helicopters may carry foot soldiers only -- no mechanized units allowed."
  PEDIA `GCON_Transport`: "To board a helicopter a ground unit must start its turn in a city with the chopper and use the Load command button."
- **Paratroopers airdrop** from a city with an Airport (or an airfield, PTW 1.04f). PEDIA `PRTO_Paratrooper`: "When a
  paratrooper is active in a city with an $LINK<airport=BLDG_Airport> it may perform an airdrop." README-PTW 1.04f:
  "Added ability to airdrop from airfields." Drop range is the operational range (BIQ: Paratrooper 6, Modern Paratrooper 8).
- **Air unit stats (BIQ, C3C):** Fighter A4 D2 B3 range 6; Jet Fighter A8 D4 B3 range 9; F-15 A8 D4 B6 range 9;
  Stealth Fighter A8 D6 B6 range 12; Bomber D2 B12 RoF3 range 10; Stealth Bomber D5 B18 RoF3 range 16.
  How "attack" and "defense" enter air-to-air combat is UNVERIFIED.

---

## 1.12 Nuclear weapons

- **Manhattan Project enables nukes for every civ.** PEDIA `BLDG_Manhattan_Project`: "Allows the construction of
  nuclear weapons by {all} civilizations. Nuclear Weapons can {Not} be built until this Wonder has been completed."
- **Tactical Nuke: bombard range 6, can be carried by nuclear subs.** PEDIA `PRTO_Nuke`: "They can be loaded onto
  nuclear subs and launched from the sea." / "The bombard range of a Tactical Nuke is 6 squares." Needs Aluminum and
  Uranium. BIQ: 300 shields.
- **ICBM: unlimited range, can't move from the city that built it.** PEDIA `PRTO_ICBM`: "An [ICBM] ... has an
  unlimited range" / "They cannot be moved from the city that builds them but can strike any target on the map." BIQ: 500 shields.
- **Using nukes angers the world.** PEDIA `PRTO_ICBM`: "Nuclear weapons, if used, have a tremendous negative effect upon world opinion."
- **SDI (small wonder, needs 5 SAM Batteries): 75% chance to stop incoming nukes.** PEDIA `BLDG_SDI`: "Offers a 75%
  chance of intercepting enemy ICBMs." EDHELP: "the selected Small Wonder will cause enemy missile attacks to fail 75%
  of the time." It also stops tactical nukes (https://apolyton.net/forum/civilization-series/civilization-iii/135536-strategic-missile-defense,
  MattPilot: "Infact it shoots down everything with the "nuke" flag.").
- **Blast effects: each unit in the blast has a 50% chance to die, and the city loses half its population.** Only
  source is CFC-CSE (Valant2, vanilla): "Has a 50% chance to destroy each unit caught in its blast area" and a nuke
  "reduces a city's population by 50%." Blast radius, building losses, fallout pattern: UNVERIFIED.

---

## 1.13 Capture, enslavement, pillage

- **Non-combat units caught alone are captured.** PEDIA `PRTO_Worker`: "Note that workers may be captured and used by
  rival civilizations." Artillery with no escort is captured too (thread
  https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/122803-siege-city-defense, Kef: "If they get
  captured, you're doing it wrong."). What a captured Settler becomes (Settler, or Worker(s)) is UNVERIFIED.
- **Captured workers are foreign and work at half speed** (C3C). README-C3C: "CAPTURED WORKERS: In order to
  differentiate a foreign (i.e., captured or enslaved) worker unit, it now displays different unit art than the
  standard worker." / "Note: Captured workers work at 50% the speed of regular workers."
- **Barbarians destroy the workers and settlers they catch.** https://apolyton.net/forum/civilization-series/civilization-iii/236659-unusual-barbarians
  (Quintillus): "They can't build them, and when they capture workers or settlers they are destroyed."
- **Capturing a unit counts as an act of war** (UNVERIFIED in a fetched source; one search excerpt says so).
- **The "Capture" unit action only applies to capturable special units** (kings, princesses) in game modes outside
  scope. EDHELP *Capture*: "the unit has the capability to capture units which are vulnerable to being captured (i.e.
  king units, princess units, etc.)"
- **Enslave (C3C): 1/3 chance after each win to turn the loser into a set unit type.** PEDIA `GCON_Enslavement`: "A unit
  with the "enslave" capability has a 1/3 chance of enslaving a defeated unit everytime it wins a battle. ... the unit
  is turned into another unit type (as specificed in the editor) and is immediately under the control of the victorious
  player." Stock enslavers: Privateer, English Man-O-War (makes another Man-O-War: `PRTO_Man-O-War`), Mayan Javelin
  Thrower (makes a Worker: `PRTO_Javelin_Thrower`). BIQ `slaveUnitType: Worker`. Only the attacker enslaves
  (https://forums.civfanatics.com/threads/anyone-know-exactly-how-enslavement-works.107253, Willem: "It's the unit that is doing the attacking.").
- **Pillage destroys a tile improvement.** EDHELP *Pillage*: "If checked, the selected unit may pillage terrain
  improvements." Pillaged barricades fall back to fortresses (PEDIA `GCON_Barricades`) and pillaged airfields are
  destroyed (PEDIA `GCON_Airfields`). Which improvement goes first when a tile has several (for example road, then
  mine), and the movement cost of pillaging: UNVERIFIED.

---

## 1.14 Upgrades

- **Where:** ground units in a city with a Barracks, ships with a Harbor, air units with an Airport.
  PEDIA `BLDG_Barracks`: "A city with a [Barracks] can be used to upgrade ground units." (`BLDG_Harbor`, `BLDG_Airport`
  say the same for naval and air units.) README-PTW: "Added information about upgrading units to civilopedia entries
  for Barracks, Harbor, and Airport." The new unit's strategic resources must be available in that city (Tom's
  Hardware, https://forums.tomshardware.com/threads/upgrades.167686/, "The Stare": "You must have any required
  strategic resources in that citys' resource display.").
- **Cost = (new shield cost - old shield cost) x multiplier gold. Multiplier 3 in C3C, 2 in vanilla/PTW.**
  EDHELP *Unit Upgrade Cost*: "if the base unit costs 20 shields and its upgrade costs 40 shields, the difference is 20
  shields. Under this example, then, the exact cost for the upgrade would be 60 shields (20 x 3 = 60)." BIQ
  `upgradeCostPerShield 3`. https://forums.civfanatics.com/threads/upgrade-cost.94826 (Ginger_Ale): "times 2 (in
  Vanilla civ and PtW) and times 3 in Conquests."
- **Leonardo's Workshop halves the cost.** PEDIA `BLDG_Inventor's_Workshop` (all three versions): "Obsolete military
  units can be upgraded at half price."
- **Experience is kept, except elite drops to veteran** (community). https://forums.civfanatics.com/threads/upgrade-or-disband.231677
  (Tone, #13): "elites are dowgraded to vets but all others hold their status."
- **Upgrade All (Shift-U) upgrades every unit of the active unit's type; fortified units stay fortified.** README-V:
  "Upgrade All: To use the Upgrade All order (SHIFT-U) you must activate a unit. All units of this type will then be
  upgraded." README-V 1.21f: "Upgrade all now leaves fortified units fortified."
- **Unique units are in the normal upgrade chain** (README-V 1.17f: "Civ-specific units are now part of the regular upgrade chain."; README-V 1.21f: "War chariots now upgrade to knights.").
- **An upgraded unit heals to full / keeps damage / keeps its moves:** UNVERIFIED.

---

## 1.15 AI and difficulty

- **Difficulty does not change combat odds** except through the barbarian bonus. CFC-CSE (Valant2): the difficulty
  level "has NO effect on attack/defense calculations." No AI attack or defense bonus field exists in the editor's
  difficulty tab per CFC-RULES and EDHELP.
- **The AI gets free units, not combat bonuses.** Thread 37490 (Pyrkaige, editor table): "Number of extra defensive
  units: 0 0 0 2 4 8" and "Number of extra offensive units: 0 0 0 1 2 4" (Chieftain..Deity), and "At Monarch and above,
  the AI civs get extra offensive and defensive units when they start the game."
- **AI barbarian bonus.** TheNiceOne (thread 37490): "the AI has still a 200% bonus against barbarians even though the
  human player loses its entire bonus on deity." This conflicts with EDHELP's "any player (human or AI)" (see Disputed).

---

## Disputed

1. **Stacking of defense bonuses: additive vs multiplicative.** EDHELP calls the fortify, river and fortress bonuses
   "cumulative" and Valant2 (CFC-CSE) says "Bonus percentages add". vulture
   (https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/64407-civ-3-combat-calculations-with-retreat-odds):
   "I believe the factors combine together as I've done above, rather than adding together to give a 70% increase".
   The manual (p. 110, via royfurr in thread 7886) says terrain is figured "BEFORE THE CITY SIZE AND WALLS TAKE
   EFFECT", which some read as compounding. Additive has the Firaxis help text behind it.
2. **Fortify inside walls or a fortress.** EDHELP says cumulative. https://forums.civfanatics.com/threads/defense-bonuses-question.21715
   (search excerpt): "the wall bonus supercedes the fortification bonus" and "in a fortress, there is no point in
   Fortifying." A related claim is that units in cities count as fortified automatically. Neither claim was confirmed.
3. **Promotion against barbarians.** Theoden (thread 115547): promotion odds are halved against barbarians. Catt (thread
   80836): against barbarians "the promotion odds do not apply" (read as: no promotion).
4. **Army cost.** BIQ (C3C): 400 shields. CivFanatics FAQ v3.0: "200 shields". Probably a version difference (vanilla
   vs C3C); vanilla BIQ was not checked.
5. **Do armies retreat?** https://forums.civfanatics.com/threads/are-armies-fast-units.84239/ (Longasc): "No, Armies
   NEVER retreat even if they have increased movement." Chieftess: "Armies DO retreat, atleast in C3C." README-V says an
   army inherits "mobility" only when all its units have it.
6. **Fast defender retreat.** boogaboo (thread 100878) says the retreat bonus works on defense too. Offa (thread 104978)
   says "Zero chance if defender has 1 HP, or fast defender", which may mean "against a fast defender".
7. **Retreat formula.** Offa's `att / (def + 50)` vs BomberEscort's ranges, which don't fit that formula exactly. The
   game's code is not known.
8. **Building and citizen bombard defense: 16 or 4.** README-V 1.17f and BIQ say 16. EDHELP still says "Default is 4"
   (likely text left over from before 1.17f).
9. **Barbarian bonus for the AI.** EDHELP: the bonus applies to "any player (human or AI)" at the game's difficulty.
   TheNiceOne (thread 37490): the AI always uses the Regent value (200%).
10. **Leonardo's Workshop.** Civ3's Civilopedia says it halves upgrade costs (all versions). A CFC post (Randy, thread
    94826) says "Militaristic Halves the upgrade cost of units", and some posters describe free upgrades (Civ2-style).
    Trust the Civilopedia; the militaristic claim is unconfirmed.
11. **ZOC hit chance.** Harovan (APO-ZOC): the shot always fires and works like a normal attack. Willem: "more like 30%".
    Flintlock's reverse-engineered `1 - 16D/(16D + A)` (thread 682034) is the most specific.
12. **Seafaring and sinking.** CFC-SINK found survival depends only on the trait (50% vs 75%). Older posts tie it to
    sea vs ocean squares; the unit's Sink in Sea / Sink in Ocean flags decide *where* it can sink, the trait decides how often.

## UNVERIFIED / open questions

- The per-round formula A/(A+D) and 1 HP per round are community-derived. No Firaxis statement was found. Rounding of
  modified strengths is unknown.
- Which unit defends a stack, and how ties are broken.
- Exact promotion chances (no BIQ field). The 1/2, 1/4, 1/8 table comes from one forum post; Catt's 1/8 vs 1/4
  vet-to-elite figure is attributed second-hand to Soren Johnson.
- Leader rules: the 1/16, 1/12 and half-on-defense figures are community numbers. Whether a unit marked "*" can make a
  second leader. How "one leader at a time" fits with "more than one leader in one game turn".
- Whether the retreat roll uses base movement or moves left to decide "fast", and whether a retreating attacker keeps
  its remaining moves.
- Whether defensive bombard can kill with a lethal unit, and how Walls' bombard defense 8 combines with the base 16.
- Size of the "Bombardment in Ports" vulnerability.
- Which units can fortify; whether units in cities get the fortify bonus automatically.
- Movement: entering a tile with fewer moves left than its cost; using enemy roads and railroads; river crossing costs
  (none found); whether roads over rivers need a tech.
- Naval: whether Astronomy/Navigation/Magnetism remove the sink check for units flagged Sink in Sea / Sink in Ocean;
  what happens to cargo when a transport dies; whether ships can attack a coastal tile holding only non-combat units;
  whether units that land from ships lose their remaining moves.
- Air: how attack, defense and anti-air values are rolled in interceptions and against Flak/SAM; whether a stealth
  fighter can intercept (MoonWolf, thread 86619: "stealth fighters haven't the ability to shoot down incoming bombers/fighters").
- Nukes: blast radius, the 50% unit kill and 50% population loss (single vanilla-era source), building damage, fallout,
  launch restrictions.
- Capture: what a captured Settler becomes; whether capturing counts as declaring war; whether a Leader caught alone is
  captured or killed.
- Pillage: order of improvements removed and movement cost.
- Upgrades: does an upgraded unit keep damage and moves; is the elite-to-veteran drop real.
- Town walls giving ZOC (civ-wiki.de claim only).
- Web research stopped when the session's web-search budget ran out. CivFanatics forum threads were also
  rate-limited (HTTP 429), so several threads found by search were never opened: "city defense discussion"
  (689330), "Fortress vs. Fortifications" (287626), "Defensive artillery in city" (329600). They may settle items above.
