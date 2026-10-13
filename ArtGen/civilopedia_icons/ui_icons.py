"""The icons of advances and buildings in the game's screens for standalone
mode, made from their Civilopedia icons (build.py), so every screen shows the
same picture of an advance or a building:

    C7/ModernArt/Art/Tech Chooser/Advances/<advance>.png         the science advisor's tech tree
    C7/ModernArt/Art/city screen/Buildings/<building>_small.png  production menus, the tech tree
    C7/ModernArt/Art/city screen/Buildings/<building>_large.png  the city screen's production box

and C7/Lua/standalone/icon_files.lua, which tells the standalone textures
(C7/Lua/standalone/textures.lua) the file of each entry.

    python ArtGen/civilopedia_icons/ui_icons.py

Civ3 shows these icons from its own sprite sheets, and the standalone asset
pack has only placeholders in their place: an advance showed the word "Tech"
and a building "Bldg" (owner's report, 2026-10-12).

The screens show them at Civ3's sizes: 32x32 small icons and 50x40 large
building icons. Like all of ModernArt the files have twice those pixels, for a
UI scaled up (DisplayScale), as the units' thumbnails next to them in the
production menus do. Each is the Civilopedia's large (128x128) icon made
smaller; the large building icon is square, centered on a 50x40 transparent
picture.

build.py runs this after making the Civilopedia's icons.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

SCALE = 2
ADVANCES_REL = 'Art/Tech Chooser/Advances'
BUILDINGS_REL = 'Art/city screen/Buildings'
LUA = os.path.join(C.REPO, 'C7', 'Lua', 'standalone', 'icon_files.lua')
SMALL = (32, 32)
LARGE_BUILDING = (50, 40)


def load(section, stem):
    path = os.path.join(C.ICONS, section, f'{stem}_large.png')
    return C.to_float(Image.open(path).convert('RGBA'))


def icon(large, size):
    """The icon shown at size (w, h), with twice its pixels, from the
    Civilopedia's large icon: as big a square as fits, centered."""
    w, h = size[0] * SCALE, size[1] * SCALE
    n = min(w, h)
    pic = C.sharpen(C.resize(large, (n, n)), 0.3, 0.6)
    out = np.zeros((h, w, 4), np.float32)
    x, y = (w - n) // 2, (h - n) // 2
    out[y:y + n, x:x + n] = pic
    return C.to_image(out)


def save(rel, img):
    path = os.path.join(C.MODERN, *rel.split('/'))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    return path


def lua_string(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def entries(rules):
    """The Civilopedia's entries (build.entries). Other areas have a build.py
    too, so it's loaded by its path."""
    import importlib.util
    spec = importlib.util.spec_from_file_location('civilopedia_build', os.path.join(C.HERE, 'build.py'))
    build = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(build)
    return build.entries(rules)


def main():
    rules = C.load_ruleset()
    advances, buildings = [], []
    for section, key, stem, _ in entries(rules):
        if section == 'Advances':
            save(f'{ADVANCES_REL}/{stem}.png', icon(load(section, stem), SMALL))
            advances.append((key, f'{ADVANCES_REL}/{stem}.png'))
        elif section == 'Buildings':
            large = load(section, stem)
            save(f'{BUILDINGS_REL}/{stem}_small.png', icon(large, SMALL))
            save(f'{BUILDINGS_REL}/{stem}_large.png', icon(large, LARGE_BUILDING))
            buildings.append((key, f'{BUILDINGS_REL}/{stem}_small.png', f'{BUILDINGS_REL}/{stem}_large.png'))

    lines = [
        '-- The icon files of the advances and buildings of the bundled rules in',
        '-- standalone mode, by Civilopedia entry (C7/ModernArt). Made by',
        '-- ArtGen/civilopedia_icons/ui_icons.py: don\'t edit.',
        'return {',
        '  advances = {',
    ]
    lines += [f'    [{lua_string(k)}] = {lua_string(p)},' for k, p in advances]
    lines += ['  },', '  buildings = {']
    lines += [f'    [{lua_string(k)}] = {{ small = {lua_string(s)}, large = {lua_string(l)} }},' for k, s, l in buildings]
    lines += ['  },', '}', '']
    with open(LUA, 'wb') as f:
        f.write('\n'.join(lines).encode('utf-8'))
    print(f'{len(advances)} advances, {len(buildings)} buildings; {LUA}')


if __name__ == '__main__':
    main()
