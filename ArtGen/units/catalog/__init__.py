"""The unit catalog: which Civ3 unit folders are remade, and how.

Every module in this package defines a dict UNITS mapping a unit folder name
(as under Art/Units, e.g. 'Warrior') to an entry:

    {'archetype': 'humanoid', ...archetype parameters...}

Units without an entry are not remade (the game falls back to the original).
Each module covers one group of units (ancient.py, mounted.py, naval.py, ...)
so several people can add units without touching the same file. A unit may
only be defined once across all modules.
"""
import importlib
import os
import pkgutil


def load():
    units = {}
    owner = {}
    here = os.path.dirname(__file__)
    for m in sorted(pkgutil.iter_modules([here]), key=lambda m: m.name):
        mod = importlib.import_module(f'catalog.{m.name}')
        for name, entry in getattr(mod, 'UNITS', {}).items():
            if name.lower() in owner:
                raise ValueError(f'unit {name!r} is in both catalog/{owner[name.lower()]}.py and catalog/{m.name}.py')
            owner[name.lower()] = m.name
            units[name] = entry
    return units
