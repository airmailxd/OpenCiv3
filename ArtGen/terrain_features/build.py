"""Builds the remade linear features, improvements and resources into C7/ModernArt:
rivers, roads, railroads, irrigation, pollution, craters, terrain yield markers,
terrain buildings (fortresses, colonies, camp, mine), goody huts and the resource icons."""
import importlib
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

MODULES = ['rivers', 'irrigation', 'blight', 'tnt', 'roads', 'buildings', 'goodyhuts', 'resources']

if __name__ == '__main__':
    only = sys.argv[1:] or MODULES
    for name in only:
        if not os.path.exists(os.path.join(HERE, name + '.py')):
            print(f'-- {name}: missing, skipped', flush=True)
            continue
        t = time.time()
        importlib.import_module(name).build()
        print(f'-- {name}: {time.time() - t:.0f}s', flush=True)
