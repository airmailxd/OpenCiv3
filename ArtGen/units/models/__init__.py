"""Unit archetypes: the code that builds and animates a kind of unit model.

Each module in this package registers its archetypes with @archetype('name').
An archetype is a class taking (unit_name, params) and providing:

    frame(action, f, n, ctx) -> mesh.Mesh
        The model at frame f of n of an INI action (e.g. 'ATTACK1'), in model
        space: facing +y, standing on z = 0 at the origin, meters.
    mats -> mesh.Materials (the model's material table)
    loops(action) -> bool (optional; whether the action cycles)

`ctx` is a framework.ActionContext with the original flic's measurements
(frame count, speed, per-frame height/extent profile) for timing events.

Add a new kind of unit by adding a module here (e.g. models/mounted.py) with
its own @archetype classes; build.py imports every module automatically.
"""
import importlib
import os
import pkgutil

ARCHETYPES = {}


def archetype(name):
    def deco(cls):
        if name in ARCHETYPES and ARCHETYPES[name] is not cls:
            raise ValueError(f'archetype {name!r} registered twice')
        ARCHETYPES[name] = cls
        cls.archetype_name = name
        return cls
    return deco


def load_all():
    here = os.path.dirname(__file__)
    for m in pkgutil.iter_modules([here]):
        importlib.import_module(f'models.{m.name}')
    return ARCHETYPES


def get(name):
    if not ARCHETYPES:
        load_all()
    return ARCHETYPES[name]
