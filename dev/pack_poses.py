"""Packs a raw pose container (.sman, versions 2-5) losslessly for shipping, and checks the packed file restores it.

The rider's raw poses are mostly per-frame float keys, which LZ4 (the compression s&box code can read) barely shrinks.
Arranged first they pack to about 62% of their size: the headers go first, then every key, each track's keys XORed
with its previous frame's and the whole key stream split into four byte planes. The package
(Engine/RiderFiles.cs and PackedPoseLayout.cs) reverses this byte for byte as it loads the file, so the clips play
exactly as from the original.

Usage: pack_poses.py <original.sman> <packed.sman>     (needs: pip install lz4 numpy)
Packed layout: "SMZ1", u32 original length, u32 header length, then one LZ4 frame of headers + arranged keys.
"""
import hashlib
import struct
import sys

import lz4.frame
import numpy as np

MAGIC = b'SMZ1'


class Stream:
    def __init__(self, data):
        self.data, self.at = data, 0

    def take(self, n):
        self.at += n
        return self.data[self.at - n:self.at]


def walk(headers, copy, track):
    """Walks a container's headers from `headers`, handing every header byte to `copy` and each track's key count to
    `track` (which deals with that track's keys)."""
    def take(n):
        chunk = headers.take(n)
        copy(chunk)
        return chunk

    def u8():
        return take(1)[0]

    def u16():
        return struct.unpack('<H', take(2))[0]

    def u32():
        return struct.unpack('<I', take(4))[0]

    magic, version = u32(), u32()
    if magic != 0x4e414d53 or version not in (2, 3, 4, 5):
        raise ValueError('not a version 2-5 raw pose container')
    bones = u16()
    for _ in range(bones):
        take(u8())
        take(2)
    refs = u16()
    take(refs * bones * 48)
    for _ in range(u32()):
        take(u8())
        take(8)
        frames = u16()
        take(4)
        if version >= 3:
            take(32)
        if version >= 4:
            for _ in range(u8()):
                take(48)
                take(u32() * 4)
        for kind in take(bones * 3):
            track(0 if kind == 0 else 1 if kind == 1 else frames)


def pack(data):
    source = Stream(data)
    meta = bytearray()
    keys = []

    def track(count):
        if count:
            words = np.frombuffer(source.take(count * 16), dtype='<u4').reshape(count, 4)
            keys.append(np.concatenate([words[:1], words[1:] ^ words[:-1]]).reshape(-1))

    walk(source, meta.extend, track)
    if source.at != len(data):
        raise ValueError('trailing data')
    words = np.concatenate(keys) if keys else np.zeros(0, dtype='<u4')
    planes = words.view(np.uint8).reshape(-1, 4).T.copy().tobytes()
    body = lz4.frame.compress(bytes(meta) + planes, compression_level=lz4.frame.COMPRESSIONLEVEL_MAX,
                              block_size=lz4.frame.BLOCKSIZE_MAX4MB)
    return MAGIC + struct.pack('<II', len(data), len(meta)) + body


def unpack(packed):
    """The reverse, as the addon does it: here to check a packed file before shipping it."""
    if packed[:4] != MAGIC:
        raise ValueError('not a packed pose container')
    length, meta_length = struct.unpack('<II', packed[4:12])
    payload = lz4.frame.decompress(packed[12:])
    words = np.frombuffer(payload, dtype=np.uint8, offset=meta_length).reshape(4, -1).T.copy().view('<u4').reshape(-1)
    out = bytearray()
    at = 0

    def track(count):
        nonlocal at
        if count:
            keys = words[at:at + count * 4].reshape(count, 4)
            at += count * 4
            out.extend(np.bitwise_xor.accumulate(keys, axis=0).astype('<u4').tobytes())

    walk(Stream(payload[:meta_length]), out.extend, track)
    if len(out) != length or at != len(words):
        raise ValueError('packed container does not restore its original length')
    return bytes(out)


if __name__ == '__main__':
    source, target = sys.argv[1], sys.argv[2]
    original = open(source, 'rb').read()
    packed = pack(original)
    restored = unpack(packed)
    if restored != original:
        raise SystemExit('restore mismatch: not writing')
    open(target, 'wb').write(packed)
    print(f'{len(original):,} -> {len(packed):,} bytes ({len(packed) / len(original):.0%}); restores byte for byte; '
          f'original sha256 {hashlib.sha256(original).hexdigest()}')
