"""Puts a GenMM-generated clip (a stand idle with the board in hand) in place of the source clip in a pose bank and its clip
set: the body from the generated BVH (its loop seam eased onto the source's own lead-in), the root kept, the board
rebuilt in the carrying hand from its average hold, every other bone from the nearest source frame; 60 fps, packed.
Usage: integrate.py <unpacked pose bank> <clip name> <syn.bvh> <assets folder>... [--write]
(each assets folder holds skater-local-v5.sman and skater-corrected.sman; unpack the bank with pack_poses.unpack)"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import pack_poses  # noqa: E402

import bvh_read  # noqa: E402
import rawpose  # noqa: E402
from rawpose import qconj, qmul, qrot  # noqa: E402

SEAM = 20


def unit(q):
    return q / np.linalg.norm(q, axis=-1, keepdims=True)


def slerp(a, b, t):
    a, b = unit(a), unit(b)
    dot = (a * b).sum(-1, keepdims=True)
    b = np.where(dot < 0, -b, b)
    dot = np.abs(dot)
    theta = np.arccos(np.clip(dot, -1, 1))
    small = theta < 1e-6
    s = np.sin(theta)
    wa = np.where(small, 1 - t, np.sin((1 - t) * theta) / np.where(small, 1, s))
    wb = np.where(small, t, np.sin(t * theta) / np.where(small, 1, s))
    return unit(wa * a + wb * b)


def fk(parents, positions, rotations):
    """Model-space places and turns from locals (frames x bones)."""
    wp = np.zeros_like(positions)
    wr = np.zeros_like(rotations)
    for b, p in enumerate(parents):
        if p < 0:
            wp[:, b], wr[:, b] = positions[:, b], unit(rotations[:, b])
        else:
            wp[:, b] = wp[:, p] + qrot(wr[:, p], positions[:, b])
            wr[:, b] = unit(qmul(wr[:, p], rotations[:, b]))
    return wp, wr


def main():
    args = [a for a in sys.argv[1:] if a != '--write']
    write = '--write' in sys.argv
    bank, clip_name, syn_path, folders = args[0], args[1], args[2], args[3:]
    raw = open(bank, 'rb').read()
    container = rawpose.read_container(raw)
    names, parents = container['names'], container['parents']
    clip = next(c for c in container['clips'] if c['name'] == clip_name)
    src_pos, src_rot = rawpose.clip_frames(container, clip)          # 60 fps, all bones
    src_rot = unit(src_rot)
    gnames, _, _, g_pos, g_rot, _ = bvh_read.read(syn_path)          # 30 fps, body bones, hips as root
    frames = g_pos.shape[0]
    body = [names.index(n) for n in gnames]
    # The source at 30 fps (as exported) for the seam and the nearest frames.
    s30_rot = src_rot[::2]
    s30_pos = src_pos[::2]
    feat_s = s30_rot[:, body].reshape(s30_rot.shape[0], -1)
    feat_g = g_rot.reshape(frames, -1)
    nearest = np.array([np.argmin(((feat_s - f) ** 2).sum(-1)) for f in feat_g])
    # Loop seam: the last frames ease onto the source's own lead-in to the generated first frame.
    lead = (nearest[0] - SEAM + np.arange(SEAM)) % s30_rot.shape[0]
    for k in range(SEAM):
        w = (k + 1) / SEAM
        w = w * w * (3 - 2 * w)
        f = frames - SEAM + k
        g_rot[f] = slerp(g_rot[f], s30_rot[lead[k]][body], w)
        g_pos[f, 0] = g_pos[f, 0] * (1 - w) + s30_pos[lead[k], body[0]] * w
    # Full skeleton at 30 fps: body from GenMM (joint places from the source's mean, the hips' from GenMM), the rest from
    # the nearest source frame.
    bones = len(names)
    pos30 = s30_pos[nearest].copy()
    rot30 = s30_rot[nearest].copy()
    for j, b in enumerate(body):
        rot30[:, b] = g_rot[:, j]
        if j == 0:
            pos30[:, b] = g_pos[:, 0]
        else:
            pos30[:, b] = src_pos[:, b].mean(axis=0)
    root = names.index('root')
    pos30[:, root], rot30[:, root] = src_pos[0, root], src_rot[0, root]
    # The board in the carrying hand: its average hold in the source, carried by the generated hand.
    board = names.index('board')
    sw_p, sw_r = fk(parents, src_pos, src_rot)
    best = None
    for hand in ('right_hand', 'left_hand'):
        h = names.index(hand)
        rel_r = unit(qmul(qconj(sw_r[:, h]), sw_r[:, board]))
        rel_p = qrot(qconj(sw_r[:, h]), sw_p[:, board] - sw_p[:, h])
        spread = np.linalg.norm(rel_p - rel_p.mean(0), axis=-1).max()
        if best is None or spread < best[0]:
            ref = rel_r[0]
            aligned = np.where(((rel_r * ref).sum(-1) < 0)[:, None], -rel_r, rel_r)
            best = (spread, h, rel_p.mean(0), unit(aligned.mean(0)))
    spread, hand, hold_p, hold_r = best
    gw_p, gw_r = fk(parents, pos30, rot30)
    board_w_p = gw_p[:, hand] + qrot(gw_r[:, hand], np.broadcast_to(hold_p, gw_p[:, hand].shape))
    board_w_r = unit(qmul(gw_r[:, hand], np.broadcast_to(hold_r, gw_r[:, hand].shape)))
    parent = parents[board]
    pos30[:, board] = qrot(qconj(gw_r[:, parent]), board_w_p - gw_p[:, parent])
    rot30[:, board] = unit(qmul(qconj(gw_r[:, parent]), board_w_r))
    # 60 fps, looping.
    count = frames * 2
    a = np.arange(count) // 2
    b = (a + 1) % frames
    t = ((np.arange(count) % 2) * 0.5)[:, None, None]
    pos60 = pos30[a] * (1 - t) + pos30[b] * t
    rot60 = slerp(rot30[a], rot30[b], t)
    # Checks against the source: the board's hold, the hips' range, smoothness at 60 fps.
    w60_p, w60_r = fk(parents, pos60, rot60)
    hold = np.linalg.norm(qrot(qconj(w60_r[:, hand]), w60_p[:, board] - w60_p[:, hand]) - hold_p, axis=-1).max()

    def speeds(rot):
        rel = qmul(rot[1:], qconj(rot[:-1]))
        return 2 * np.arctan2(np.linalg.norm(rel[..., :3], axis=-1), np.abs(rel[..., 3])) * 60

    sp_s, sp_g = speeds(src_rot[:, body]), speeds(rot60[:, body])
    wrap = speeds(np.stack([rot60[-1], rot60[0]])[:, body])
    print(f'carry hand {names[hand]} (board spread in it {spread * 39.37:.2f} in), board held to {hold * 39.37:.3f} in')
    print(f'source {src_pos.shape[0]} frames, generated {count} frames at 60 fps ({count / 60:.1f} s)')
    print(f'joint speed p99 source {np.percentile(sp_s, 99):.2f} generated {np.percentile(sp_g, 99):.2f} rad/s; '
          f'max change per frame source {np.abs(np.diff(sp_s, axis=0)).max():.2f} generated {np.abs(np.diff(sp_g, axis=0)).max():.2f}; '
          f'loop wrap speed {wrap.max():.2f} rad/s')
    encoded = rawpose.encode_clip(container, clip, pos60, rot60)
    new_raw = rawpose.replace_clip(raw, container, clip_name, encoded)
    check = rawpose.read_container(new_raw)
    new_clip = next(c for c in check['clips'] if c['name'] == clip_name)
    cp, cr = rawpose.clip_frames(check, new_clip)
    print('decoded back: frames', new_clip['frames'], 'max place error', np.abs(cp - pos60).max().round(6),
          'clips', len(check['clips']))
    packed = pack_poses.pack(new_raw)
    assert pack_poses.unpack(packed) == new_raw
    for folder in folders:
        slim = open(os.path.join(folder, 'skater-corrected.sman'), 'rb').read()
        new_slim = rawpose.set_slim_frames(slim, clip_name, count)
        print(folder, 'pose bank', len(packed), 'bytes; clip set', len(new_slim), 'bytes')
        if write:
            open(os.path.join(folder, 'skater-local-v5.sman'), 'wb').write(packed)
            open(os.path.join(folder, 'skater-corrected.sman'), 'wb').write(new_slim)
    print('written' if write else 'dry run')


main()
