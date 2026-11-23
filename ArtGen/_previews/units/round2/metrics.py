"""Civ-tint share and sprite size: python metrics.py Unit1 Unit2 ..."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
UNITS = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'units'))
sys.path.insert(0, UNITS); os.chdir(UNITS)
import numpy as np
import flic as F, catalog, framework
cat = catalog.load()
for u in sys.argv[1:]:
    job = framework.UnitJob(u, cat[u])
    name = job.flics()['DEFAULT']
    fl = F.load_unit_flic(u, name)
    b, t, _ = job.render_flic('DEFAULT', name, rows=list(range(8)), cols=[0])
    W, H = fl.width * 2, fl.height * 2
    sh, sz, hts = [], [], []
    for d in range(8):
        bb = b[d*H:(d+1)*H, :W]; tt = t[d*H:(d+1)*H, :W]
        body = (bb[..., 3] > 200) | (tt[..., 3] > 128)
        # leave out the soft shadow: dark, semi-transparent
        n = body.sum(); sh.append((tt[..., 3] > 128).sum() / max(n, 1)); sz.append(n)
        ys = np.nonzero(body.any(1))[0]; hts.append(ys.max() - ys.min())
    print(f'{u:28s} civ {100*np.mean(sh):5.1f}%  px {np.mean(sz):7.0f}  h {np.mean(hts):5.1f}')
