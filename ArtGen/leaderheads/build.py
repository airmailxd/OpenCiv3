"""Draws the leader heads for standalone mode (no Civ3 files): a head and
shoulders portrait of each civilization's leader, shown in diplomacy, the
new game screen and the LAN lobby:

    C7/ModernArt/Art/Leaderheads/<civ name>.png

    python ArtGen/leaderheads/build.py               # every civilization
    python ArtGen/leaderheads/build.py Rome Egypt    # just these

A portrait is the leader's own 3D model, the one the leader's unit (Civ3's
"King" units, ArtGen/units/catalog/leaders.py) is drawn with on the map,
seen up close at eye level and turned a little to the side, with a face
detailed for the close-up (eyes, lids, brows, lips). The civ-colored parts
are in the civilization's color (the standalone civ colors, by the ruleset's
primaryColorIndex), and it stands before a soft backdrop tinted with that
color. So it is original art in the same look as the units and the
Civilopedia's icons.

Like all of ModernArt it has twice the pixels it's shown with: 230x230 for
Civ3's 115x115 leader heads. The standalone rules (C7/Lua/standalone/
ruleset.lua) point each civilization's leaderArtFile at its portrait, and
the standalone textures (C7/Lua/standalone/textures.lua) show it. There's
one portrait per leader, not one per era and mood like Civ3's.

A contact sheet goes to ArtGen/_previews/leaderheads.png.
"""
import multiprocessing as mp
import os
import sys

for _v in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ.setdefault(_v, '1')

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'civilopedia_icons'))

import numpy as np  # noqa: E402
import common as C  # noqa: E402  (ArtGen/civilopedia_icons; puts units and common on the path)
import civ3art  # noqa: E402
import mesh as M  # noqa: E402
import render as R  # noqa: E402
import rig  # noqa: E402
import models  # noqa: E402
from models import humanoid as H  # noqa: E402
import units as U  # noqa: E402  (the Civilopedia's unit renderer)

SIZE = 115 * civ3art.SCALE
FOLDER = 'Art/Leaderheads'

# Each civilization's leader unit art (its "King" unit's folder), by the
# civilization's Civilopedia entry.
LEADER_ART = {
    'RACE_ROMANS': 'King Roman', 'RACE_EGYPTIANS': 'King Egyptian', 'RACE_GREEKS': 'King Greek',
    'RACE_BABYLON': 'King Babylonian', 'RACE_GERMANS': 'King German', 'RACE_RUSSIAN': 'King Russian',
    'RACE_CHINESE': 'King Chinese', 'RACE_AMERICAN': 'King American', 'RACE_JAPANESE': 'King Japanese',
    'RACE_FRENCH': 'King French', 'RACE_INDIAN': 'King Indian', 'RACE_PERSIAN': 'King Persian',
    'RACE_AZTECS': 'King Aztec', 'RACE_ZULU': 'King Zulu', 'RACE_IROQUOIS': 'King Iroquois',
    'RACE_ENGLISH': 'King English', 'RACE_MONGOLS': 'King Mongol', 'RACE_SPANISH': 'King Spanish',
    'RACE_VIKINGS': 'King Viking', 'RACE_OTTOMANS': 'King Ottoman', 'RACE_CELTS': 'King Celtic',
    'RACE_ARABS': 'King Arab', 'RACE_CARTHAGINIANS': 'King Carthaginian', 'RACE_KOREANS': 'King Korean',
    'RACE_SUMERIA': 'King Sumeria', 'RACE_HATTI': 'King Hatti', 'RACE_DUTCH': 'King Netherlands',
    'RACE_PORTUGAL': 'King Portugal', 'RACE_BYZANTINES': 'King Byzantines', 'RACE_INCANS': 'King Incan',
    'RACE_MAYANS': 'King Mayan',
}

# The view: the model turned a little to its right (toward the light), and
# tipped toward the camera to undo the map camera's 30 degree tilt, so the
# leader is seen at eye level.
YAW = 162.0
TILT = -R.ELEVATION
# How the head sits in the picture: its center (fractions of the picture)
# and the picture's height in meters at the usual zoom.
HEAD_AT = (0.5, 0.41)
VIEW_HEIGHT = 0.78
# Tall headgear (feather bonnets, plumes, crowns) zooms out down to this
# height, and lowers the head down to this far, to stay in the picture.
MAX_VIEW_HEIGHT = 1.2
LOWEST_HEAD = 0.55
TOP_MARGIN = 0.04
# The tallest headgear, in meters above the head center, that the picture
# makes room for.
HEADGEAR = 0.6


class PortraitStyle(R.Style):
    ss = 4
    contour_alpha = 0.45
    inner_line = 0.22


def file_name(civ):
    return C.file_stem(civ['name']) + '.png'


def art_path(civ):
    """The media path of a civilization's portrait, as the game uses it."""
    return f'{FOLDER}/{file_name(civ)}'


def civ_color(civ):
    return C.hex_rgb(C.CIV_COLORS[civ.get('primaryColorIndex', 0) % len(C.CIV_COLORS)])


# ---------------------------------------------------------------- the model

def _face(model, female=False):
    """Parts for the close-up face, in head space (before the humanoid's
    head scale): eyes with whites, irises and upper lids, brows, nostrils
    and lips. They replace the map model's dark dot eyes. Women get finer,
    arched brows, lashes and rosier lips."""
    mats = model.mats
    skin = mats['skin']
    hair = mats['hair']
    skin_rgb = np.array(mats.color[skin], np.float32)
    hair_rgb = np.array(mats.color[hair], np.float32)
    white = mats.add('eye_white', tuple(int(v) for v in C.mix(np.array([236, 230, 218], np.float32), skin_rgb, 0.18)),
                     spec=0.25, gloss=30)
    iris = mats.add('iris', '#3a2618', spec=0.5, gloss=40)
    lid = mats.add('lid', tuple(int(v) for v in skin_rgb * 0.86), spec=0.1, gloss=10)
    # brows a little darker than the hair (bald or white-haired leaders keep
    # light brows)
    brow = mats.add('brow', tuple(int(v) for v in hair_rgb * 0.85), spec=0.05, gloss=8)
    lips = mats.add('lip', tuple(int(v) for v in C.mix(skin_rgb, np.array([165, 62, 64] if female else [150, 70, 60],
                                                                            np.float32), 0.55 if female else 0.45)),
                    spec=0.2 if female else 0.15, gloss=16)
    lash = mats.add('lash', '#2a1c16')
    line = mats.add('mouth_line', tuple(int(v) for v in skin_rgb * 0.45))
    parts = []
    for sx in (1, -1):
        parts.append(M.ellipsoid(0.0175, 0.0065, 0.0105, white, (sx * 0.037, 0.1125, 0.1215), seg=10))
        parts.append(M.ellipsoid(0.0085, 0.0042, 0.0085, iris, (sx * 0.0355, 0.1168, 0.1205), seg=10))
        parts.append(M.ellipsoid(0.0195, 0.0072, 0.0062, lid, (sx * 0.037, 0.1135, 0.1305), seg=10))
        if female:
            parts.append(M.capsule((sx * 0.018, 0.1205, 0.1425), (sx * 0.038, 0.1185, 0.1485), 0.0032, 0.0028, brow,
                                   seg=6, rings=1))
            parts.append(M.capsule((sx * 0.038, 0.1185, 0.1485), (sx * 0.058, 0.1095, 0.1435), 0.0028, 0.0018, brow,
                                   seg=6, rings=1))
            parts.append(M.capsule((sx * 0.022, 0.1175, 0.1255), (sx * 0.054, 0.1125, 0.1265), 0.0022, 0.0022, lash,
                                   seg=5, rings=1))
        else:
            parts.append(M.capsule((sx * 0.016, 0.1205, 0.1415), (sx * 0.058, 0.1105, 0.1445), 0.0048, 0.0036, brow,
                                   seg=6, rings=1))
        parts.append(M.ellipsoid(0.011, 0.012, 0.009, skin, (sx * 0.013, 0.134, 0.087), seg=8))
    parts.append(M.ellipsoid(0.026, 0.0085, 0.0068, lips, (0, 0.1125, 0.064), seg=10))
    parts.append(M.capsule((-0.021, 0.1195, 0.0645), (0.021, 0.1195, 0.0645), 0.0021, 0.0021, line, seg=5, rings=1))
    return M.Mesh.concat(parts).transformed(M.scale(H.HEAD))


def portrait_model(folder, female=False):
    """The posed leader model in world space (turned and tipped for the
    portrait), its materials and the position of its head."""
    name, entry = U.unit_entry(folder)
    model = models.ARCHETYPES[entry['archetype']](name, entry)
    eyes = model.mats['eyes']
    model.parts = [(b, M.Mesh(m.V[m.M != eyes], m.N[m.M != eyes], m.M[m.M != eyes]) if b == 'head' else m, t)
                   for b, m, t in model.parts]
    model.parts.append(('head', _face(model, female), None))
    act = model.action('DEFAULT', None)
    t = rig.frame_times(15, act.loop)[0]
    pose = act.fn(t)
    pose.setdefault('t', t)
    mesh = model.pose_full(pose, act, model.anim_name('DEFAULT'), t)
    Wh = model.world(pose, act)['head']
    head = Wh[:3, 3] + Wh[:3, :3] @ (np.array([0, 0.01, 0.11], np.float32) * H.HEAD)
    T = M.rot_x(TILT) @ U._yaw(YAW)
    return mesh.transformed(T), model.mats, T[:3, :3] @ head


def frame(mesh, head):
    """The camera: the head at HEAD_AT, zoomed out and lowered as far as
    needed (and allowed) to keep tall headgear in the picture."""
    unit = R.Camera(1.0, (0.0, 0.0))
    hx, hy, _ = unit.project(head[None])[0]
    S = unit.project(mesh.V).reshape(-1, 3)
    # what's on the head: above it and no wider than a headdress (not a
    # staff or standard held beside it, which may run out of the picture)
    near = (np.abs(S[:, 0] - hx) < 0.2) & (hy - S[:, 1] < HEADGEAR)
    above = hy - S[near, 1].min()    # meters above the head center
    view = VIEW_HEIGHT
    at = HEAD_AT[1]
    need = above / (at - TOP_MARGIN)
    if need > view:
        # lower the head first, then zoom out
        at = min(LOWEST_HEAD, TOP_MARGIN + above / view)
        need = above / (at - TOP_MARGIN)
        view = min(MAX_VIEW_HEIGHT, max(view, need))
    ppm = SIZE / view
    return R.Camera(ppm, (SIZE * HEAD_AT[0] - hx * ppm, SIZE * at - hy * ppm))


# ---------------------------------------------------------------- the picture

def backdrop(color, seed):
    """A soft, lit-from-the-upper-left backdrop: a pale light behind the head
    fading into a deeper shade of the civ's color."""
    n = SIZE
    base = C.mute(color, 0.45, 1.0)
    light = C.mix(C.hex_rgb('e6dcc6'), base, 0.25)
    deep = C.mix(C.hex_rgb('3c3630'), base, 0.42)
    d = C.radial(n, n * 0.40, n * 0.34, n * 0.78)
    t = np.clip(d, 0, 1)[..., None] ** 1.1
    rgb = C.mix(light, deep, t)
    nz = C.noise(n, n / 4, seed, 3)[..., None]
    rgb = rgb * (0.96 + 0.07 * nz)
    rgb = C.vignette(rgb, 0.22)
    return np.concatenate([rgb, np.ones((n, n, 1), np.float32)], -1)


def make(civ):
    """A civilization's portrait (float RGBA, SIZE x SIZE, opaque)."""
    folder = LEADER_ART[civ['civilopediaEntry']]
    mesh, mats, head = portrait_model(folder, civ.get('leaderGender') == 'female')
    cam = frame(mesh, head)
    b, t = R.render_cell(mesh, mats.arrays(), cam, SIZE, SIZE, style=PortraitStyle, shadow=False)
    color = civ_color(civ)
    figure = U.tinted(b, t, C.mute(color, 0.8, 0.95))
    seed = sum(map(ord, civ['civilopediaEntry']))
    bg = backdrop(color, seed)
    img = C.over(figure, C.over(_drop_shadow(figure), bg))
    img[..., :3] = C.frame_edges(img[..., :3])
    return img


def _drop_shadow(obj, dx=6, dy=5, r=5, alpha=0.30):
    a = C.blur(obj[..., 3], r)
    a = np.roll(np.roll(a, dy, 0), dx, 1)
    sh = np.zeros_like(obj)
    sh[..., 3] = a * alpha
    return sh


def civilizations():
    return [c for c in C.load_ruleset()['civilizations'] if not c.get('isBarbarian')]


def _job(civ):
    try:
        img = make(civ)
        C.save_png(img, os.path.join(civ3art.MODERN_ART, *art_path(civ).split('/')))
        return civ['name'], img, None
    except Exception:
        import traceback
        return civ['name'], None, traceback.format_exc()


def main(argv):
    opts = dict(a[2:].split('=', 1) if '=' in a else (a[2:], '1') for a in argv if a.startswith('--'))
    names = [a.lower() for a in argv if not a.startswith('--')]
    civs = [c for c in civilizations() if not names or c['name'].lower() in names]
    missing = [c['name'] for c in civs if c['civilopediaEntry'] not in LEADER_ART]
    if missing:
        sys.exit(f'No leader art for {", ".join(missing)}: add them to LEADER_ART')
    n = int(opts.get('jobs', os.cpu_count() or 4))
    done, failed = [], []
    with mp.Pool(min(n, len(civs))) as pool:
        for name, img, err in pool.imap(_job, civs):
            if err:
                print(f'  FAILED {name}:\n{err}', flush=True)
                failed.append(name)
            else:
                done.append((name, img))
    cols = 8
    rows = (len(done) + cols - 1) // cols
    pad = 6
    sheet = np.zeros((rows * (SIZE + pad) + pad, cols * (SIZE + pad) + pad, 4), np.float32)
    sheet[..., :3] = 0.82
    sheet[..., 3] = 1
    for i, (_, img) in enumerate(done):
        r, c = divmod(i, cols)
        y, x = pad + r * (SIZE + pad), pad + c * (SIZE + pad)
        sheet[y:y + SIZE, x:x + SIZE] = img
    preview = os.path.join(civ3art.PREVIEWS, 'leaderheads.png')
    C.save_png(sheet, preview)
    print(f'{len(done)} leader heads, {len(failed)} failed; preview: {preview}')
    if failed:
        sys.exit(1)


if __name__ == '__main__':
    main(sys.argv[1:])
