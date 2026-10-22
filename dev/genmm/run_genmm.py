"""Runs GenMM's run_random_generation.py (github.com/wyysf-98/GenMM, cloned to GENMM_ROOT or dev/genmm/GenMM) with the
class keyframe range its current head reads but never sets (empty: no keyframes are pinned). Example, from the export:
    run_genmm.py -i stand.bvh -o out -d cpu --up_axis Z_UP --num_frames 3x_nframes --loop 1 -s 7"""
import os
import runpy
import sys

ROOT = os.environ.get('GENMM_ROOT', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'GenMM'))
sys.path.insert(0, ROOT)
os.chdir(ROOT)
from GenMM import GenMM  # noqa: E402

GenMM.KEYFRAME_INDICES = slice(0, 0)
sys.argv = ['run_random_generation.py'] + sys.argv[1:]
runpy.run_path(os.path.join(ROOT, 'run_random_generation.py'), run_name='__main__')
