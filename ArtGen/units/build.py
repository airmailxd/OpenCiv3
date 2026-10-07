"""Renders every catalogued unit's flics into C7/ModernArt/Art/Units.

    python build.py                    # all catalogued units
    python build.py Warrior Spearman   # just these
    python build.py --jobs=8           # worker processes (default: all cores)
    python build.py --list             # catalogued units, and which Civ3 units are not
    python build.py --animations       # also the animations only played with animations on

By default only the flics the game shows with animations off are rendered:
standing, fortified, and workers at work (see flic.flic_kind and
framework.save_sheets for where each goes). --animations adds the rest
(attacks, deaths, runs...), which go to C7/ModernArtAnimations, not committed.

Each flic the unit's INI lists becomes <flic>.png and <flic>_tint.png next to
where the game looks for the flic (see framework.save_sheets). Flics are
rendered in parallel. Uncatalogued units are skipped, so the game keeps their
original art. Per-flic timings and sheet sizes go to
ArtGen/_previews/units/build_stats.json.
"""
import json
import multiprocessing as mp
import os
import sys
import time

# One thread per process: the pool already uses every core.
for _v in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ.setdefault(_v, '1')

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import flic as F  # noqa: E402  (puts ArtGen/common on the path)
import civ3art  # noqa: E402
import catalog  # noqa: E402
import framework  # noqa: E402

_jobs = {}


def _job(unit):
    if unit not in _jobs:
        _jobs[unit] = framework.UnitJob(unit, catalog.load()[unit])
    return _jobs[unit]


def render_one(task):
    unit, action, name, still_only = task
    t0 = time.time()
    try:
        job = _job(unit)
        if still_only:
            # Only the last frame is shown with animations off.
            n = F.load_unit_flic(unit, name).n_frames
            base, tint, fl = job.render_flic(action, name, cols=[n - 1])
            paths = framework.save_still(unit, name, fl, base, tint)
        else:
            base, tint, fl = job.render_flic(action, name)
            paths = framework.save_sheets(unit, name, fl, base, tint)
        clip = framework.clipped_fraction(base, tint, fl)
        sizes = {os.path.basename(p): os.path.getsize(p) for p in paths}
        return dict(unit=unit, action=action, flic=name, seconds=round(time.time() - t0, 1),
                    frames=fl.n_anims * fl.n_frames, sheet=[int(base.shape[1]), int(base.shape[0])],
                    bytes=sizes, clipped_pct=round(clip, 2))
    except Exception as e:  # report and keep going with the other flics
        import traceback
        return dict(unit=unit, action=action, flic=name, error=f'{e!r}', trace=traceback.format_exc())


def tasks_for(units, animations=False):
    out = []
    for unit in units:
        seen = set()
        for action, name in F.unit_flics(unit).items():
            try:
                civ3art.media_path(F.flic_rel(unit, name))
            except FileNotFoundError:
                print(f'  {unit}: {name} ({action}) is missing, skipped')
                continue
            if name.lower() in seen:
                continue
            kind = F.flic_kind(unit, name)
            if not animations and kind == 'animation':
                continue
            seen.add(name.lower())
            out.append((unit, action, name, kind == 'still' and not animations))
    # biggest first, so the pool finishes evenly
    def cost(t):
        try:
            fl = F.read_flic(civ3art.media_path(F.flic_rel(t[0], t[2])))
            return fl.width * fl.height * fl.n_frames
        except Exception:
            return 0
    return sorted(out, key=cost, reverse=True)


def main(argv):
    opts = dict(a[2:].split('=', 1) if '=' in a else (a[2:], '1') for a in argv if a.startswith('--'))
    names = [a for a in argv if not a.startswith('--')]
    cat = catalog.load()
    if 'list' in opts:
        print('catalogued:', ', '.join(sorted(cat)))
        print('not catalogued (original art):', ', '.join(u for u in F.all_unit_folders() if u not in cat))
        return
    units = names or sorted(cat)
    missing = [u for u in units if u not in cat]
    if missing:
        print('not in the catalog, skipped:', ', '.join(missing))
    units = [u for u in units if u in cat]
    tasks = tasks_for(units, animations='animations' in opts)
    n = int(opts.get('jobs', os.cpu_count() or 4))
    print(f'{len(units)} units, {len(tasks)} flics, {n} processes', flush=True)
    t0 = time.time()
    results = []
    with mp.Pool(n) as pool:
        for r in pool.imap_unordered(render_one, tasks):
            results.append(r)
            if 'error' in r:
                print(f"  FAILED {r['unit']}/{r['flic']}: {r['error']}\n{r['trace']}", flush=True)
            else:
                warn = f"  (clipped {r['clipped_pct']}%)" if r['clipped_pct'] > 1.0 else ''
                print(f"  {r['unit']}/{r['flic']}: {r['frames']} frames {r['seconds']}s "
                      f"{sum(r['bytes'].values()) / 1024:.0f} KB{warn}", flush=True)
    total = time.time() - t0
    stats_path = os.path.join(civ3art.PREVIEWS, 'units', 'build_stats.json')
    os.makedirs(os.path.dirname(stats_path), exist_ok=True)
    old = {}
    if os.path.exists(stats_path):
        try:
            old = {(r['unit'], r['flic']): r for r in json.load(open(stats_path))}
        except Exception:
            old = {}
    for r in results:
        old[(r['unit'], r['flic'])] = r
    json.dump(list(old.values()), open(stats_path, 'w'), indent=1)
    by_unit = {}
    for r in results:
        if 'error' in r:
            continue
        u = by_unit.setdefault(r['unit'], [0, 0.0, 0])
        u[0] += sum(r['bytes'].values())
        u[1] += r['seconds']
        u[2] += r['frames']
    print(f'done in {total:.0f}s wall')
    for u, (b, s, fr) in sorted(by_unit.items()):
        print(f'  {u:24s} {fr:5d} frames  {s:7.0f} cpu-s  {b / 1024 / 1024:6.2f} MB')
    if any('error' in r for r in results):
        sys.exit(1)


if __name__ == '__main__':
    main(sys.argv[1:])
