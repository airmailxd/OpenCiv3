# Civ colors in Civ3, and the proposed engine change

**Source:** these notes were read from the files of Civ3 Complete, not written from memory:
- `Art/Units/Palettes/ntp00..31.pcx` and `otp00..16.pcx`;
- `Conquests/conquests.biq`, decompressed with blast and read at the RACE section;
- `Art/Terrain/Territory.pcx`;
- the strings in `Conquests/Civ3Conquests.exe`.

The colors themselves are in `civ_colors.json`, which is small.

## 1. Palettes: one civ color per ntp file, 70 civ-dependent entries

**The files:**
- **ntp files:** each `ntpNN.pcx` is a 1x1 image whose 256-color palette is the point. Its single pixel is index 7.
- **How Conquests loads them:** it builds the path from `Art\Units\Palettes\ntp` plus the two-digit index plus `.pcx` (the strings in the exe).
- **otp files:** no `otp` string appears in the Conquests exe. They have a different layout (a 17-entry ramp at 10-26 and a yellow-to-civ blend at 28-35), and look like a leftover from vanilla Civ3. Conquests doesn't use them.

**What varies between the 32 ntp files:** only entries 0-69. The rest of the palette is identical. Within 0-69, three regions vary:

| Entries | What they are | Count |
|---|---|---|
| 0-15 | **the civ ramp**: 16 shades, light to dark | 16 |
| 16-62, even | **tinted copies of fixed colors**: entry k is the civ-tinted version of the color the flic palette holds at k | 24 |
| 16-63, odd | fixed colors (greys, skin, wood). The same in every file within ±2 | 0 |
| 64-69 | **flat civ colors for the map and UI**: 64 = the pure civ color (same as ramp entry 7), 65 = a darker one, 66-67 = 64 again, 68 = a variant, 69 = darkest or contrast | 6 |

**How unit pixels use these entries:**
- This is exactly the flic split C7 already uses (`PCXToGodot`): index < 16, or even and < 64, is the tint layer.
- A unit pixel with index i is drawn with the civ palette's entry i:
  - for i < 16, a shade of the ramp;
  - for even 16-62, the civ-tinted version of that specific skin, wood or cloth color, as in `ntp00` (ntp00 is the untinted grey and natural-color reference).

**Is there a second color?** No. There is no second ramp in a palette. Each ntp file is one color. A "secondary color" exists only as a second palette index per civ in the BIQ (section 3).

## 2. The ramp is a table, not a formula

**The light half (0-6)** runs from a pale tint of the color to the full color. The tint is often hue-shifted, not white:
- red ntp01 goes from #ff9c8a (salmon) to #e70c0c;
- blue ntp06 goes from #99dcf6 (sky) to #3964fc.

**The dark half (7-15)** darkens the color, but not by one scalar:
- red keeps G and B constant while R falls to 0.51;
- blue loses R first (0.73, then 0.33, then 0) and B last (0.88, then 0.71, then 0.57), so it gets more saturated as it darkens;
- green (ntp04) ends at 0.43, 0.48, 0.43.

**The tinted entries (16-62)** keep the value of the ntp00 base color with the civ hue. An example: ntp00's #dddddd becomes #db4a4a in ntp01.

**Conclusion:** a single HSV or multiply formula cannot reproduce the ramp, so **use the table**. The current flat multiply (`UnitTint.gdshader`: flat color × white-shaded tint) gives:
- the wrong hue at the light end (white tints where Civ3 has salmon or sky);
- too little saturation shift at the dark end;
- grey instead of tinted skin, wood and cloth in the 16-62 entries.

## 3. Assignment: default color, then unique color

- **The BIQ fields:** each RACE has `DefaultColor` and `UniqueColor`, both ntp indices 0-31. These are the editor's "Default color" and "Unique color". The Conquests values, by race number:
  - 0 Barbarians 0/0, 1 Rome 1/1, 2 Egypt 3/3, 3 Greece 10/10, 4 Babylon 1/13, 5 Germany 6/6, 6 Russia 9/9, 7 China 5/15;
  - 8 America 5/5, 9 Japan 4/11, 10 France 7/7, 11 India 8/12, 12 Persia 10/14, 13 Aztecs 4/4, 14 Zulu 3/16, 15 Iroquois 8/8;
  - 16 England 2/2, 17 Mongols 3/26, 18 Spain 5/20, 19 Scandinavia 8/31, 20 Ottomans 2/18, 21 Celts 4/25, 22 Arabia 7/30, 23 Carthage 9/11;
  - 24 Korea 6/19, 25 Sumeria 5/20, 26 Hittites 14/27, 27 Netherlands 2/18, 28 Portugal 8/17, 29 Byzantines 11/1, 30 Inca 7/8, 31 Maya 6/5.
- **Defaults are shared on purpose.** For example, 5 = China, America, Spain and Sumeria. The unique colors are the escape when two civs in the same game share a default. Several unique colors equal the default (Rome, Egypt, the Aztecs), so those civs have no escape.
- **The scenario LEAD section** can override a player's color (`LEAD.Color`; ImportCiv3 already reads it).
- **What the files don't say:** what Civ3 does when both of a civ's colors are taken. The exe has no readable rule, and I haven't verified it in a running game. C7's current rule is: default, then unique, then a duplicate check, then a seeded random free index. That's a reasonable stand-in. If you want Civ3-exact behavior, someone should test it in Civ3 with clashing civs, for example Spain plus Sumeria plus China.

## 4. Other places the color shows

- **Borders:** `Art/Terrain/Territory.pcx` paints the border lines only with palette indices 64 and 65, plus 1 for the fill. So borders use the civ palette's **flat color (64) and its darker shade (65)**, not the ramp.
- **City labels, the minimap and the scoreboard:** these are drawn by code, not with PCX palettes, so I can't read them from the files. Using flat entry 64, or 7, the same color, is consistent with the borders. C7's `LoadIntensifiedColor` for the minimap stands in for it.

## Proposed engine change (keep it small)

1. **A civ palette texture:** a 70 x 32 RGBA image (one row per ntp file, columns 0-69), built at load from the ntp palettes, or from `civ_colors.json` in standalone mode.
2. **Flic units (Civ3 art):**
   - Have PCXToGodot write the tint layer's palette index (0-63) into a channel, for example R = index / 63, instead of the ntp00 white shade.
   - `UnitTint.gdshader` then samples the civ row at that column. This matches Civ3 pixel for pixel, including the tinted skin and wood (16-62).
3. **Remastered units (rendered):**
   - These have a continuous grey tint AOV, not indices.
   - Map its luminance onto the civ's 16-entry ramp: column = (1 - L) × 15, interpolated. They then get Civ3's hue-shifted light end and saturated dark end, not a flat multiply.
   - If the shader can tell which kind of unit it has, it can use a separate uniform or texture; otherwise use one flag per sprite.
4. **Borders:** use row[64] for the line and row[65] for its dark edge.
5. **Assignment:** keep default, then unique (already right). A Civ3-verified rule for the case where both are taken can follow.

The Mac can implement the engine side. On the PC I can produce the 70x32 LUT PNG from the palettes if that helps; tell me the path and format.
