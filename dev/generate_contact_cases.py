"""Generate original synthetic contact records for private instruction probes."""
import json
from pathlib import Path
import random
import sys

rng = random.Random(3915)

def cross(a, b):
    return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]

def dot(a, b):
    return sum(x*y for x, y in zip(a, b))

def random_vector(scale=8):
    return [rng.randint(-scale, scale) / 8 for _ in range(3)]

cases = []
for case_index, passes in enumerate([0, 1, 2, 5, 25, 1, 2, 5, 25, 25]):
    bodies = []
    for body_index in range(3):
        bodies.append({
            "inverseMass": [0.5, 1.0, 0.0][body_index],
            "inverseInertia": [0.5, 1.0, 2.0] if body_index < 2 else [0.0]*3,
            "linear": random_vector(16) if body_index < 2 else [0.0]*3,
            "angular": random_vector() if body_index < 2 else [0.0]*3,
            "correction": random_vector(2) if case_index > 6 and body_index < 2 else [0.0]*3,
            "correctionAngular": random_vector(2) if case_index > 6 and body_index < 2 else [0.0]*3,
        })
    contacts = []
    for contact_index in range(1 + case_index % 4):
        ia = contact_index % 2
        ib = ia + 1
        a, b = bodies[ia], bodies[ib]
        offset_a, offset_b = random_vector(4), random_vector(4)
        directions = [[0.0, 0.0, 1.0], [1.0, 0.0, 0.0], [0.0, 1.0, 0.0]]
        angular_a, angular_b, denominators = [], [], []
        for direction in directions:
            lever_a, lever_b = cross(offset_a, direction), cross(offset_b, direction)
            response_a = [v*i for v, i in zip(lever_a, a["inverseInertia"])]
            response_b = [v*i for v, i in zip(lever_b, b["inverseInertia"])]
            angular_a.append(response_a)
            angular_b.append(response_b)
            denominators.append(a["inverseMass"] + b["inverseMass"] + dot(lever_a, response_a) + dot(lever_b, response_b))
        contacts.append({
            "bodyA": ia, "bodyB": ib, "offsetA": offset_a, "offsetB": offset_b,
            "directions": directions, "angularA": angular_a, "angularB": angular_b,
            "coefficients": [[direction[axis]/mass for direction, mass in zip(directions, denominators)] for axis in range(3)],
            "bias": [0.125, 0.0, 0.0, 0.25] if case_index > 4 else [0.0]*4,
            "impulse": [0.0]*4, "staticFriction": 0.75, "slidingFriction": 0.5,
        })
    cases.append({"name": f"coupled-{case_index}", "passes": passes, "bodies": bodies, "contacts": contacts})

output = Path(sys.argv[1])
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({"schema": 1, "cases": cases}, indent=2))
print(f"Generated {len(cases)} original synthetic cases: {output}")
