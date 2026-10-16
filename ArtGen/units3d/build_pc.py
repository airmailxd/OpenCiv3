"""Builds the remastered stills of the PC's units: vehicles, ships, riders,
chariots (runs in the ArtGen venv), as build.py does for the foot units.

    python ArtGen/units3d/build_pc.py Catapult Galley Horseman      # render + compose
    python ArtGen/units3d/build_pc.py Catapult --no-render           # compose the last render
    python ArtGen/units3d/build_pc.py Catapult --install             # also into C7/ModernArt

For each unit: render_vehicles.py renders the 8 directions into
ArtGen/_previews/units3d/<Unit>/ (crisp: no denoiser, as reviewed), then
compose.write_unit writes the one-column sheets and metadata to
ArtGen/_previews/units3d/sheets/<Unit>/; --install copies them over the
unit's folder in C7/ModernArt/Art/Units/ (the unit's old sheets are
replaced, its thumbnail kept). Install only units stzur has approved.
"""
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build  # noqa: E402  (the foot units' builder: blender(), PREVIEWS, UNITS_ART, its install step)
import compose  # noqa: E402

DESAT = 0.9   # the vehicles were reviewed at full color; a touch calmer, as the foot units' 0.85


def render(units):
    env = dict(os.environ, CRISP='1')
    cmd = [build.blender(), '-b', '--factory-startup', '--python-exit-code', '1', '--python',
           os.path.join(HERE, 'render_vehicles.py'), '--', *units]
    log = subprocess.run(cmd, capture_output=True, text=True, env=env)
    for line in log.stdout.splitlines():
        if line.startswith('RENDERED'):
            print(' ', line, flush=True)
    if log.returncode:
        raise SystemExit(f'rendering failed:\n{log.stdout[-1500:]}\n{log.stderr[-800:]}')


def compose_unit(unit, install=False):
    out = os.path.join(build.PREVIEWS, unit)
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json'), encoding='utf-8'))
    sheets = os.path.join(build.PREVIEWS, 'sheets', unit)
    import shutil
    shutil.rmtree(sheets, ignore_errors=True)
    meta, grown = compose.write_unit(unit, out, cal, sheets, desat=DESAT)
    print(f'  {unit}: {len(meta["flics"])} flics, grown {grown} -> {os.path.relpath(sheets, build.REPO)}', flush=True)
    if install:
        dst = os.path.join(build.UNITS_ART, unit)
        os.makedirs(dst, exist_ok=True)
        keep = {f'{unit}_32.png'}
        for name in os.listdir(dst):
            if name not in keep and (name.endswith('.png') or name.endswith('.json')):
                os.remove(os.path.join(dst, name))
        for name in os.listdir(sheets):
            shutil.copy2(os.path.join(sheets, name), dst)
        print(f'  {unit}: installed into {os.path.relpath(dst, build.REPO)}', flush=True)


def main(argv):
    units = [a for a in argv if not a.startswith('--')]
    subprocess.run([sys.executable, os.path.join(HERE, 'catalog_json.py')], check=True)
    if '--no-render' not in argv:
        render(units)
    for unit in units:
        compose_unit(unit, install='--install' in argv)


if __name__ == '__main__':
    main(sys.argv[1:])
