"""Makes lifelike candidate portraits of the leaders: a realistic color
photograph of each leader, generated on this machine with FLUX.2 [klein] 4B
(Apache-2.0) through mflux (https://github.com/filipstrand/mflux).

    python ArtGen/leaderheads/lifelike.py                 # every leader
    python ArtGen/leaderheads/lifelike.py Rome Egypt      # just these
    python ArtGen/leaderheads/lifelike.py --seeds 1 2 3   # these seeds (default 1 2 3)

Where a likeness survives (a bust, painting or photograph; see build.py's
SOURCES), it is the reference picture: the model is asked to turn it into a
photograph of the living person, keeping the face. Where none does, the
reference is the stand-in build.py uses. A few leaders start from another
picture than build.py's (REFERENCES), and with no reference at all the picture
comes from the description alone.

Candidates go to ArtGen/leaderheads/_candidates/<civ>/seed<N>.png (not
committed), with a contact sheet per run in ArtGen/_previews/
leaderheads_lifelike.png. The one picked for each leader is copied by hand to
ArtGen/leaderheads/lifelike/<civ>.jpg (committed), and

    python ArtGen/leaderheads/lifelike.py --install

turns those into the game's portraits, C7/ModernArt/Art/Leaderheads/<civ>.png
(230x230), and writes CREDITS.txt there.

Needs mflux on the PATH (uv tool install mflux) and the ArtGen venv for
build.py's helpers (Pillow, numpy, scipy).
"""
import os
import shutil
import subprocess
import sys
import time

from PIL import Image, ImageDraw

import build

HERE = os.path.dirname(os.path.abspath(__file__))
CANDIDATES = os.path.join(HERE, '_candidates')
MODEL = 'flux2-klein-4b'
QUANTIZE = '8'
SIZE = 768

# Every portrait is the same kind of photograph, so the set looks of a piece.
STYLE = ('Realistic color photograph, head and shoulders portrait, the face centered and turned slightly '
         'toward the camera, soft natural light from the upper left, plain dark warm-gray studio '
         'backdrop, 85mm lens, shallow depth of field, natural skin texture with pores and fine '
         'lines, photorealistic, sharp focus on the eyes. Period-accurate clothing of real handwoven '
         'fabric with natural folds, not modern garments. Not a painting, not a sculpture, no text, '
         'no frame.')

EDIT = ('Turn this {kind} into a realistic photograph of the living person it shows. Keep the same '
        'face: the same face shape, features, proportions, wrinkles, age and expression, the same '
        'hairline and hairstyle, and the same pose. ')

# For leaders whose look differs a lot from their reference (another age, build or
# costume), the reference only guides the general look.
EDIT_LOOSE = ('Turn this {kind} into a realistic photograph of the person described below. Use the {kind} '
              'as a reference for the regalia and general look, but follow the description for age, build, '
              'face shape, expression and head angle wherever it differs. ')
LOOSE = {'Maya', 'Inca', 'Aztecs', 'Babylon', 'Egypt', 'Celts', 'Scandinavia', 'Hittites', 'Russia', 'Spain'}

# Each leader: what the reference picture is (None: no reference), and who to show.
LEADERS = {
    'Rome': ('marble bust', 'Julius Caesar, Roman general and dictator, about 55 years old: Mediterranean '
             'olive skin, short dark hair combed forward over a receding hairline, a deeply lined '
             'forehead, hollow cheeks, a stern weathered face, brown eyes. He is dressed as a general on '
             'campaign: a molded cuirass of bronze and dark leather with embossed shoulder plates and '
             'bronze studs, a deep crimson wool military cloak pinned at the shoulder with a round '
             'bronze brooch, and a crimson wool scarf knotted at the neck.'),
    'Egypt': ('marble portrait head', 'Cleopatra VII, Pharaoh and queen of Egypt, about 30 years old, majestic and commanding: olive skin, dark eyes lined heavily with black kohl and green malachite, a proud imperious gaze straight at the camera, chin raised; a heavy black braided royal wig to the shoulders, crowned with a gold vulture crown whose wings fold down over the wig, a gold triple uraeus of rearing cobras at the brow, and a gold modius crown of cow horns cradling a red sun disk on top; heavy gold earrings, a wide gold-and-lapis-lazuli broad collar covering the shoulders, a gold-pleated white linen gown and a gold cuff on the upper arm. Royal splendor of a pharaoh, rich gold and deep blue.'),
    'Greece': ('marble head', 'Alexander the Great, king of Macedon, about 25 years old, clean-shaven: '
               'thick tousled wavy light-brown hair swept up from the forehead, an intense upward gaze, '
               'wearing a bronze cuirass over a red tunic.'),
    'Babylon': ('diorite statue head', 'Hammurabi, king of Babylon, about 55 years old, an Amorite of Mesopotamia: olive-brown skin, dark eyes, a long full black beard in tight rows of curls streaked with gray, a tall stiff drum-shaped royal cap with a thick rolled rim, smooth and hard like molded leather or stiff felt, its sides straight and upright; a fringed wool robe draped over one shoulder. No knit cap, no beanie, no watch cap.'),
    'Germany': ('oil painting', 'Otto von Bismarck, German chancellor, about 64 years old: bald on top, '
                'a thick white mustache, heavy brows, pale skin, wearing a dark Prussian military frock '
                'coat with a high collar.'),
    'Russia': ('oil painting', 'Catherine the Great at her coronation in 1762, a young and beautiful woman about 33 years old: fair luminous skin, bright blue eyes, a fine straight nose, rosy cheeks, a soft confident smile, powdered light-brown hair dressed up under the jeweled Great Imperial Crown of Russia studded with diamonds and pearls and topped with a red spinel and a cross, a silver brocade gown, an ermine-lined gold mantle embroidered with double-headed eagles, the light-blue sash of the Order of Saint Andrew.'),
    'China': ('black-and-white photograph', 'Mao Zedong, about 57 years old: East Asian, a broad face, '
              'dark hair swept back from a high forehead, wearing a gray buttoned Sun Yat-sen tunic suit. '
              'In natural color.'),
    'America': ('black-and-white photograph', 'Abraham Lincoln, president of the United States, 54 '
                'years old: gaunt craggy face, a dark chin beard without a mustache, untidy dark hair, '
                'deep-set gray eyes, wearing a black frock coat, white shirt and black bow tie. In natural '
                'color.'),
    'Japan': ('Japanese ink and color painting', 'Tokugawa Ieyasu, shogun of Japan, about 60 years old: '
              'Japanese, a round full face, a thin mustache and small beard, wearing a black lacquered '
              'court cap and a black silk court robe with wide shoulders.'),
    'France': ('oil painting', 'Joan of Arc, about 19 years old, a French peasant girl turned soldier: '
               'fair skin, short dark-brown hair cut in a bob, a determined face, wearing polished steel '
               'plate armor.'),
    'India': ('black-and-white photograph', 'Mohandas Gandhi, about 70 years old: Indian, brown skin, '
              'bald, round wire-rimmed glasses, a gentle smile, a white handspun khadi shawl over his '
              'shoulders. In natural color.'),
    'Persia': ('oil painting', 'Xerxes I, Achaemenid king of kings of Persia, about 35 years old: olive skin, dark eyes, a long squared black beard in tight rows of curls, long curled hair, a tall fluted cylindrical royal crown of hammered gold set with jewels, heavy gold earrings, a draped robe of rich purple wool with wide pleated sleeves and a gold-embroidered border. No modern shirt.'),
    'Aztecs': ('painting', 'Moctezuma II, huey tlatoani of the Aztec empire, a younger lean man about 38 years old with a narrow refined face, high cheekbones, a thin wispy mustache, a slight haughty tilt of the chin and a proud, cold, appraising look: light golden-brown skin, straight black hair cut at the shoulders with straight bangs, the xiuhuitzolli, a pointed turquoise mosaic royal diadem rising to a peak at the front, a small gold nose ornament through the septum, jade and gold earspools, a turquoise-blue cotton tilmatli cloak knotted at the right shoulder over a red and gold feathered collar. Palette: turquoise, red, gold. Head turned a little to his right. No headband, no war bonnet.'),
    'Zululand': ('engraving', 'Shaka, king of the Zulu, about 35 years old: dark brown skin, a strong '
                 'build, a short beard, a headband of otter fur with a tall blue crane feather, cowtail '
                 'ornaments around the shoulders.'),
    'Iroquois': ('oil painting', 'Hiawatha, a Haudenosaunee (Mohawk and Onondaga) chief of the 1400s, a man about 35 years old with a strong, rugged masculine face, a heavy brow, a square jaw and a thick neck: warm reddish-brown skin, dark eyes, high cheekbones, fine dark blue-black traditional tattoo lines in geometric bands across the cheeks, chin and upper chest as in the painting, black hair pulled back and tied, white shell ornaments in the ears, a bare tattooed chest with no shirt, a red wool blanket-cloak worn over one shoulder, a calm, dignified, direct gaze. No shirt, no collar, no buttons.'),
    'England': ('oil painting', 'Elizabeth I, queen of England, about 42 years old: very pale skin, '
                'red-gold curled hair, a high starched lace ruff, ropes of pearls, a jeweled embroidered '
                'gown.'),
    'Mongols': ('Chinese ink painting', 'Genghis Khan, founder of the Mongol empire, about 60 years old: '
                'Mongolian, weathered sun-tanned skin, narrow eyes, a long thin gray-streaked beard and '
                'mustache, a white felt hat trimmed with fur, a heavy brocade deel robe.'),
    'Spain': ('oil painting', 'Isabella I, queen of Castile, as a young and beautiful woman about 24 years old at her accession in 1474: fair smooth skin, blue-green eyes, a delicate oval face, rosy cheeks, long auburn-red hair parted in the middle and falling over the shoulders under a sheer white silk veil held by a slender gold circlet set with pearls, a dark crimson velvet gown with a square neckline edged in gold embroidery, a small gold cross pendant, a serene, gentle, regal expression.'),
    'Scandinavia': ('oil painting', 'Ragnar Lodbrok, a legendary Viking sea-king of the 9th century, a huge powerfully built warrior about 45 years old with a thick neck, broad shoulders, a scarred, weathered face and a hard, menacing stare: fair wind-burned skin, ice-blue eyes, long reddish-blond hair and a thick braided beard with iron beads, a round iron spangenhelm helmet with a nasal guard (no horns), a knee-length iron mail byrnie over a padded tunic, a heavy wolf-fur cloak on the shoulders, holding the long wooden haft of a broad Dane axe resting on his right shoulder with its iron blade beside his head. Viking Age Norse, 800s AD.'),
    'Ottomans': ('Renaissance painting', 'Osman I, founder of the Ottoman state, about 60 years old: '
                 'Turkish, olive skin, a long full white-gray beard, a large white wrapped turban, a '
                 'brocade kaftan with a fur collar.'),
    'Celts': ('oil painting', 'Brennus, war chief of the Gauls, a brutal hardened warrior about 40 years old in the likeness of Vercingetorix: a lean hard face with a hawk nose, sunken cheeks, a thick long drooping mustache, no beard, long wild swept-back brown hair, a cold, fierce, menacing glare straight at the camera, battle scars and grime on the skin; a gold hoop earring, a heavy twisted gold torc, a riveted bronze-studded leather cuirass with dark embroidered shoulder guards and gold arm rings, a rough cream wool cloak thrown back over the shoulders. A ruthless warlord, not a hippie, no flowers, no smile. Head turned to face the camera.'),
    'Arabia': ('drawing', 'Abu Bakr, the first caliph, an Arab man of the Hijaz about 50 years old, in the 7th century: warm olive-brown skin, a long face with high cheekbones, a long straight nose, heavy dark brows over deep-set downcast eyes, a full long black beard with a faint reddish henna tint, a large white cloth turban wound high around the head, a loose cream wool robe and a draped white cloak over the shoulders, a quiet, thoughtful, humble expression, head turned and lowered slightly to his right as in the drawing.'),
    'Carthage': ('marble bust', 'Hannibal Barca, Carthaginian general, about 35 years old, of Phoenician '
                 'descent: olive-brown skin, dark eyes, short dark curly hair and a short curly beard, '
                 'wearing a bronze cuirass.'),
    'Korea': ('Korean painting', 'Taejo Wang Geon, founding king of Goryeo, about 50 years old: Korean, '
              'a calm face, a thin mustache and long thin beard, a black court hat with stiff side '
              'wings, a crimson court robe with a square embroidered badge.'),
    'Sumeria': ('diorite statue', "Gilgamesh, Sumerian king of Uruk, about 35 years old: olive-brown skin, large dark eyes, a strong face, a stiff round royal cap of thick fleece with a broad flat brim, like Gudea's statue, not a knit beanie; a fringed wool robe draped over one shoulder."),
    'Hittites': ('ancient basalt statue', 'Mursili II, Hittite great king, a man about 35 years old with a distinctly Anatolian face, unlike a Roman: a broad round face, a very large prominent hooked nose continuing the line of the forehead, large almond-shaped heavy-lidded eyes, full lips, a receding chin, a calm, dignified, faintly smiling expression: olive-brown skin, clean-shaven, long dark hair falling behind the ears, a tall conical cap of stiff white felt with a rolled brim, the Hittite royal headgear; a long fringed wool robe and chunky gold arm rings on bare upper arms. Head turned slightly to his left.'),
    'Netherlands': ('oil painting', 'William the Silent, prince of Orange, about 46 years old: fair skin, tired heavy-lidded dark eyes, short dark-brown hair under a small black silk skullcap, a trimmed beard and mustache, a black velvet doublet with a small pleated white millstone ruff at the neck. No buttons, no shirt collar, no clerical clothing.'),
    'Portugal': ('15th-century panel painting', 'Henry the Navigator, prince of Portugal, about 60 '
                 'years old: pale skin, a long thin face, a thin dark mustache, a large soft black chaperon '
                 'hat of draped and rolled wool with no brim, a high-necked black wool doublet and gown of '
                 '15th-century mourning dress. No white collar, no buttons, no clerical clothing.'),
    'Byzantines': ('Byzantine mosaic', 'Empress Theodora, about 45 years old: Mediterranean, olive skin, '
                   'large dark eyes, a jeweled pearl crown with long strands of pearls hanging beside the '
                   'face, a purple silk cloak with a jeweled collar.'),
    'Inca': ('colonial painting', 'Pachacuti, Sapa Inca, a stocky heavy-boned man about 60 years old with a distinctly Andean face, unlike a Roman or European: a broad round flat face with very wide high cheekbones, a broad short nose, full lips, narrow dark eyes, a thick neck, and an impassive, unreadable, still expression: deep reddish-brown sun-darkened skin, cropped black hair, the royal mascaypacha: a braided multicolored llautu band wound around the head with a red wool fringe hanging across the forehead and a small gold plaque and upright black-and-white feathers above it; huge flat round gold ear spools in stretched earlobes; a fine woven unku tunic of red, gold and black tocapu squares. Facing the camera squarely. No dangling earrings, no feather crown.'),
    'Maya': ('stucco portrait head', 'Smoke-Jaguar, Classic Maya king of Copan, a heavy-set man about 50 years old with a fleshy jowled face, full cheeks and a calm, serene, half-lidded gaze: warm copper-brown skin, the long sloping forehead and high-bridged aquiline nose of a Maya noble, black hair bound up high on the crown, large flat round jade ear-flare discs set in the earlobes, a broad collar of polished jade beads, a little red cinnabar paint on the cheekbones, and a towering headdress of long iridescent quetzal-green tail feathers rising from a carved jaguar-mask frame. Palette: jade and quetzal green, red cinnabar. Head turned a little to his left, toward a three-quarter view. No earrings, no peacock feathers, no headband.'),
}


# Leaders whose lifelike portrait starts from another picture than build.py's
# source: the Commons file and the square to crop, as in build.SOURCES. These
# may also be CC BY (credited), since the portrait is a new picture after them.
REFERENCES = {
    'Arabia': ('File:Portrait of Rhazes (al-Razi) (AD 865 - 925) Wellcome L0005053.jpg', (0.15, 0.12, 0.75),
               'A typical early Muslim scholar, standing in for Abu Bakr, of whom no likeness exists: '
               'an imagined portrait of the physician al-Razi, Wellcome Collection',
               'Wellcome Collection'),
    'Iroquois': ('File:345Chief Sa Ga Yeath Qua Pieth Tow by John Verelst, 1710. Archives of Canada..jpg',
                 (0.25, 0.0, 0.5),
                 'A Haudenosaunee leader, standing in for Hiawatha, of whom no likeness survives: the Mohawk '
                 'leader Sa Ga Yeath Qua Pieth Tow by John Verelst, 1710 (Library and Archives Canada)',
                 'John Verelst'),
    'Celts': ('File:Lionel Royer - Vercingetorix Throwing down His Weapons at the feet of Julius Caesar.jpg',
              (0.215, 0.065, 0.14),
              'A Gallic war chief, standing in for Brennus, of whom no likeness survives: Vercingetorix in '
              '"Vercingetorix Throws Down His Arms at the Feet of Julius Caesar" by Lionel Royer, 1899',
              'Lionel Royer'),
    'Russia': ('File:Catherine II by V.Eriksen (1766-7, Statens Museum for Kunst, Denmark).jpg',
               (0.366, 0.415, 0.36),
               'Catherine the Great: coronation portrait by Vigilius Eriksen, 1766-67 (Statens Museum for Kunst)',
               'Vigilius Eriksen'),
}
REFERENCE_LICENSES = ('Public domain', 'CC0', 'CC BY 4.0')


def reference(civ, path):
    """The leader's reference picture, cropped as build.py crops it, at SIZE."""
    if civ in REFERENCES:
        title, box = REFERENCES[civ][:2]
        info = build.file_info(title)
        if info['license'] not in REFERENCE_LICENSES:
            raise SystemExit(f'{title} is {info["license"]}')
        cached = os.path.join(build.CACHE, build._cache_name(title) + '.src.jpg')
        if not os.path.exists(cached):
            with open(cached, 'wb') as f:
                f.write(build._get(info['url']))
        im = Image.open(cached).convert('RGB')
    else:
        title, box, _ = build.SOURCES[civ]
        im, _ = build.source_image(title)
    build.crop(im, box, size=SIZE).save(path)


def generate(civ, seeds):
    kind, who = LEADERS[civ]
    out = os.path.join(CANDIDATES, civ)
    os.makedirs(out, exist_ok=True)
    todo = [s for s in seeds if not os.path.exists(os.path.join(out, f'seed{s}.png'))]
    if not todo:
        return
    if kind:
        ref = os.path.join(out, 'reference.png')
        reference(civ, ref)
        cmd = ['mflux-generate-flux2-edit', '--image-paths', ref, '--prompt', (EDIT_LOOSE if civ in LOOSE else EDIT).format(kind=kind) + who + ' ' + STYLE]
    else:
        cmd = ['mflux-generate-flux2', '--prompt', who + ' ' + STYLE]
    cmd += ['--model', MODEL, '--quantize', QUANTIZE, '--width', str(SIZE), '--height', str(SIZE),
            '--seed', *map(str, todo), '--output', os.path.join(out, 'gen.png')]
    t = time.time()
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    # mflux names the pictures gen_seed_<N>.png when it makes several, gen.png for one
    for s in todo:
        made = os.path.join(out, f'gen_seed_{s}.png')
        if not os.path.exists(made):
            made = os.path.join(out, 'gen.png')
        os.replace(made, os.path.join(out, f'seed{s}.png'))
    print(f'  {civ:<12} {len(todo)} in {time.time() - t:.0f}s', flush=True)


def contact_sheet(civs, seeds):
    """A row per leader: the reference (if any), then each candidate."""
    cell, label = 192, 18
    cols = 1 + len(seeds)
    sheet = Image.new('RGB', (cols * cell, len(civs) * (cell + label)), (24, 22, 20))
    draw = ImageDraw.Draw(sheet)
    for r, civ in enumerate(civs):
        y = r * (cell + label)
        draw.text((4, y + 3), civ, fill=(230, 220, 200))
        names = ['reference.png'] + [f'seed{s}.png' for s in seeds]
        for c, name in enumerate(names):
            p = os.path.join(CANDIDATES, civ, name)
            if os.path.exists(p):
                sheet.paste(Image.open(p).convert('RGB').resize((cell, cell), Image.LANCZOS), (c * cell, y + label))
    os.makedirs(build.PREVIEWS, exist_ok=True)
    path = os.path.join(build.PREVIEWS, 'leaderheads_lifelike.png')
    sheet.save(path)
    return path


# ---------------------------------------------------------------- install

# The chosen portraits, one per leader, as ArtGen/leaderheads/lifelike/<civ>.jpg
# (768x768, committed): picked by the owner from the candidates in review rounds
# on 2026-10-14. Some were made with an earlier wording of LEADERS than the one
# above, some on another machine (diffusers' Flux2KleinPipeline, 8-bit), and a
# few were touched up by a second edit of the picked candidate (FIXES).
CHOSEN = os.path.join(HERE, 'lifelike')

# Leaders whose chosen picture was made without a reference picture.
NO_REFERENCE = {'Hittites'}

# Leaders whose picture wasn't made from their reference in one edit.
FIXES = {
    'Hittites': 'made from the description alone (no likeness of Mursili II survives), then its cap '
                'changed by a second edit; the description follows the Hittite royal reliefs',
    'Egypt': 'its crown changed to a gold circlet with a uraeus by a second edit',
}


def install_picture(civ):
    """The game's portrait from a chosen picture: scaled to build.SIZE with
    build.py's edge, vignette and grade (without its levels)."""
    im = Image.open(os.path.join(CHOSEN, f'{civ}.jpg')).convert('RGB')
    while im.width > build.SIZE * 3:
        im = im.resize((im.width // 2, im.height // 2), Image.LANCZOS)
    im = im.resize((build.SIZE, build.SIZE), Image.LANCZOS)
    return build.finish(im, sharpen=25, level=False)


def write_credits(civs):
    lines = [
        'Leader portraits (standalone mode)',
        '==================================',
        '',
        'Made by ArtGen/leaderheads/lifelike.py: realistic photographs of the leaders',
        'generated with FLUX.2 [klein] 4B (Black Forest Labs, Apache-2.0), each from a',
        'historical picture of the leader (or of a stand-in where no likeness survives)',
        'and a written description of how they looked, then scaled with a vignette',
        'and edge. They are imagined likenesses, not photographs. The pictures they',
        'were made from:',
        '',
    ]
    for civ in civs:
        name, leader = civ['name'], civ.get('leader', '')
        lines.append(f'{name}.png ({leader})')
        if name in FIXES:
            lines.append(f'  Note: {FIXES[name]}')
        if name in NO_REFERENCE:
            lines.append('')
            continue
        if name in REFERENCES:
            title, _, note, artist = REFERENCES[name]
        else:
            title, _, note = build.SOURCES[name]
            artist = None
        info = build.file_info(title)
        lines += [
            f'  After:   {note}',
            f'  Source:  {info["page"]}',
            f'  Artist:  {artist or build.ARTIST.get(name, info["artist"])}',
            f'  License: {info["license"]}',
            '',
        ]
    with open(os.path.join(build.OUT, 'CREDITS.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))


def install():
    civs = build.civilizations()
    missing = [c['name'] for c in civs if not os.path.exists(os.path.join(CHOSEN, f'{c["name"]}.jpg'))]
    if missing:
        raise SystemExit(f'No chosen portrait for {", ".join(missing)} in {CHOSEN}')
    done = []
    for civ in civs:
        im = install_picture(civ['name'])
        im.save(os.path.join(build.OUT, f'{civ["name"]}.png'), optimize=True)
        done.append((civ['name'], im))
    write_credits(civs)
    print(f'{len(done)} leader heads; preview: {build.contact_sheet(done)}')


def main(argv):
    if argv == ['--install']:
        install()
        return
    seeds = [1, 2, 3]
    if '--seeds' in argv:
        i = argv.index('--seeds')
        rest = argv[i + 1:]
        n = next((j for j, a in enumerate(rest) if not a.isdigit()), len(rest))
        seeds, argv = [int(s) for s in rest[:n]], argv[:i] + rest[n:]
    if not shutil.which('mflux-generate-flux2-edit'):
        raise SystemExit('mflux is not on the PATH (uv tool install mflux)')
    civs = argv or list(LEADERS)
    for civ in civs:
        generate(civ, seeds)
    print('contact sheet:', contact_sheet(civs, seeds))


if __name__ == '__main__':
    main(sys.argv[1:])
