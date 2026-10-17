"""Builds the remastered stills of foot units (runs in the ArtGen venv):

    python ArtGen/units3d/build.py Warrior Spearman          # render + compose
    python ArtGen/units3d/build.py Warrior --install          # also into C7/ModernArt
    python ArtGen/units3d/build.py Warrior --samples 64
    python ArtGen/units3d/build.py Worker --anim              # also its animated actions (anim.py)

For each unit: Blender renders the 8 directions (render_unit.py; retried, as
Cycles with cloth simulation crashes now and then), compose.py writes the
one-column sheets and metadata to ArtGen/_previews/units3d/sheets/<Unit>/,
and --install copies them over the unit's folder in C7/ModernArt/Art/Units/
(the old animated sheets of that unit are replaced; its thumbnail is kept).
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, '..', '..'))
PREVIEWS = os.path.join(REPO, 'ArtGen', '_previews', 'units3d')
UNITS_ART = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Units')

sys.path.insert(0, HERE)
import compose  # noqa: E402
import remaster  # noqa: E402


def blender():
    for path in (os.environ.get('BLENDER'), shutil.which('blender'),
                 os.path.expanduser('~/Applications/Blender.app/Contents/MacOS/Blender'),
                 '/Applications/Blender.app/Contents/MacOS/Blender'):
        if path and os.path.exists(path):
            return path
    raise SystemExit('Blender not found: set BLENDER')


def render(unit, out, samples, tries=3):
    for attempt in range(1, tries + 1):
        shutil.rmtree(out, ignore_errors=True)
        cmd = [blender(), '-b', '--factory-startup', '--python-exit-code', '1', '--python',
               os.path.join(HERE, 'render_unit.py'), '--', unit, out, '--samples', str(samples)]
        log = subprocess.run(cmd, capture_output=True, text=True)
        if all(os.path.exists(os.path.join(out, f'{p}_{r:04d}.png')) for p in ('unit', 'tint', 'shadow') for r in range(8)):
            return
        print(f'  {unit}: render attempt {attempt} failed:', log.stdout[-400:], log.stderr[-400:], flush=True)
    raise SystemExit(f'{unit}: rendering failed {tries} times')


def render_anim(unit, out, samples, tries=3, only=None):
    """render_anim.py's frames of every animated action of the unit (anim.ACTIONS), retried;
    `only` (game actions, e.g. 'FORTIFY'): just those flics, keeping the others' frames."""
    for attempt in range(1, tries + 1):
        if not only:
            shutil.rmtree(out, ignore_errors=True)
        cmd = [blender(), '-b', '--factory-startup', '--python-exit-code', '1', '--python',
               os.path.join(HERE, 'render_anim.py'), '--', unit, out, '--samples', str(samples)]
        if only:
            cmd += ['--only', only]
        log = subprocess.run(cmd, capture_output=True, text=True)
        if log.returncode == 0 and os.path.isdir(out):
            return
        print(f'  {unit}: animation render attempt {attempt} failed:', log.stdout[-400:], log.stderr[-400:], flush=True)
    raise SystemExit(f'{unit}: rendering the animations failed {tries} times')


def build(unit, samples=128, install=False, anim=False, only=None):
    out = os.path.join(PREVIEWS, 'passes', unit)
    if not (only and os.path.exists(os.path.join(out, 'unit_0007.png'))):   # --actions: keep the still as rendered
        render(unit, out, samples)
    anim_dir = None
    if anim:
        anim_dir = os.path.join(PREVIEWS, 'passes', unit + '_anim')
        render_anim(unit, anim_dir, samples, only=only)
    cal = json.load(open(os.path.join(HERE, 'calibration', f'{unit}.json')))
    entry = remaster.REMASTER.get(unit, {})
    sheets = os.path.join(PREVIEWS, 'sheets', unit)
    shutil.rmtree(sheets, ignore_errors=True)
    meta, grown = compose.write_unit(unit, out, cal, sheets, desat=entry.get('desat', 0.85), anim_dir=anim_dir)
    print(f'  {unit}: {len(meta["flics"])} flics, grown {grown}', flush=True)
    if install:
        dst = os.path.join(UNITS_ART, unit)
        os.makedirs(dst, exist_ok=True)
        keep = {f'{unit}_32.png'}
        for name in os.listdir(dst):
            if name not in keep and (name.endswith('.png') or name.endswith('.json')):
                os.remove(os.path.join(dst, name))
        for name in os.listdir(sheets):
            shutil.copy2(os.path.join(sheets, name), dst)
        print(f'  {unit}: installed into {os.path.relpath(dst, REPO)}', flush=True)


def main(argv):
    install = '--install' in argv
    anim = '--anim' in argv
    samples = 128
    if '--samples' in argv:
        samples = int(argv[argv.index('--samples') + 1])
    only = argv[argv.index('--actions') + 1] if '--actions' in argv else None   # e.g. --actions FORTIFY
    units = [a for a in argv if not a.startswith('--') and not a.isdigit() and a != only]
    subprocess.run([sys.executable, os.path.join(HERE, 'catalog_json.py')], check=True)
    for unit in units:
        build(unit, samples, install, anim, only)


if __name__ == '__main__':
    main(sys.argv[1:])
