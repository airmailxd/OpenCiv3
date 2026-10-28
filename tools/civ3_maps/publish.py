"""Publishes this machine's Civ3 maps to the corpus repo
(github.com/airmailxd/civ3-map-corpus), an hour's maps at a time:

    <machine>/<source>/<YYYY-MM-DD_HH00>/maps.zip, manifest.csv, seeds.csv

where <source> is "host" for the corpus's std folder (generate.py on this
PC) and vm_civ3gen-<n> for each VM's folder. Before each batch the Quick
Start maps are renamed to their seeds (MapGenParityTest's
RenameQuickStartMaps, which checks each save's settings against its name),
so a map's name gives its seed and settings. The maps already published are
read from the repo's manifests.

    python tools/civ3_maps/publish.py --repo=C:/Dev/civ3-map-corpus --machine=szpc --every=3600

--every=0 publishes once.
"""
import csv
import datetime
import hashlib
import os
import re
import subprocess
import sys
import time
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ENGINE_TESTS = os.path.normpath(os.path.join(HERE, '..', '..', 'EngineTests'))
NAME = re.compile(r'^(?P<rules>[A-Z]+)_(?P<size>.+)_(?P<land>Pan|Con|Arc)(?P<water>\d\d)_(?P<climate>Ari|Nrm|Wet)-(?P<temp>Wrm|Tmp|Col)-(?P<age>\dB)_s(?P<seed>\d+)\.SAV$')
# The civs of each world size (the BIQ's WSIZ), for seeds.csv.
CIVS = {'Tiny': 4, 'Small': 6, 'Standard': 8, 'Large': 10, 'Huge': 12}
# GitHub refuses files over 100 MB; at about 32 KB a zipped map this keeps
# each zip near 70 MB.
MAPS_PER_ZIP = 2000
AUTHOR = 'airmailxd <airmailxd@users.noreply.github.com>'


def git(repo, *args, check=True):
    return subprocess.run(['git', '-C', repo, *args], check=check, capture_output=True, text=True)


def rename_quick_start_maps(corpus):
    env = dict(os.environ, C7_MAPGEN_RENAME=corpus)
    r = subprocess.run(['dotnet', 'test', ENGINE_TESTS, '--filter', 'FullyQualifiedName~RenameQuickStartMaps'],
                       env=env, capture_output=True, text=True)
    if r.returncode != 0:
        print('  renaming failed; publishing what is already named', flush=True)


def sources(corpus):
    """(source name, folder) for the host run and each VM."""
    if os.path.isdir(os.path.join(corpus, 'std')):
        yield 'host', os.path.join(corpus, 'std')
    for name in sorted(os.listdir(corpus)):
        if name.startswith('vm_') and os.path.isdir(os.path.join(corpus, name)):
            yield name, os.path.join(corpus, name)


def published(repo_source):
    done = set()
    for root, _, files in os.walk(repo_source):
        if 'manifest.csv' in files:
            with open(os.path.join(root, 'manifest.csv'), newline='') as f:
                done.update(row['name'] for row in csv.DictReader(f))
    return done


def sha1(path):
    h = hashlib.sha1()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def publish_source(repo, machine, source, folder, stamp):
    repo_source = os.path.join(repo, machine, source)
    done = published(repo_source)
    new = sorted(n for n in os.listdir(folder) if NAME.match(n) and n not in done)
    if not new:
        return 0
    batch = os.path.join(repo_source, stamp)
    i = 2
    while os.path.exists(batch):
        batch = os.path.join(repo_source, f'{stamp}_{i}')
        i += 1
    os.makedirs(batch)
    parts = [new[i:i + MAPS_PER_ZIP] for i in range(0, len(new), MAPS_PER_ZIP)]
    for p, names in enumerate(parts):
        zip_name = 'maps.zip' if len(parts) == 1 else f'maps_{p + 1}.zip'
        with zipfile.ZipFile(os.path.join(batch, zip_name), 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for n in names:
                z.write(os.path.join(folder, n), n)
    with open(os.path.join(batch, 'manifest.csv'), 'w', newline='') as f:
        w = csv.writer(f)
        w.writerow(['name', 'rules', 'size', 'bytes', 'sha1'])
        for n in new:
            m = NAME.match(n)
            path = os.path.join(folder, n)
            w.writerow([n, m['rules'], m['size'], os.path.getsize(path), sha1(path)])
    with open(os.path.join(batch, 'seeds.csv'), 'w', newline='') as f:
        w = csv.writer(f)
        w.writerow(['name', 'seed', 'landform', 'water', 'climate', 'temperature', 'age', 'civs'])
        for n in new:
            m = NAME.match(n)
            w.writerow([n, int(m['seed']), m['land'], m['water'], m['climate'], m['temp'], m['age'], CIVS.get(m['size'], '')])
    return len(new)


def publish(repo, corpus, machine):
    rename_quick_start_maps(corpus)
    git(repo, 'pull', '--rebase', '--quiet', check=False)
    stamp = datetime.datetime.now().strftime('%Y-%m-%d_%H00')
    counts = {source: publish_source(repo, machine, source, folder, stamp) for source, folder in sources(corpus)}
    total = sum(counts.values())
    if total == 0:
        print(f'{stamp}: nothing new', flush=True)
        return
    git(repo, 'add', machine)
    summary = ', '.join(f'{s} {n}' for s, n in counts.items() if n)
    message = f'{machine} {stamp}: {total} maps ({summary})\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\n'
    git(repo, '-c', 'user.name=airmailxd', '-c', 'user.email=airmailxd@users.noreply.github.com',
        'commit', '--quiet', '--author', AUTHOR, '-m', message)
    for attempt in range(5):
        if git(repo, 'push', '--quiet', check=False).returncode == 0:
            print(f'{stamp}: pushed {total} maps ({summary})', flush=True)
            return
        # Another machine pushed first.
        git(repo, 'pull', '--rebase', '--quiet', check=False)
        time.sleep(10)
    print(f'{stamp}: push failed; the commit stays and goes with the next', flush=True)


def main(argv):
    opts = dict(a[2:].split('=', 1) for a in argv if a.startswith('--') and '=' in a)
    repo = opts.get('repo') or sys.exit('--repo=<clone of the corpus repo> needed')
    corpus = opts.get('corpus', r'C:\Dev\civ3_map_corpus')
    machine = opts.get('machine') or sys.exit('--machine=<name> needed')
    every = int(opts.get('every', 3600))
    while True:
        publish(repo, corpus, machine)
        if every <= 0:
            return
        time.sleep(every)


if __name__ == '__main__':
    main(sys.argv[1:])
