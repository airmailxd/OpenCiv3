"""The Espionage screen's own art (Shift+E): its background and a sheet of
its parts.

    python ArtGen/screens/build.py   (or espionage.py alone)

Writes, into C7/ModernArt/Art/Espionage (twice the size the screen shows
them at, like all of ModernArt):

    EspionageBackground.png   the screen, 1024x768
    EspionageParts.png        its buttons, check boxes and marks, 256x88

C7/Lua/standalone/textures.lua names them (textures.espionage) and
C7/UIElements/Diplomacy/EspionageScreen.cs places its words and controls on
them, with the layout below.

The background is the advisor screens' own (the asset pack's sandstone
frame and paper page, C7/Assets/Art/Advisors/foreign.png) with the page's
cut-away corner filled in, and on the page the advisors' kind of furniture:
taupe bands that head each column (as on the Domestic and Military
advisors' pages) and two white cards with grey heads and a black rule round
them (as at the foot of the Military advisor's). The parts are drawn here:

    row 0   the plate a list's entry or choice is drawn on (normal, hover,
            selected, disabled), 64x32 each, stretched by its 8-pixel
            margins to any size
    row 1   the raised button Execute and Cancel are (normal, hover,
            pressed, disabled), 64x32 each, stretched the same way
    row 2   the mission check box (normal, hover, checked, checked under
            the mouse, disabled), then the embassy and spy marks, 24x24 each
"""
import importlib.util
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
PACK = os.path.join(REPO, 'C7', 'Assets', 'Art', 'Advisors', 'foreign.png')
OUT_DIR = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Espionage')

_spec = importlib.util.spec_from_file_location('pedia_art', os.path.join(HERE, '..', 'civilopedia', 'build.py'))
pedia = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pedia)

SCALE = 2

# The layout, in the screen's pixels (x0, y0, x1, y1). EspionageScreen.cs
# has the same numbers.
BANDS = [
    (80, 78, 380, 102),     # Rivals
    (396, 78, 680, 102),    # Missions
    (696, 78, 968, 102),    # Target City
    (696, 452, 968, 476),   # Operational Cost
]
CARDS = [
    (396, 340, 680, 546),   # the briefing: the mission picked, or why it can't be sent
    (396, 560, 680, 704),   # our assets
]
CARD_HEAD = 22

# In the asset pack's background: the page's cut-away corner (the top right,
# where the sandstone shows) and how far down the page's lower part, with
# its own top right corner, is copied to fill it.
NOTCH = (826, 0, 1024, 233)
NOTCH_FROM_DY = 154
FEATHER = 6
# The page (its paper), and the shadow it casts: how dark next to the page,
# and how far out it fades to a third of that.
PAGE = (67, 65, 982, 716)
SHADOW_DEPTH = 0.55
SHADOW_REACH = 5.0

INK = np.array([40, 28, 16]) / 255
PAPER_WHITE = np.array([242, 240, 229]) / 255
CARD_GREY = np.array([195, 193, 182]) / 255


def load_pack():
    return np.asarray(Image.open(PACK).convert('RGB')).astype(np.float32) / 255


def fill_notch(rgb):
    """The page made whole. Where the corner was cut away, the page is the
    same columns lower down (the paper, the page's right edge and the
    shadow it casts to the right), blended in at the bottom. Above it the
    sandstone gets the shadow the page's top edge casts: copied from just
    left of the corner, then drawn as the original's falls off."""
    x0, y0, x1, y1 = NOTCH
    out = rgb.copy()
    page_top = PAGE[1]
    patch = rgb[page_top + NOTCH_FROM_DY:y1 + NOTCH_FROM_DY, x0:x1]
    h, w = patch.shape[:2]
    ys = np.mgrid[0:h, 0:w][0].astype(np.float32)
    k = np.clip((h - 1 - ys) / FEATHER, 0, 1)[..., None]
    out[page_top:y1, x0:x1] = patch * k + rgb[page_top:y1, x0:x1] * (1 - k)
    # Above the page, the sandstone is shadowed by the page's top edge
    # running on to the page's new corner, where it was shadowed round the
    # cut-away part's corner: its darkening by the distance to the one is
    # traded for the darkening by the distance to the other.
    def shadow(d):
        return 1 - SHADOW_DEPTH * np.exp(-d / SHADOW_REACH)
    ys, xs = np.mgrid[y0:page_top, x0:x1].astype(np.float32) + 0.5
    before = np.hypot(np.maximum(xs - x0, 0), page_top - ys)
    after = np.hypot(np.maximum(xs - PAGE[2], 0), page_top - ys)
    out[y0:page_top, x0:x1] = rgb[y0:page_top, x0:x1] * (shadow(after) / shadow(before))[..., None]
    return out


def upscale(rgb):
    h, w = rgb.shape[:2]
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')
    return np.asarray(img.resize((w * SCALE, h * SCALE), Image.LANCZOS).convert('RGB')).astype(np.float32) / 255


def scaled(r):
    return tuple(v * SCALE for v in r)


def band(rgb, r):
    """A taupe band across the page, the paper darkened under it as the
    advisors' are."""
    m = pedia.rect_mask(rgb.shape[:2], scaled(r), 2.0 * SCALE)
    tint = np.array([0.81, 0.80, 0.77])
    return rgb * (1 - m[..., None]) + rgb * tint * m[..., None]


def card(rgb, r):
    """A white card with a grey head and a black rule round it, a soft
    shadow down and to the right."""
    shape = rgb.shape[:2]
    x0, y0, x1, y1 = scaled(r)
    shadow = pedia.blur(pedia.rect_mask(shape, (x0 + 3 * SCALE, y0 + 3 * SCALE, x1 + 3 * SCALE, y1 + 3 * SCALE)), 1.6 * SCALE)
    rgb = rgb * (1 - 0.30 * shadow[..., None])
    m = pedia.rect_mask(shape, (x0, y0, x1, y1))
    # The card's paper: faintly mottled.
    tex = 1 + 0.012 * pedia.fbm(shape[0], shape[1], 30 * SCALE, 900 + int(y0), octaves=2)
    body = PAPER_WHITE[None, None, :] * tex[..., None]
    rgb = rgb * (1 - m[..., None]) + body * m[..., None]
    head = pedia.rect_mask(shape, (x0, y0, x1, y0 + CARD_HEAD * SCALE))
    rgb = rgb * (1 - head[..., None]) + CARD_GREY * tex[..., None] * head[..., None]
    # The rule under the head and round the card.
    under = pedia.rect_mask(shape, (x0, y0 + CARD_HEAD * SCALE, x1, y0 + CARD_HEAD * SCALE + 1))
    rgb = rgb * (1 - 0.35 * under[..., None])
    # A line one screen pixel wide, as the Military advisor's cards have.
    ring = np.clip(m - pedia.rect_mask(shape, (x0 + SCALE, y0 + SCALE, x1 - SCALE, y1 - SCALE)), 0, 1)
    return rgb * (1 - ring[..., None])


def background():
    rgb = upscale(fill_notch(load_pack()))
    for r in BANDS:
        rgb = band(rgb, r)
    for r in CARDS:
        rgb = card(rgb, r)
    return to_image(rgb, np.ones(rgb.shape[:2], np.float32))


# ---- The parts --------------------------------------------------------------

SHEET = (256, 88)


def to_image(rgb, alpha):
    rgba = np.dstack([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)[..., None]])
    rgba = (rgba * 255 + 0.5).astype(np.uint8)
    rgba[rgba[..., 3] == 0, :3] = 0
    return Image.fromarray(rgba, 'RGBA')


class Sprite:
    """A transparent sprite w x h screen pixels, at twice that, painted in
    straight (not premultiplied) color."""

    def __init__(self, w, h):
        self.shape = (h * SCALE, w * SCALE)
        self.rgb = np.zeros(self.shape + (3,), np.float32)
        self.a = np.zeros(self.shape, np.float32)

    def over(self, color, alpha):
        c = np.asarray(color, np.float32)
        if c.ndim == 1:
            c = c[None, None, :]
        a = np.clip(alpha, 0, 1)
        out_a = a + self.a * (1 - a)
        rgb = (c * a[..., None] + self.rgb * (self.a * (1 - a))[..., None]) / np.maximum(out_a, 1e-6)[..., None]
        self.rgb, self.a = rgb, out_a

    def rect(self, r, radius=0.0):
        return pedia.rect_mask(self.shape, scaled(r), radius * SCALE)

    def ring(self, r, width, radius=0.0):
        x0, y0, x1, y1 = r
        inner = (x0 + width, y0 + width, x1 - width, y1 - width)
        return np.clip(self.rect(r, radius) - self.rect(inner, max(0.0, radius - width)), 0, 1)

    def poly(self, points, ss=4):
        """An anti-aliased mask of a polygon, in screen pixels."""
        h, w = self.shape
        ys, xs = np.mgrid[0:h * ss, 0:w * ss].astype(np.float32)
        xs = (xs + 0.5) / (ss * SCALE)
        ys = (ys + 0.5) / (ss * SCALE)
        inside = np.zeros(xs.shape, bool)
        n = len(points)
        for i in range(n):
            (xa, ya), (xb, yb) = points[i], points[(i + 1) % n]
            if ya == yb:
                continue
            crosses = ((ya > ys) != (yb > ys)) & (xs < (xb - xa) * (ys - ya) / (yb - ya) + xa)
            inside ^= crosses
        return inside.reshape(h, ss, w, ss).mean(axis=(1, 3)).astype(np.float32)

    def disc(self, cx, cy, r):
        h, w = self.shape
        ys, xs = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
        d = np.hypot(xs / SCALE - cx, ys / SCALE - cy)
        return np.clip((r - d) * SCALE + 0.5, 0, 1)

    def grow(self, mask, by):
        """The mask widened by `by` screen pixels (a soft max)."""
        n = int(round(by * SCALE))
        out = mask.copy()
        for dy in range(-n, n + 1):
            for dx in range(-n, n + 1):
                if dx * dx + dy * dy <= n * n:
                    out = np.maximum(out, np.roll(np.roll(mask, dy, 0), dx, 1))
        return out


def rgb255(*c):
    return np.array(c, np.float32) / 255


# The plates: (fill, its opacity, rim, its opacity, rim width).
PLATES = {
    'normal': (rgb255(110, 95, 70), 0.30, rgb255(92, 78, 56), 0.55, 1.0),
    'hover': (rgb255(214, 160, 70), 0.42, rgb255(160, 100, 30), 0.85, 1.0),
    'selected': (rgb255(160, 100, 42), 0.50, rgb255(226, 136, 28), 1.0, 2.0),
    'disabled': (rgb255(150, 144, 132), 0.18, rgb255(130, 124, 112), 0.50, 1.0),
}


def plate(state):
    fill, fill_a, rim, rim_a, rim_w = PLATES[state]
    s = Sprite(64, 32)
    r = (0.5, 0.5, 63.5, 31.5)
    body = s.rect(r, 5)
    s.over(fill, body * fill_a)
    # Light catching the upper edge, as on the advisors' raised plates.
    if state != 'disabled':
        top = s.rect((3, 1.5, 61, 2.5)) * 0.22
        s.over(np.ones(3), top * body)
    s.over(rim, s.ring(r, rim_w, 5) * rim_a)
    return s


# The buttons: (face, outline, opacity, raised).
BUTTONS = {
    'normal': (rgb255(232, 225, 204), rgb255(58, 54, 44), 1.0, True),
    'hover': (rgb255(244, 220, 156), rgb255(120, 80, 24), 1.0, True),
    'pressed': (rgb255(198, 182, 146), rgb255(58, 54, 44), 1.0, False),
    'disabled': (rgb255(214, 210, 200), rgb255(150, 145, 135), 0.75, True),
}


def button(state):
    face, line, opacity, raised = BUTTONS[state]
    s = Sprite(64, 32)
    r = (0.5, 0.5, 63.5, 31.5)
    body = s.rect(r, 4)
    ys = (np.mgrid[0:s.shape[0], 0:s.shape[1]][0].astype(np.float32) + 0.5) / s.shape[0]
    grad = (1.05 - 0.10 * ys) if raised else (0.95 + 0.08 * ys)
    s.over(face[None, None, :] * grad[..., None], body * opacity)
    if state != 'disabled':
        # A bevel along the top and the bottom only, so the button can be
        # stretched to any width or height.
        light = s.rect((3, 1.5, 61, 3.0))
        dark = s.rect((3, 29.0, 61, 30.5))
        if raised:
            s.over(np.ones(3), light * 0.55)
            s.over(np.zeros(3), dark * 0.18)
        else:
            s.over(np.zeros(3), light * 0.22)
            s.over(np.ones(3), dark * 0.30)
    s.over(line, s.ring(r, 1.0, 4) * opacity)
    return s


def check_box(state):
    s = Sprite(24, 24)
    r = (3.5, 3.5, 20.5, 20.5)
    hover = state in ('hover', 'checked_hover')
    disabled = state == 'disabled'
    face = rgb255(225, 220, 210) if disabled else rgb255(255, 242, 206) if hover else rgb255(250, 246, 232)
    s.over(face, s.rect(r, 3))
    if not disabled:
        # Set into the page: a shadow along its inside top and left.
        s.over(np.zeros(3), s.ring(r, 2.0, 3) * s.rect((0, 0, 24, 11)) * 0.18)
    rim = rgb255(150, 145, 135) if disabled else rgb255(196, 116, 28) if hover else rgb255(70, 60, 45)
    s.over(rim, s.ring(r, 1.5 if hover else 1.0, 3) * (0.7 if disabled else 1.0))
    if state.startswith('checked'):
        tick = s.poly([(6.5, 12.0), (9.0, 9.6), (11.2, 12.4), (17.2, 5.6), (19.6, 7.8), (11.2, 17.4)])
        s.over(INK, tick)
    return s


def embassy():
    """A building with columns and a pediment: an embassy."""
    s = Sprite(24, 24)
    blue = rgb255(36, 64, 124)
    shape = np.maximum.reduce([
        s.poly([(3.0, 9.0), (12.0, 3.0), (21.0, 9.0)]),       # the pediment
        s.rect((3.5, 9.0, 20.5, 10.8)),                       # the architrave
        s.rect((5.0, 11.5, 7.4, 17.5)),                       # four columns
        s.rect((9.0, 11.5, 11.4, 17.5)),
        s.rect((12.6, 11.5, 15.0, 17.5)),
        s.rect((16.6, 11.5, 19.0, 17.5)),
        s.rect((3.5, 18.0, 20.5, 19.6)),                      # the steps
        s.rect((2.5, 19.8, 21.5, 21.4)),
    ])
    # A pale halo, so the mark reads on any plate.
    s.over(rgb255(250, 244, 226), s.grow(shape, 1.0) * 0.9)
    s.over(blue, shape)
    return s


def spy():
    """An open eye: a spy."""
    s = Sprite(24, 24)
    n = 24
    upper = [(2.5 + 19 * i / n, 12 - 6.2 * np.sin(np.pi * i / n)) for i in range(n + 1)]
    lower = [(21.5 - 19 * i / n, 12 + 6.2 * np.sin(np.pi * i / n)) for i in range(1, n)]
    eye = s.poly(upper + lower)
    s.over(rgb255(250, 244, 226), s.grow(eye, 1.0) * 0.9)
    s.over(rgb255(60, 26, 22), eye)
    s.over(rgb255(252, 248, 236), s.poly([(x, y + (12 - y) * 0.28) for x, y in upper + lower]) * eye)
    s.over(rgb255(150, 34, 30), s.disc(12, 12, 4.0))
    s.over(rgb255(24, 10, 8), s.disc(12, 12, 1.8))
    s.over(np.ones(3), s.disc(10.8, 10.8, 0.9) * 0.85)
    return s


def put(sheet_rgb, sheet_a, sprite, x, y):
    h, w = sprite.shape
    x, y = x * SCALE, y * SCALE
    sheet_rgb[y:y + h, x:x + w] = sprite.rgb
    sheet_a[y:y + h, x:x + w] = sprite.a


def parts():
    w, h = SHEET
    rgb = np.zeros((h * SCALE, w * SCALE, 3), np.float32)
    a = np.zeros((h * SCALE, w * SCALE), np.float32)
    for i, state in enumerate(('normal', 'hover', 'selected', 'disabled')):
        put(rgb, a, plate(state), i * 64, 0)
    for i, state in enumerate(('normal', 'hover', 'pressed', 'disabled')):
        put(rgb, a, button(state), i * 64, 32)
    for i, state in enumerate(('normal', 'hover', 'checked', 'checked_hover', 'disabled')):
        put(rgb, a, check_box(state), i * 24, 64)
    put(rgb, a, embassy(), 128, 64)
    put(rgb, a, spy(), 152, 64)
    return to_image(rgb, a)


def build():
    os.makedirs(OUT_DIR, exist_ok=True)
    out = []
    for name, make in (('EspionageBackground.png', background), ('EspionageParts.png', parts)):
        img = make()
        path = os.path.join(OUT_DIR, name)
        img.save(path, optimize=True)
        print('wrote', path)
        out.append(img)
    return out


if __name__ == '__main__':
    build()
