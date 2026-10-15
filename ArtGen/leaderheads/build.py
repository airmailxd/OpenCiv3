"""The game's leader heads are now lifelike portraits made by lifelike.py
(python ArtGen/leaderheads/lifelike.py --install); this script's SOURCES and
helpers are what it builds on. Running this script still makes the earlier
painted portraits, over the lifelike ones, so it asks for --painted-set.

Makes the leader heads for standalone mode (no Civ3 files): a head and
shoulders portrait of each civilization's leader, shown in diplomacy, the
new game screen and the LAN lobby:

    C7/ModernArt/Art/Leaderheads/<civ name>.png

    python ArtGen/leaderheads/build.py               # every civilization
    python ArtGen/leaderheads/build.py Rome Egypt    # just these
    python ArtGen/leaderheads/build.py --sources     # also preview the whole
                                                     # source pictures, gridded

Each portrait is a real historical likeness of the leader: a painting,
photograph, bust, relief or engraving from Wikimedia Commons that is in the
public domain (or CC0). For leaders with no likeness from their own time it's
a well-known later painting or statue of them (see SOURCES). The script
downloads each source once into ArtGen/leaderheads/_cache (not committed),
crops it to the head and shoulders, and gives every portrait the same gentle
finish (levels, a little warmth, a soft vignette and a beveled edge) so the
set looks of a piece. Nothing is painted or generated; the faces are the
artworks' own.

It also writes C7/ModernArt/Art/Leaderheads/CREDITS.txt: each portrait's
Commons page, artist and license, read from Commons.

Like all of ModernArt the portraits have twice the pixels they're shown
with: 230x230 for Civ3's 115x115 leader heads. The standalone rules
(C7/Lua/standalone/ruleset.lua) point each civilization's leaderArtFile at
its portrait, and the standalone textures (C7/Lua/standalone/textures.lua)
show it. There's one portrait per leader, not one per era and mood like
Civ3's.

A contact sheet goes to ArtGen/_previews/leaderheads.png.
"""
import io
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
RULESET = os.path.join(REPO, 'C7', 'Lua', 'civ3', 'ruleset.json')
OUT = os.path.join(REPO, 'C7', 'ModernArt', 'Art', 'Leaderheads')
CACHE = os.path.join(HERE, '_cache')
PREVIEWS = os.path.join(REPO, 'ArtGen', '_previews')

SIZE = 230
# Wikimedia asks for a User-Agent that says who's asking.
USER_AGENT = 'OpenCiv3-ArtGen/1.0 (https://github.com/C7-Game/Prototype; ArtGen/leaderheads/build.py)'
API = 'https://commons.wikimedia.org/w/api.php'
# Sources are fetched at most this wide (Commons scales them down for us).
MAX_WIDTH = 2400

# Each civilization's portrait: the Commons file, and the square to crop from
# it as (left, top, side), all fractions of the picture's width. The note
# says what the picture is.
SOURCES = {
    'Rome': ('File:Bust of Julius Caesar.jpg', (0.13, 0.04, 0.66),
             'Julius Caesar: Roman marble bust in the Greek Cross Hall of the Vatican Museums'),
    'Egypt': ('File:Cleopatra-VII-Antikensammlung-Berlin.jpg', (0.12, 0.18, 0.86),
              'Cleopatra VII: the "Berlin Cleopatra", a Roman marble portrait of about 40-30 BC'),
    'Greece': ('File:Bust Alexander BM 1857.jpg',
              (0.0, 0.06, 1.0), 'Alexander the Great: Hellenistic marble head in the British Museum'),
    'Babylon': ('File:Tête royale dite tête de Hammurabi - Musée du Louvre Antiquités orientales SB 95.jpg',
                (0.05, 0.05, 0.90), 'Hammurabi: the diorite royal head known as the "head of Hammurabi", Louvre'),
    'Germany': ('File:Franz von Lenbach - Bildnis Otto Eduard Leopold von Bismarck (Indianapolis).jpg',
                (0.18, 0.12, 0.66), 'Otto von Bismarck by Franz von Lenbach, 1879 (Indianapolis Museum of Art)'),
    'Russia': ('File:Catherine II after Roslin, Rokotov (1780s, Kunsthistorisches Museum).jpg', (0.1, 0.0, 0.8),
               'Catherine the Great by Fyodor Rokotov after Roslin, 1780s'),
    'China': ('File:Mao Zedong 1950 Portrait (3x4 cropped).jpg',
             (0.12, 0.08, 0.76), 'Mao Zedong, portrait photograph by Chen Zhengqing, 1950'),
    'America': ('File:Abraham Lincoln O-77 matte collodion print.jpg', (0.1, 0.05, 0.8),
                'Abraham Lincoln photographed by Alexander Gardner, 1863'),
    'Japan': ('File:Tokugawa Ieyasu2.JPG', (0.31, 0.42, 0.24),
              'Tokugawa Ieyasu by Kano Tan\'yu, 17th century'),
    'France': ('File:Ingres coronation charles vii.jpg',
              (0.33, 0.1, 0.32), 'Joan of Arc: detail of "Joan of Arc at the Coronation of Charles VII" by Ingres, 1854 (no likeness from her lifetime survives)'),
    'India': ('File:Gandhi portrait, 1940.jpg',
             (0.04, 0.0, 0.96), 'Mohandas K. Gandhi, photograph, 1940'),
    'Persia': ('File:Rembrandt van Rijn and Workshop (Probably Govaert Flinck), Man in Oriental Costume, c. 1635, NGA 572.jpg',
              (0.17, 0.08, 0.66), 'A typical Persian king, standing in for Xerxes, whose own reliefs are defaced: "Man in Oriental Costume" by Rembrandt and workshop (probably Govaert Flinck), about 1635'),
    'Aztecs': ('File:Moctezuma Xocoyotzin.png',
              (0.22, 0.03, 0.5), 'Moctezuma II, 17th-century painting attributed to Antonio Rodriguez'),
    'Zululand': ('File:Shaka Zulu incisione ottocentesca.jpg',
                (0.315, 0.3, 0.35), 'Shaka, 19th-century print after the drawing by James King'),
    'Iroquois': ('File:Joseph Brant painting by George Romney 1776 (2).jpg',
                (0.2, 0.06, 0.56), 'A Haudenosaunee leader, standing in for Hiawatha, of whom no likeness survives: the Mohawk leader Thayendanegea (Joseph Brant) by George Romney, 1776'),
    'England': ('File:Darnley stage 3.jpg', (0.17, 0.0, 0.52),
                'Elizabeth I: the "Darnley Portrait", about 1575'),
    'Mongols': ('File:YuanEmperorAlbumGenghisPortrait.jpg',
               (0.125, 0.24, 0.75), "Genghis Khan (Temujin): portrait from the Yuan emperors' album, National Palace Museum, Taipei"),
    'Spain': ('File:Juan de Flandes - Isabel la Católica (Palacio Real, Madrid, 1500-04. Óleo sobre tabla, 63 x 55 cm).jpg',
              (0.1, 0.05, 0.8), 'Isabella I of Castile by Juan de Flandes, about 1500'),
    'Scandinavia': ('File:Leif Erikson Discovers America Hans Dahl.jpg',
                   (0.295, 0.11, 0.14), 'A typical Viking chieftain, standing in for Ragnar Lodbrok, of whom no likeness survives: Leif Erikson in "Leif Erikson Discovers America" by Hans Dahl'),
    'Ottomans': ('File:Paolo Veronese (Nachfolger) - Sultan Osman I. - 2242 - Bavarian State Painting Collections.jpg',
                (0.135, 0.19, 0.77), 'Osman I, 16th-century Venetian portrait (follower of Paolo Veronese)'),
    'Celts': ('File:Le Brenn et sa part de butin (Paul Jamin).jpg',
             (0.1, 0.145, 0.17), 'Brennus: detail of "Le Brenn et sa part de butin" by Paul Jamin, 1893'),
    'Arabia': ('File:Abu Bakr Caliph Calligraphy.png',
              (0.006, 0.0, 0.988), 'Abu Bakr: his name in the roundel design by Kazasker Mustafa Izzet Efendi (Hagia Sophia, 19th century); no likeness of him exists, and Islamic tradition shows him by his name'),
    'Carthage': ('File:Hannibal Barca bust from Capua photo.jpg', (0.1, 0.0, 0.8),
                 'Hannibal: the marble bust found at Capua'),
    'Korea': ('File:Yi Jaegwan - Portrait of Scholar-Official An in his Fiftieth Year - Google Art Project.jpg',
              (0.1, 0.25, 0.8), 'A typical Korean official, standing in for Wang Kon, whose own portraits on Commons are too small or not public domain: "Portrait of Scholar-Official An in his Fiftieth Year" by Yi Jae-gwan, 19th century'),
    'Sumeria': ('File:Statue of Gudea MET DT861.jpg',
                (0.22, 0.08, 0.56), 'A typical Sumerian ruler, standing in for Gilgamesh, of whom no likeness survives: Gudea of Lagash, diorite statue, Metropolitan Museum of Art'),
    'Hittites': ('File:Priest-King or Diety, about 1600 BC, Hittite, North Syria, basalt with bone eyes - Cleveland Museum of Art - DSC08114.JPG',
                (0.35, 0.07, 0.29), 'A typical Hittite king, standing in for Mursilis, of whom no likeness survives: basalt statue of a priest-king with bone eyes, about 1600 BC, Cleveland Museum of Art'),
    'Netherlands': ('File:William I, Prince of Orange by Adriaen Thomasz. Key Rijksmuseum Amsterdam SK-A-3148.jpg',
                    (0.1, 0.05, 0.8), 'William the Silent by Adriaen Thomasz. Key, about 1579'),
    'Portugal': ('File:Painéis de São Vicente de Fora - Painel do Infante (MNAA, inv. 1361 Pint).png',
                 (0.625, 0.265, 0.32),
                 'Henry the Navigator: detail of the Panel of the Infante, Saint Vincent Panels, by Nuno Goncalves, 1470s'),
    'Byzantines': ('File:Mosaic of empress Theodora, Ravenna, San Vitale, 547.jpg', (0.15, 0.10, 0.62),
                   'Empress Theodora: mosaic in the Basilica of San Vitale, Ravenna, 547'),
    'Inca': ('File:Brooklyn Museum - Pachacuti, Tenth Inca, 1 of 14 Portraits of Inca Kings (cropped).jpg',
            (0.3, 0.12, 0.46), 'Pachacuti: Cuzco School painting, 18th century (Brooklyn Museum)'),
    'Maya': ('File:Mayan - Stucco Portrait Head - Walters 20092026.jpg',
            (0.1, 0.12, 0.84), 'A typical Maya noble, standing in for Smoke-Jaguar, of whom no likeness is in the public domain: Classic Maya stucco portrait head from Campeche, Walters Art Museum'),
}

# Artists Commons' metadata doesn't name, by civilization.
ARTIST = {'Scandinavia': 'Hans Dahl', 'Korea': 'Yi Jae-gwan (1783-1837)'}

# A little more for a few sources: contrast for a faded print; less
# sharpening for small sources scaled up, so they don't get an artificial
# texture; 'cover', boxes (left, top, right, bottom, in fractions of the
# width) painted over with the color around them, for a painted inscription
# beside the sitter; 'disc', the corners outside a round picture filled
# with a dark warm tone.
TOUCH = {
    'Zululand': {'sharpen': 20},
    'Scandinavia': {'sharpen': 20},
    'Celts': {'sharpen': 15},
    'Arabia': {'color': 0.4, 'disc': True},
    'Ottomans': {'cover': [(0.02, 0.57, 0.30, 0.76)]},
}
# The painted finish (paint.py) adjusted for a few pictures: see paint.paint.
PAINT = {
    # Hand-drawn outlines (polygons, in fractions of the tile) where the
    # mask can't find the sitter's edge: outside them is all painted ground
    # (with hand_only, inside them is all sitter).
    'Rome': {'broad': 140, 'keep': 0.5, 'color': 0.45, 'hand_only': True, 'edge': 8, 'shrink': 2,
             'hand': [[(0.29, 0.3), (0.34, 0.1), (0.45, 0.04), (0.6, 0.03), (0.72, 0.06), (0.78, 0.15), (0.78, 0.38),
                       (0.71, 0.6), (0.76, 0.74), (0.85, 0.82), (0.86, 1), (0.27, 1), (0.28, 0.8), (0.3, 0.66),
                       (0.29, 0.5)]]},
    # statues before a light wall: the sitter's outline pulled in further,
    # so no light rim of the wall glows round the head
    'Babylon': {'erode': 15, 'plain': 1.0, 'edge': 7, 'shrink': 2},
    'Hittites': {'erode': 8, 'hand_only': True, 'level': 0.36, 'local': 1.5, 'feather': 4, 'edge': 10, 'shrink': 6, 'bg': 1.0,
                 'hand': [[(0.24, 0.42), (0.3, 0.25), (0.42, 0.06), (0.5, 0.0), (0.58, 0.04), (0.72, 0.25), (0.8, 0.45),
                           (0.86, 0.6), (0.82, 0.68), (0.8, 0.8), (0.75, 0.9), (0.72, 1), (0.3, 1), (0.26, 0.85),
                           (0.24, 0.7), (0.22, 0.55)]]},
    'Sumeria': {'bg': 0.0, 'surround': 1.0, 'around': (0.5, 0.45, 0.3, 0.45), 'deep': 0.8, 'post': 0.55,
                'wallgrade': 0.6, 'plain': 1.0},
    'Greece': {'hand_only': True,
               'hand': [[(0.16, 0.3), (0.19, 0.15), (0.3, 0.09), (0.55, 0.08), (0.75, 0.13), (0.85, 0.22), (0.89, 0.5),
                         (0.86, 1), (0.22, 1), (0.16, 0.65)]]},
    # a print: all in the shared key, like the black and white photographs,
    # its hatching softened into tone
    'Zululand': {'color': 0.0, 'soften': 1.3, 'texture': 0, 'contrast': 0.85, 'level': 0.36, 'hand_only': True,
                 'hand': [[(0.29, 0.35), (0.31, 0.18), (0.42, 0.12), (0.56, 0.1), (0.67, 0.14), (0.71, 0.22),
                           (0.7, 0.45), (0.62, 0.62), (0.64, 0.76), (1, 0.86), (1, 1), (0, 1), (0, 0.88),
                           (0.34, 0.77), (0.37, 0.62), (0.3, 0.5)]]},
    # a pale face on pale silk the mask can't tell apart
    # a pale face on pale silk: the whole picture darkened first, and an
    # outline for the hat, face and shoulders, which the mask can't find
    # the white collar and beard match the paper: a hand outline
    'Korea': {'edge': 3, 'hand_only': True, 'feather': 4,
              'hand': [[(0.39, 0.08), (0.61, 0.08), (0.62, 0.27), (0.8, 0.27), (0.84, 0.32), (0.82, 0.4), (0.64, 0.42),
                        (0.63, 0.55), (0.68, 0.61), (0.9, 0.75), (1, 0.82), (1, 1), (0, 1), (0, 0.93), (0.13, 0.78),
                        (0.32, 0.61), (0.36, 0.55), (0.35, 0.42), (0.18, 0.4), (0.16, 0.32), (0.2, 0.27), (0.38, 0.27)]]},
    # photographs: more of the real picture kept on the face, so the
    # strokes don't break skin into patches
    'America': {'keep': 0.55, 'erode': 30, 'feather': 3},
    'China': {'keep': 0.65},
    'India': {'keep': 0.65},
    'Mongols': {'color': 0.65, 'level': 0.30, 'keep': 0.85, 'feather': 3, 'edge': 8, 'shrink': 5, 'bg': 1.0,
                'hand': [[(0.22, 0.6), (0.24, 0.25), (0.33, 0.1), (0.5, 0.05), (0.67, 0.1), (0.78, 0.25), (0.84, 0.6),
                          (1, 0.7), (1, 1), (0, 1), (0, 0.66), (0.14, 0.62)]]},
    'Japan': {'edge': 6, 'shrink': 2, 'feather': 5, 'texture': 0.3, 'broad': 140,
              'hand': [[(0.36, 0.45), (0.4, 0.3), (0.5, 0.2), (0.62, 0.18), (0.7, 0.25), (0.76, 0.35), (0.8, 0.5),
                        (0.82, 0.55), (1, 0.52), (1, 1), (0, 1), (0, 0.75), (0.3, 0.68), (0.36, 0.6)]]},
    # a painting kept whole, its own background darkened into the key
    'Celts': {'bg': 0.0, 'surround': 1.0, 'deep': 0.9, 'texture': 0.5, 'level': 0.34, 'local': 1.4, 'post': 0.5},
    'Byzantines': {'keep': 0.75, 'broad': 150},
    'Aztecs': {'broad': 140,
               'hand': [[(0.38, 0.25), (0.42, 0.1), (0.5, 0.05), (0.62, 0.02), (0.75, 0.02), (0.8, 0.1), (0.78, 0.3),
                         (0.85, 0.45), (0.9, 0.62), (1, 0.68), (1, 1), (0, 1), (0.1, 0.62), (0.25, 0.5),
                         (0.38, 0.5)]]},
    'Portugal': {'level': 0.42},
    'Ottomans': {'keep': 0.6},
    'Inca': {'level': 0.36, 'keep': 0.6},
    # Abu Bakr's roundel: its green dimmed toward an aged verdigris
    'Arabia': {'green': 0.2, 'level': 0},
}

# The brightness every portrait's mid-tones are brought toward (0..1).
TARGET_LEVEL = 0.42
# Faces much paler than the set's (Wang Kon's stand-in, a pale painted face
# on silk): (cx, cy, rx, ry, brightness) of an ellipse brought to about the
# set's typical face brightness.
FACE = {}


# ---------------------------------------------------------------- Commons

def _get(url, tries=4):
    req = urllib.request.Request(url, headers={'User-Agent': USER_AGENT})
    for i in range(tries):
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code not in (429, 500, 502, 503, 504) or i == tries - 1:
                raise
        except urllib.error.URLError:
            if i == tries - 1:
                raise
        time.sleep(2 + 4 * i)


def _strip(html):
    # without hidden parts (Commons repeats some names in hidden spans)
    text = re.sub(r'<(\w+)[^>]*display:\s*none[^>]*>.*?</\1>', '', html or '', flags=re.S)
    text = re.sub(r'<[^>]+>', '', text)
    return re.sub(r'\s+', ' ', text).strip()


def _cache_name(title):
    return re.sub(r'[^A-Za-z0-9._-]+', '_', title[5:])[:120]


def file_info(title):
    """A Commons file's page, size, download URL and license metadata (cached)."""
    path = os.path.join(CACHE, _cache_name(title) + '.json')
    if os.path.exists(path):
        with open(path, encoding='utf-8') as f:
            return json.load(f)
    q = urllib.parse.urlencode({'action': 'query', 'format': 'json', 'titles': title, 'prop': 'imageinfo',
                                'iiprop': 'url|size|extmetadata', 'iiurlwidth': MAX_WIDTH})
    pages = json.loads(_get(f'{API}?{q}'))['query']['pages']
    page = next(iter(pages.values()))
    if 'imageinfo' not in page:
        raise SystemExit(f'{title}: not on Commons')
    ii = page['imageinfo'][0]
    meta = ii.get('extmetadata', {})
    info = {
        'title': page['title'],
        'page': ii['descriptionurl'],
        'width': ii['width'],
        'height': ii['height'],
        # the original if it's small enough, else Commons' scaled copy
        'url': ii['url'] if ii['width'] <= MAX_WIDTH or 'thumburl' not in ii else ii['thumburl'],
        'license': _strip(meta.get('LicenseShortName', {}).get('value')),
        'artist': _strip(meta.get('Artist', {}).get('value')) or 'unknown',
        'credit': _strip(meta.get('Credit', {}).get('value')),
        'date': _strip(meta.get('DateTimeOriginal', {}).get('value')),
        'license_templates': license_templates(title),
    }
    os.makedirs(CACHE, exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=1, ensure_ascii=False)
    return info


def license_templates(title):
    """The license templates on a file's page. A photo of an old statue or
    painting can be public domain as an artwork yet carry the photographer's
    own CC BY-SA license, which the summary license name doesn't show."""
    q = urllib.parse.urlencode({'action': 'query', 'format': 'json', 'titles': title, 'prop': 'revisions',
                                'rvprop': 'content', 'rvslots': 'main'})
    page = next(iter(json.loads(_get(f'{API}?{q}'))['query']['pages'].values()))
    text = page['revisions'][0]['slots']['main']['*']
    # {{self|cc-by-sa-4.0|...}} lists the uploader's licenses as parameters
    names = [n for m in re.findall(r'\{\{\s*self\s*\|([^}]*)\}\}', text, re.I) for n in m.split('|')]
    names += re.findall(r'\{\{\s*([^|}\n]+)', text)
    return [n.strip() for n in names if re.match(r'\s*(pd|cc|gfdl|fal\b|attribution|licensed|public|not-pd)', n, re.I)]


def is_free(info):
    """Only public domain and CC0 pictures are used (no attribution or
    share-alike terms on the game's art): the license is public domain or
    CC0, no license on the page asks for attribution or share-alike, and
    the page doesn't say it's still under copyright in the US
    ({{Not-PD-US-...}})."""
    if not re.match(r'(public domain|pd\b|pd-|cc0)', info['license'].strip(), re.I):
        return False
    return not any(re.match(r'(cc-by|gfdl|fal\b|attribution|not-pd)', t, re.I) for t in info['license_templates'])


def source_image(title):
    info = file_info(title)
    if not is_free(info):
        raise SystemExit(f'{title} is {info["license"]} ({", ".join(info["license_templates"])}),'
                         ' not just public domain or CC0')
    ext = os.path.splitext(info['url'].split('?')[0])[1].lower() or '.jpg'
    path = os.path.join(CACHE, _cache_name(title) + '.src' + ext)
    if not os.path.exists(path):
        data = _get(info['url'])
        with open(path, 'wb') as f:
            f.write(data)
        time.sleep(1)  # be gentle with Commons
    im = Image.open(path)
    im = ImageOps.exif_transpose(im)
    if im.mode in ('RGBA', 'LA', 'P'):
        im = im.convert('RGBA')
        bg = Image.new('RGBA', im.size, (236, 230, 216, 255))
        im = Image.alpha_composite(bg, im)
    return im.convert('RGB'), info


# ---------------------------------------------------------------- the picture

def crop(im, box, size=SIZE):
    """The square (left, top, side), in fractions of the width, scaled to
    size (SIZE). Parts outside the picture are filled with its edge colors."""
    w, h = im.size
    left, top, side = (v * w for v in box)
    x0, y0, s = int(round(left)), int(round(top)), max(1, int(round(side)))
    if x0 < 0 or y0 < 0 or x0 + s > w or y0 + s > h:
        pad = max(-x0, -y0, x0 + s - w, y0 + s - h, 0)
        im = ImageOps.expand(im, border=pad, fill=tuple(int(v) for v in np.asarray(im).reshape(-1, 3).mean(0)))
        x0, y0 = x0 + pad, y0 + pad
    sq = im.crop((x0, y0, x0 + s, y0 + s))
    # downscale in steps for a clean result from big sources
    while sq.width > size * 3:
        sq = sq.resize((sq.width // 2, sq.height // 2), Image.LANCZOS)
    return sq.resize((size, size), Image.LANCZOS)


def _luma(a):
    return a @ np.array([0.299, 0.587, 0.114], np.float32)


def finish(im, sharpen=45, level=True):
    """The same grade for every portrait, so the set looks of a piece:
    levels, mid-tones brought to a common brightness, black and white
    pictures toned warm like old prints, slightly muted and warmed color,
    a little sharpening, a soft vignette and a beveled edge."""
    im = ImageOps.autocontrast(im, cutoff=(0.4, 0.4), preserve_tone=True)
    a = np.asarray(im, np.float32) / 255
    # even out brightness: a gamma that brings the middle of the picture's
    # tones (weighted to its center, where the face is) toward one level
    n = SIZE
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    wgt = np.exp(-(((x - n / 2) / (n * 0.3)) ** 2 + ((y - n * 0.45) / (n * 0.3)) ** 2))
    mean = float((_luma(a) * wgt).sum() / wgt.sum())
    g = np.clip(np.log(TARGET_LEVEL) / np.log(max(1e-3, min(mean, 0.97))), 0.7, 1.45)
    if level:
        a = np.clip(a, 0, 1) ** g
    # black and white (and near it): toned like a warm old print
    sat = float((a.max(-1) - a.min(-1)).mean())
    if sat < 0.1:
        lum = _luma(a)[..., None]
        sepia = np.clip(lum * np.array([1.08, 0.98, 0.82], np.float32) + np.array([0.02, 0.01, 0.0], np.float32), 0, 1)
        k = 0.75 * (1 - sat / 0.1)
        a = a * (1 - k) + sepia * k
    im = Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')
    im = ImageEnhance.Color(im).enhance(0.88)
    im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=sharpen, threshold=2))
    a = np.asarray(im, np.float32) / 255
    # warm: a touch of old varnish over everything
    a = a * np.array([1.03, 1.0, 0.92], np.float32) + np.array([0.012, 0.006, 0.0], np.float32)
    d =np.hypot((x - n / 2) / (n * 0.62), (y - n * 0.45) / (n * 0.66))
    a *= (1 - 0.30 * np.clip(d - 0.35, 0, 1) ** 1.6)[..., None]
    # beveled edge: a dark rim, light inner line top and left, dark bottom and right
    w = 3
    a[:w, :] = a[:w, :] * 0.86 + 0.14
    a[:, :w] = a[:, :w] * 0.86 + 0.14
    a[-w:, :] *= 0.72
    a[:, -w:] *= 0.72
    a[[0, -1], :] *= 0.35
    a[:, [0, -1]] *= 0.35
    return Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')


def _cover(im, boxes):
    """Paints the boxes over with the median color of a band around them."""
    a = np.asarray(im).copy()
    w = im.width
    for l, t, r, b in boxes:
        x0, y0, x1, y1 = (int(v * w) for v in (l, t, r, b))
        m = max(4, (x1 - x0) // 8)
        ring = np.concatenate([a[max(0, y0 - m):y0, x0:x1].reshape(-1, 3), a[y1:y1 + m, x0:x1].reshape(-1, 3),
                               a[y0:y1, max(0, x0 - m):x0].reshape(-1, 3), a[y0:y1, x1:x1 + m].reshape(-1, 3)])
        a[y0:y1, x0:x1] = np.median(ring, 0)
    out = Image.fromarray(a)
    # soften the patch's edges into the painting
    mask = Image.new('L', im.size, 0)
    dr = ImageDraw.Draw(mask)
    for l, t, r, b in boxes:
        dr.rectangle([v * w for v in (l, t, r, b)], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(max(2, w // 200)))
    return Image.composite(out.filter(ImageFilter.GaussianBlur(max(2, w // 300))), im, mask)


def _disc(im):
    """The corners outside the round picture, filled dark and warm."""
    n = im.width
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    r = np.hypot(x - (n - 1) / 2, y - (n - 1) / 2) / (n / 2)
    k = np.clip((r - 0.985) / 0.02, 0, 1)[..., None]
    a = np.asarray(im, np.float32) * (1 - k) + np.array([42, 32, 24], np.float32) * k
    return Image.fromarray(a.astype(np.uint8))


def make(civ_name, painted=False):
    """A civilization's portrait; painted, with the set's shared painted
    finish (paint.py) done on a picture twice the size and scaled down."""
    title, box, _ = SOURCES[civ_name]
    im, info = source_image(title)
    extra = TOUCH.get(civ_name, {})
    if 'cover' in extra:
        im = _cover(im, extra['cover'])
    # how much the crop is enlarged: sharpening only for sources with pixels to spare
    upscale = SIZE / max(1.0, box[2] * im.width)
    im = crop(im, box, SIZE * 2 if painted else SIZE)
    if extra.get('disc'):
        im = _disc(im)
    if 'contrast' in extra:
        im = ImageEnhance.Contrast(im).enhance(extra['contrast'])
    if 'color' in extra:
        im = ImageEnhance.Color(im).enhance(extra['color'])
    sharpen = extra.get('sharpen', 45 if upscale < 0.8 else 25)
    if painted:
        import paint
        a = paint.paint(im, seed=sum(map(ord, civ_name)),
                        **{'level': TARGET_LEVEL, **{k: v for k, v in PAINT.get(civ_name, {}).items() if k != 'post'}})
        im = Image.fromarray((a * 255 + 0.5).astype(np.uint8), 'RGB').resize((SIZE, SIZE), Image.LANCZOS)
        sharpen = 0   # (sharpening brightens the ground's edge into rims)
    out = finish(im, sharpen=sharpen, level=not painted)
    if painted and PAINT.get(civ_name, {}).get('surround'):
        # (the levels in finish lift a darkened surround again: darkened once
        # more after them, toward the set's dark umber)
        ps = PAINT[civ_name]
        out = _surround_post(out, ps.get('around', (0.5, 0.62, 0.22, 0.28)), ps.get('post', 0.6))
    if painted and civ_name in FACE:
        out = _face_level(out, *FACE[civ_name])
    return out, info


def _surround_post(im, around, amount):
    """Everything outside a soft oval (cx, cy, rx, ry in fractions of the
    tile) multiplied darker by up to amount, and warmed toward umber."""
    a = np.asarray(im, np.float32) / 255
    n = a.shape[0]
    cx, cy, rx, ry = around
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    ov = np.exp(-(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2))[..., None]
    k = amount * (1 - ov)
    umber = _luma(a)[..., None] * np.array([0.62, 0.48, 0.34], np.float32)
    a = a * (1 - k) + umber * k * 0.55
    return Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')


def _face_level(im, cx, cy, rx, ry, target):
    """A face much lighter than the set's (a pale painted face): within a
    soft ellipse (center and radii in fractions of the tile), a gamma that
    brings its mean brightness to target, and a slight warm tint."""
    a = np.asarray(im, np.float32) / 255
    n = a.shape[0]
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    r = np.hypot((x - cx) / rx, (y - cy) / ry)
    # (a wide, smooth falloff: no edge where the face's grade stops)
    t = np.clip((1.6 - r) / 0.6, 0, 1)
    w = (t * t * (3 - 2 * t))[..., None]
    mean = float(_luma(a)[r < 1].mean())
    g = np.log(target) / np.log(max(1e-3, min(mean, 0.99)))
    d = a ** g
    # (a gamma on a yellowish face saturates it: half its color taken back)
    d = d * 0.5 + _luma(d)[..., None] * 0.5
    warm = np.clip(d * np.array([1.06, 0.97, 0.86], np.float32), 0, 1)
    a = a * (1 - w) + warm * w
    return Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')


# ---------------------------------------------------------------- previews

def source_preview(civ_name, im):
    """The whole source, with a grid every tenth of its width, and the crop."""
    title, box, _ = SOURCES[civ_name]
    pv = im.copy()
    pv.thumbnail((900, 900))
    s = pv.width
    dr = ImageDraw.Draw(pv)
    for i in range(1, int(10 * pv.height / s) + 1):
        dr.line([(0, i * s / 10), (pv.width, i * s / 10)], fill=(0, 255, 255), width=1)
        dr.text((2, i * s / 10 + 1), f'{i / 10:.1f}', fill=(255, 255, 0))
    for i in range(1, 10):
        dr.line([(i * s / 10, 0), (i * s / 10, pv.height)], fill=(0, 255, 255), width=1)
        dr.text((i * s / 10 + 2, 2), f'{i / 10:.1f}', fill=(255, 255, 0))
    l, t, side = (v * s for v in box)
    dr.rectangle([l, t, l + side, t + side], outline=(255, 0, 0), width=3)
    os.makedirs(os.path.join(PREVIEWS, 'leaderheads_sources'), exist_ok=True)
    pv.save(os.path.join(PREVIEWS, 'leaderheads_sources', f'{civ_name}.jpg'), quality=85)


def contact_sheet(done):
    cols, pad = 8, 6
    rows = (len(done) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * (SIZE + pad) + pad, rows * (SIZE + pad + 14) + pad), (210, 210, 210))
    dr = ImageDraw.Draw(sheet)
    for i, (name, img) in enumerate(done):
        r, c = divmod(i, cols)
        x, y = pad + c * (SIZE + pad), pad + r * (SIZE + pad + 14)
        sheet.paste(img, (x, y))
        dr.text((x + 2, y + SIZE + 1), name, fill=(0, 0, 0))
    os.makedirs(PREVIEWS, exist_ok=True)
    path = os.path.join(PREVIEWS, 'leaderheads.png')
    sheet.save(path)
    return path


# ---------------------------------------------------------------- credits

def write_credits(civs):
    lines = [
        'Leader portraits (standalone mode)',
        '==================================',
        '',
        'Made by ArtGen/leaderheads/build.py from public domain and CC0 pictures',
        'on Wikimedia Commons: cropped to the head and shoulders, scaled, and given',
        'a shared painted finish (ArtGen/leaderheads/paint.py: edge-preserving stroke',
        'smoothing, one palette, one painted backdrop, brush texture; image processing',
        'only, nothing generated), a color grade, vignette and edge.',
        '',
    ]
    for civ in civs:
        name, leader = civ['name'], civ.get('leader', '')
        title, _, note = SOURCES[name]
        info = file_info(title)
        lines += [
            f'{name}.png ({leader})',
            f'  {note}',
            f'  Source:  {info["page"]}',
            f'  Artist:  {ARTIST.get(name, info["artist"])}',
            f'  License: {info["license"]}',
            '',
        ]
    with open(os.path.join(OUT, 'CREDITS.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))


# ---------------------------------------------------------------- main

def civilizations():
    with open(RULESET, encoding='utf-8') as f:
        return [c for c in json.load(f)['civilizations'] if not c.get('isBarbarian')]


UNIFIED_SAMPLES = ['America', 'Rome', 'Byzantines', 'England', 'India', 'Hittites']


def unified_samples():
    """Before and after the shared painted finish, side by side, for a few
    portraits of each kind of source (photos, a bust, a mosaic, a painting,
    a statue), to ArtGen/_previews/leaderheads_unified_samples.png, with
    both at the size the game shows them underneath. Doesn't touch the
    game's pictures."""
    pad, label, small = 8, 16, 57
    cols = len(UNIFIED_SAMPLES)
    sheet = Image.new('RGB', (cols * (SIZE + pad) + pad, 2 * (SIZE + pad + label) + 2 * pad + small),
                      (205, 205, 205))
    dr = ImageDraw.Draw(sheet)
    for i, name in enumerate(UNIFIED_SAMPLES):
        x = pad + i * (SIZE + pad)
        for row, painted in enumerate((False, True)):
            img, _ = make(name, painted=painted)
            y = pad + row * (SIZE + pad + label)
            sheet.paste(img, (x, y + label))
            dr.text((x + 2, y + 2), f'{name}: {"painted" if painted else "now"}', fill=(0, 0, 0))
            sheet.paste(img.resize((small, small), Image.LANCZOS),
                        (x + row * (small + 6), 2 * (SIZE + pad + label) + pad))
        print(f'  {name}', flush=True)
    path = os.path.join(PREVIEWS, 'leaderheads_unified_samples.png')
    os.makedirs(PREVIEWS, exist_ok=True)
    sheet.save(path)
    return path


def main(argv):
    opts = {a[2:] for a in argv if a.startswith('--')}
    if not opts & {'painted-set', 'unified-samples'}:
        sys.exit('The game uses the lifelike portraits (lifelike.py --install); '
                 'pass --painted-set to overwrite them with the painted ones')
    if 'unified-samples' in opts:
        print(f'samples: {unified_samples()}')
        return
    names = [a.lower() for a in argv if not a.startswith('--')]
    all_civs = civilizations()
    missing = [c['name'] for c in all_civs if c['name'] not in SOURCES]
    if missing:
        sys.exit(f'No portrait source for {", ".join(missing)}: add them to SOURCES')
    civs = [c for c in all_civs if not names or c['name'].lower() in names]
    painted = 'flat' not in opts
    done, pairs = [], []
    for civ in civs:
        name = civ['name']
        img, info = make(name, painted=painted)
        img.save(os.path.join(OUT, f'{name}.png'), optimize=True)
        if 'sources' in opts:
            source_preview(name, source_image(SOURCES[name][0])[0])
        if 'compare' in opts:
            pairs.append((name, make(name)[0], img))
        print(f'  {name:12} {info["license"]:14} {info["title"]}', flush=True)
        done.append((name, img))
    if not names:
        write_credits(all_civs)
    if pairs:
        print(f'before and after: {compare_sheet(pairs)}')
    print(f'{len(done)} leader heads; preview: {contact_sheet(done)}')


def compare_sheet(pairs):
    """Each portrait without and with the painted finish, side by side, to
    ArtGen/_previews/leaderheads_unified_all.png."""
    cols, pad, label = 4, 6, 14
    rows = (len(pairs) + cols - 1) // cols
    cw = 2 * SIZE + 3 * pad
    sheet = Image.new('RGB', (cols * cw + pad, rows * (SIZE + pad + label) + pad), (205, 205, 205))
    dr = ImageDraw.Draw(sheet)
    for i, (name, before, after) in enumerate(pairs):
        r, c = divmod(i, cols)
        x, y = pad + c * cw, pad + r * (SIZE + pad + label)
        sheet.paste(before, (x, y))
        sheet.paste(after, (x + SIZE + pad, y))
        dr.text((x + 2, y + SIZE + 1), f'{name}: before / painted', fill=(0, 0, 0))
    path = os.path.join(PREVIEWS, 'leaderheads_unified_all.png')
    os.makedirs(PREVIEWS, exist_ok=True)
    sheet.save(path)
    return path


if __name__ == '__main__':
    main(sys.argv[1:])
