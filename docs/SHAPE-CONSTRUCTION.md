# Physical shape construction

`CollisionShapeConstruction.TryConstruct` converts a packed source shape to the world-space descriptor consumed by contact generation. It uses the deterministic [contact arithmetic](CONTACT-ARITHMETIC.md) operations. The function is internal; an engine adapter must provide geometry and stable source identities.

Source and destination buffers hold at least 24 and 44 unsigned words respectively. An optional transform holds four rows, with basis vectors followed by translation; an empty transform preserves local coordinates. Shape kinds are sphere (1), capsule (2), triangle (3), box (4), cylinder (5) and mesh container (6). A mesh container returns false without changing the buffers because its triangles require separate traversal.

For primitives, source rows 0-2 contain basis vectors and row 3 the center. For triangles, those rows contain vertices A/B/C and the normal. Source words 17-19 carry dimensions or triangle adjacency metadata; word 20 is the round radius and word 23 contains flags. Cylinder construction reorders its axial and radial basis directions. Dimensions are retained rather than rescaled by the supplied transform.

Triangle flag bit 1 requests normal reconstruction. Construction clears that bit, computes the normal, and caches it in the source. It transforms vertices, constructs three ordered edges, and retains reciprocal-estimate precision in both normalized edges and lengths. Degenerate geometry follows the same word operations; no guessed normal or finite fallback is substituted. Destination fields that a shape does not write remain unchanged.

## Validation

The native editor matches 39,744 exact comparisons across 288 cases, each constructed twice. Coverage includes all six dispatch entries, identity and nonidentity transforms, arbitrary preserved fields, degenerate and tiny triangles, cached normals and source metadata. Nine unit cases cover metadata, transformed sphere centers, cylinder basis order, triangle caching and mesh-container handling.

## Remaining integration

This constructs descriptors; it does not generate contact pairs. Full manifold generation, broadphase selection, authored mesh decoding, material selection, activation and live physical-rider scheduling remain unfinished. The reference arithmetic is a retained emulator profile, not proof of hardware parity.
