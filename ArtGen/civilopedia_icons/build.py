"""Builds the Civilopedia's icons for standalone mode (no Civ3 files): a large
(128x128) and a small (32x32) icon for every entry of the bundled rules,
into C7/Civilopedia/Art/Civilopedia/Icons/<Section>/, and the index
C7/Civilopedia/Text/PediaIcons.txt that C7Engine/C7GameData/PediaIcons.cs
reads (paths relative to C7/Civilopedia).

    python ArtGen/civilopedia_icons/build.py              # everything
    python ArtGen/civilopedia_icons/build.py Units Races  # just these sections
    python ArtGen/civilopedia_icons/build.py --index      # only rewrite PediaIcons.txt
    python ArtGen/civilopedia_icons/build.py --jobs=8

Every icon is original art made from the project's own remade map art
(ArtGen units, terrain and city renderers and C7/ModernArt) or drawn here:

* Units: the unit's 3D model (ArtGen/units) rendered up close (units.py).
* Terrain, resources, worker actions: views of the remade map art
  (landscape.py, terrain.py, worker_actions.py).
* Buildings and wonders: small 3D scenes drawn with the city renderer
  (bld3d.py, improvements.py, bld_*.py); spaceship parts with kit3d.
* Advances, governments, civilizations: symbolic objects and emblems made of
  3D primitives and reliefs (kit3d.py, advances.py + adv_*.py,
  governments.py, civs.py).

The advances' and buildings' icons are also what the game's screens show
them with in standalone mode (the tech tree, production menus, the city
screen): ui_icons.py makes those files from them, after the icons are made.

Contact sheets of each section go to ArtGen/_previews/civilopedia/.
"""
import multiprocessing as mp
import os
import sys
import time

for _v in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ.setdefault(_v, '1')

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

SECTIONS = ['Units', 'Advances', 'Buildings', 'Governments', 'Terrain', 'Resources', 'WorkerActions', 'Races']


def entries(rules):
    """(section, key, stem, data) for every Civilopedia entry with an icon,
    in the order of PediaIcons.txt."""
    out = []
    for p in rules['unitPrototypes']:
        out.append(('Units', p['civilopediaEntry'], p))
    for t in rules['techs']:
        if t.get('eraCivilopediaName') == 'Hidden':
            continue
        out.append(('Advances', t['civilopediaEntry'], t))
    for b in rules['buildings']:
        out.append(('Buildings', b['civilopediaEntry'], b))
    for inflow in rules.get('inflows', []):
        if inflow['name'] == 'Wealth':
            out.append(('Buildings', 'BLDG_Wealth', {'name': 'Wealth', 'civilopediaEntry': 'BLDG_Wealth',
                                                    'iconRowIndex': inflow.get('iconRowIndex', 29)}))
    for g in rules['governments']:
        out.append(('Governments', g['civilopediaEntry'], g))
    for t in rules['terrainTypes']:
        out.append(('Terrain', 'TERR_' + t['displayName'].replace(' ', '_'), t))
    for k in ('TERR_River', 'TERR_Fresh_Water_Lake', 'TERR_Grassland_with_Shield'):
        out.append(('Terrain', k, {}))
    for r in rules['resources']:
        out.append(('Resources', r['civilopediaEntry'], r))
    for t in rules['terraForms']:
        out.append(('WorkerActions', t['civilopediaEntry'], t))
    out.append(('WorkerActions', 'TFRM_Build_Colony', {}))
    for c in rules['civilizations']:
        if c.get('isBarbarian') or not c.get('civilopediaEntry'):
            continue
        out.append(('Races', c['civilopediaEntry'], c))
    stems = set()
    res = []
    for section, key, data in out:
        stem = C.file_stem(key.split('_', 1)[1])
        assert (section, stem.lower()) not in stems, f'two entries make {section}/{stem}'
        stems.add((section, stem.lower()))
        res.append((section, key, stem, data))
    return res


def building_kind(b):
    if 'greatWonderProperties' in b:
        return 'wonder'
    if b.get('isSmallWonder'):
        return 'small_wonder'
    return 'improvement'


def make_icon(section, key, data):
    """(large, small) float RGBA arrays of an entry's icons."""
    if section == 'Units':
        import units
        return units.make(data)
    if section == 'Advances':
        import advances
        return advances.make(data)
    if section == 'Buildings':
        import improvements
        return improvements.make(data, building_kind(data))
    if section == 'Governments':
        import governments
        return governments.make(data)
    if section == 'Terrain':
        import terrain
        return terrain.terrain_icon(key)
    if section == 'Resources':
        import terrain
        return terrain.resource_icon(data)
    if section == 'WorkerActions':
        import worker_actions
        return worker_actions.icon(key)
    if section == 'Races':
        import civs
        return civs.make(data)
    raise KeyError(section)


def _job(task):
    section, key, stem, data = task
    t0 = time.time()
    try:
        large, small = make_icon(section, key, data)
        C.save_icon(section, stem, large, small)
        # keep a copy for the contact sheet
        return dict(section=section, key=key, stem=stem, large=large, small=small, seconds=time.time() - t0)
    except Exception:
        import traceback
        return dict(section=section, key=key, stem=stem, error=traceback.format_exc())


def write_index(rules):
    """C7/Civilopedia/Text/PediaIcons.txt."""
    rel = C.ICONS_REL
    lines = [
        '; PediaIcons.txt for standalone mode: the Civilopedia icon of every entry of the',
        '; bundled rules. Made by ArtGen/civilopedia_icons/build.py; paths are relative to',
        '; C7/Civilopedia.',
        ';',
        '; #TECH_<advance> is an advance\'s small icon and #TECH_<advance>_LARGE its large one.',
        '; #ICON_<entry> gives the large icon, then the small one. Buildings give a type',
        '; (SINGLE: one icon), the row of their icon in the city screen\'s art, then the',
        '; large icon and the small one.',
        '',
    ]
    by_section = {}
    for section, key, stem, data in entries(rules):
        by_section.setdefault(section, []).append((key, stem, data))
    titles = {'Units': 'UNITS', 'Advances': 'ADVANCES', 'Buildings': 'BUILDINGS, WONDERS AND SMALL WONDERS',
              'Governments': 'GOVERNMENTS', 'Terrain': 'TERRAIN', 'Resources': 'RESOURCES',
              'WorkerActions': 'WORKER ACTIONS', 'Races': 'CIVILIZATIONS'}
    for section in SECTIONS:
        lines.append(f'; ---- {titles[section]}')
        items = by_section.get(section, [])
        if section == 'Advances':
            for key, stem, _ in items:
                lines += [f'#{key}', f'{rel}/{section}/{stem}_small.png']
            for key, stem, _ in items:
                lines += [f'#{key}_LARGE', f'{rel}/{section}/{stem}_large.png']
        else:
            for key, stem, data in items:
                lines.append(f'#ICON_{key}')
                if section == 'Buildings':
                    lines += ['SINGLE', str(int(data.get('iconRowIndex', 0)))]
                lines += [f'{rel}/{section}/{stem}_large.png', f'{rel}/{section}/{stem}_small.png']
        lines.append('')
    path = os.path.join(C.PEDIA, 'Text', 'PediaIcons.txt')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(('\n'.join(lines)).encode('utf-8'))
    return path


def main(argv):
    opts = dict(a[2:].split('=', 1) if '=' in a else (a[2:], '1') for a in argv if a.startswith('--'))
    wanted = [a for a in argv if not a.startswith('--')] or SECTIONS
    rules = C.load_ruleset()
    print('index:', write_index(rules))
    if 'index' in opts:
        return
    tasks = [e for e in entries(rules) if e[0] in wanted]
    n = int(opts.get('jobs', os.cpu_count() or 4))
    print(f'{len(tasks)} icons, {n} processes', flush=True)
    t0 = time.time()
    results = []
    with mp.Pool(n) as pool:
        for r in pool.imap(_job, tasks, chunksize=1):
            if 'error' in r:
                print(f"  FAILED {r['key']}:\n{r['error']}", flush=True)
            results.append(r)
    failed = [r for r in results if 'error' in r]
    for section in wanted:
        items = [(r['stem'], r['large'], r['small']) for r in results if r['section'] == section and 'error' not in r]
        for i in range(0, len(items), 40):
            path = C.contact_sheet(f'{section}_{i // 40}', items[i:i + 40], cols=10)
            print('preview:', path)
    print(f'done in {time.time() - t0:.0f}s, {len(results) - len(failed)} icons, {len(failed)} failed')
    if failed:
        sys.exit(1)
    # The game's screens show advances and buildings with the same pictures.
    if 'Advances' in wanted or 'Buildings' in wanted:
        import ui_icons
        ui_icons.main()


if __name__ == '__main__':
    main(sys.argv[1:])
