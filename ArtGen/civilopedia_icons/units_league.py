"""Civilopedia icons of the League modes' units that have art of their own
(ArtGen/units catalog/league_*.py, civ3art.MOD_UNIT_ORIGINALS).

These units share their Civilopedia entries with stock units in the mods'
rules (QC's Teutonic Knight is PRTO_Knight), so each icon is made under
its art name, like units_modern.py does:
Art/Civilopedia/Icons/Units/<art name, as a file stem>_large.png and _small.png.

    python ArtGen/civilopedia_icons/units_league.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

# Art name -> the backdrop it is shown on.
UNITS = {
    'Roughrider': 'Land', 'Special Forces': 'Land', 'Teutonic Knight': 'Land',
    'Japanese Rifleman': 'Land', 'Hojo General': 'Land', 'Wildcat': 'Air', 'Japanese Warship': 'Sea',
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
    print('preview:', C.contact_sheet('Units_league', items, cols=8))


if __name__ == '__main__':
    main()
