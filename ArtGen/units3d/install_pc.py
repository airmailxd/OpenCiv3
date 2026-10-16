"""Install the PC's remastered stills into C7/ModernArt (runs in the ArtGen venv).

    python ArtGen/units3d/install_pc.py              # dry run: list what would change
    python ArtGen/units3d/install_pc.py --apply      # do it (only with stzur's approval)
    python ArtGen/units3d/install_pc.py --apply --with-frozen   # also the riders and chariots

For each unit: the folder C7/ModernArt/Art/Units/<Unit>/ loses its old .png
and .json files except the <Unit>_32.png thumbnail, then gets the sheets from
ArtGen/_previews/units3d/sheets/<Unit>/ (as build.py --install does). Units
without sheets are reported and skipped. The War Elephant is never installed.
"""
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, '..', '..'))
SHEETS = os.path.join(REPO, 'ArtGen', '_previews', 'units3d', 'sheets')
UNITS_ART = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Units')

sys.path.insert(0, HERE)

PASSED = {
    'siege': ['Catapult', 'Cannon', 'Trebuchet', 'Hwacha', 'Artillery', 'Flak', 'TOW Infantry'],
    'armor': ['Tank', 'Panzer', 'Modern Armor', 'Mech Infantry', 'Mobile SAM', 'Radar Artillery'],
    'air': ['Fighter', 'Bomber', 'Helicopter', 'Jet Fighter', 'F-15', 'Stealth Fighter', 'Stealth Bomber',
            'ICBM', 'Tactical Nuke', 'Cruise Missile', 'Holy Relic'],
    'royals': ['King Japanese', 'King Korean', 'King Mayan', 'King Mongol', 'King Netherlands', 'King Ottoman',
               'King Persian', 'King Portugal', 'King Roman', 'King Russian', 'King Spanish', 'King Sumeria',
               'King Viking', 'King Zulu', 'Leader Ancient Times', 'Leader Industrial Ages', 'Leader Middle Ages',
               'Leader Modern Times', 'Princess', 'SciLeader', 'Shogun', 'The King',
               'Army Ancient Times', 'Army Industrial Ages', 'Army Modern Times', 'King Austria', 'King Aztec',
               'King Incan'],
    'footmen': ['Impi'],
}


def _from_modules():
    """Ships, soldiers and footmen batch B straight from their modules (all passed)."""
    out = {}
    try:
        import ships
        out['ships'] = sorted(ships.BUILDERS)
    except Exception:   # bpy-only module outside Blender
        pass
    for mod, key in (('remaster_soldiers', 'soldiers'),):
        try:
            m = __import__(mod)
            out[key] = sorted(getattr(m, 'REMASTER', {}))
        except Exception:
            pass
    return out


def frozen():
    try:
        import riders
        import chariots
        return sorted(set(getattr(riders, 'UNITS', [])) | set(getattr(chariots, 'UNITS', [])))
    except Exception:
        return []


def install(unit, apply):
    src = os.path.join(SHEETS, unit)
    dst = os.path.join(UNITS_ART, unit)
    if not os.path.isdir(src) or not os.listdir(src):
        return f'SKIP {unit}: no sheets'
    if not os.path.isdir(dst):
        return f'SKIP {unit}: no game folder {dst}'
    thumb = f'{unit}_32.png'
    old = [f for f in os.listdir(dst) if f.lower().endswith(('.png', '.json')) and f != thumb]
    new = os.listdir(src)
    if apply:
        for f in old:
            os.remove(os.path.join(dst, f))
        for f in new:
            shutil.copy2(os.path.join(src, f), os.path.join(dst, f))
    return f'{"DONE" if apply else "WOULD"} {unit}: -{len(old)} +{len(new)}'


def main():
    apply = '--apply' in sys.argv
    groups = dict(PASSED, **_from_modules())
    if '--with-frozen' in sys.argv:
        groups['frozen (riders, chariots)'] = [u for u in frozen() if u != 'War Elephant']
    for g, units in groups.items():
        print(f'== {g} ({len(units)})')
        for u in units:
            print('  ' + install(u, apply))


if __name__ == '__main__':
    main()
