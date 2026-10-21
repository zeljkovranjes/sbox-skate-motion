"""Writes a clip set (SMAN v1) without its keyframes: same skeleton, clip names, rates and frame counts.

When every clip has a raw pose bound (the rider's clips do), sampling never reads a clip set's own keys, so the slim set
plays exactly the same. Usage: slim_clipset.py <in.sman> <out.sman>
"""
import struct
import sys

src, dst = sys.argv[1], sys.argv[2]
data = open(src, 'rb').read()
at = 0


def take(n):
    global at
    chunk = data[at:at + n]
    at += n
    return chunk


assert take(4) == b'SMAN'
version = struct.unpack('<I', take(4))[0]
assert version == 1
out = bytearray(b'SMAN') + struct.pack('<I', 1)
skeleton_start = at
bones = struct.unpack('<H', take(2))[0]
for _ in range(bones):
    take(take(1)[0])        # name
    take(2 + 12 + 16)       # parent, position, rotation
out += data[skeleton_start:at]
count = struct.unpack('<I', take(4))[0]
out += struct.pack('<I', count)
kept = 0
for _ in range(count):
    name_len = take(1)[0]
    name = take(name_len)
    rate, frames = struct.unpack('<fH', take(6))
    kinds = take(bones * 2)
    keys = lambda kind: 0 if kind == 0 else 1 if kind == 1 else frames  # noqa: E731
    for bone in range(bones):
        take(keys(kinds[bone * 2]) * 8)        # packed rotations: four int16
        take(keys(kinds[bone * 2 + 1]) * 12)   # positions: three floats
    out += bytes([name_len]) + name + struct.pack('<fH', rate, frames) + bytes(bones * 2)
    kept += 1
assert at == len(data), (at, len(data))
open(dst, 'wb').write(out)
print(f'{kept} clips, {bones} bones: {len(data):,} -> {len(out):,} bytes')
