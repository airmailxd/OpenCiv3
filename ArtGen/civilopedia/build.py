"""Draws the art of OpenCiv3's own Civilopedia into C7/Civilopedia/Art/Civilopedia/.

The Civilopedia lays its pages out at fixed spots on 1024x768 backgrounds
(see C7/UIElements/Civilopedia/Civilopedia.cs), so these are drawn with the
geometry of Civ3's: the window, the panels of each kind of page, the tabs
their headings are written on, the slot for the entry's name and the well
for its icon are all where the code expects them. The look is our own:
parchment panels set into a stone-colored frame, lit from the upper left.

Everything is drawn with code and fixed random seeds, so a rebuild gives the
same files. Run: python ArtGen/civilopedia/build.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, 'C7', 'Civilopedia', 'Art', 'Civilopedia')

W, H = 1024, 768

# ---- The layout, measured from Civ3's art --------------------------------
# Rectangles are (x0, y0, x1, y1) with x1 and y1 just outside.

WINDOW = (172, 11, 849, 745)
TITLE_PLATE = (198, 27, 392, 62)
NAME_SLOT = (197, 68, 696, 102)       # the bar (199..694, 70..99) sits in it
ICON_WELL = (694, 59, 824, 189)       # the 128x128 icon at (695, 60)
NAV_PANEL = (694, 327, 824, 652)      # Up/Previous/Next and the letters
CLOSE_PLATE = (725, 681, 793, 722)    # the close button at (728, 684), 62x34
PANEL_X = (198, 671)                  # every page's panels are this wide



def tab(kind, y):
    """A tab for a heading (written 16px in from the tab's left), 15 high:
    a main one against the panel's left edge, a sub-heading's a little in,
    or one against the right edge."""
    x0, x1 = {'main': (199, 325), 'sub': (211, 301), 'right': (545, 671)}[kind]
    return (x0, y, x1, y + 15, kind)


# Each page's panels as (top, bottom), its heading tabs, and the y of each
# engraved line across a panel.


PAGES = {
    'civilopediaBackground': dict(panels=[(108, 711)], tabs=[], rules=[]),
    # Advances: text; Requires / Leads To; Allows with three rows.
    'civilopediaA': dict(panels=[(108, 231), (240, 515), (524, 720)],
                         tabs=[tab('main', 252), tab('right', 252), tab('main', 535),
                               tab('sub', 563), tab('sub', 612), tab('sub', 662)], rules=[]),
    # Units: text; cost; stats; requires (advances, resources).
    'civilopediaB': dict(panels=[(108, 369), (378, 415), (424, 515), (523, 711)],
                         tabs=[tab('main', 436), tab('main', 531), tab('sub', 558), tab('sub', 636)],
                         rules=[626]),
    # Not used by the layout code (yet), but Civ3 has it.
    'civilopediaC': dict(panels=[(108, 231), (241, 319), (328, 501)],
                         tabs=[tab('main', 249), tab('main', 336)], rules=[]),
    # Terrain and civilizations: text; yield/leader; statistics/traits; resources/unit.
    'civilopediaD': dict(panels=[(108, 343), (351, 431), (440, 545), (555, 688)],
                         tabs=[tab('main', 361), tab('main', 449), tab('sub', 476), tab('main', 562)],
                         rules=[]),
    # Worker actions: text; turns and workers; requires.
    'civilopediaE': dict(panels=[(108, 395), (402, 457), (464, 684)],
                         tabs=[tab('main', 471), tab('sub', 498), tab('sub', 590)], rules=[577]),
    # Governments and resources: text; requires; tile bonus.
    'civilopediaF': dict(panels=[(108, 486), (494, 599), (606, 711)],
                         tabs=[tab('main', 502), tab('sub', 529), tab('main', 616)], rules=[]),
    # Buildings: text; cost and upkeep; requires.
    'civilopediaG': dict(panels=[(108, 369), (377, 515), (522, 711)],
                         tabs=[tab('main', 531), tab('sub', 558), tab('sub', 636)], rules=[626]),
}

# The covers lay plain frame over panels a page has nothing for, as
# (page, panel indexes).
COVERS = {
    'B-cover': ('civilopediaG', [2]),
    'F-lower': ('civilopediaF', [2]),
    'F-upper': ('civilopediaF', [1]),
    'civilopediaE-coverUp': ('civilopediaE', [2]),
    'civilopediaA-top-coverUp': ('civilopediaA', [0]),
    'civilopediaA-bottom-coverUp': ('civilopediaA', [2]),
    'civilopediaC-coverUp': ('civilopediaC', [0]),
    'civilopediaD-coverUp': ('civilopediaD', [2, 3]),
    'civilopediaF-coverUp': ('civilopediaF', [1, 2]),
}

# ---- Colors ----------------------------------------------------------------

FRAME = np.array([172, 162, 137]) / 255
FRAME_DARK = np.array([58, 54, 44]) / 255
PARCHMENT = np.array([238, 226, 194]) / 255
PAPER = np.array([246, 242, 229]) / 255
TAB = np.array([251, 248, 240]) / 255
IVORY = np.array([232, 225, 204]) / 255

# The bars behind entry names, by the file's color name: light cream on the
# left, where the name is written, turning to the section's color.
BAR_COLORS = {
    'blue': (128, 156, 202),
    'green': (136, 172, 118),
    'grey': (168, 168, 162),
    'orange': (224, 158, 98),
    'purple': (154, 144, 198),
    'red': (212, 126, 106),
    'yello': (220, 198, 118),
}


# ---- Helpers ----------------------------------------------------------------

def save(rel, rgb, alpha):
    """Writes float RGB (h, w, 3) and alpha (h, w) in 0..1 as an RGBA PNG."""
    rgba = np.dstack([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)[..., None]])
    rgba = (rgba * 255 + 0.5).astype(np.uint8)
    rgba[rgba[..., 3] == 0, :3] = 0
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(rgba, 'RGBA').save(path, optimize=True)


def smooth_noise(h, w, cell, seed, stretch=(1, 1)):
    """Soft value noise in -1..1 with features about `cell` pixels across
    (times `stretch` = (x, y) for streaks)."""
    rng = np.random.RandomState(seed)
    cx, cy = max(1, cell * stretch[0]), max(1, cell * stretch[1])
    sw, sh = int(w / cx) + 3, int(h / cy) + 3
    small = rng.rand(sh, sw).astype(np.float32)
    big = Image.fromarray(small, 'F').resize((int(sw * cx), int(sh * cy)), Image.BICUBIC)
    a = np.asarray(big)[:h, :w]
    return (a - 0.5) * 2


def fbm(h, w, cell, seed, octaves=3, stretch=(1, 1)):
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        total += amp * smooth_noise(h, w, max(1, cell / 2 ** o), seed + o * 101, stretch)
        norm += amp
        amp *= 0.5
    return total / norm


def blur(a, radius):
    """A Gaussian blur with standard deviation `radius`."""
    n = max(1, int(math.ceil(radius * 3)))
    k = np.exp(-np.arange(-n, n + 1) ** 2 / (2 * radius ** 2))
    k /= k.sum()
    out = a.astype(np.float32)
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]
        pad[axis] = (n, n)
        p = np.pad(out, pad, mode='edge')
        size = out.shape[axis]
        out = sum(w * np.take(p, np.arange(i, i + size), axis=axis) for i, w in enumerate(k))
    return out


def rect_mask(shape, r, radius=0.0):
    """Anti-aliased mask of a rectangle with rounded corners."""
    h, w = shape
    x0, y0, x1, y1 = r[:4]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    # Distance outside the rounded rectangle (negative inside).
    cx = np.clip(xs, x0 + radius, x1 - radius)
    cy = np.clip(ys, y0 + radius, y1 - radius)
    d = np.hypot(xs - cx, ys - cy) - radius
    inside_x = np.maximum(x0 - xs, xs - x1)
    inside_y = np.maximum(y0 - ys, ys - y1)
    if radius <= 0:
        d = np.maximum(inside_x, inside_y)
    return np.clip(0.5 - d, 0, 1)


def edge_distances(shape, r):
    """Distances of each pixel to the four sides of a rectangle (0 for
    pixels outside a side)."""
    h, w = shape
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    x0, y0, x1, y1 = r[:4]
    return tuple(np.maximum(d, 0) for d in (xs - x0, ys - y0, x1 - xs, y1 - ys))


# ---- The window -------------------------------------------------------------

def frame_material():
    """The frame's stone color: soft mottling with a faint horizontal grain,
    a nod to the cloth of a book's binding."""
    mott = fbm(H, W, 90, 11, octaves=4)
    grain = fbm(H, W, 3, 23, octaves=2, stretch=(14, 1))
    shade = 1 + 0.035 * mott + 0.018 * grain
    # A little lighter towards the upper left, where the light comes from.
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    shade *= 1.03 - 0.06 * ((xs - 172) / 677 * 0.4 + (ys - 11) / 734 * 0.6)
    return FRAME[None, None, :] * shade[..., None]


def bevel(rgb, r, width, strength, raised=True, radius=0.0):
    """Lightens the upper-left edges of a rectangle and darkens its
    lower-right ones (or the other way round, for a sunken one)."""
    shape = rgb.shape[:2]
    left, top, right, bottom = edge_distances(shape, r)
    inside = rect_mask(shape, r, radius)
    light = np.clip(1 - np.minimum(left, top) / width, 0, 1)
    dark = np.clip(1 - np.minimum(right, bottom) / width, 0, 1)
    # Where both apply (the corners going down-left and up-right), the
    # nearer edge wins.
    nearer_light = np.minimum(left, top) <= np.minimum(right, bottom)
    light = np.where(nearer_light, light, 0)
    dark = np.where(nearer_light, 0, dark)
    sign = 1 if raised else -1
    k = (sign * light - sign * dark) * strength * inside
    return np.where(k[..., None] > 0, rgb + (1 - rgb) * k[..., None], rgb * (1 + k[..., None]))


def outline(rgb, r, color, alpha=1.0, radius=0.0):
    """A 1px line just inside a rectangle's edge."""
    shape = rgb.shape[:2]
    outer = rect_mask(shape, r, radius)
    x0, y0, x1, y1 = r[:4]
    inner = rect_mask(shape, (x0 + 1, y0 + 1, x1 - 1, y1 - 1), max(0, radius - 1))
    ring = np.clip(outer - inner, 0, 1) * alpha
    return rgb * (1 - ring[..., None]) + np.asarray(color) * ring[..., None]


def sunken(rgb, r, fill, depth=1.0, seed=0, mottle=0.03):
    """Sets a panel into the frame: a dark rim, a soft shadow along the
    top and left inside it, and the frame's lip catching the light along
    the bottom and right outside it."""
    shape = rgb.shape[:2]
    x0, y0, x1, y1 = r[:4]
    # The lip: light just outside the lower and right edges, dark outside
    # the upper and left ones.
    lip = rect_mask(shape, (x0 - 2, y0 - 2, x1 + 2, y1 + 2)) - rect_mask(shape, r)
    left, top, right, bottom = edge_distances(shape, (x0 - 2, y0 - 2, x1 + 2, y1 + 2))
    upper_left = np.minimum(left, top) < np.minimum(right, bottom)
    rgb = np.where((lip > 0)[..., None] & upper_left[..., None], rgb * (1 - 0.28 * lip[..., None]), rgb)
    rgb = np.where((lip > 0)[..., None] & ~upper_left[..., None], rgb + (1 - rgb) * 0.45 * lip[..., None], rgb)

    # The panel itself.
    h, w = shape
    m = rect_mask(shape, r)
    tex = 1 + mottle * fbm(h, w, 70, 100 + seed, octaves=4) + mottle * 0.4 * fbm(h, w, 9, 300 + seed, octaves=2)
    color = np.asarray(fill)[None, None, :] * tex[..., None]
    left, top, right, bottom = edge_distances(shape, r)
    shadow = 0.30 * depth * np.exp(-np.minimum(top, left * 1.4) / 3.2)
    shadow += 0.07 * depth * np.exp(-np.minimum(top, left) / 14)
    # A faint light line along the inside of the lower and right edges.
    shine = 0.25 * np.exp(-np.minimum(right, bottom) / 1.2) * (np.minimum(top, left) > 3)
    color = color * (1 - shadow[..., None])
    color = color + (1 - color) * shine[..., None]
    rgb = rgb * (1 - m[..., None]) + color * m[..., None]
    rgb = outline(rgb, r, FRAME_DARK, 0.55)
    return rgb


def raised_plate(rgb, r, fill, radius=3.0, seed=0):
    """A plate standing out of the frame, with a soft shadow down-right."""
    shape = rgb.shape[:2]
    x0, y0, x1, y1 = r[:4]
    shadow = blur(rect_mask(shape, (x0 + 2, y0 + 2, x1 + 2, y1 + 2), radius), 2.0)
    rgb = rgb * (1 - 0.35 * shadow[..., None])
    m = rect_mask(shape, r, radius)
    h, w = shape
    tex = 1 + 0.02 * fbm(h, w, 40, 500 + seed, octaves=3)
    _, top, _, bottom = edge_distances(shape, r)
    grad = 1.04 - 0.08 * np.clip(top / max(1, y1 - y0), 0, 1)
    color = np.asarray(fill)[None, None, :] * (tex * grad)[..., None]
    rgb = rgb * (1 - m[..., None]) + color * m[..., None]
    rgb = bevel(rgb, r, 2.5, 0.45, raised=True, radius=radius)
    rgb = outline(rgb, r, FRAME_DARK, 0.6, radius)
    return rgb


def heading_tab(rgb, t):
    """The pale tab a panel's heading is written on, with a rounded end
    pointing into the panel and a soft shadow."""
    x0, y0, x1, y1, kind = t
    shape = rgb.shape[:2]
    radius = 7.0
    # Square ends where the tab meets the panel's edge.
    if kind == 'main':
        r = (x0 - radius - 2, y0, x1, y1)
    elif kind == 'right':
        r = (x0, y0, x1 + radius + 2, y1)
    else:
        r = (x0, y0, x1, y1)
    clip = rect_mask(shape, (x0, y0 - 3, x1, y1 + 5))
    shadow = blur(rect_mask(shape, (r[0] + 1.5, r[1] + 2, r[2] + 1.5, r[3] + 2), radius), 1.4) * clip
    rgb = rgb * (1 - 0.22 * shadow[..., None])
    m = rect_mask(shape, r, radius) * rect_mask(shape, (x0, y0, x1, y1))
    _, top, _, _ = edge_distances(shape, r)
    grad = 1.0 - 0.05 * np.clip(top / 15, 0, 1)
    color = TAB[None, None, :] * grad[..., None]
    rgb = rgb * (1 - m[..., None]) + color * m[..., None]
    # A thin warm line along its bottom, like a ribbon's fold.
    _, _, _, bottom = edge_distances(shape, r)
    fold = m * np.exp(-bottom / 0.8) * 0.25
    return rgb * (1 - fold[..., None]) + np.array([150, 132, 98]) / 255 * fold[..., None]


def engraved_line(rgb, y, x0, x1):
    """A line cut into a panel: dark with a light edge under it."""
    rgb = rgb.copy()
    dark = np.array([150, 136, 104]) / 255
    rgb[y, x0:x1] = rgb[y, x0:x1] * 0.6 + dark * 0.4
    rgb[y + 1, x0:x1] = rgb[y + 1, x0:x1] * 0.6 + 0.4
    return rgb


def window_base():
    """The empty window: the frame with its edges, title plate, name slot,
    icon well, navigation panel and close plate, but no page panels."""
    shape = (H, W)
    rgb = frame_material()
    alpha = rect_mask(shape, WINDOW, 6.0)
    # The window's edge: a dark line, a raised bevel, and an inner border
    # cut into the frame like a book's tooled cover.
    rgb = bevel(rgb, WINDOW, 5.0, 0.45, raised=True, radius=6.0)
    rgb = outline(rgb, WINDOW, FRAME_DARK, 1.0, 6.0)
    x0, y0, x1, y1 = WINDOW
    inner = (x0 + 8, y0 + 8, x1 - 8, y1 - 8)
    rgb = outline(rgb, inner, np.array([112, 104, 84]) / 255, 0.55, 4.0)
    rgb = outline(rgb, (inner[0] + 1, inner[1] + 1, inner[2] - 1, inner[3] - 1), np.array([212, 204, 182]) / 255, 0.55, 3.0)

    rgb = raised_plate(rgb, TITLE_PLATE, IVORY, radius=4.0, seed=1)
    rgb = sunken(rgb, NAME_SLOT, np.array([120, 110, 90]) / 255, seed=2, mottle=0.02)
    rgb = icon_well(rgb)
    rgb = sunken(rgb, NAV_PANEL, PAPER, seed=4, mottle=0.02)
    rgb = raised_plate(rgb, CLOSE_PLATE, IVORY, radius=5.0, seed=5)
    return rgb, alpha


def icon_well(rgb):
    """The well the entry's large icon sits in: dark, so an icon with
    transparent edges still has something behind it."""
    shape = rgb.shape[:2]
    rgb = sunken(rgb, ICON_WELL, np.array([96, 88, 72]) / 255, depth=0.8, seed=3, mottle=0.03)
    # Lighter towards the middle, like a lit niche.
    x0, y0, x1, y1 = ICON_WELL
    ys, xs = np.mgrid[0:shape[0], 0:shape[1]].astype(np.float32)
    d = np.hypot((xs - (x0 + x1) / 2 + 12) / 90, (ys - (y0 + y1) / 2 + 12) / 90)
    glow = np.clip(1 - d, 0, 1) ** 1.5 * 0.35 * rect_mask(shape, (x0 + 1, y0 + 1, x1 - 1, y1 - 1))
    return rgb + (1 - rgb) * glow[..., None] * 0.6


def page(base_rgb, spec, seed):
    rgb = base_rgb
    for i, (top, bottom) in enumerate(spec['panels']):
        rgb = sunken(rgb, (PANEL_X[0], top, PANEL_X[1], bottom), PARCHMENT, seed=seed * 10 + i)
    for t in spec['tabs']:
        rgb = heading_tab(rgb, t)
    for y in spec['rules']:
        rgb = engraved_line(rgb, y, 222, 649)
    return rgb


def build_pages():
    base_rgb, alpha = window_base()
    for n, (name, spec) in enumerate(PAGES.items()):
        save(name + '.png', page(base_rgb, spec, n), alpha)
    # A cover is the bare frame over the panels it hides, reaching a little
    # past them so their rims go too.
    for name, (page_name, indexes) in COVERS.items():
        panels = PAGES[page_name]['panels']
        top = min(panels[i][0] for i in indexes) - 3
        bottom = max(panels[i][1] for i in indexes) + 3
        r = (PANEL_X[0] - 3, top, PANEL_X[1] + 3, bottom)
        save(name + '.png', base_rgb, rect_mask((H, W), r))


# ---- Bars, buttons and arrows ----------------------------------------------

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def build_bars():
    bw, bh = 496, 30
    ys, xs = np.mgrid[0:bh, 0:bw].astype(np.float32)
    cream = np.array([246, 240, 222]) / 255
    for name, color in BAR_COLORS.items():
        c = np.array(color) / 255
        t = smoothstep(0.12, 0.85, xs / (bw - 1))
        rgb = cream * (1 - t[..., None]) + c * t[..., None]
        # Rounded like a ribbon: lighter along the top, darker at the bottom.
        v = (ys + 0.5) / bh
        rgb *= (1.04 - 0.12 * v ** 2)[..., None]
        rgb = rgb + (1 - rgb) * (0.25 * np.exp(-(ys - 1.5) ** 2 / 2.0))[..., None]
        tex = 1 + 0.015 * fbm(bh, bw, 30, 700 + len(name), octaves=2)
        rgb *= tex[..., None]
        edge = c * 0.62
        rgb[0] = rgb[0] * 0.4 + edge * 0.6
        rgb[-1] = rgb[-1] * 0.3 + edge * 0.7
        sheet = np.zeros((bh, 648, 3), np.float32)
        a = np.zeros((bh, 648), np.float32)
        sheet[:, :bw] = rgb
        a[:, :bw] = 1
        save(f'bar-{name}.png', sheet, a)


def supersampled_polygon(size, points, ss=8):
    """An anti-aliased mask of a polygon in a (w, h) box."""
    w, h = size
    img = Image.new('L', (w * ss, h * ss), 0)
    ImageDraw.Draw(img).polygon([(x * ss, y * ss) for x, y in points], fill=255)
    return np.asarray(img.resize((w, h), Image.LANCZOS)).astype(np.float32) / 255


STATE_COLORS = {
    'normal': np.array([210, 178, 108]) / 255,
    'hover': np.array([228, 132, 52]) / 255,
    'disabled': np.array([130, 158, 190]) / 255,
}


def arrow_sprite(direction, state):
    """A block arrow, faceted like a cut stone: the half facing the upper
    left is lit, the other shaded. 15x24 pointing left or right, or 24x15
    pointing up."""
    # Drawn pointing left in a 15x24 box, as (along, across) points: the
    # head's tip, its back corners, and the stem.
    shape = [(0.5, 12), (8.5, 0.8), (8.5, 6.5), (14.5, 6.5), (14.5, 17.5), (8.5, 17.5), (8.5, 23.2)]
    lit_side = [(0.5, 12), (8.5, 0.8), (8.5, 6.5), (14.5, 6.5), (14.5, 12)]
    if direction == 'up':
        w, h = 24, 15

        def place(p):
            return (p[1], p[0])
    else:
        w, h = 15, 24

        def place(p):
            return (w - p[0], p[1]) if direction == 'right' else p
    whole = supersampled_polygon((w, h), [place(p) for p in shape])
    # The upper half is lit (the left one, pointing up).
    lit = supersampled_polygon((w, h), [place(p) for p in lit_side])
    base = STATE_COLORS[state]
    rgb = np.zeros((h, w, 3), np.float32) + base * 0.74
    rgb = rgb * (1 - lit[..., None]) + np.clip(base * 1.1 + 0.04, 0, 1) * lit[..., None]
    # A dark rim so the arrow reads on the frame and on paper.
    eroded = np.asarray(Image.fromarray((whole * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3))) / 255
    rim = np.clip(whole - eroded, 0, 1)
    rgb = rgb * (1 - 0.6 * rim[..., None]) + np.array([64, 54, 40]) / 255 * 0.6 * rim[..., None]
    return rgb, whole


def put(sheet_rgb, sheet_a, sprite, x, y):
    rgb, a = sprite
    h, w = a.shape
    sheet_rgb[y:y + h, x:x + w] = rgb
    sheet_a[y:y + h, x:x + w] = a


def build_arrows():
    # The history's back and forward buttons, by the title.
    rgb = np.zeros((130, 170, 3), np.float32)
    a = np.zeros((130, 170), np.float32)
    for x, direction, state in [(37, 'left', 'normal'), (53, 'right', 'normal'), (69, 'left', 'hover'),
                                (85, 'right', 'hover'), (101, 'left', 'disabled'), (117, 'right', 'disabled')]:
        put(rgb, a, arrow_sprite(direction, state), x, 1)
    save('fwdBackarrows.png', rgb, a)

    # Up, Previous and Next, on the navigation panel.
    rgb = np.zeros((200, 200, 3), np.float32)
    a = np.zeros((200, 200), np.float32)
    for x, direction, state in [(1, 'left', 'normal'), (17, 'right', 'normal'), (33, 'left', 'hover'),
                                (49, 'right', 'hover'), (65, 'left', 'disabled'), (81, 'right', 'disabled')]:
        put(rgb, a, arrow_sprite(direction, state), x, 1)
    for x, state in [(1, 'normal'), (26, 'hover'), (51, 'disabled')]:
        put(rgb, a, arrow_sprite('up', state), x, 27)
    save('civilo_Up_Next_Back_ARROWS.png', rgb, a)


def build_descbox():
    """The Description/Effects button: normal, hover and pressed."""
    rgb = np.zeros((120, 310, 3), np.float32)
    a = np.zeros((120, 310), np.float32)
    w, h = 102, 17
    for x, state, raised in [(1, 'normal', True), (104, 'hover', True), (207, 'pressed', False)]:
        fill = {'normal': np.array([244, 238, 222]) / 255, 'hover': np.array([236, 178, 104]) / 255,
                'pressed': np.array([150, 176, 204]) / 255}[state]
        r = (0, 0, w, h)
        m = rect_mask((h, w), r, 4.0)
        _, top, _, _ = edge_distances((h, w), r)
        grad = (1.05 - 0.12 * np.clip(top / h, 0, 1)) if raised else (0.96 + 0.06 * np.clip(top / h, 0, 1))
        plate = fill[None, None, :] * grad[..., None]
        plate = bevel(plate, r, 2.0, 0.4, raised=raised, radius=4.0)
        plate = outline(plate, r, FRAME_DARK, 0.7, 4.0)
        rgb[1:1 + h, x:x + w] = plate
        a[1:1 + h, x:x + w] = m
    save('descbox.png', rgb, a)


# ---- Section icons ----------------------------------------------------------

class Icon:
    """A square icon drawn in a 128-unit space, supersampled, with shapes
    lit from the upper left, thin dark contours and soft shadows."""

    def __init__(self, size, backdrop, ss=4):
        self.size = size
        self.n = size * ss
        self.k = self.n / 128
        n = self.n
        ys, xs = np.mgrid[0:n, 0:n].astype(np.float32) / self.k
        self.xs, self.ys = xs, ys
        # The backdrop: a soft glow from the upper left over the color.
        top, bottom = np.array(backdrop[0]) / 255, np.array(backdrop[1]) / 255
        d = np.clip(np.hypot(xs - 40, ys - 34) / 120, 0, 1)
        self.rgb = top * (1 - d[..., None]) + bottom * d[..., None]
        self.rgb = self.rgb * (1 + 0.025 * fbm(n, n, 40 * ss, 900, octaves=3))[..., None]

    def _mask(self, draw):
        img = Image.new('L', (self.n, self.n), 0)
        draw(ImageDraw.Draw(img), self.k)
        return np.asarray(img).astype(np.float32) / 255

    def poly(self, points):
        return self._mask(lambda d, k: d.polygon([(x * k, y * k) for x, y in points], fill=255))

    def ellipse(self, box):
        return self._mask(lambda d, k: d.ellipse([v * k for v in box], fill=255))

    def rect(self, box, radius=0):
        return self._mask(lambda d, k: d.rounded_rectangle([v * k for v in box], radius=radius * k, fill=255))

    def arc(self, box, start, end, width):
        return self._mask(lambda d, k: d.arc([v * k for v in box], start, end, fill=255, width=int(width * k)))

    def bar(self, a, b, width):
        """A straight bar from a to b as a polygon (crisp ends)."""
        (x0, y0), (x1, y1) = a, b
        dx, dy = x1 - x0, y1 - y0
        length = math.hypot(dx, dy)
        nx, ny = -dy / length * width / 2, dx / length * width / 2
        return self.poly([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)])

    def shadow(self, mask, offset=(3, 4), radius=3.0, strength=0.38):
        ox, oy = int(offset[0] * self.k), int(offset[1] * self.k)
        shifted = np.zeros_like(mask)
        shifted[oy:, ox:] = mask[:self.n - oy, :self.n - ox]
        s = blur(shifted, radius * self.k)
        self.rgb = self.rgb * (1 - strength * s[..., None])

    def fill(self, mask, color, light=0.22, outline=1.1, contour=(40, 34, 28)):
        """Paints a shape: its color lighter towards the upper left of its
        own extent, and a thin dark contour."""
        c = np.array(color, np.float32) / 255
        ys, xs = np.nonzero(mask > 0.5)
        if len(xs) == 0:
            return
        x0, x1 = xs.min() / self.k, xs.max() / self.k
        y0, y1 = ys.min() / self.k, ys.max() / self.k
        t = ((self.xs - x0) / max(1, x1 - x0) * 0.45 + (self.ys - y0) / max(1, y1 - y0) * 0.55)
        shade = 1 + light * (0.5 - t)
        col = np.clip(c[None, None, :] * shade[..., None], 0, 1)
        if outline > 0:
            grown = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(
                ImageFilter.MaxFilter(int(outline * self.k) * 2 + 1))).astype(np.float32) / 255
            self.rgb = self.rgb * (1 - grown[..., None]) + np.array(contour) / 255 * grown[..., None]
        self.rgb = self.rgb * (1 - mask[..., None]) + col * mask[..., None]

    def paint(self, mask, color, alpha=1.0):
        """Paints flat color with no contour, for details."""
        m = mask[..., None] * alpha
        self.rgb = self.rgb * (1 - m) + np.array(color, np.float32) / 255 * m

    def result(self):
        img = Image.fromarray((np.clip(self.rgb, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')
        img = img.resize((self.size, self.size), Image.LANCZOS)
        rgb = np.asarray(img).astype(np.float32) / 255
        # A thin dark border, so the icon sits on any background.
        rgb[0, :] = rgb[-1, :] = rgb[:, 0] = rgb[:, -1] = rgb[0, 0] * 0.3 + np.array([40, 34, 28]) / 255 * 0.7
        rgb[1, 1:-1] = rgb[1, 1:-1] + (1 - rgb[1, 1:-1]) * 0.18
        rgb[1:-1, 1] = rgb[1:-1, 1] + (1 - rgb[1:-1, 1]) * 0.18
        return rgb, np.ones((self.size, self.size), np.float32)


def draw_main(ic):
    """The main menu: an open book."""
    back = ic.poly([(64, 46), (10, 40), (8, 100), (64, 108), (120, 100), (118, 40)])
    ic.shadow(back)
    ic.fill(back, (122, 44, 36), light=0.3)
    left = ic.poly([(64, 44), (50, 37), (30, 34), (14, 38), (14, 96), (30, 92), (50, 94), (64, 102)])
    right = ic.poly([(64, 44), (78, 37), (98, 34), (114, 38), (114, 96), (98, 92), (78, 94), (64, 102)])
    ic.fill(left, (242, 234, 212), light=0.12, outline=0.8)
    ic.fill(right, (232, 222, 196), light=0.12, outline=0.8)
    for i in range(6):
        y = 48 + i * 8
        ic.paint(ic.bar((21, y - 1), (57, y + 1 - i * 0.2), 1.6), (120, 108, 88), 0.55)
        ic.paint(ic.bar((71, y + 1 - i * 0.2), (107, y - 1), 1.6), (120, 108, 88), 0.55)
    ic.paint(ic.bar((64, 44), (64, 102), 2.0), (90, 70, 50), 0.6)


def draw_index(ic):
    """The index: a list under a magnifying glass."""
    page = ic.poly([(22, 18), (84, 18), (84, 110), (22, 110)])
    ic.shadow(page)
    ic.fill(page, (240, 234, 216), light=0.12)
    for i in range(9):
        y = 30 + i * 9
        ic.paint(ic.bar((30, y), (76 - (i % 3) * 10, y), 2.2), (110, 100, 84), 0.65)
    ring = ic.ellipse((52, 46, 106, 100))
    lens = ic.ellipse((59, 53, 99, 93))
    handle = ic.bar((96, 90), (116, 112), 10)
    ic.shadow(np.maximum(ring, handle))
    ic.fill(handle, (92, 60, 38), light=0.35)
    ic.fill(ring, (176, 140, 74), light=0.45)
    ic.paint(lens, (196, 214, 222), 0.75)
    ic.paint(ic.ellipse((64, 58, 80, 70)), (255, 255, 255), 0.55)


def draw_tribes(ic):
    pole = ic.bar((38, 18), (38, 118), 6)
    flag = ic.poly([(41, 24), (110, 28), (94, 51), (110, 74), (41, 78)])
    ic.shadow(np.maximum(pole, flag))
    ic.fill(pole, (120, 84, 50), light=0.3)
    ic.fill(flag, (168, 52, 44), light=0.3)
    ic.fill(ic.ellipse((32, 9, 44, 21)), (214, 178, 84), light=0.5)
    ic.paint(ic.poly([(41, 24), (110, 28), (106, 34), (41, 30)]), (255, 230, 200), 0.18)
    emblem = ic.ellipse((56, 38, 82, 64))
    ic.fill(emblem, (222, 186, 92), light=0.4, outline=0.8)
    ic.paint(ic.poly([(69, 42), (72, 49), (79, 50), (73, 55), (75, 62), (69, 58), (63, 62), (65, 55), (59, 50), (66, 49)]),
             (150, 44, 36), 0.85)


def draw_advances(ic):
    sheet = ic.poly([(30, 30), (98, 30), (98, 96), (30, 96)])
    top = ic.rect((22, 20, 106, 36), 7)
    bottom = ic.rect((22, 90, 106, 106), 7)
    ic.shadow(np.maximum(sheet, np.maximum(top, bottom)))
    ic.fill(sheet, (240, 230, 204), light=0.12)
    for i in range(6):
        y = 44 + i * 8
        ic.paint(ic.bar((38, y), (86 - (i % 2) * 14, y), 2.0), (110, 98, 80), 0.6)
    ic.fill(top, (204, 178, 128), light=0.4)
    ic.fill(bottom, (204, 178, 128), light=0.4)
    quill = ic.poly([(112, 10), (100, 18), (80, 48), (70, 72), (74, 74), (88, 52), (108, 24)])
    ic.shadow(quill, strength=0.3)
    ic.fill(quill, (236, 236, 228), light=0.25, outline=0.8)
    ic.paint(ic.bar((110, 12), (71, 74), 1.4), (140, 130, 110), 0.8)


def draw_great_wonders(ic):
    ground = ic.poly([(0, 96), (128, 92), (128, 128), (0, 128)])
    ic.paint(ground, (196, 170, 116))
    ic.paint(ic.poly([(70, 100), (114, 100), (128, 104), (128, 108), (78, 106)]), (120, 98, 64), 0.45)
    lit = ic.poly([(60, 22), (12, 100), (68, 104)])
    dark = ic.poly([(60, 22), (68, 104), (114, 100)])
    ic.fill(np.maximum(lit, dark), (220, 196, 140), light=0.0)
    ic.paint(lit, (232, 210, 156))
    ic.paint(dark, (176, 148, 98))
    for i in range(1, 8):
        y = 22 + i * 10
        t = (y - 22) / 80
        ic.paint(ic.bar((60 - 48 * t, y), (60 + 8 * t, y + 4 * t / 1.0), 0.9), (150, 124, 82), 0.5)
        ic.paint(ic.bar((60 + 8 * t, y + 4 * t), (60 + 54 * t, y - 2 * t), 0.9), (120, 98, 64), 0.5)


def draw_small_wonders(ic):
    ic.paint(ic.poly([(0, 104), (128, 100), (128, 128), (0, 128)]), (88, 112, 128))
    glow = np.clip(1 - np.hypot(ic.xs - 64, ic.ys - 30) / 40, 0, 1) ** 2
    ic.rgb = ic.rgb + (1 - ic.rgb) * glow[..., None] * 0.8
    rocks = ic.poly([(30, 112), (40, 98), (88, 96), (100, 112)])
    tower = ic.poly([(52, 42), (76, 42), (84, 102), (44, 102)])
    ic.shadow(np.maximum(tower, rocks))
    ic.fill(rocks, (120, 112, 100), light=0.3)
    ic.fill(tower, (232, 226, 210), light=0.25)
    for y0 in (56, 78):
        t0, t1 = (y0 - 42) / 60, (y0 + 10 - 42) / 60
        ic.paint(ic.poly([(52 - 8 * t0, y0), (76 + 8 * t0, y0), (76 + 8 * t1, y0 + 10), (52 - 8 * t1, y0 + 10)]),
                 (170, 70, 56), 0.9)
    ic.fill(ic.rect((48, 36, 80, 44), 1), (110, 96, 80), light=0.2)
    ic.fill(ic.rect((53, 22, 75, 37), 2), (250, 226, 150), light=0.3, outline=0.9)
    ic.fill(ic.poly([(50, 23), (64, 10), (78, 23)]), (90, 78, 66), light=0.3)


def draw_improvements(ic):
    wall = ic.poly([(30, 62), (98, 62), (98, 106), (30, 106)])
    towers = [ic.poly([(12, 40), (42, 40), (42, 106), (12, 106)]), ic.poly([(86, 40), (116, 40), (116, 106), (86, 106)])]
    crenels = []
    for x in range(30, 98, 12):
        crenels.append(ic.rect((x + 2, 54, x + 9, 63)))
    for tx in (12, 86):
        for i in range(3):
            crenels.append(ic.rect((tx + 1 + i * 10.5, 32, tx + 8 + i * 10.5, 41)))
    everything = np.maximum.reduce([wall] + towers + crenels)
    ic.shadow(everything)
    ic.fill(everything, (204, 192, 166), light=0.3)
    for y in range(70, 104, 9):
        ic.paint(ic.bar((14, y), (114, y), 0.8), (150, 138, 112), 0.5)
    gate = np.maximum(ic.ellipse((52, 72, 76, 96)), ic.rect((52, 84, 76, 106)))
    ic.fill(gate, (70, 54, 40), light=0.2, outline=0.6)
    for tx in (22, 96):
        ic.paint(ic.rect((tx, 54, tx + 6, 66), 3), (60, 50, 40), 0.85)


def draw_governments(ic):
    steps = [ic.rect((14, 98, 114, 106)), ic.rect((20, 91, 108, 99))]
    roof = ic.poly([(16, 44), (64, 18), (112, 44)])
    beam = ic.rect((20, 43, 108, 52))
    columns = [ic.rect((x - 4.5, 52, x + 4.5, 92)) for x in (30, 47, 64, 81, 98)]
    everything = np.maximum.reduce(steps + [roof, beam] + columns)
    ic.shadow(everything)
    for m in steps + [beam]:
        ic.fill(m, (226, 222, 210), light=0.25, outline=0.8)
    ic.fill(roof, (232, 228, 216), light=0.3, outline=0.8)
    for m in columns:
        ic.fill(m, (238, 234, 222), light=0.5, outline=0.8)
    ic.paint(ic.poly([(32, 41), (64, 25), (96, 41)]), (190, 184, 168), 0.6)


def draw_units(ic):
    blades = [ic.bar((20, 20), (104, 104), 7), ic.bar((108, 20), (24, 104), 7)]
    guards = [ic.bar((92, 82), (82, 92), 6), ic.bar((36, 82), (46, 92), 6)]
    ic.shadow(np.maximum.reduce(blades))
    for b in blades:
        ic.fill(b, (210, 214, 220), light=0.5, outline=0.8)
    for g in guards:
        ic.fill(g, (196, 160, 80), light=0.4, outline=0.8)
    for grip in (ic.bar((96, 96), (108, 108), 6), ic.bar((32, 96), (20, 108), 6)):
        ic.fill(grip, (110, 70, 42), light=0.3, outline=0.8)
    # A heater shield: straight sides, then curving in to a point.
    steps = np.linspace(0, 1, 12)
    right = [(98 - 34 * s, 62 + 44 * math.sin(s * math.pi / 2)) for s in steps]
    left = [(128 - x, y) for x, y in reversed(right[:-1])]
    pts = [(30, 24), (98, 24)] + right + left
    shield = ic.poly(pts)
    ic.shadow(shield)
    ic.fill(shield, (200, 168, 86), light=0.4)
    inner = ic.poly([(64 + (x - 64) * 0.84, 62 + (y - 62) * 0.86) for x, y in pts])
    ic.fill(inner, (92, 82, 148), light=0.35, outline=0)
    ic.paint(ic.bar((64, 34), (64, 92), 6), (214, 186, 104), 0.9)
    ic.paint(ic.bar((42, 54), (86, 54), 6), (214, 186, 104), 0.9)


def draw_worker_actions(ic):
    ic.paint(ic.poly([(0, 100), (128, 96), (128, 128), (0, 128)]), (118, 98, 66))
    pick_handle = ic.bar((26, 110), (90, 26), 6)
    shovel_handle = ic.bar((102, 106), (44, 30), 6)
    pick_head = ic.poly([(58, 26), (74, 16), (92, 14), (112, 22), (122, 36), (108, 30), (92, 26), (78, 30), (66, 36)])
    # The spade, laid along the shovel's handle beyond its top end.
    end, start = np.array([44.0, 30.0]), np.array([102.0, 106.0])
    u = (end - start) / np.linalg.norm(end - start)
    v = np.array([-u[1], u[0]])
    spade = [(0, -5), (3, -12), (24, -12), (32, 0), (24, 12), (3, 12), (0, 5)]
    blade = ic.poly([tuple(end - u * 2 + u * a + v * b) for a, b in spade])
    grip = ic.bar(tuple(start - u * 3 + v * 8), tuple(start - u * 3 - v * 8), 6)
    every = np.maximum.reduce([pick_handle, shovel_handle, pick_head, blade, grip])
    ic.shadow(every)
    ic.fill(shovel_handle, (150, 106, 62), light=0.3, outline=0.8)
    ic.fill(grip, (120, 84, 50), light=0.3, outline=0.8)
    ic.fill(blade, (170, 176, 184), light=0.5, outline=0.8)
    ic.fill(pick_handle, (160, 114, 68), light=0.3, outline=0.8)
    ic.fill(pick_head, (150, 156, 164), light=0.5, outline=0.8)


def draw_terrain(ic):
    sky = np.clip((ic.ys - 0) / 90, 0, 1)
    ic.rgb = np.array([150, 182, 210]) / 255 * (1 - sky[..., None]) + np.array([214, 222, 214]) / 255 * sky[..., None]
    far = ic.poly([(0, 80), (20, 58), (36, 66), (52, 50), (66, 62), (128, 70), (128, 90), (0, 90)])
    ic.paint(far, (150, 168, 150))
    mountain_lit = ic.poly([(74, 22), (36, 92), (80, 92)])
    mountain_dark = ic.poly([(74, 22), (80, 92), (116, 92)])
    ic.shadow(np.maximum(mountain_lit, mountain_dark), strength=0.25)
    ic.fill(np.maximum(mountain_lit, mountain_dark), (140, 128, 112), light=0)
    ic.paint(mountain_lit, (154, 140, 122))
    ic.paint(mountain_dark, (112, 100, 88))
    ic.paint(ic.poly([(74, 22), (63, 42), (70, 40), (76, 46), (82, 40), (88, 44)]), (244, 244, 240))
    hills = ic.poly([(0, 128)] + [(x, 92 - 14 * math.sin(x / 128 * math.pi * 1.6 + 0.3)) for x in range(0, 129, 4)] + [(128, 128)])
    ic.fill(hills, (112, 146, 80), light=0.25, outline=0.8)
    ic.paint(ic.poly([(0, 128)] + [(x, 110 - 6 * math.sin(x / 128 * math.pi * 2.4)) for x in range(0, 129, 4)] + [(128, 128)]),
             (96, 128, 66))


def draw_resources(ic):
    crown = ic.poly([(40, 30), (88, 30), (110, 52), (18, 52)])
    pavilion = ic.poly([(18, 52), (110, 52), (64, 112)])
    gem = np.maximum(crown, pavilion)
    ic.shadow(gem)
    ic.fill(gem, (176, 46, 58), light=0.0)
    facets = [
        ([(40, 30), (88, 30), (78, 52), (50, 52)], (220, 92, 100)),
        ([(18, 52), (40, 30), (50, 52)], (200, 70, 80)),
        ([(88, 30), (110, 52), (78, 52)], (150, 34, 46)),
        ([(18, 52), (50, 52), (64, 112)], (178, 48, 60)),
        ([(50, 52), (78, 52), (64, 112)], (150, 36, 48)),
        ([(78, 52), (110, 52), (64, 112)], (118, 26, 38)),
    ]
    for pts, color in facets:
        ic.paint(ic.poly(pts), color)
    for a, b in [((40, 30), (50, 52)), ((88, 30), (78, 52)), ((18, 52), (110, 52)), ((50, 52), (64, 112)), ((78, 52), (64, 112))]:
        ic.paint(ic.bar(a, b, 1.0), (250, 200, 200), 0.35)
    ic.paint(ic.poly([(46, 34), (60, 34), (54, 46)]), (255, 240, 240), 0.6)


def draw_concepts(ic):
    # The hook goes from the left over the top and round to the bottom,
    # where the stem drops from it.
    hook = ic.arc((34, 12, 94, 72), 175, 450, 15)
    stem = ic.rect((57, 64, 71, 82), 2)
    dot = ic.ellipse((53, 89, 75, 111))
    mark = np.maximum.reduce([hook, stem, dot])
    ic.shadow(mark)
    ic.fill(mark, (226, 184, 82), light=0.5, outline=1.2)


SECTION_ICONS = [
    # (large file, small file, backdrop light, backdrop dark, drawing)
    ('menu_red large', 'menu_red small', (204, 120, 96), (122, 52, 40), draw_main),
    ('menus large', 'menus small', (176, 188, 198), (92, 104, 118), draw_index),
    ('Races/MenuLarge', 'Races/MenuSMALL', (238, 206, 140), (176, 124, 62), draw_tribes),
    ('Techlarge', 'Techsmall', (160, 182, 214), (70, 94, 138), draw_advances),
    ('Wonderslarge', 'Wonderssmall', (246, 230, 186), (208, 182, 124), draw_great_wonders),
    ('swonderlarge', 'swondersmall', (156, 170, 186), (64, 80, 102), draw_small_wonders),
    ('Cityimpslarge', 'Cityimpssmall', (230, 196, 150), (168, 116, 70), draw_improvements),
    ('Govtlarge', 'Govtsmall', (188, 196, 204), (96, 104, 114), draw_governments),
    ('Unitlarge', 'Unitsmall', (184, 176, 210), (90, 82, 124), draw_units),
    ('WorkerActionslarge', 'WorkerActionssmall', (196, 216, 170), (100, 132, 84), draw_worker_actions),
    ('Terrainlarge', 'Terrainsmall', (170, 196, 214), (120, 150, 170), draw_terrain),
    ('Resourceslarge', 'Resourcessmall', (190, 214, 170), (92, 124, 80), draw_resources),
    ('concepts large', 'concepts small', (110, 112, 120), (46, 48, 56), draw_concepts),
]


def build_icons():
    for large, small, light, dark, draw in SECTION_ICONS:
        for name, size in ((large, 128), (small, 32)):
            ic = Icon(size, (light, dark), ss=4 if size == 128 else 12)
            draw(ic)
            rgb, a = ic.result()
            save(os.path.join('Icons', name + '.png'), rgb, a)


if __name__ == '__main__':
    build_pages()
    build_bars()
    build_arrows()
    build_descbox()
    build_icons()
    print('wrote', OUT)
