"""Builds the numpy-foot units' stills (runs in the ArtGen venv), as build.py does for the catalog's foot units:

    python ArtGen/units3d/npf_build.py "RPG Trooper" Insurgent          # render + compose
    python ArtGen/units3d/npf_build.py Insurgent --tag=_v2              # a variant: passes_v2/, sheets_v2/
    python ArtGen/units3d/npf_build.py Insurgent --no-render            # compose the last render

Renders with npf_render.py (retried), composes with compose.write_unit into
ArtGen/_previews/units3d/sheets<tag>/<Unit>/. Nothing is installed (the user sees the family first).
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build  # noqa: E402  (blender(), PREVIEWS)
import compose  # noqa: E402
import npf_units  # noqa: E402


def render(unit, out, samples, tries=3):
    for attempt in range(1, tries + 1):
        shutil.rmtree(out, ignore_errors=True)
        cmd = [build.blender(), '-b', '--factory-startup', '--python-exit-code', '1', '--python',
               os.path.join(HERE, 'npf_render.py'), '--', unit, out, '--samples', str(samples)]
        log = subprocess.run(cmd, capture_output=True, text=True)
        if all(os.path.exists(os.path.join(out, f'{p}_{r:04d}.png')) for p in ('unit', 'tint', 'shadow') for r in range(8)):
            return
        print(f'  {unit}: render attempt {attempt} failed:', log.stdout[-1500:], log.stderr[-800:], flush=True)
    raise SystemExit(f'{unit}: rendering failed {tries} times')


def main(argv):
    tag = next((a.split('=', 1)[1] for a in argv if a.startswith('--tag=')), '')
    samples = int(next((a.split('=', 1)[1] for a in argv if a.startswith('--samples=')), 128))
    units = [a for a in argv if not a.startswith('--')]
    for unit in units:
        out = os.path.join(build.PREVIEWS, 'passes' + tag, unit)
        if '--no-render' not in argv:
            render(unit, out, samples)
        cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json'), encoding='utf-8'))
        sheets = os.path.join(build.PREVIEWS, 'sheets' + tag, unit)
        shutil.rmtree(sheets, ignore_errors=True)
        meta, grown = compose.write_unit(unit, out, cal, sheets, desat=npf_units.ENTRIES[unit].get('desat', 0.85))
        print(f'  RENDERED {unit}: {len(meta["flics"])} flics, grown {grown} -> {sheets}', flush=True)


if __name__ == '__main__':
    main(sys.argv[1:])
