"""Naval units (the 'ship' archetype, designs in models/ship_designs.py).

Entry keys: design (builder name), colors (material overrides), tint
(materials drawn in the civ color besides 'tint'/'sail_tint'), scale (fine
size factor; the length is fitted to the original automatically), anchor
(override of the fitted waterline anchor), fire_t (normalized time the
weapons fire), modern (explosions on death), bob, kick, death_roll,
death_side, sink_start, sink_end.
"""

WOOD_ANCIENT = {'wood': '#b89a5c', 'wood_dark': '#5e4628', 'wood_light': '#dcc088', 'deck': '#c0a470',
                'rail': '#5c4628', 'sail': '#f2e2b6'}
WOOD_SAIL = {'wood': '#7e6648', 'wood_dark': '#463626', 'wood_light': '#b8a27c', 'deck': '#8a7658',
             'rail': '#4a3a28', 'sail': '#e2d6bc'}
STEEL = {'steel': '#687078', 'steel_dark': '#3a3e46', 'steel_light': '#a2a8b0', 'deck_steel': '#4c5058'}


def _c(*ds, **kw):
    out = {}
    for d in ds:
        out.update(d)
    out.update(kw)
    return out


UNITS = {
    'Galley': {'archetype': 'ship', 'brace': 18, 'billow_k': 1.4, 'design': 'galley', 'colors': WOOD_ANCIENT, 'fire_t': 0.1},
    'Curragh': {'archetype': 'ship', 'brace': 18, 'billow_k': 1.4, 'rig_h': 1.2, 'rig_w': 1.4, 'design': 'curragh', 'fire_t': 0.2,
                'colors': _c(WOOD_ANCIENT, wood='#7e6a4a', wood_dark='#40321f', wood_light='#b0a074',
                             deck='#968458')},
    'Dromon': {'archetype': 'ship', 'brace': 18, 'billow_k': 1.4, 'design': 'dromon', 'colors': WOOD_ANCIENT, 'fire_t': 0.1},
    'Caravel': {'archetype': 'ship', 'brace': 20, 'billow_k': 1.8, 'rig_h': 1.3, 'rig_w': 1.45, 'design': 'caravel', 'colors': _c(WOOD_SAIL, wood='#80684a')},
    'Carrack': {'archetype': 'ship', 'brace': 20, 'billow_k': 1.8, 'rig_h': 1.22, 'rig_w': 1.0, 'design': 'carrack', 'colors': _c(WOOD_SAIL, wood='#7a5e40')},
    'Galleon': {'archetype': 'ship', 'brace': 20, 'billow_k': 1.8, 'rig_h': 1.12, 'rig_w': 1.1, 'design': 'galleon', 'colors': _c(WOOD_SAIL, wood='#5a4e42', gold='#a09478', deck='#827562', wood_light='#b0a084')},
    'Frigate': {'archetype': 'ship', 'brace': 20, 'billow_k': 1.8, 'rig_h': 1.25, 'rig_w': 1.3, 'design': 'frigate', 'fire_t': 0.04, 'colors': _c(WOOD_SAIL, wood='#7a6248')},
    'Privateer': {'archetype': 'ship', 'brace': 20, 'billow_k': 1.8, 'rig_h': 1.4, 'rig_w': 1.2, 'design': 'privateer',
                  'colors': _c(WOOD_SAIL, wood='#4e4c4a', wood_dark='#2c2c2e', rail='#3a3634', wood_light='#9a948a',
                              sail='#d4ccbc', deck='#6e6658')},
    'Man-O-War': {'archetype': 'ship', 'brace': 20, 'billow_k': 1.8, 'rig_h': 1.27, 'rig_w': 1.3, 'design': 'man_o_war',
                  'colors': _c(WOOD_SAIL, wood='#585048', gold='#b0a070', black='#26221e', sail='#e4ddce')},
    'Ironclad': {'archetype': 'ship', 'beam_k': 1.0, 'height_k': 1.1, 'design': 'ironclad', 'modern': True,
                 'colors': _c(STEEL, steel='#646c6a', steel_dark='#3e4246')},
    'Transport': {'archetype': 'ship', 'beam_k': 1.1, 'height_k': 1.0, 'design': 'transport', 'modern': True,
                  'colors': _c(STEEL, steel_dark='#4a4c4e', steel_light='#b8b6aa', tarp='#9aac6c')},
    'Destroyer': {'archetype': 'ship', 'beam_k': 1.5, 'height_k': 2.0, 'design': 'destroyer', 'modern': True, 'colors': STEEL},
    'Cruiser': {'archetype': 'ship', 'beam_k': 1.45, 'height_k': 1.5, 'design': 'cruiser', 'modern': True,
                'colors': _c(STEEL, steel='#76767a', deck_wood='#8a7a62')},
    'Aegis Cruiser': {'archetype': 'ship', 'beam_k': 1.5, 'height_k': 1.75, 'design': 'aegis', 'modern': True,
                      'colors': _c(STEEL, steel_dark='#4a4e58', steel_light='#9a9ca2')},
    'Battleship': {'archetype': 'ship', 'beam_k': 1.3, 'height_k': 1.45, 'design': 'battleship', 'modern': True,
                   'colors': _c(STEEL, deck_wood='#6e6860')},
    'Carrier': {'archetype': 'ship', 'beam_k': 1.45, 'height_k': 1.3, 'design': 'carrier', 'modern': True,
                'colors': _c(STEEL, steel='#76736e', flight='#74726e')},
    'Submarine': {'archetype': 'ship', 'beam_k': 1.2, 'height_k': 1.35, 'design': 'submarine', 'modern': True, 'death_break': 40, 'explosions': [],
                  'colors': {'hull_sub': '#6a9284'}},
    'Nuclear Submarine': {'archetype': 'ship', 'beam_k': 1.1, 'height_k': 1.5, 'design': 'nuclear_sub', 'modern': True, 'death_break': 35, 'explosions': [],
                          'colors': {'hull_sub': '#5a8a7c'}},
}
