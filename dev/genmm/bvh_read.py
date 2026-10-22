"""Reads a BVH (any rotation order per joint, 3 or 6 channels) into joint names, parents, offsets and per-frame local
positions and quaternions (xyzw)."""
import re

import numpy as np


def axis_quat(axis, degrees):
    half = np.radians(degrees) * 0.5
    q = np.zeros(degrees.shape + (4,))
    q[..., 'XYZ'.index(axis)] = np.sin(half)
    q[..., 3] = np.cos(half)
    return q


def qmul(a, b):
    ax, ay, az, aw = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    bx, by, bz, bw = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return np.stack([aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
                     aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz], axis=-1)


def read(path):
    text = open(path).read()
    head, motion = text.split('MOTION')
    names, parents, offsets, channels = [], [], [], []
    stack = []
    last = None
    for line in head.splitlines():
        line = line.strip()
        m = re.match(r'(ROOT|JOINT)\s+(\S+)', line)
        if m:
            names.append(m.group(2))
            parents.append(stack[-1] if stack else -1)
            offsets.append(None)
            channels.append([])
            last = len(names) - 1
            continue
        if line.startswith('End Site'):
            last = None
            stack.append(-2)
            continue
        if line == '{':
            if last is not None:
                stack.append(last)
                last = None
            continue
        if line == '}':
            stack.pop()
            continue
        if line.startswith('OFFSET') and stack and stack[-1] >= 0 and offsets[stack[-1]] is None:
            offsets[stack[-1]] = np.array([float(v) for v in line.split()[1:4]])
        if line.startswith('CHANNELS') and stack and stack[-1] >= 0:
            channels[stack[-1]] = line.split()[2:]
    lines = [l for l in motion.strip().splitlines() if l.strip()]
    frames = int(lines[0].split(':')[1])
    frame_time = float(lines[1].split(':')[1])
    data = np.array([[float(v) for v in l.split()] for l in lines[2:2 + frames]])
    positions = np.tile(np.array(offsets)[None], (frames, 1, 1)).astype(np.float64)
    rotations = np.zeros((frames, len(names), 4))
    rotations[..., 3] = 1.0
    col = 0
    for j, chans in enumerate(channels):
        q = np.zeros((frames, 4))
        q[:, 3] = 1.0
        for c in chans:
            v = data[:, col]
            col += 1
            if c.endswith('position'):
                positions[:, j, 'XYZ'.index(c[0])] = v
            else:
                q = qmul(q, axis_quat(c[0], v))
        rotations[:, j] = q
    return names, parents, np.array(offsets), positions, rotations, frame_time
