"""Writes a raw pose clip's body (the hips and every bone under them) as a BVH for GenMM: the hips are the root, with its
place over the clip's root (which stands still on the ground in an idle), every other joint rotation only.
Usage: export_bvh.py <raw container> <clip name> <out.bvh> [fps]"""
import math
import sys

import numpy as np

import rawpose


def quat_to_matrix(q):
    x, y, z, w = q[..., 0], q[..., 1], q[..., 2], q[..., 3]
    m = np.empty(q.shape[:-1] + (3, 3))
    m[..., 0, 0] = 1 - 2 * (y * y + z * z)
    m[..., 0, 1] = 2 * (x * y - z * w)
    m[..., 0, 2] = 2 * (x * z + y * w)
    m[..., 1, 0] = 2 * (x * y + z * w)
    m[..., 1, 1] = 1 - 2 * (x * x + z * z)
    m[..., 1, 2] = 2 * (y * z - x * w)
    m[..., 2, 0] = 2 * (x * z - y * w)
    m[..., 2, 1] = 2 * (y * z + x * w)
    m[..., 2, 2] = 1 - 2 * (x * x + y * y)
    return m


def matrix_to_euler_zyx(m):
    """Angles (degrees) z, y, x with m = Rz(z) Ry(y) Rx(x), the BVH 'Zrotation Yrotation Xrotation' order."""
    sy = np.clip(-m[..., 2, 0], -1.0, 1.0)
    y = np.arcsin(sy)
    z = np.arctan2(m[..., 1, 0], m[..., 0, 0])
    x = np.arctan2(m[..., 2, 1], m[..., 2, 2])
    return np.degrees(np.stack([z, y, x], axis=-1))


def body(container):
    names, parents = container['names'], container['parents']
    hips = names.index('hips')

    def under(bone):
        while bone >= 0:
            if bone == hips:
                return True
            bone = parents[bone]
        return False
    return [b for b in range(len(names)) if under(b)]


def write_bvh(path, container, bones, positions, rotations, fps):
    names, parents = container['names'], container['parents']
    children = {b: [c for c in bones if parents[c] == b] for b in bones}
    offsets = positions.mean(axis=0)
    lines = ['HIERARCHY']

    def joint(b, depth):
        pad = '  ' * depth
        o = offsets[b]
        if depth == 0:
            lines.append(f'ROOT {names[b]}')
        else:
            lines.append(f'{pad}JOINT {names[b]}')
        lines.append(pad + '{')
        lines.append(f'{pad}  OFFSET {o[0]:.6f} {o[1]:.6f} {o[2]:.6f}')
        if depth == 0:
            lines.append(f'{pad}  CHANNELS 6 Xposition Yposition Zposition Zrotation Yrotation Xrotation')
        else:
            lines.append(f'{pad}  CHANNELS 3 Zrotation Yrotation Xrotation')
        if children[b]:
            for c in children[b]:
                joint(c, depth + 1)
        else:
            lines.append(f'{pad}  End Site')
            lines.append(pad + '  {')
            lines.append(f'{pad}    OFFSET 0.000000 0.000000 0.000000')
            lines.append(pad + '  }')
        lines.append(pad + '}')
    joint(bones[0], 0)
    order = []

    def visit(b):
        order.append(b)
        for c in children[b]:
            visit(c)
    visit(bones[0])
    frames = positions.shape[0]
    unit = rotations.astype(np.float64)
    unit = unit / np.linalg.norm(unit, axis=-1, keepdims=True)
    euler = matrix_to_euler_zyx(quat_to_matrix(unit))
    lines.append('MOTION')
    lines.append(f'Frames: {frames}')
    lines.append(f'Frame Time: {1.0 / fps:.8f}')
    for f in range(frames):
        values = []
        for b in order:
            if b == bones[0]:
                values += list(positions[f, b])
            values += list(euler[f, b])
        lines.append(' '.join(f'{v:.6f}' for v in values))
    open(path, 'w').write('\n'.join(lines) + '\n')
    return order


if __name__ == '__main__':
    src, name, out = sys.argv[1], sys.argv[2], sys.argv[3]
    fps = float(sys.argv[4]) if len(sys.argv) > 4 else 30.0
    container = rawpose.read_container(open(src, 'rb').read())
    clip = next(c for c in container['clips'] if c['name'] == name)
    positions, rotations = rawpose.clip_frames(container, clip)
    step = max(1, int(round(clip['fps'] / fps)))
    positions, rotations = positions[::step], rotations[::step]
    bones = body(container)
    order = write_bvh(out, container, bones, positions, rotations, clip['fps'] / step)
    np.savez(out + '.src.npz', positions=positions, rotations=rotations, order=np.array(order))
    print(f'{name}: {positions.shape[0]} frames at {clip["fps"] / step:.0f} fps, {len(bones)} body bones -> {out}')
