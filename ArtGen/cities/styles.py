"""Culture and era styles: palettes, landmarks and the mix of ordinary buildings.

Colors are sampled from the original sheets (then made a little cleaner).
Landmark positions are given in the original 166x95 cell (the base of the
building), so they sit where the original's do."""
import buildings as bd
from city import Kind
from render3d import STUCCO, WINDOWS, ROOF_TILE, THATCH, SLATE, BRICK, STONE, WOOD, GLASS, PLAIN

S = lambda r, g, b: (r / 255, g / 255, b / 255)


def _fl(**kw):
    return lambda sc, r, h, P, rng: bd.flat_house(sc, r, h, P, rng, **kw)


def _with(fn, **kw):
    return lambda sc, r, h, P, rng: fn(sc, r, h, P, rng, **kw)


# ======================================================================= MIDEAST
ME_SAND = [S(222, 200, 146), S(210, 186, 134), S(230, 212, 162), S(202, 180, 130), S(226, 214, 176)]
MIDEAST = []
MIDEAST.append(dict(  # ancient
    palette=dict(walls=ME_SAND, roofs=[S(196, 120, 80)], dome_big=[S(132, 138, 150)], dome_small=[S(236, 214, 150), S(225, 200, 130)],
                 stone=ME_SAND, plaza=S(196, 172, 104), gold=S(220, 180, 80), leaves=[S(90, 120, 40)]),
    landmarks={
        0: [(bd.minaret, 12, 12, 64, (52, 47)), (bd.domed_hall, 36, 36, 50, (110, 48)), (_with(bd.block, arcade=1.0, parts=2), 40, 28, 32, (72, 38))],
        1: [(bd.minaret, 12, 12, 64, (55, 47)), (bd.domed_hall, 36, 36, 50, (107, 48)), (_with(bd.block, arcade=1.0, parts=2), 40, 28, 32, (73, 38))],
        2: [(bd.minaret, 12, 12, 64, (55, 47)), (bd.domed_hall, 36, 36, 50, (112, 46)), (_with(bd.block, arcade=1.0, parts=2), 44, 30, 36, (78, 30))],
    },
    ornaments={1: [('palm', 20, 52, 4)], 2: [('palm', 135, 60, 4), ('palm', 25, 50, 4)]},
    fill=[Kind(_with(bd.block, arcade=0.3, dome_chance=0.12), 6, (18, 38), (16, 30), (14, 26)),
          Kind(bd.courtyard_house, 1.2, (22, 30), (22, 30), (12, 18)),
          Kind(_fl(dome_chance=1.0), 1.0, (14, 20), (14, 20), (16, 24), square=True)],
    gap=1.0,
))

ME_IND_ROOF = [S(150, 150, 146), S(120, 122, 122), S(196, 128, 96)]
MIDEAST.append(dict(  # middle ages
    palette=dict(walls=ME_SAND, roofs=[S(176, 104, 78)], dome_big=[S(178, 102, 80), S(168, 110, 90)], dome_small=[S(182, 108, 84), S(236, 214, 150)],
                 stone=ME_SAND, plaza=S(196, 172, 104), gold=S(220, 180, 80), leaves=[S(90, 120, 40)]),
    landmarks={
        0: [(bd.domed_hall, 34, 34, 52, (112, 50)), (_with(bd.domed_hall, dome_col=S(212, 190, 130)), 30, 30, 56, (86, 34)),
            (bd.minaret, 12, 12, 74, (100, 30)), (_with(bd.round_tower, roof='cren'), 16, 16, 34, (55, 50))],
        1: [(bd.domed_hall, 34, 34, 52, (110, 48)), (_with(bd.domed_hall, dome_col=S(212, 190, 130)), 30, 30, 56, (86, 34)),
            (bd.minaret, 12, 12, 74, (100, 28)), (bd.minaret, 11, 11, 64, (128, 50)),
            (_with(bd.rotunda, dome_col=S(182, 108, 84)), 18, 18, 28, (142, 50)), (_with(bd.round_tower, roof='cren'), 16, 16, 34, (58, 52))],
        2: [(bd.domed_hall, 34, 34, 52, (112, 46)), (_with(bd.domed_hall, dome_col=S(212, 190, 130)), 30, 30, 56, (88, 32)),
            (bd.minaret, 12, 12, 74, (106, 24)), (_with(bd.round_tower, roof='cren'), 16, 16, 40, (34, 34)),
            (_with(bd.block, arcade=1.0, parts=1), 26, 24, 34, (138, 34))],
    },
    ornaments={2: [('palm', 140, 62, 4)]},
    fill=[Kind(_with(bd.block, arcade=0.4, dome_chance=0.35), 6, (18, 36), (16, 30), (16, 30)),
          Kind(bd.courtyard_house, 1.0, (22, 30), (22, 30), (14, 20)),
          Kind(_fl(dome_chance=1.0), 1.0, (14, 20), (14, 20), (18, 26), square=True)],
    gap=1.0,
))
MIDEAST.append(dict(  # industrial
    palette=dict(walls=ME_SAND, roofs=ME_IND_ROOF, dome_big=[S(178, 102, 80)], dome_small=[S(182, 108, 84)],
                 stone=ME_SAND, plaza=S(176, 160, 110), gold=S(220, 180, 80), brick=S(170, 82, 62),
                 factory_walls=[S(206, 186, 140), S(150, 140, 120)], factory_roofs=[S(128, 128, 126), S(150, 150, 146)],
                 metal=[S(150, 152, 152), S(120, 122, 124)], roofmat=SLATE),
    landmarks={
        0: [(_with(bd.tall_house, roof='hip'), 30, 28, 52, (96, 52)), (_with(bd.factory, chimneys=2), 32, 22, 50, (45, 48)),
            (_with(bd.hip_house, roofs=[S(196, 128, 96)], roofmat=ROOF_TILE), 30, 30, 26, (62, 48)),
            (bd.chimney_stack, 10, 10, 44, (116, 50)), (_with(bd.gas_tank, sphere=False), 16, 16, 16, (66, 62))],
        1: [(_with(bd.tall_house, roof='hip'), 30, 28, 52, (96, 52)), (_with(bd.factory, chimneys=2), 32, 22, 50, (42, 46)),
            (_with(bd.hip_house, roofs=[S(196, 128, 96)], roofmat=ROOF_TILE), 30, 30, 26, (62, 50)),
            (_with(bd.tall_house, roof='flat'), 26, 26, 60, (72, 30)), (bd.chimney_stack, 10, 10, 64, (93, 30)),
            (bd.chimney_stack, 10, 10, 46, (114, 50)), (_with(bd.gas_tank, sphere=True), 18, 18, 18, (36, 50))],
        2: [(_with(bd.tall_house, roof='hip'), 30, 28, 52, (96, 52)), (_with(bd.factory, chimneys=3), 40, 26, 66, (72, 28)),
            (_with(bd.hip_house, roofs=[S(196, 128, 96)], roofmat=ROOF_TILE), 30, 30, 26, (64, 50)),
            (_with(bd.gas_tank, sphere=True), 18, 18, 20, (20, 44)), (_with(bd.gas_tank, sphere=True), 16, 16, 18, (36, 50)),
            (bd.chimney_stack, 10, 10, 46, (114, 50)), (_with(bd.rotunda, dome_col=S(182, 108, 84)), 18, 18, 30, (146, 48))],
    },
    fill=[Kind(_with(bd.tall_house, roof='hip'), 3, (18, 28), (16, 26), (24, 36)),
          Kind(_with(bd.tall_house, roof='flat'), 3, (16, 28), (16, 26), (18, 30)),
          Kind(_with(bd.block, arcade=0.2, dome_chance=0.1), 2, (18, 30), (16, 26), (14, 22)),
          Kind(bd.warehouse, 1.5, (24, 34), (14, 20), (14, 18)),
          Kind(bd.gas_tank, 0.15, (12, 16), (12, 16), (12, 16), square=True)],
    gap=1.5,
))
MIDEAST.append(dict(  # modern
    palette=dict(walls=ME_SAND, roofs=ME_IND_ROOF, dome_big=[S(186, 104, 80)], dome_small=[S(182, 108, 84)],
                 stone=ME_SAND, plaza=S(170, 160, 128), gold=S(220, 180, 80), brick=S(170, 82, 62),
                 modern_walls=[S(220, 196, 140), S(204, 184, 140), S(230, 212, 160), S(186, 172, 146), S(214, 200, 170)],
                 glass=[S(120, 140, 150)], factory_walls=[S(190, 182, 160)], factory_roofs=[S(130, 130, 128)],
                 metal=[S(160, 162, 162), S(130, 132, 134)], roofmat=SLATE, glass_chance=0.0, wdark=0.5),
    landmarks={
        0: [(bd.tower_block, 26, 24, 72, (98, 56)), (bd.round_highrise, 22, 22, 52, (70, 46)),
            (_with(bd.tall_house, roof='hip', roofs=[S(150, 80, 64)]), 30, 26, 30, (50, 50))],
        1: [(bd.tower_block, 26, 24, 76, (104, 50)), (bd.round_highrise, 24, 24, 60, (58, 40)),
            (_with(bd.gas_tank, sphere=False), 18, 18, 18, (34, 46)), (_with(bd.gas_tank, sphere=False), 16, 16, 18, (124, 42))],
        2: [(bd.domed_tower, 30, 30, 92, (116, 46)), (bd.tower_block, 26, 24, 88, (84, 40)), (bd.round_highrise, 24, 24, 62, (56, 40)),
            (_with(bd.gas_tank, sphere=False), 20, 20, 20, (22, 46)), (_with(bd.gas_tank, sphere=False), 16, 16, 18, (134, 42))],
    },
    fill=[Kind(bd.apartment, 5, (18, 30), (16, 28), (22, 40)),
          Kind(_with(bd.tall_house, roof='flat'), 2, (16, 26), (16, 26), (18, 28)),
          Kind(_with(bd.block, arcade=0.3, dome_chance=0.05), 1.5, (16, 26), (16, 24), (12, 20)),
          Kind(bd.tower_block, 1, (18, 24), (18, 24), (44, 70))],
    gap=1.5,
))


# ========================================================================= EURO
EU_THATCH = [S(204, 184, 116), S(186, 168, 104), S(214, 196, 130)]
EU_STONE = [S(160, 154, 132), S(174, 166, 144), S(146, 142, 126), S(186, 174, 150)]
EU_SLATE = [S(100, 98, 92), S(86, 86, 82), S(112, 110, 102)]
EU_IND_WALLS = [S(196, 186, 176), S(168, 150, 140), S(220, 214, 206), S(150, 120, 108)]
EU_MOD_WALLS = [S(206, 204, 204), S(176, 174, 176), S(226, 224, 220), S(150, 146, 150), S(188, 180, 170)]
EURO = []
EURO.append(dict(  # ancient: thatched huts and long halls
    palette=dict(walls=[S(150, 120, 80)], hutwalls=[S(130, 100, 70)], thatch=EU_THATCH, roofs=EU_THATCH, roofmat=THATCH,
                 plaza=S(186, 178, 100), leaves=[S(90, 120, 40)], stone=EU_STONE),
    landmarks={
        0: [(bd.longhouse, 52, 18, 18, (108, 34)), (_with(bd.round_hut, beehive=True), 26, 26, 22, (66, 30))],
        1: [(bd.longhouse, 52, 18, 18, (108, 34)), (_with(bd.round_hut, beehive=True), 34, 34, 26, (68, 24)), (bd.longhouse, 40, 16, 16, (60, 50))],
        2: [(bd.longhouse, 56, 18, 18, (112, 30)), (_with(bd.round_hut, beehive=True), 34, 34, 26, (72, 22)), (bd.longhouse, 44, 16, 16, (64, 50)),
            (_with(bd.round_hut, beehive=True), 12, 12, 22, (138, 14))],
    },
    fill=[Kind(_with(bd.round_hut, beehive=True), 3, (14, 20), (14, 20), (12, 16), square=True),
          Kind(bd.longhouse, 3, (26, 40), (14, 18), (13, 17)),
          Kind(bd.round_hut, 1, (13, 17), (13, 17), (15, 19), square=True)],
    gap=1.5, plaza_alpha=0.85,
))
EURO.append(dict(  # middle ages: stone, slate, castle and church
    palette=dict(walls=EU_STONE + [S(200, 190, 160)], stone=EU_STONE, roofs=EU_SLATE, roofmat=SLATE,
                 plaza=S(150, 160, 96), marble=S(176, 176, 170), leaves=[S(80, 110, 40)]),
    landmarks={
        0: [(_with(bd.round_tower, roof='cren'), 18, 18, 36, (50, 40)), (bd.church, 34, 18, 52, (100, 54)),
            (_with(bd.keep), 20, 20, 30, (98, 30))],
        1: [(_with(bd.round_tower, roof='cren'), 18, 18, 40, (52, 36)), (bd.keep, 24, 24, 52, (94, 28)),
            (bd.church, 36, 18, 58, (122, 46))],
        2: [(_with(bd.round_tower, roof='cren'), 18, 18, 40, (40, 44)), (bd.keep, 24, 24, 56, (88, 22)),
            (bd.church, 36, 18, 60, (110, 42)), (_with(bd.round_tower, roof='cone'), 14, 14, 36, (150, 40))],
    },
    ornaments={0: [('fountain', 83, 47, 9)], 1: [('fountain', 75, 45, 9)], 2: [('fountain', 80, 45, 9)]},
    plaza_pad=9,
    fill=[Kind(_with(bd.gable_house, wallmat=STONE), 5, (16, 28), (14, 22), (18, 28)),
          Kind(_with(bd.gable_house, walls=[S(214, 204, 176), S(200, 188, 160)], wallmat=WOOD), 2, (14, 22), (14, 20), (18, 24)),
          Kind(_with(bd.hip_house, wallmat=STONE), 1, (16, 22), (16, 22), (18, 26))],
    gap=1.5, plaza_alpha=0.75,
))
EURO.append(dict(  # industrial
    palette=dict(walls=EU_IND_WALLS, stone=EU_STONE, roofs=EU_SLATE, roofmat=SLATE, plaza=S(140, 136, 110),
                 brick=S(150, 74, 58), factory_walls=[S(130, 90, 78), S(150, 140, 130)], factory_roofs=EU_SLATE,
                 metal=[S(130, 132, 134)], dome_big=[S(120, 110, 104)], marble=S(200, 196, 188), brick_factory=True),
    landmarks={
        0: [(_with(bd.rotunda, dome_col=S(110, 100, 96)), 30, 30, 32, (74, 36)), (_with(bd.tall_house, roof='flat'), 22, 24, 40, (62, 54)),
            (_with(bd.factory, chimneys=2), 34, 22, 46, (114, 44))],
        1: [(_with(bd.rotunda, dome_col=S(110, 100, 96)), 30, 30, 32, (68, 34)), (_with(bd.tall_house, roof='flat'), 22, 24, 42, (62, 54)),
            (_with(bd.factory, chimneys=2), 34, 22, 54, (100, 56)), (bd.chimney_stack, 10, 10, 66, (106, 20))],
        2: [(_with(bd.rotunda, dome_col=S(110, 100, 96)), 30, 30, 32, (74, 34)), (_with(bd.tall_house, roof='flat'), 24, 24, 54, (56, 34)),
            (_with(bd.factory, chimneys=3), 40, 24, 58, (90, 56)), (bd.chimney_stack, 10, 10, 66, (92, 16)), (bd.chimney_stack, 10, 10, 54, (114, 18))],
    },
    ornaments={0: [('fountain', 78, 50, 7)], 1: [('fountain', 74, 50, 7)]},
    fill=[Kind(_with(bd.tall_house, roof='gable'), 3, (16, 24), (14, 22), (22, 32)),
          Kind(_with(bd.tall_house, roof='flat'), 3, (16, 26), (16, 24), (20, 30)),
          Kind(bd.warehouse, 1.5, (24, 34), (14, 20), (14, 18))],
    gap=1.5, plaza_alpha=0.7,
))
EURO.append(dict(  # modern
    palette=dict(walls=EU_MOD_WALLS, modern_walls=EU_MOD_WALLS, roofs=EU_SLATE, roofmat=SLATE, plaza=S(140, 140, 130),
                 brick=S(150, 74, 58), factory_walls=[S(150, 146, 150)], factory_roofs=EU_SLATE, glass=[S(120, 140, 160)],
                 metal=[S(170, 172, 174)], glass_chance=0.25),
    landmarks={
        0: [(bd.tower_block, 24, 24, 76, (60, 46)), (_with(bd.factory, chimneys=2), 30, 22, 46, (110, 50)), (bd.round_highrise, 22, 22, 32, (80, 58))],
        1: [(bd.tower_block, 24, 24, 76, (54, 46)), (bd.tower_block, 22, 22, 84, (100, 26)), (_with(bd.factory, chimneys=2), 30, 22, 46, (112, 46))],
        2: [(bd.tower_block, 26, 26, 92, (70, 26)), (bd.tower_block, 22, 22, 78, (56, 44)), (bd.round_highrise, 28, 28, 74, (110, 40)),
            (bd.tower_block, 22, 22, 82, (92, 20))],
    },
    fill=[Kind(bd.apartment, 5, (18, 28), (16, 26), (20, 36)),
          Kind(_with(bd.tall_house, roof='flat'), 2, (16, 24), (16, 24), (16, 24)),
          Kind(bd.tower_block, 1.2, (18, 24), (18, 24), (44, 70))],
    gap=1.5, plaza_alpha=0.7,
))

# ======================================================================== ROMAN
RO_WALLS = [S(232, 214, 168), S(222, 200, 150), S(240, 226, 190), S(214, 192, 140)]
RO_TILE = [S(212, 150, 80), S(198, 134, 72), S(220, 166, 98), S(204, 146, 86)]
ROMAN = []
ROMAN.append(dict(  # ancient
    palette=dict(walls=RO_WALLS, roofs=RO_TILE, roofmat=ROOF_TILE, plaza=S(200, 184, 128), marble=S(238, 232, 214),
                 dome_big=[S(200, 120, 70)], leaves=[S(90, 120, 40)]),
    landmarks={
        0: [(bd.temple, 32, 24, 36, (88, 30))],
        1: [(bd.temple, 32, 24, 36, (86, 28))],
        2: [(bd.temple, 32, 24, 36, (90, 30)), (bd.temple, 22, 18, 26, (134, 62))],
    },
    ornaments={0: [('statue', 76, 42, 3)], 1: [('statue', 76, 40, 3)], 2: [('statue', 80, 42, 3)]},
    fill=[Kind(bd.hip_house, 3, (16, 26), (14, 22), (16, 22)),
          Kind(bd.gable_house, 3, (16, 26), (14, 22), (16, 22)),
          Kind(_with(bd.courtyard_house), 0.6, (22, 28), (22, 28), (12, 16))],
    gap=1.5, plaza_alpha=0.8,
))
ROMAN.append(dict(  # middle ages: duomo, pantheon, temple
    palette=dict(walls=RO_WALLS, roofs=RO_TILE, roofmat=ROOF_TILE, plaza=S(196, 180, 120), marble=S(238, 232, 214),
                 dome_big=[S(190, 184, 170)], dome_rib=S(206, 112, 64), stone=[S(176, 166, 140)]),
    landmarks={
        0: [(bd.duomo, 34, 34, 56, (52, 48)), (bd.temple, 28, 22, 36, (90, 28)), (bd.rotunda, 28, 28, 30, (112, 44))],
        1: [(bd.duomo, 34, 34, 56, (54, 46)), (bd.temple, 28, 22, 36, (88, 28)), (bd.rotunda, 28, 28, 30, (112, 44))],
        2: [(bd.duomo, 34, 34, 56, (56, 46)), (bd.temple, 28, 22, 36, (88, 30)), (bd.rotunda, 28, 28, 30, (114, 44)),
            (bd.triumphal_arch, 22, 10, 36, (76, 14)), (_with(bd.keep), 18, 18, 34, (48, 62))],
    },
    ornaments={0: [('statue', 74, 42, 3)], 1: [('statue', 72, 40, 3)], 2: [('statue', 74, 40, 3)]},
    fill=[Kind(bd.hip_house, 3, (16, 24), (14, 22), (16, 22)),
          Kind(bd.gable_house, 3, (16, 24), (14, 22), (16, 22)),
          Kind(_with(bd.round_tower, roof='cone'), 0.4, (12, 14), (12, 14), (22, 28), square=True)],
    gap=1.5, plaza_alpha=0.8,
))
ROMAN.append(dict(  # industrial
    palette=dict(walls=RO_WALLS, roofs=RO_TILE + [S(170, 160, 80)], roofmat=ROOF_TILE, plaza=S(180, 168, 120), marble=S(232, 226, 206),
                 dome_big=[S(206, 140, 80)], dome_rib=S(206, 112, 64), brick=S(176, 96, 66),
                 factory_walls=[S(214, 196, 150), S(190, 160, 120)], factory_roofs=[S(170, 160, 80), S(180, 110, 70)], metal=[S(150, 150, 146)]),
    landmarks={
        0: [(_with(bd.tall_house, roof='hip'), 28, 26, 40, (72, 50)), (_with(bd.factory, chimneys=3), 34, 22, 50, (100, 40)),
            (_with(bd.domed_hall, turrets=False), 22, 22, 32, (60, 34))],
        1: [(_with(bd.tall_house, roof='hip'), 28, 26, 40, (72, 52)), (_with(bd.factory, chimneys=3), 34, 22, 50, (100, 40)),
            (_with(bd.domed_hall, turrets=False), 22, 22, 32, (54, 36)), (_with(bd.tall_house, roof='hip'), 24, 24, 46, (96, 22))],
        2: [(_with(bd.tall_house, roof='hip'), 28, 26, 40, (76, 52)), (_with(bd.factory, chimneys=3), 34, 22, 50, (104, 40)),
            (_with(bd.domed_hall, turrets=False), 22, 22, 32, (48, 46)), (_with(bd.tall_house, roof='flat'), 26, 24, 50, (96, 18)),
            (bd.chimney_stack, 10, 10, 56, (36, 34))],
    },
    fill=[Kind(_with(bd.tall_house, roof='hip'), 3, (16, 26), (14, 22), (20, 30)),
          Kind(bd.gable_house, 2, (16, 26), (14, 22), (16, 22)),
          Kind(bd.warehouse, 1.2, (22, 32), (14, 20), (14, 18))],
    gap=1.5, plaza_alpha=0.75,
))
ROMAN.append(dict(  # modern
    palette=dict(walls=RO_WALLS, modern_walls=[S(232, 218, 176), S(214, 198, 160), S(240, 230, 200)], roofs=RO_TILE, roofmat=ROOF_TILE,
                 plaza=S(176, 166, 130), marble=S(236, 230, 214), dome_big=[S(230, 222, 200)], brick=S(176, 96, 66),
                 factory_walls=[S(210, 196, 160)], factory_roofs=[S(170, 160, 80)], glass=[S(130, 140, 140)], metal=[S(170, 168, 160)],
                 crown=S(214, 160, 70), crown_chance=0.6, glass_chance=0.1),
    landmarks={
        0: [(_with(bd.tower_block, crown=S(214, 160, 70)), 30, 26, 64, (70, 40)), (_with(bd.factory, chimneys=2), 26, 22, 44, (104, 44))],
        1: [(_with(bd.tower_block, crown=S(214, 160, 70)), 30, 26, 64, (64, 40)), (_with(bd.tower_block, crown=S(214, 160, 70)), 24, 22, 80, (90, 26)),
            (_with(bd.factory, chimneys=2), 26, 22, 44, (110, 44))],
        2: [(_with(bd.tower_block, crown=None, glass=False), 34, 30, 84, (60, 34)), (_with(bd.tower_block, crown=S(214, 160, 70)), 28, 24, 82, (86, 28)),
            (_with(bd.domed_tower, dome_col=S(236, 232, 220)), 32, 32, 84, (120, 40))],
    },
    fill=[Kind(bd.apartment, 4, (18, 28), (16, 26), (20, 34)),
          Kind(_with(bd.tall_house, roof='hip'), 2, (16, 24), (16, 24), (18, 28)),
          Kind(bd.tower_block, 1, (18, 24), (18, 24), (40, 64))],
    gap=1.5, plaza_alpha=0.7,
))

# ======================================================================== ASIAN
AS_THATCH = [S(170, 160, 120), S(150, 144, 108), S(184, 172, 130)]
AS_WOOD = [S(150, 84, 56), S(132, 76, 52), S(170, 110, 70)]
AS_TILE = [S(120, 120, 124), S(104, 106, 112), S(136, 134, 134)]
ASIAN = []
ASIAN.append(dict(  # ancient: tiered thatch huts
    palette=dict(walls=AS_WOOD, hutwalls=AS_WOOD, thatch=AS_THATCH, roofs=AS_THATCH, roofmat=THATCH, plaza=S(218, 200, 96),
                 podium=S(150, 140, 110), leaves=[S(90, 120, 40)], gold=S(220, 190, 80)),
    landmarks={
        0: [(_with(bd.round_hut, tiers=3), 28, 28, 32, (58, 36))],
        1: [(_with(bd.round_hut, tiers=3), 28, 28, 32, (66, 30))],
        2: [(_with(bd.round_hut, tiers=3), 28, 28, 32, (54, 30)), (_with(bd.round_hut, tiers=2), 20, 20, 26, (66, 56))],
    },
    fill=[Kind(_with(bd.asian_hall, roofs=AS_THATCH, two_tier=False, podium=False), 5, (18, 26), (16, 22), (16, 22)),
          Kind(_with(bd.round_hut, tiers=2), 1.5, (15, 19), (15, 19), (18, 22), square=True)],
    gap=2.0, plaza_alpha=0.85,
))
ASIAN.append(dict(  # middle ages: tiled halls and pagodas
    palette=dict(walls=[S(210, 196, 170), S(196, 180, 150)], hutwalls=AS_WOOD + [S(214, 200, 170)], thatch=AS_THATCH, roofs=AS_TILE, roofmat=SLATE,
                 plaza=S(218, 200, 96), podium=S(160, 150, 130), gold=S(226, 190, 80)),
    landmarks={
        0: [(_with(bd.asian_hall, two_tier=True), 34, 30, 36, (70, 34)), (_with(bd.pagoda, tiers=3), 20, 20, 42, (104, 50))],
        1: [(_with(bd.asian_hall, two_tier=True), 36, 32, 40, (66, 30)), (_with(bd.pagoda, tiers=3), 20, 20, 44, (104, 44))],
        2: [(_with(bd.asian_hall, two_tier=True), 36, 32, 40, (66, 28)), (_with(bd.pagoda, tiers=3), 20, 20, 44, (100, 40)),
            (_with(bd.round_hut, tiers=2), 22, 22, 26, (128, 58))],
    },
    fill=[Kind(_with(bd.asian_hall, two_tier=False), 5, (16, 26), (14, 22), (16, 24)),
          Kind(_with(bd.round_hut, tiers=2), 0.8, (14, 18), (14, 18), (18, 22), square=True)],
    gap=1.5, plaza_alpha=0.85,
))
ASIAN.append(dict(  # industrial
    palette=dict(walls=[S(220, 210, 176), S(196, 186, 160), S(150, 126, 100)], hutwalls=AS_WOOD, roofs=AS_TILE, roofmat=SLATE,
                 plaza=S(200, 186, 110), podium=S(150, 146, 136), gold=S(226, 190, 80), brick=S(176, 86, 60),
                 factory_walls=[S(196, 186, 160), S(150, 110, 90)], factory_roofs=AS_TILE, metal=[S(140, 142, 144)]),
    landmarks={
        0: [(_with(bd.asian_hall, two_tier=True), 30, 28, 34, (86, 40)), (bd.chimney_stack, 10, 10, 50, (66, 48)), (_with(bd.factory, chimneys=1), 30, 22, 40, (100, 56))],
        1: [(_with(bd.asian_hall, two_tier=True), 30, 28, 34, (86, 38)), (bd.chimney_stack, 10, 10, 60, (60, 34)), (bd.chimney_stack, 10, 10, 50, (70, 48)),
            (_with(bd.factory, chimneys=1), 30, 22, 40, (100, 56))],
        2: [(_with(bd.asian_hall, two_tier=True), 30, 28, 34, (100, 30)), (bd.chimney_stack, 10, 10, 62, (54, 32)), (bd.chimney_stack, 10, 10, 54, (66, 46)),
            (_with(bd.gas_tank, sphere=False), 22, 22, 28, (30, 28)), (_with(bd.factory, chimneys=1), 30, 22, 40, (90, 56))],
    },
    fill=[Kind(_with(bd.asian_hall, two_tier=False), 2, (16, 24), (14, 22), (16, 22)),
          Kind(_with(bd.tall_house, roof='flat'), 2, (16, 24), (14, 22), (18, 28)),
          Kind(bd.warehouse, 2, (24, 32), (14, 20), (14, 18))],
    gap=1.5, plaza_alpha=0.7,
))
ASIAN.append(dict(  # modern
    palette=dict(walls=[S(214, 196, 160)], modern_walls=[S(214, 190, 150), S(150, 150, 154), S(120, 122, 128), S(196, 170, 130)],
                 roofs=AS_TILE, roofmat=SLATE, plaza=S(160, 156, 130), brick=S(176, 86, 60), factory_walls=[S(140, 140, 144)],
                 factory_roofs=AS_TILE, glass=[S(110, 116, 126)], metal=[S(170, 170, 172)], glass_chance=0.2),
    landmarks={
        0: [(bd.tower_block, 28, 26, 66, (66, 40)), (_with(bd.factory, chimneys=2), 30, 22, 40, (106, 48))],
        1: [(bd.tower_block, 28, 26, 66, (64, 38)), (bd.tower_block, 24, 24, 80, (104, 26)), (_with(bd.factory, chimneys=2), 30, 22, 40, (112, 52))],
        2: [(bd.tower_block, 28, 26, 70, (82, 36)), (bd.round_highrise, 24, 24, 66, (58, 40)), (bd.tower_block, 24, 24, 80, (112, 26)),
            (bd.round_highrise, 22, 22, 58, (126, 40)), (_with(bd.factory, chimneys=2), 26, 22, 40, (30, 44))],
    },
    fill=[Kind(bd.apartment, 5, (18, 28), (16, 26), (20, 34)),
          Kind(bd.tower_block, 1.2, (18, 24), (18, 24), (40, 64))],
    gap=1.5, plaza_alpha=0.7,
))

# ========================================================================= AMER
AM_THATCH = [S(196, 160, 80), S(178, 144, 70), S(208, 174, 94)]
AM_WOOD = [S(150, 110, 70), S(130, 96, 64)]
AMER = []
AMER.append(dict(  # ancient: thatched long houses around a plaza
    palette=dict(walls=AM_WOOD, hutwalls=AM_WOOD, thatch=AM_THATCH, roofs=AM_THATCH, roofmat=THATCH, plaza=S(206, 196, 140),
                 leaves=[S(90, 120, 40)], marble=S(226, 214, 180)),
    landmarks={
        0: [(bd.aframe, 32, 22, 22, (62, 32))],
        1: [(bd.aframe, 32, 22, 22, (60, 32))],
        2: [(bd.aframe, 34, 22, 24, (66, 30)), (bd.aframe, 30, 20, 22, (104, 36))],
    },
    ornaments={0: [('obelisk', 84, 48, 2)], 1: [('obelisk', 82, 46, 2)], 2: [('obelisk', 84, 44, 2)]},
    fill=[Kind(bd.aframe, 3, (20, 30), (15, 20), (14, 20)),
          Kind(bd.longhouse, 3, (24, 34), (15, 19), (14, 18)),
          Kind(_with(bd.round_hut, beehive=True), 0.5, (12, 15), (12, 15), (10, 13), square=True)],
    gap=2.5, plaza_alpha=0.85,
))
AMER.append(dict(  # middle ages / colonial
    palette=dict(walls=[S(200, 180, 140), S(180, 170, 150)], hutwalls=AM_WOOD, thatch=AM_THATCH, stone=[S(130, 130, 126), S(146, 144, 136)],
                 roofs=[S(96, 96, 98), S(84, 84, 88)], roofmat=SLATE, plaza=S(190, 186, 130), marble=S(214, 206, 180)),
    landmarks={
        0: [(_with(bd.tall_house, roof='gable', walls=[S(200, 180, 140)], roofs=[S(110, 104, 96)]), 28, 26, 34, (62, 36)), (bd.church, 30, 16, 40, (108, 48))],
        1: [(_with(bd.tall_house, roof='gable', walls=[S(200, 180, 140)], roofs=[S(110, 104, 96)]), 28, 26, 34, (62, 36)), (bd.church, 30, 16, 46, (110, 50)),
            (bd.church, 34, 18, 44, (74, 56))],
        2: [(_with(bd.tall_house, roof='gable', walls=[S(200, 180, 140)], roofs=[S(110, 104, 96)]), 28, 26, 36, (60, 34)), (bd.church, 30, 16, 46, (110, 48)),
            (bd.church, 34, 18, 44, (72, 56)), (bd.church, 30, 16, 40, (40, 52))],
    },
    ornaments={0: [('obelisk', 80, 44, 2)], 1: [('obelisk', 78, 42, 2)], 2: [('obelisk', 80, 40, 2)]},
    fill=[Kind(bd.aframe, 3, (18, 28), (14, 20), (14, 20)),
          Kind(_with(bd.gable_house, roofs=AM_THATCH, roofmat=THATCH, wallmat=WOOD), 2, (16, 24), (14, 20), (16, 22)),
          Kind(_with(bd.gable_house, wallmat=STONE), 2, (16, 26), (14, 20), (18, 26))],
    gap=2.0, plaza_alpha=0.8,
))
AMER.append(dict(  # industrial
    palette=dict(walls=[S(140, 140, 140), S(120, 118, 116), S(214, 210, 200), S(100, 96, 96)], stone=[S(130, 130, 126)],
                 roofs=[S(80, 80, 84), S(100, 100, 104)], roofmat=SLATE, plaza=S(120, 118, 106), brick=S(176, 70, 50),
                 factory_walls=[S(110, 108, 108), S(140, 90, 80)], factory_roofs=[S(80, 80, 84)], metal=[S(140, 142, 142)], brick_factory=True),
    landmarks={
        0: [(_with(bd.tall_house, roof='flat', walls=[S(220, 216, 206)]), 28, 26, 40, (64, 44)), (_with(bd.factory, chimneys=3), 32, 22, 52, (108, 48)),
            (_with(bd.tall_house, roof='gable'), 24, 20, 36, (90, 30))],
        1: [(_with(bd.tall_house, roof='flat', walls=[S(220, 216, 206)]), 28, 26, 44, (58, 44)), (_with(bd.factory, chimneys=3), 32, 22, 56, (110, 46)),
            (_with(bd.tall_house, roof='hip'), 24, 22, 46, (90, 26))],
        2: [(_with(bd.tall_house, roof='flat', walls=[S(220, 216, 206)]), 28, 26, 48, (62, 34)), (_with(bd.factory, chimneys=3), 34, 22, 58, (106, 48)),
            (_with(bd.tall_house, roof='hip'), 24, 22, 48, (88, 22)), (_with(bd.gas_tank, sphere=False), 22, 22, 22, (146, 46)),
            (bd.chimney_stack, 10, 10, 56, (40, 30))],
    },
    fill=[Kind(_with(bd.tall_house, roof='flat'), 3, (16, 26), (16, 24), (18, 30)),
          Kind(_with(bd.tall_house, roof='gable'), 2, (16, 24), (14, 22), (20, 30)),
          Kind(bd.warehouse, 2, (24, 34), (14, 20), (14, 18))],
    gap=1.5, plaza_alpha=0.7,
))
AMER.append(dict(  # modern: skyscrapers
    palette=dict(walls=[S(160, 160, 160)], modern_walls=[S(226, 226, 226), S(160, 160, 164), S(120, 120, 124), S(190, 186, 176)],
                 roofs=[S(90, 90, 94)], roofmat=SLATE, plaza=S(130, 130, 124), brick=S(176, 70, 50), factory_walls=[S(140, 140, 144)],
                 factory_roofs=[S(90, 90, 94)], glass=[S(110, 120, 130), S(90, 100, 106)], metal=[S(190, 190, 186)], glass_chance=0.35,
                 crown=S(90, 190, 170), crown_chance=0.15),
    landmarks={
        0: [(_with(bd.tower_block, glass=False), 28, 26, 72, (62, 44)), (_with(bd.factory, chimneys=2), 28, 22, 50, (94, 36))],
        1: [(_with(bd.tower_block, glass=False), 28, 26, 72, (56, 46)), (_with(bd.tower_block, glass=False), 24, 24, 70, (84, 46)),
            (_with(bd.factory, chimneys=2), 28, 22, 50, (98, 30))],
        2: [(_with(bd.tower_block, glass=False), 28, 26, 80, (64, 44)), (_with(bd.tower_block, crown=S(90, 190, 170)), 30, 30, 70, (120, 40)),
            (bd.tower_block, 26, 26, 92, (90, 24)), (_with(bd.tower_block, glass=False), 22, 22, 84, (52, 30))],
    },
    fill=[Kind(bd.apartment, 4, (18, 28), (16, 26), (20, 34)),
          Kind(bd.tower_block, 1.5, (18, 24), (18, 24), (40, 64))],
    gap=1.5, plaza_alpha=0.7,
))


# ======================================================================= walls
# Per culture and era: (wall color, material, towers, battlements).
WALLS = {
    'MIDEAST': [(S(226, 196, 112), STUCCO, 'round', True), (S(220, 190, 110), STONE, 'round', True),
                (S(214, 186, 120), STONE, 'square', True), (S(220, 204, 160), STUCCO, 'square', False)],
    'EURO': [(S(176, 158, 116), STONE, 'none', False), (S(150, 148, 136), STONE, 'round', True),
             (S(150, 96, 76), BRICK, 'square', True), (S(206, 204, 200), PLAIN, 'square', False)],
    'ROMAN': [(S(214, 200, 160), STONE, 'square', True), (S(206, 192, 156), STONE, 'square', True),
              (S(186, 110, 80), BRICK, 'square', True), (S(226, 220, 200), PLAIN, 'square', False)],
    'ASIAN': [(S(170, 164, 140), STONE, 'square', True), (S(166, 160, 146), STONE, 'square', True),
              (S(176, 96, 74), BRICK, 'square', True), (S(206, 204, 196), PLAIN, 'square', False)],
    'AMER': [(S(140, 104, 66), WOOD, 'none', False), (S(150, 112, 72), WOOD, 'square', False),
             (S(176, 92, 70), BRICK, 'square', True), (S(206, 204, 200), PLAIN, 'square', False)],
}

RUINS = dict(
    palette=dict(walls=[S(170, 166, 150)], ruin=[S(176, 180, 168), S(204, 182, 136), S(160, 166, 156), S(218, 198, 150), S(150, 146, 132)],
                 plaza=S(180, 160, 118)),
    landmarks={},
    fill=[Kind(bd.ruin, 1, (16, 28), (14, 24), (10, 22))], scale=1.0,
    gap=2.0, plaza_alpha=0.85,
)
