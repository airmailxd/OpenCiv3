"""Builds all of the remade map art into C7/ModernArt by running each area's build.py."""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))

if __name__ == '__main__':
    areas = sorted(d for d in os.listdir(HERE) if os.path.isfile(os.path.join(HERE, d, 'build.py')))
    for area in areas:
        print(f'== {area}', flush=True)
        subprocess.run([sys.executable, os.path.join(HERE, area, 'build.py')], check=True)
