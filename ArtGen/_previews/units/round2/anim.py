"""Animation strip: python anim.py OUT.png Unit ACTION [--dirs=1,3] [--step=2]"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
UNITS = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'units'))
sys.path.insert(0, UNITS); sys.path.insert(0, HERE)
CWD = os.getcwd(); os.chdir(UNITS)
from PIL import Image
import flic as F, catalog, framework
from preview import tinted
from look import BG, crop_all, sheet
args = [a for a in sys.argv[1:] if not a.startswith('--')]
opts = dict(a[2:].split('=', 1) for a in sys.argv[1:] if a.startswith('--') and '=' in a)
out, unit, action = os.path.join(CWD, args[0]), args[1], args[2]
dirs = [int(v) for v in opts.get('dirs', '1,3').split(',')]
step = int(opts.get('step', 2))
cat = catalog.load()
job = framework.UnitJob(unit, cat[unit])
name = job.flics()[action]
fl = F.load_unit_flic(unit, name)
cols = list(range(0, fl.n_frames, step))
base, tint, _ = job.render_flic(action, name, rows=dirs, cols=cols)
W, H = fl.width * 2, fl.height * 2
rows = []
for d in dirs:
    r = []
    for f in cols:
        c = tinted(base[d*H:(d+1)*H, f*W:(f+1)*W], tint[d*H:(d+1)*H, f*W:(f+1)*W])
        r.append(Image.fromarray(c, 'RGBA'))
    rows.append(r)
img = sheet(crop_all(rows), 1)
img.save(out); print(out, img.size, fl.n_frames)
