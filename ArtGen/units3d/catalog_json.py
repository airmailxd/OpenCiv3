"""Writes the old unit catalog (ArtGen/units/catalog) as JSON, for the Blender
side, which can't import it (some entries need numpy and scipy):

    python ArtGen/units3d/catalog_json.py   # -> ArtGen/_previews/units3d/catalog.json

Tuples become lists; values JSON can't hold (functions, arrays) become their
repr and aren't used by the Blender side.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'units'))

import catalog  # noqa: E402

OUT = os.path.join(HERE, '..', '_previews', 'units3d', 'catalog.json')


def main():
    units = catalog.load()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w') as f:
        json.dump(units, f, indent=1, default=repr, sort_keys=True)
    print(f'{len(units)} units -> {os.path.normpath(OUT)}')


if __name__ == '__main__':
    main()
