"""The League modes' own Civilopedia entries in standalone mode
(C7/Lua/standalone/league_art.json; C7Engine/GameSetup.LeagueArt.cs gives
the items these keys as their rules load).

The League's mods (C7/Lua/qc, modern, future, mdj) give many of their items
a stock item's Civilopedia entry: Modern's "Wartime (Draft)" is
TECH_Chivalry. In standalone mode they have keys of their own instead
(TECH_MODERN_Wartime), and PediaIcons.txt and icon_files.lua list those
keys too, each with the picture league_art.json names for it ("art", a file
stem of its section). Pointers to stock keys (Modern's Currency is
TECH_Currency again) need nothing here: the stock key is listed already.

A picture still to be drawn ("status": "new") that isn't there yet is
listed with the picture the item borrowed before (that of the entry in
"from"), so nothing shows worse until it lands; EngineTests' LeagueArtTest
lists those items as pending.

The league's new pictures are drawn by their own modules (adv_league.py,
bld_league.py, ...), not by build.py: these entries are only indexed.
"""
import json
import os

import common as C

PATH = os.path.join(C.REPO, 'C7', 'Lua', 'standalone', 'league_art.json')

SECTIONS = {'techs': 'Advances', 'buildings': 'Buildings', 'governments': 'Governments',
            'resources': 'Resources', 'civs': 'Races'}


def load():
    if not os.path.exists(PATH):
        return {'modes': {}}
    with open(PATH, encoding='utf-8') as f:
        return json.load(f)


def stem_of(key):
    return C.file_stem(key.split('_', 1)[1])


def has_icon(section, stem):
    return os.path.exists(os.path.join(C.ICONS, section, f'{stem}_large.png'))


def entries(rules):
    """(section, key, stem, data) for the league's own keys that the bundled
    rules haven't got, stem being the file the index lists for it: its own
    picture, or until that is drawn the one it borrowed. data has the
    item's name, the stock entry it borrowed and, for a building, the row
    of the stock building's icon."""
    stock = {}
    for b in rules['buildings']:
        stock[b['civilopediaEntry']] = b
    have = set()
    for s, rule in (('techs', 'techs'), ('buildings', 'buildings'), ('governments', 'governments'),
                    ('resources', 'resources'), ('civs', 'civilizations')):
        for e in rules.get(rule, []):
            if e.get('civilopediaEntry'):
                have.add(e['civilopediaEntry'])
    out = []
    for mode, sections in load()['modes'].items():
        for s, section in SECTIONS.items():
            for name, e in sections.get(s, {}).items():
                key = e.get('key')
                if not key or key in have:
                    continue
                have.add(key)
                stem = e.get('art') or stem_of(key)
                if not has_icon(section, stem):
                    stem = stem_of(e['from'])
                data = {'name': name, 'civilopediaEntry': key, 'from': e['from'], 'league': True}
                if s == 'buildings':
                    data['iconRowIndex'] = stock.get(e['from'], {}).get('iconRowIndex', 0)
                out.append((section, key, stem, data))
    return out
