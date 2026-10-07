"""Skeletons, poses and keyframe animation.

A Skeleton is a tree of bones, each with a rest offset from its parent (in
the parent's space). A Pose maps bone names to (pitch, roll, yaw) degrees
(see mesh.euler) plus 'root' translation/rotation. Parts (meshes) are bound
to bones in bone space; Rig.mesh(pose) poses and merges them.

Animations are keyframe tracks over normalized time t in [0, 1]:
    anim = Keys([(0.0, {'arm_r': (10, 0, 0)}), (0.5, {...}), (1.0, {...})], loop=True)
    pose = anim(t)
Channels a key leaves out keep the value from the base pose. Interpolation
is Catmull-Rom (smooth, passes through each key), wrapping when loop=True.
"""
import math

import numpy as np

import mesh as M

ZERO3 = np.zeros(3, np.float32)


class Skeleton:
    def __init__(self):
        self.bones = {}      # name -> (parent, offset xyz)
        self.order = []

    def add(self, name, parent, offset):
        self.bones[name] = (parent, np.asarray(offset, np.float32))
        self.order.append(name)
        return self

    def world(self, pose):
        """World 4x4 transform of every bone for a pose."""
        out = {}
        root_t = np.asarray(pose.get('root_pos', ZERO3), np.float32)
        root_r = pose.get('root_rot', ZERO3)
        for name in self.order:
            parent, off = self.bones[name]
            r = pose.get(name, ZERO3)
            L = M.translate(*off) @ M.euler(*r)
            if parent is None:
                L = M.translate(*root_t) @ M.euler(*root_r) @ L
                out[name] = L
            else:
                out[name] = out[parent] @ L
        return out


class Rig:
    """A skeleton with parts. Parts are (bone, mesh in bone space, tag); tags
    let an action hide or swap parts (e.g. a dropped weapon)."""

    def __init__(self, skeleton, mats):
        self.skel = skeleton
        self.mats = mats
        self.parts = []

    def attach(self, bone, mesh, tag=None):
        self.parts.append((bone, mesh, tag))
        return self

    def mesh(self, pose, hide=(), extra=()):
        W = self.skel.world(pose)
        out = []
        for bone, m, tag in self.parts:
            if tag is not None and tag in hide:
                continue
            out.append(m.transformed(W[bone]))
        for m in extra:
            if m is not None:
                out.append(m)
        return M.Mesh.concat(out), W


# ------------------------------------------------------------ interpolation

def _catmull(p0, p1, p2, p3, u):
    u2, u3 = u * u, u * u * u
    return 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3)


class Keys:
    """Keyframes [(t, {channel: value}), ...] for t in [0, 1]. Values are
    tuples/arrays (angles in degrees, positions in meters) or scalars."""

    def __init__(self, keys, loop=False, base=None, ease=None):
        self.keys = sorted(keys, key=lambda k: k[0])
        self.loop = loop
        self.base = base or {}
        self.ease = ease
        chans = set(self.base)
        for _, k in self.keys:
            chans |= set(k)
        self.channels = chans
        # fill each key with every channel (carry the previous key's value forward)
        filled = []
        prev = {c: np.asarray(v, np.float32) for c, v in self.base.items()}
        for t, k in self.keys:
            cur = dict(prev)
            for c, v in k.items():
                cur[c] = np.asarray(v, np.float32)
            filled.append((t, cur))
            prev = cur
        # channels that appear late get their first value back-filled
        for c in chans:
            first = next((f[1][c] for f in filled if c in f[1]), None)
            for f in filled:
                if c not in f[1]:
                    f[1][c] = first
                else:
                    break
        self.filled = filled

    def __call__(self, t):
        ks = self.filled
        n = len(ks)
        if n == 1:
            return {c: v.copy() for c, v in ks[0][1].items()}
        if self.loop:
            t = t % 1.0
        else:
            t = min(max(t, ks[0][0]), ks[-1][0])
        # segment
        i = 0
        while i < n - 2 and t > ks[i + 1][0]:
            i += 1
        t0, t1 = ks[i][0], ks[i + 1][0]
        u = 0.0 if t1 <= t0 else (t - t0) / (t1 - t0)
        if self.loop and t > ks[-1][0]:
            # wrap segment from the last key to the first (at t = 1 + first)
            i = n - 1
            t0, t1 = ks[-1][0], ks[0][0] + 1.0
            u = (t - t0) / (t1 - t0)
        if self.ease:
            u = self.ease(u)

        def get(j):
            if self.loop:
                return ks[j % n][1]
            return ks[min(max(j, 0), n - 1)][1]
        p0, p1, p2, p3 = get(i - 1), get(i), get(i + 1), get(i + 2)
        out = {}
        for c in self.channels:
            out[c] = _catmull(p0[c], p1[c], p2[c], p3[c], u)
        return out


def smoothstep(u):
    return u * u * (3 - 2 * u)


def blend(a, b, w):
    """Blend two poses (dicts) by weight w (0 = a, 1 = b)."""
    out = dict(a)
    for c, v in b.items():
        if c in a:
            out[c] = np.asarray(a[c], np.float32) * (1 - w) + np.asarray(v, np.float32) * w
        else:
            out[c] = v
    return out


def merge(*poses):
    """Later poses override earlier channels; '+' prefixed channels add."""
    out = {}
    for p in poses:
        for c, v in p.items():
            if c.startswith('+'):
                k = c[1:]
                out[k] = np.asarray(out.get(k, ZERO3), np.float32) + np.asarray(v, np.float32)
            else:
                out[c] = v
    return out


def frame_times(n, loop):
    """Normalized time of each of n frames: loops never repeat the first
    frame at the end; one-shots run from 0 to 1 inclusive."""
    if loop:
        return [i / n for i in range(n)]
    return [i / max(n - 1, 1) for i in range(n)]
