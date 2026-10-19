# Primitive contact features

`CollisionShapeFeatures.Select` selects a core point, segment or polygon for a supplied direction and contact side. It accepts the same 44-word descriptors as shape construction and projection. It preserves the deterministic collision arithmetic profile, including estimate refinement, signed zeros and the different rounding orders used by each shape.

The destination requires 144 words. Word 140 identifies a point (0), segment (1), triangle (3), box face (4) or cylinder cap (8). Edge records begin at word 4 with a 16-word stride: start point, direction, auxiliary vector and replicated length. Words 132 and 136 hold the selected normal and point where the selected path writes them. Unused fields retain their previous contents; callers must not infer validity from stale values.

The descriptor address argument preserves packed identity in box segment auxiliary records. It is an explicit numeric identity, never a managed pointer or memory access. Its three trailing lanes retain the selected axis offset. This field must not be interpreted as a geometric midpoint. Cylinder paths transform retained point storage even when selecting an edge or cap; the feature kind controls which results consumers use.

Sphere and capsule selection clear the primary flag. Triangle face winding supplies its flag, while triangle point and segment selections retain it. Cylinder selection retains the flag. Box selection clears it. Contact-side winding, strict threshold comparisons, first-corner tie selection and normal refinement are preserved independently for each primitive.

## Validation

The engine comparison covers 9,128 selections and 1,350,944 exact word comparisons with no differences. It includes both contact sides, degenerate dimensions, adjacent threshold words, transformed shapes, retained buffer contents and relocated descriptor identities. Independent original execution also repeats 4,160 selections after reversing query order and randomizing scratch registers and stack storage. Targeted edge directions cover the remaining triangle selection branch.

Twelve unit cases cover construction-to-feature selection, ordered closed polygons on both sides, strict capsule/box boundaries, descriptor relocation, retained flags, zero-length segments, tie handling and buffer validation.

These are synthetic descriptor comparisons using the retained arithmetic profile. They do not establish hardware parity, complete manifold generation or live gameplay equivalence. Pair-axis selection, feature-pair contact assembly, mesh contact ingestion and physical rider scheduling remain unfinished.
