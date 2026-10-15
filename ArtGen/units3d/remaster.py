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
        'colors': {'skin': '#b07a4c', 'hair': '#3a2616', 'wraps': '#4a3020', 'wraps_band': '#22160c',
                   'wood': '#6a4a2e', 'stone': '#b8a888', 'leather': '#4a3020', 'belt': '#3a2618'},
        'upper_bulk': 1.2,
        'hair_asset': None,
        'mane': 0.26,
        'pelt_cape': ('#8a7050', '#5a4430'),
        'grime': 0.45,
        'props': {'stone_axe': ('war_club', {'length': 0.62, 'knob': 0.075, 'scale': 1.35})},
    },
    'Spearman': {
        'macro': {'muscle': 0.95, 'weight': 0.7, 'height': 0.75, 'proportions': 0.7},
        'colors': {'skin': '#b07a4c', 'helmet': '#8a6a3a', 'crest': '#3a2a1c', 'bottom': '#4e3a26',
                   'shield_face': '#a8926a', 'shield_rim': '#4a3020'},
        'grime': 0.4,
        'beard': True,
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
