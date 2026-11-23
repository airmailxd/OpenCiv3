"""The reviewer's measures (C:/Dev/_handoff/review/meas.py) on our look sheets: for each unit row of the 1x
section, the figure area, civ-blue share and non-civ brightness, with the same criteria."""
import sys
import numpy as np
from PIL import Image
a = np.asarray(Image.open(sys.argv[1]).convert('RGB')).astype(float)
names = sys.argv[2:]
bg = np.array([96, 132, 62], float)
frame = np.abs(a - np.array([40, 40, 40])).sum(2) < 12
s = a.sum(2) + 1e-6
chrom = a / s[..., None]
bgc = bg / bg.sum()
# the 1x section: the last len(names) bands of non-frame rows
rows = (~frame).mean(1) > 0.05
bands, st = [], None
for i, v in enumerate(rows):
    if v and st is None: st = i
    if not v and st is not None: bands.append((st, i)); st = None
if st is not None: bands.append((st, len(rows)))
bands = bands[-len(names):]
for name, (y0, y1) in zip(names, bands):
    cols = (~frame[y0:y1]).mean(0) > 0.5
    cells, st = [], None
    for i, v in enumerate(cols):
        if v and st is None: st = i
        if not v and st is not None: cells.append((st, i)); st = None
    if st is not None: cells.append((st, len(cols)))
    res = []
    for x0, x1 in cells:
        sub = a[y0:y1, x0:x1]; ch = chrom[y0:y1, x0:x1]; br = s[y0:y1, x0:x1]
        ground = (np.abs(ch - bgc).sum(2) < 0.06) & (br <= bg.sum() * 1.08)
        fig = ~ground & ~frame[y0:y1, x0:x1]
        r, g, b = sub[..., 0], sub[..., 1], sub[..., 2]
        blue = fig & (b > r + 40) & (b > g + 25)
        non = fig & ~blue
        lum = 0.299 * r + 0.587 * g + 0.114 * b
        res.append((fig.sum(), blue.sum(), lum[non].mean()))
    res = np.array(res)
    print(f'{name:26s} area {res[:,0].mean():6.0f}  blue {100*res[:,1].sum()/res[:,0].sum():5.1f}%  '
          f'nonciv lum {np.average(res[:,2], weights=res[:,0]-res[:,1]):5.1f}')
