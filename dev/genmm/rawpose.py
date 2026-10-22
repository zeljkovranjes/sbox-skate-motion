"""Reads and rewrites a raw pose container (SMAN versions 2-5, as the Skate Motion library reads it), and the slim
keyframe clip set (SMAN version 1) that names the same clips."""
import struct

import numpy as np


class Reader:
    def __init__(self, data, at=0):
        self.data, self.at = data, at

    def take(self, n):
        self.at += n
        return self.data[self.at - n:self.at]

    def u8(self):
        return self.take(1)[0]

    def u16(self):
        return struct.unpack('<H', self.take(2))[0]

    def s16(self):
        return struct.unpack('<h', self.take(2))[0]

    def u32(self):
        return struct.unpack('<I', self.take(4))[0]

    def f32(self):
        return struct.unpack('<f', self.take(4))[0]

    def text(self):
        return self.take(self.u8()).decode('utf-8')


def read_container(data):
    """Skeleton, reference poses and every clip (with its byte span) of a raw pose container."""
    r = Reader(data)
    magic, version = r.u32(), r.u32()
    assert magic == 0x4e414d53 and version in (2, 3, 4, 5), 'not a raw pose container'
    bones = r.u16()
    names, parents = [], []
    for _ in range(bones):
        names.append(r.text())
        parents.append(r.s16())
    refs = r.u16()
    references = np.frombuffer(r.take(refs * bones * 48), dtype='<f4').reshape(refs, bones, 3, 4)
    count = r.u32()
    clips = []
    for _ in range(count):
        start = r.at
        clip = {'name': r.text()}
        clip['fps'], clip['rate'] = r.f32(), r.f32()
        clip['frames'], clip['reference'] = r.u16(), r.u16()
        clip['sampler'], clip['flags'] = r.u8(), r.u8()
        clip['trajectory'] = r.take(32) if version >= 3 else b''
        streams = b''
        if version >= 4:
            sr = Reader(data, r.at)
            for _ in range(sr.u8()):
                sr.take(48)
                sr.take(sr.u32() * 4)
            streams = data[r.at:sr.at]
            r.at = sr.at
        clip['streams'] = streams
        kinds = list(r.take(bones * 3))
        clip['kinds'] = kinds
        keys = []
        for kind in kinds:
            n = 0 if kind == 0 else 1 if kind == 1 else clip['frames']
            keys.append(np.frombuffer(r.take(n * 16), dtype='<f4').reshape(n, 4).copy())
        clip['keys'] = keys
        clip['span'] = (start, r.at)
        clips.append(clip)
    assert r.at == len(data), 'trailing data'
    return {'version': version, 'names': names, 'parents': parents, 'references': references, 'clips': clips}


def qmul(a, b):
    ax, ay, az, aw = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    bx, by, bz, bw = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return np.stack([aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
                     aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz], axis=-1)


def qconj(q):
    return q * np.array([-1, -1, -1, 1], dtype=q.dtype)


def qrot(q, v):
    """v rotated by q (any shape broadcasting over the last axis)."""
    axis = q[..., :3]
    w = q[..., 3:4]
    inner = v * w + np.cross(axis, v)
    return v + 2.0 * np.cross(axis, inner)


def raw_keys(container, clip):
    """Each frame's raw key per bone: position (xyzw), rotation (xyzw); an unkeyed track is the identity."""
    bones = len(container['names'])
    pos = np.zeros((clip['frames'], bones, 4), dtype=np.float64)
    rot = np.zeros((clip['frames'], bones, 4), dtype=np.float64)
    rot[..., 3] = 1.0
    for bone in range(bones):
        for component, out in ((0, pos), (1, rot)):
            kind, keys = clip['kinds'][bone * 3 + component], clip['keys'][bone * 3 + component]
            if kind == 1:
                out[:, bone] = keys[0]
            elif kind == 2:
                out[:, bone] = keys
    return pos, rot


def clip_frames(container, clip):
    """Every frame's local place (xyz) and turn (xyzw) per bone: the clip's reference pose composed with its keys, as
    the library samples it (ReferencedAnimationClip)."""
    ref = container['references'][clip['reference']].astype(np.float64)
    key_pos, key_rot = raw_keys(container, clip)
    ref_pos, ref_rot = ref[:, 0, :3], ref[:, 1, :]
    positions = ref_pos[None] + qrot(ref_rot[None], key_pos[..., :3])
    rotations = qmul(ref_rot[None], key_rot)
    return positions, rotations


def encode_clip(container, clip, positions, rotations):
    """The bytes of a clip with new per-frame local places and turns (taken back off the reference pose into keys), at
    the same fps, rate, reference, sampler, flags, trajectory and pose streams; scale tracks keep their first key."""
    bones = len(container['names'])
    frames = positions.shape[0]
    ref = container['references'][clip['reference']].astype(np.float64)
    ref_pos, ref_rot = ref[:, 0, :3], ref[:, 1, :]
    inverse = qconj(ref_rot)[None]
    key_rot = qmul(inverse, rotations)
    key_pos = qrot(inverse, positions - ref_pos[None])
    old_pos, _ = raw_keys(container, clip)
    lane = old_pos[0, :, 3]
    name = clip['name'].encode('utf-8')
    out = bytearray(struct.pack('<B', len(name)) + name)
    out += struct.pack('<ffHHBB', clip['fps'], clip['rate'], frames, clip['reference'], clip['sampler'], clip['flags'])
    out += clip['trajectory'] + clip['streams']
    kinds, keys = [], []
    for bone in range(bones):
        position = np.concatenate([key_pos[:, bone], np.full((frames, 1), lane[bone])], axis=1)
        rotation = key_rot[:, bone]
        for values, identity in ((position, np.array([0, 0, 0, lane[bone]])), (rotation, np.array([0, 0, 0, 1.0]))):
            if np.allclose(values, identity[None], atol=1e-6):
                kinds.append(0)
                keys.append(values[:0])
            elif np.allclose(values, values[:1], atol=1e-6):
                kinds.append(1)
                keys.append(values[:1])
            else:
                kinds.append(2)
                keys.append(values)
        scale_kind = clip['kinds'][bone * 3 + 2]
        scale = clip['keys'][bone * 3 + 2]
        if scale_kind == 2:
            scale_kind, scale = 1, scale[:1]
        kinds.append(scale_kind)
        keys.append(scale)
    out += bytes(kinds)
    for k in keys:
        out += np.ascontiguousarray(k, dtype='<f4').tobytes()
    return bytes(out)


def replace_clip(data, container, name, encoded):
    for clip in container['clips']:
        if clip['name'] == name:
            start, end = clip['span']
            return data[:start] + encoded + data[end:]
    raise KeyError(name)


def set_slim_frames(data, name, frames):
    """A slim clip set (version 1) with the named clip's frame count changed (its keys are not used)."""
    r = Reader(data)
    assert r.take(4) == b'SMAN' and r.u32() == 1
    bones = r.u16()
    for _ in range(bones):
        r.take(r.u8())
        r.take(2 + 12 + 16)
    count = r.u32()
    for _ in range(count):
        clip_name = r.take(r.u8()).decode('utf-8')
        rate_at = r.at
        rate, old = struct.unpack('<fH', r.take(6))
        kinds = r.take(bones * 2)
        for index, kind in enumerate(kinds):
            n = 0 if kind == 0 else 1 if kind == 1 else old
            r.take(n * (8 if index % 2 == 0 else 12))   # packed rotations (four int16), positions (three floats)
        if clip_name == name:
            return data[:rate_at + 4] + struct.pack('<H', frames) + data[rate_at + 6:]
    raise KeyError(name)
