"""Civilopedia icons of the Modern mode's own units (C7/Lua/modern).

Modern's new units share their Civilopedia entries with the stock units whose
art names the mod reused (the Super Trooper is PRTO_Samurai), so they can't
have an icon by entry. Each has its own art name instead (ArtGen/units,
civ3art.MOD_UNIT_ORIGINALS), and C7Engine/GameSetup.Modern.cs points the
unit's Civilopedia art at the icon made here for that art name:
Art/Civilopedia/Icons/Units/<art name, as a file stem>_large.png and _small.png.

    python ArtGen/civilopedia_icons/units_modern.py

build.py runs it with the Units section.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

# Art name -> the backdrop it is shown on. The Iron Dome is an air unit in
# the rules only so that it can't move; it stands on the ground.
UNITS = {
    'Super Trooper': 'Land', 'Laser Tank': 'Land', 'Bradley': 'Land', 'Convoy': 'Land',
    'Iron Dome': 'Land', 'Battalion': 'Land', 'Combat Engineer': 'Land', 'RPG Trooper': 'Land',
    'Insurgent': 'Land', 'Rebel': 'Land', 'Militant': 'Land', 'Militia': 'Land', 'Skirmisher': 'Land',
    'Pirate': 'Sea', 'Troop Ship': 'Sea', 'Fighter Bomber': 'Air',
}


def stem(art):
    return C.file_stem(art)


def main():
    import units
    items = []
    for art, category in UNITS.items():
        proto = {'civilopediaEntry': 'PRTO_' + stem(art), 'categories': [category],
                 'art': {'mainArt': {'defaultName': art}}}
        large, small = units.make(proto)
        print(*C.save_icon('Units', stem(art), large, small))
        items.append((stem(art), large, small))
    print('preview:', C.contact_sheet('Units_modern', items, cols=8))


if __name__ == '__main__':
    main()
