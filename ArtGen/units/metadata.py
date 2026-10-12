"""Writes the description of each remade unit's animations, which the game
reads in standalone mode (no Civ3 files) in place of the unit's .INI and the
headers of its .flc files:

    C7/ModernArt/Art/Units/<unit>/<unit>.json

    python ArtGen/units/metadata.py                  # every catalogued unit
    python ArtGen/units/metadata.py Warrior Archer   # just these

The file holds, like the INI, the flic of each action, and for each flic
what the game reads from its header: the frame size (the sheet cells are
twice that), the number of directions (sheet rows) and frames, the time per
frame, and where the frame sits on the 240x240 canvas the game places
units with. A flic's sheet may have fewer columns than the flic has frames
(a still has one, a move cycle every other frame); the game then shows each
column for the frames it stands for. See C7Engine/C7GameData/UnitArtMetadata.cs.

    {
      "actions": {"DEFAULT": "WarriorDefault", "RUN": "WarriorRun", ...},
      "flics": {
        "WarriorDefault": {"size": [w, h], "directions": 8, "frames": 10,
                           "frameMs": 66, "offset": [left, top], "canvas": [240, 240]},
        ...
      }
    }

Every action of the original's INI is listed, also those whose sheets are
only made with --animations (attacks, deaths...); the game falls back on
DEFAULT for an action whose sheet isn't there.
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import flic as F  # noqa: E402  (puts ArtGen/common on the path)
import civ3art  # noqa: E402
import catalog  # noqa: E402


def flic_header(path):
    """The header values the game reads from a flic (Util.LoadFlicHeader)."""
    with open(path, 'rb') as f:
        b = f.read(128)

    def u16(o):
        return struct.unpack_from('<H', b, o)[0]

    if u16(4) != 0xAF12:
        raise ValueError(f'{path}: not a flic')
    n_frames, n_anims, per = u16(6), u16(0x60), u16(0x62)
    if n_anims == 0:
        n_anims, per = 1, n_frames
    return {
        'size': [u16(8), u16(10)],
        'directions': n_anims,
        'frames': per,
        'frameMs': struct.unpack_from('<i', b, 16)[0],
        'offset': [u16(100), u16(102)],
        'canvas': [u16(104), u16(106)],
    }


def describe(unit):
    """The metadata of one unit folder, from the original's INI and flics."""
    actions, flics = {}, {}
    for action, file_name in F.unit_flics(unit).items():
        try:
            path = civ3art.media_path(F.flic_rel(unit, file_name))
        except FileNotFoundError:
            continue
        name = os.path.splitext(file_name)[0]
        # The same flic may be spelled differently by two actions.
        name = next((n for n in flics if n.lower() == name.lower()), name)
        actions[action] = name
        if name not in flics:
            flics[name] = flic_header(path)
    if 'DEFAULT' not in actions:
        raise ValueError(f'{unit}: no DEFAULT animation')
    order = {a: i for i, a in enumerate(F.ACTIONS)}
    actions = dict(sorted(actions.items(), key=lambda kv: (order.get(kv[0], len(order)), kv[0])))
    return {'actions': actions, 'flics': dict(sorted(flics.items(), key=lambda kv: kv[0].lower()))}


def to_json(data):
    """Compact but readable: one line per action and per flic."""
    lines = ['{', '  "actions": {']
    items = list(data['actions'].items())
    for i, (a, n) in enumerate(items):
        lines.append(f'    {json.dumps(a)}: {json.dumps(n)}' + (',' if i < len(items) - 1 else ''))
    lines += ['  },', '  "flics": {']
    items = list(data['flics'].items())
    for i, (n, h) in enumerate(items):
        lines.append(f'    {json.dumps(n)}: {json.dumps(h)}' + (',' if i < len(items) - 1 else ''))
    lines += ['  }', '}', '']
    return '\n'.join(lines)


def metadata_path(unit):
    return os.path.join(civ3art.MODERN_ART, 'Art', 'Units', unit, unit + '.json')


def write(unit):
    path = metadata_path(unit)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:  # LF line endings on every platform
        f.write(to_json(describe(unit)).encode('utf-8'))
    return path


def main(argv):
    units = [a for a in argv if not a.startswith('--')] or sorted(catalog.load())
    for unit in units:
        print(write(unit))


if __name__ == '__main__':
    main(sys.argv[1:])
