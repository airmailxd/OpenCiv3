"""Draws the base terrain tile sheets (Art/Terrain/x*.pcx land "triple sheets" and
w*.pcx water sheets) for the Graphics Overhaul.

How the game uses them (C7Engine/TerrainTextureFiles.cs, Lua terrain.lua, MapView.cs):
a sheet is 9x9 cells of 128x64 diamonds. The diamond drawn for a tile T sits half a
tile above T: its four corners are the centers of the tiles N (top), NW (left), NE
(right) and T itself (bottom). A sheet holds three terrain types (first, middle,
end), and the cell index is N + 3*NW + 9*NE + 27*T, each digit 0/1/2 for the
terrain at that corner. wSSS/wOOO hold 81 random variants of all-sea/all-ocean.

How this script draws them: everything is a function of position on the ground.
In "ground" coordinates (a, b) a diamond is the unit square with corners
N=(0,1), NW=(0,0), NE=(1,1), T=(1,0), and the tile centers are the integer points.

* Each terrain's surface texture is periodic with period 1 in a and b, so it
  continues across tile edges whichever cells are placed side by side.
* Which terrain covers a point is decided by smooth weights of the four corners
  (see "corner weights" below): each corner's weight falls to zero on the far
  edges, so along an edge only that edge's two corners matter and the two
  sprites sharing it agree. The weights carry noise anchored at the corner (the
  same around every tile) and a world-periodic raggedness, so the boundaries
  are organic but still consistent from sprite to sprite.
* Shores get a sandy beach, a foam line and lit shallows; the coast/sea/ocean
  water sheets blend softly. The 81 cells of wSSS/wOOO add interior-only
  variation that fades to nothing at the diamond's edges.

Run standalone: `python ArtGen/terrain_base/build.py [sheet names]`.
preview.py composes a test map like the game does to check the seams.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'common'))
import civ3art  # noqa: E402

CW, CH = 128 * 2, 64 * 2  # cell size at 2x
NOISE_N = 256

SHEETS = {
    'xtgc': ('tundra', 'grass', 'coast'),
    'xpgc': ('plains', 'grass', 'coast'),
    'xdgc': ('desert', 'grass', 'coast'),
    'xdpc': ('desert', 'plains', 'coast'),
    'xdgp': ('desert', 'grass', 'plains'),
    'xggc': ('grass', 'grass', 'coast'),
    'wCSO': ('coast', 'sea', 'ocean'),
    'wSSS': ('sea', 'sea', 'sea'),
    'wOOO': ('ocean', 'ocean', 'ocean'),
}
WATER = {'coast', 'sea', 'ocean'}
TYPES = ['tundra', 'grass', 'plains', 'desert', 'coast', 'sea', 'ocean']

# Corner positions in ground coordinates, in index digit order: N, NW, NE, T.
CORNERS = np.array([(0.0, 1.0), (0.0, 0.0), (1.0, 1.0), (1.0, 0.0)])


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


# ---------------------------------------------------------------- coordinates

def cell_coords():
    """Ground coordinates (a, b) of the centers of a 2x cell's pixels."""
    py, px = np.mgrid[0:CH, 0:CW].astype(np.float64)
    X = (px + 0.5) / 2.0  # 1x pixel coordinates within the 128x64 cell
    Y = (py + 0.5) / 2.0
    a = (X / 64.0 + Y / 32.0) / 2.0 - 0.5
    b = (X / 64.0 - Y / 32.0) / 2.0 + 0.5
    return a, b


A, B = cell_coords()


# ---------------------------------------------------------------- noise

def spectrum_noise(seed, kmin, kmax, slope=1.0, stretch=1.0, n=NOISE_N, period=1.0):
    """A random smooth field on an n x n torus covering `period` ground units in
    a and b. Frequencies (cycles per ground unit, measured on screen so that
    `stretch` > 1 elongates features horizontally) are band-limited to
    [kmin, kmax] with a 1/k^slope falloff. Normalized to std 1."""
    rng = np.random.default_rng(seed)
    k = np.fft.fftfreq(n, 1.0 / n) / period  # cycles per ground unit
    ka, kb = np.meshgrid(k, k, indexing='ij')
    # Screen frequency (relative): x ~ ka + kb, y ~ 2 (ka - kb) at 2:1 iso.
    fx = (ka + kb) / np.sqrt(2) * stretch
    fy = (ka - kb) / np.sqrt(2)
    kk = np.sqrt(fx ** 2 + fy ** 2)
    band = np.clip((kk - kmin * 0.6) / (kmin * 0.4 + 1e-9), 0, 1) * np.exp(-np.maximum(kk - kmax, 0) ** 2 / (0.15 * kmax) ** 2)
    amp = np.where(kk > 0, band / np.maximum(kk, 1e-9) ** slope, 0)
    spec = amp * (rng.normal(size=(n, n)) + 1j * rng.normal(size=(n, n)))
    f = np.real(np.fft.ifft2(spec))
    f -= f.mean()
    f /= f.std() + 1e-12
    return f


def sample(field, a, b, period=1.0):
    """Bilinear, wrapped sample of a torus field at ground coords."""
    n = field.shape[0]
    u = (a / period) % 1.0 * n
    v = (b / period) % 1.0 * n
    i0 = np.floor(u).astype(int)
    j0 = np.floor(v).astype(int)
    fu = u - i0
    fv = v - j0
    i0 %= n
    j0 %= n
    i1 = (i0 + 1) % n
    j1 = (j0 + 1) % n
    return (field[i0, j0] * (1 - fu) * (1 - fv) + field[i1, j0] * fu * (1 - fv)
            + field[i0, j1] * (1 - fu) * fv + field[i1, j1] * fu * fv)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def mix(c0, c1, t):
    t = np.asarray(t)[..., None]
    return c0 * (1 - t) + c1 * t


# ---------------------------------------------------------------- terrain textures

def N(seed, kmin, kmax, slope=1.0, stretch=1.0, a=None, b=None):
    a = A if a is None else a
    b = B if b is None else b
    return sample(spectrum_noise(seed, kmin, kmax, slope, stretch), a, b)


# Colors: each terrain's base color is the original sheet's median color (see
# ORIGINAL below) with the saturation lowered a little (KISS: never more
# saturated than the original; calm enough to look at for hours). Water is
# nudged from the original's mint-teal toward a calmer, natural blue-teal.
#
# Textures are soft, low-contrast mottling: no ripples, wave streaks or bump
# relief, so large areas of one terrain look calm. The lowest frequency used is
# 3 cycles per tile: lower ones would repeat once per tile, which shows up as a
# grid over large areas of one terrain.

# Median colors of the originals' all-one-terrain cells (H degrees, S, V), for reference:
ORIGINAL = {'tundra': (64, 0.15, 0.87), 'grass': (60, 0.63, 0.61), 'plains': (42, 0.59, 0.86),
            'desert': (45, 0.41, 0.94), 'coast': (146, 0.35, 0.84), 'sea': (173, 0.65, 0.74),
            'ocean': (169, 0.58, 0.61)}
BASE = {'tundra': (66, 0.12, 0.84), 'grass': (66, 0.50, 0.58), 'plains': (41, 0.51, 0.80),
        'desert': (45, 0.30, 0.91), 'coast': (168, 0.27, 0.78), 'sea': (182, 0.43, 0.66),
        'ocean': (191, 0.45, 0.55)}


def hsv_rgb(h, s, v):
    """Vectorized HSV (h in degrees, s and v 0..1, scalars or arrays) to RGB."""
    h = np.asarray(h, float) / 60.0 % 6
    s = np.asarray(s, float)
    v = np.asarray(v, float)
    c = v * s
    x = c * (1 - np.abs(h % 2 - 1))
    z = np.zeros_like(h * s * v)
    i = np.floor(h).astype(int)
    r = np.choose(i, [c, x, z, z, x, c])
    g = np.choose(i, [x, c, c, x, z, z])
    b = np.choose(i, [z, z, x, c, c, x])
    m = v - c
    return np.stack([r + m, g + m, b + m], -1)


def surface(name, dv, dh=0.0, ds=0.0):
    """The terrain's base color with soft variation: dv relative value, dh hue
    degrees, ds saturation (fields around 0)."""
    h, s, v = BASE[name]
    return hsv_rgb(h + dh, np.clip(s + ds, 0, 1), np.clip(v * (1 + dv), 0, 1))


def tex_grass():
    mid = N(12, 3, 7)
    clump = N(13, 6, 12, 0.8)
    fine = N(14, 14, 24, 0.5)
    hue = N(15, 3, 6)
    dv = 0.045 * mid - 0.03 * clump + 0.015 * fine
    return surface('grass', dv, dh=-3 * hue, ds=0.02 * hue), None


def tex_plains():
    mid = N(22, 3, 7)
    soft = N(23, 6, 12, 0.8)
    fine = N(24, 14, 24, 0.5)
    hue = N(25, 3, 6)
    dv = 0.035 * mid + 0.015 * soft + 0.01 * fine
    # Faint greener patches (never redder than the base: that reads as dirt).
    g = smoothstep(0.0, 1.6, hue)
    return surface('plains', dv, dh=6 * g, ds=-0.03 * g), None


def tex_desert():
    mid = N(32, 3, 7)
    soft = N(33, 6, 12, 0.8)
    fine = N(34, 14, 24, 0.5)
    dv = 0.03 * mid + 0.015 * soft + 0.008 * fine
    return surface('desert', dv, ds=0.02 * mid), None


def tex_tundra():
    mid = N(41, 3, 7)
    moss = N(42, 5, 11)
    fine = N(44, 14, 24, 0.5)
    dv = 0.03 * mid + 0.008 * fine - 0.02 * moss
    # Faint mossy patches: a touch greener and more saturated.
    m = smoothstep(0.6, 1.8, moss)
    return surface('tundra', dv, dh=10 * m, ds=0.06 * m), None


def tex_water(name, seed):
    mid = N(seed, 3, 6)
    soft = N(seed + 1, 5, 10, 0.8)
    dv = 0.03 * mid + 0.01 * soft
    return surface(name, dv, dh=-2 * mid), None


def tex_coast():
    return tex_water('coast', 51)


def tex_sea():
    return tex_water('sea', 61)


def tex_ocean():
    return tex_water('ocean', 71)


TEXTURES = {}


def textures():
    if not TEXTURES:
        for name, fn in [('grass', tex_grass), ('plains', tex_plains), ('desert', tex_desert),
                         ('tundra', tex_tundra), ('coast', tex_coast), ('sea', tex_sea), ('ocean', tex_ocean)]:
            TEXTURES[name] = fn()[0]
    return TEXTURES


# ---------------------------------------------------------------- corner weights

# Each corner k has a smooth "tent" weight K_k = c(qa) c(qb), c(x) = cos^2(pi x / 2),
# where q is the position relative to the corner. The four weights sum to 1 over
# the diamond, and along an edge only that edge's two corners have any weight,
# so neighboring sprites agree there. A terrain's presence at a point is the sum
# of its corners' weights, each scaled by (1 + noise anchored at that corner):
# the noise is the same around every tile, so this too is consistent.
BLOB = {  # anchored noise amplitudes (low, mid frequency)
    'tundra': (0.45, 0.22), 'grass': (0.45, 0.22), 'plains': (0.45, 0.22), 'desert': (0.45, 0.22),
    'coast': (0.25, 0.05), 'sea': (0.25, 0.05), 'ocean': (0.25, 0.05),
}
RAGGED = 0.08       # world-periodic fine raggedness of the shoreline (in diff units)
LAND_BLEND = 0.50   # width of soft blends between land terrains (presence units)
BEACH = 0.50        # how far the beach reaches inland (diff units, ~2 per tile)
LAND_BIAS = 0.28    # how far land pushes past the midpoint toward water
SHALLOW = 0.42      # how far the shallows reach out from the shore

PRESENCE = {}
SHORE_NOISE = {}


def tent(x):
    return np.where(np.abs(x) < 1, np.cos(np.pi * np.clip(x, -1, 1) / 2) ** 2, 0.0)


def presence():
    """PRESENCE[type][k]: the weight a corner k of the given type gives its type."""
    if PRESENCE:
        return PRESENCE
    for ti, t in enumerate(TYPES):
        a1, a2 = BLOB[t]
        f1 = spectrum_noise(1000 + ti, 0.7, 1.6, 1.0, period=4.0)
        f2 = spectrum_noise(1100 + ti, 2.5, 6.0, 0.7, period=4.0)
        per = []
        for k, (ca, cb) in enumerate(CORNERS):
            qa, qb = A - ca, B - cb
            n = a1 * sample(f1, qa + 2.0, qb + 2.0, period=4.0) + a2 * sample(f2, qa + 2.0, qb + 2.0, period=4.0)
            per.append(tent(qa) * tent(qb) * (1 + np.clip(n, -0.85, 1.5)))
        PRESENCE[t] = per
    SHORE_NOISE['fine'] = N(1300, 8, 16, 0.5)
    SHORE_NOISE['ragged'] = N(1303, 3, 9, 0.8) + 0.2 * N(1304, 10, 20, 0.5)
    SHORE_NOISE['foam'] = N(1301, 8, 18, 0.5)
    SHORE_NOISE['land'] = N(1302, 10, 24, 0.4)
    return PRESENCE


def type_presence(corner_types, kinds):
    pr = presence()
    out = {}
    for k, t in enumerate(corner_types):
        if t in kinds:
            out[t] = out.get(t, 0) + pr[t][k]
    return out


# ---------------------------------------------------------------- composing a cell

SAND = hexc('#e2cf9c')
WET_SAND = hexc('#bca67a')
SHALLOW_C = hexc('#a9d4c6')
FOAM = hexc('#eef3ee')


def compose_cell(corner_types, variant=None):
    """RGB (CH x CW x 3, 0..1) of a cell whose corners (N, NW, NE, T) have the
    given terrain types."""
    tex = textures()
    presence()
    landp = type_presence(corner_types, {'tundra', 'grass', 'plains', 'desert'})
    waterp = type_presence(corner_types, WATER)

    land_rgb = water_rgb = None
    if landp:
        jit = 0.16 * SHORE_NOISE['land']
        g = {t: p + jit * (1 if t in ('grass', 'tundra') else -1) for t, p in landp.items()}
        gmax = np.max(list(g.values()), axis=0)
        # A terrain counts only where its own corners reach (never on the
        # opposite edges), so the blend stays consistent across tile edges.
        wts = {t: smoothstep(0, 1, 1 - (gmax - v) / LAND_BLEND) * smoothstep(0, 0.08, landp[t]) + 1e-6 * landp[t] + 1e-12
               for t, v in g.items()}
        tot = sum(wts.values())
        land_rgb = sum(tex[t] * (w / tot)[..., None] for t, w in wts.items())
    if waterp:
        wts = {t: p ** 1.5 + 1e-9 for t, p in waterp.items()}
        tot = sum(wts.values())
        water_rgb = sum(tex[t] * (w / tot)[..., None] for t, w in wts.items())
        if variant is not None:
            water_rgb = water_rgb + variant

    if water_rgb is None:
        return land_rgb
    if land_rgb is None:
        return water_rgb

    L = sum(landp.values())
    W = sum(waterp.values())
    diff = (L - W) / (L + W)  # -1 (water) .. 1 (land); the shore is at 0
    # Like the originals, land reaches past the middle of a land-water edge.
    diff = np.clip(diff + LAND_BIAS * (1 - diff ** 2), -1, 1)
    # The beach is measured on a smoother version of the shore so that its
    # width stays even while the waterline itself is ragged.
    smooth = diff + 0.4 * RAGGED * SHORE_NOISE['ragged'] * (1 - diff ** 2)
    diff = diff + RAGGED * SHORE_NOISE['ragged'] * (1 - diff ** 2)
    fine = SHORE_NOISE['fine']
    # Land side: a sandy beach with a wet edge at the waterline.
    bw = BEACH * (0.85 + 0.12 * np.clip(fine, -1.5, 1.5))
    beach = 1 - smoothstep(0.65 * bw, bw, smooth + 0.03 * SHORE_NOISE['land'])
    sand = SAND * (1 + 0.05 * SHORE_NOISE['land'])[..., None]
    land_c = mix(land_rgb, sand, 0.95 * beach)
    land_c = mix(land_c, WET_SAND, 0.6 * (1 - smoothstep(0.0, 0.07, diff)))
    # Water side: shallows lit over the sand, then a soft foam line.
    d = -diff
    sh = 1 - smoothstep(0.0, SHALLOW, d)
    water_c = mix(water_rgb, SHALLOW_C, 0.8 * sh ** 1.5)
    foam_d = 0.05 + 0.015 * fine
    foam = np.exp(-((d - foam_d) / 0.014) ** 2) * (0.45 + 0.4 * smoothstep(-0.8, 0.8, SHORE_NOISE['foam']))
    foam = foam + 0.55 * (1 - smoothstep(0.0, 0.018, d))
    water_c = mix(water_c, FOAM, np.clip(foam, 0, 1) * 0.6)
    # Anti-aliased shoreline (about a pixel wide at 2x).
    m = smoothstep(-0.008, 0.008, diff)
    return mix(water_c, land_c, m)


def water_variant(seed, kind):
    """Interior-only variation for the random all-sea / all-ocean cells: a gentle
    change of brightness that fades to zero on the diamond's edges, so they
    still join any neighbor."""
    bumpw = np.clip(np.sin(np.pi * np.clip(A, 0, 1)) * np.sin(np.pi * np.clip(B, 0, 1)), 0, 1) ** 1.2
    rng = np.random.default_rng(seed)
    n = sample(spectrum_noise(seed, 1.5, 4, 1.0, period=1.0), A, B)
    s = rng.uniform(0.6, 1.0)
    v = (0.02 * n)[..., None] * np.array([0.8, 0.95, 1.0])
    return v * (bumpw * s)[..., None]


# ---------------------------------------------------------------- sheets

def diamond_alpha():
    a = civ3art.load_original('Art/Terrain/xtgc.pcx')[..., 3]
    return civ3art.upscale_nearest(a)


def build_sheet(name, alpha):
    types = SHEETS[name]
    out = np.zeros((9 * CH, 9 * CW, 4), np.uint8)
    for i in range(81):
        digits = (i % 3, (i // 3) % 3, (i // 9) % 3, i // 27)
        ct = [types[dd] for dd in digits]
        variant = None
        if name in ('wSSS', 'wOOO'):
            variant = water_variant(5000 + i + (0 if name == 'wSSS' else 100), types[0])
        rgb = compose_cell(ct, variant)
        y, x = divmod(i, 9)
        out[y * CH:(y + 1) * CH, x * CW:(x + 1) * CW, :3] = np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8)
    out[..., 3] = alpha
    out[alpha == 0, :3] = 0
    return out


def main(names=None):
    alpha = diamond_alpha()
    for name in names or SHEETS:
        rgba = build_sheet(name, alpha)
        path = civ3art.save_modern(f'Art/Terrain/{name}.pcx', rgba)
        print('wrote', path, flush=True)


if __name__ == '__main__':
    main(sys.argv[1:] or None)
