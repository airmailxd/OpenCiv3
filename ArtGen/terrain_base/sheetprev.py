"""Side-by-side previews of sheets: original (2x nearest) vs remake, cropped."""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art
for name in sys.argv[1:]:
    rel = f'Art/Terrain/{name}.pcx'
    o = civ3art.upscale_nearest(civ3art.load_original(rel))
    m = np.asarray(Image.open(civ3art.modern_path(rel)).convert('RGBA'))
    civ3art.save_preview('sheet_' + name, o[:384, :768], m[:384, :768])
