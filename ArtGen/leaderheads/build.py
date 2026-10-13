"""Makes the leader heads for standalone mode (no Civ3 files): a head and
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
    'Greece': ('File:Alejandro Magno, Alexander The Great Bust Alexander BM 1857 (cropped).jpg', (0.0, 0.0, 1.0),
               'Alexander the Great: Hellenistic marble head in the British Museum'),
    'Babylon': ('File:Tête royale dite tête de Hammurabi - Musée du Louvre Antiquités orientales SB 95.jpg',
                (0.05, 0.05, 0.90), 'Hammurabi: the diorite royal head known as the "head of Hammurabi", Louvre'),
    'Germany': ('File:Franz von Lenbach - Bildnis Otto Eduard Leopold von Bismarck (Indianapolis).jpg',
                (0.18, 0.12, 0.66), 'Otto von Bismarck by Franz von Lenbach, 1879 (Indianapolis Museum of Art)'),
    'Russia': ('File:Catherine II after Roslin, Rokotov (1780s, Kunsthistorisches Museum).jpg', (0.1, 0.0, 0.8),
               'Catherine the Great by Fyodor Rokotov after Roslin, 1780s'),
    'China': ('File:Mao Zedong sitting (3x4 cropped)(2).jpg', (0.0, 0.0, 1.0),
              'Mao Zedong, official portrait photograph'),
    'America': ('File:Abraham Lincoln O-77 matte collodion print.jpg', (0.1, 0.05, 0.8),
                'Abraham Lincoln photographed by Alexander Gardner, 1863'),
    'Japan': ('File:Tokugawa Ieyasu2.JPG', (0.29, 0.40, 0.28),
              'Tokugawa Ieyasu by Kano Tan\'yu, 17th century'),
    'France': ('File:Dante Gabriel Rossetti - Joan of Arc (1882).jpg', (0.25, 0.0, 0.66),
               'Joan of Arc by Dante Gabriel Rossetti, 1882 (no likeness from her lifetime survives)'),
    'India': ('File:Mohandas K. Gandhi, portrait.jpg', (0.05, 0.05, 0.9),
              'Mohandas K. Gandhi, studio photograph'),
    'Persia': ('File:Ahasuerus by Ernest Normand.jpg', (0.12, 0.10, 0.80),
               'Xerxes I as Ahasuerus: detail of a painting by Ernest Normand, 19th century (his own reliefs at Persepolis are defaced)'),
    'Aztecs': ('File:Moctezuma Xocoyotzin.png', (0.2, 0.05, 0.6),
               'Moctezuma II, 17th-century painting by Antonio Rodriguez'),
    'Zululand': ('File:Shaka Zulu incisione ottocentesca.jpg', (0.22, 0.27, 0.55),
                 'Shaka, 19th-century engraving after the drawing by James King'),
    'Iroquois': ('File:Hiawatha MET DP371840.jpg', (0.08, 0.10, 0.84),
                 'Hiawatha: marble bust by Edmonia Lewis, 1868 (Metropolitan Museum of Art)'),
    'England': ('File:Darnley stage 3.jpg', (0.17, 0.0, 0.52),
                'Elizabeth I: the "Darnley Portrait", about 1575'),
    'Mongols': ('File:YuanEmperorAlbumGenghisPortrait.jpg', (0.15, 0.05, 0.7),
                'Genghis Khan (Temujin): portrait from the Yuan emperors\' album, National Palace Museum, Taipei'),
    'Spain': ('File:Juan de Flandes - Isabel la Católica (Palacio Real, Madrid, 1500-04. Óleo sobre tabla, 63 x 55 cm).jpg',
              (0.1, 0.05, 0.8), 'Isabella I of Castile by Juan de Flandes, about 1500'),
    'Scandinavia': ('File:Rayner Lothbroc & Kraka by August Malmström c 1880.jpg', (0.215, 0.335, 0.25),
                    'Ragnar Lodbrok meeting Kraka: wood engraving after August Malmstrom, about 1880'),
    'Ottomans': ('File:Paolo Veronese (Nachfolger) - Sultan Osman I. - 2242 - Bavarian State Painting Collections.jpg',
                 (0.1, 0.05, 0.8), 'Osman I, 16th-century Venetian portrait (follower of Paolo Veronese)'),
    'Celts': ('File:Brennus.jpg', (0.20, 0.28, 0.44),
              'Brennus: wood engraving by Therond of the statue by Ferdinand Taluet, 19th century'),
    'Arabia': ('File:The first caliph Abu Bakr.png', (0.0, 0.0, 1.0),
               'Abu Bakr: calligraphic roundel of his name, as he is shown in Islamic tradition (no likeness exists)'),
    'Carthage': ('File:Hannibal Barca bust from Capua photo.jpg', (0.1, 0.0, 0.8),
                 'Hannibal: the marble bust found at Capua'),
    'Korea': ('File:초등대한역사 제3편 1.jpg', (0.0, 0.0, 1.0),
              'Wang Kon (Taejo of Goryeo): illustration from a 1908 Korean history textbook'),
    'Sumeria': ('File:Hero lion Dur-Sharrukin Louvre AO19862.jpg', (0.20, 0.06, 0.52),
                'Gilgamesh: the "hero mastering a lion" relief from Dur-Sharrukin, Louvre'),
    'Hittites': ('File:Priest-King or Diety, about 1600 BC, Hittite, North Syria, basalt with bone eyes - Cleveland Museum of Art - DSC08114.JPG',
                 (0.33, 0.10, 0.36), 'A Hittite priest-king, basalt statue of about 1600 BC, Cleveland Museum of Art (no likeness of Mursili survives)'),
    'Netherlands': ('File:William I, Prince of Orange by Adriaen Thomasz. Key Rijksmuseum Amsterdam SK-A-3148.jpg',
                    (0.1, 0.05, 0.8), 'William the Silent by Adriaen Thomasz. Key, about 1579'),
    'Portugal': ('File:Henry the Navigator1.jpg', (0.0, 0.0, 1.0),
                 'Henry the Navigator: detail of the Saint Vincent Panels by Nuno Goncalves, 1470s'),
    'Byzantines': ('File:Mosaic of empress Theodora, Ravenna, San Vitale, 547.jpg', (0.15, 0.10, 0.62),
                   'Empress Theodora: mosaic in the Basilica of San Vitale, Ravenna, 547'),
    'Inca': ('File:Brooklyn Museum - Pachacuti, Tenth Inca, 1 of 14 Portraits of Inca Kings (cropped).jpg',
             (0.1, 0.05, 0.8), 'Pachacuti: Cuzco School painting, 18th century (Brooklyn Museum)'),
    'Maya': ('File:Copán Stela A 01.jpg', (0.36, 0.30, 0.32),
             'Stela A at Copan, showing 18 Rabbit, Smoke-Jaguar\'s successor (no public domain likeness of Smoke-Jaguar (Chan Imix K\'awiil) himself was found)'),
}

# Pen drawings whose fine lines would fade when scaled down: their lines are
# darkened by this gamma.
INK = {'Korea': 2.2}


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
    return [n.strip() for n in names if re.match(r'\s*(pd|cc|gfdl|fal\b|attribution|licensed|public)', n, re.I)]


def is_free(info):
    """Only public domain and CC0 pictures are used (no attribution or
    share-alike terms on the game's art): the license is public domain or
    CC0, and no license on the page asks for attribution or share-alike."""
    if not re.match(r'(public domain|pd\b|pd-|cc0)', info['license'].strip(), re.I):
        return False
    return not any(re.match(r'(cc-by|gfdl|fal\b|attribution)', t, re.I) for t in info['license_templates'])


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

def crop(im, box):
    """The square (left, top, side), in fractions of the width, scaled to
    SIZE. Parts outside the picture are filled with its edge colors."""
    w, h = im.size
    left, top, side = (v * w for v in box)
    x0, y0, s = int(round(left)), int(round(top)), max(1, int(round(side)))
    if x0 < 0 or y0 < 0 or x0 + s > w or y0 + s > h:
        pad = max(-x0, -y0, x0 + s - w, y0 + s - h, 0)
        im = ImageOps.expand(im, border=pad, fill=tuple(int(v) for v in np.asarray(im).reshape(-1, 3).mean(0)))
        x0, y0 = x0 + pad, y0 + pad
    sq = im.crop((x0, y0, x0 + s, y0 + s))
    # downscale in steps for a clean result from big sources
    while sq.width > SIZE * 3:
        sq = sq.resize((sq.width // 2, sq.height // 2), Image.LANCZOS)
    return sq.resize((SIZE, SIZE), Image.LANCZOS)


def finish(im):
    """The same gentle grade for every portrait: levels, slightly muted and
    warmed color, a little sharpening, a soft vignette and a beveled edge."""
    im = ImageOps.autocontrast(im, cutoff=(0.4, 0.4), preserve_tone=True)
    im = ImageEnhance.Color(im).enhance(0.88)
    im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=45, threshold=2))
    a = np.asarray(im, np.float32) / 255
    # warm: a touch of old varnish over everything
    a = a * np.array([1.03, 1.0, 0.92], np.float32) + np.array([0.012, 0.006, 0.0], np.float32)
    n = SIZE
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    d = np.hypot((x - n / 2) / (n * 0.62), (y - n * 0.45) / (n * 0.66))
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


def make(civ_name):
    title, box, _ = SOURCES[civ_name]
    im, info = source_image(title)
    im = crop(im, box)
    if civ_name in INK:
        g = INK[civ_name]
        im = im.point(lambda v: int(255 * (v / 255) ** g + 0.5))
    return finish(im), info


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
        'a light color grade, vignette and edge.',
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
            f'  Artist:  {info["artist"]}',
            f'  License: {info["license"]}',
            '',
        ]
    with open(os.path.join(OUT, 'CREDITS.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))


# ---------------------------------------------------------------- main

def civilizations():
    with open(RULESET, encoding='utf-8') as f:
        return [c for c in json.load(f)['civilizations'] if not c.get('isBarbarian')]


def main(argv):
    opts = {a[2:] for a in argv if a.startswith('--')}
    names = [a.lower() for a in argv if not a.startswith('--')]
    all_civs = civilizations()
    missing = [c['name'] for c in all_civs if c['name'] not in SOURCES]
    if missing:
        sys.exit(f'No portrait source for {", ".join(missing)}: add them to SOURCES')
    civs = [c for c in all_civs if not names or c['name'].lower() in names]
    done = []
    for civ in civs:
        name = civ['name']
        img, info = make(name)
        img.save(os.path.join(OUT, f'{name}.png'), optimize=True)
        if 'sources' in opts:
            source_preview(name, source_image(SOURCES[name][0])[0])
        print(f'  {name:12} {info["license"]:14} {info["title"]}', flush=True)
        done.append((name, img))
    if not names:
        write_credits(all_civs)
    print(f'{len(done)} leader heads; preview: {contact_sheet(done)}')


if __name__ == '__main__':
    main(sys.argv[1:])
