"""The remaster's creative changes to the old catalog entries (shared by the
Blender side and the venv side; plain data).

The catalog says what each Civ3 original shows; these entries push a unit
toward the owner's direction ("less AI generated, more badass ... use the
original Civ3 models as a baseline, but be creative"): heavier builds, a
pelt, a different weapon size, a stance. Keys are merged over the catalog
entry (dicts merged one level deep).
"""

REMASTER = {
    'Warrior': {
        'macro': {'muscle': 1.0, 'weight': 0.9, 'height': 0.7, 'proportions': 0.6},
        'colors': {'skin': '#b07a4c', 'hair': '#5a3e26', 'wraps': '#5a3e28', 'wraps_band': '#2a1a10',
                   'wood': '#5a3e28', 'stone': '#9a8a74', 'leather': '#4a3020', 'belt': '#3a2618'},
        'hair_asset': None,
        'mane': 0.26,
        'pelt_cape': ('#7a6448', '#4a3a28'),
        'grime': 0.45,
        'props': {'stone_axe': ('war_club', {'length': 0.62, 'knob': 0.075})},
    },
}


def apply(name, entry):
    out = dict(entry)
    for k, v in REMASTER.get(name, {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            merged = dict(out[k])
            merged.update(v)
            out[k] = merged
        else:
            out[k] = v
    return out
