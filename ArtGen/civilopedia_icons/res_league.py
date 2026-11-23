"""Civilopedia icons of the League modes' own resources (league_art.json):
each its map cell of resources.png (ArtGen/terrain_features/resources.py,
LEAGUE_ICONS) on a terrain it is found on, as terrain.resource_icon draws
the stock ones.

    python ArtGen/civilopedia_icons/res_league.py

build.py runs it with the Resources section.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

# Stem -> (map cell, terrain, overlay).
RESOURCES = {
    'LEAGUE_Pearls': (28, 'coast', None),
    'MODERN_Beach_Resort': (29, 'grassland', None),
    'MDJ_Precious_Stones': (30, 'grassland', 'hills'),
    'MDJ_Sandbar': (31, 'coast', None),
    'MDJ_Olive_Oil': (32, 'plains', None),
}


def main():
    import terrain
    items = []
    for stem, (cell, base, overlay) in RESOURCES.items():
        key = 'GOOD_' + stem
        terrain.RESOURCE_TERRAIN[key] = (base, overlay)
        large, small = terrain.resource_icon({'civilopediaEntry': key, 'icon': cell})
        C.save_icon('Resources', stem, large, small)
        items.append((stem, large, small))
    print('preview:', C.contact_sheet('Resources_league', items, cols=5))


if __name__ == '__main__':
    main()
