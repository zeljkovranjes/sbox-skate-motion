# Primitive projection intervals

`CollisionShapeProjection.Project` and `ProjectBatch` project constructed sphere, capsule, triangle, box and cylinder descriptors onto supplied directions. They operate on the core geometry; round-radius expansion belongs to the caller. They use the same deterministic word arithmetic as [shape construction](SHAPE-CONSTRUCTION.md).

A descriptor contains 44 words. A scalar direction contains four words and writes two four-word rows, with the lower and upper bounds replicated across their respective rows. The batch entry consumes consecutive direction rows and reserves three rows per result, leaving each third row unchanged. Empty batches leave all output storage unchanged.

Box scalar queries compute an axis dot product and then scale it by the dimension. Batch queries scale the axis components before the dot product. This distinction is intentional: rounding differs for some directions and dimensions. Cylinder queries retain the supplied direction and two reciprocal-root refinements; they do not normalize caller input. Triangle comparisons retain their selection order, including ties and exceptional encodings.

## Validation

Inside the editor, 38,800 exact comparisons match 1,600 scalar queries and 85 batch calls on descriptors produced by the shape-construction reference. Cases include all five primitive kinds, transforms, randomized geometry, zero and reversed directions, tiny and extreme words, untouched output rows and empty batches. Forty-six box queries differ between scalar and batch output in this corpus; both paths match their own references.

Ten unit cases cover construction-to-projection, distinct box rounding, output padding, empty batches, round-radius exclusion and the cylinder's supplied-direction behavior. These checks validate the retained arithmetic profile; they do not establish hardware or full-world equivalence.

## Remaining integration

Feature extraction, pair-axis selection and full manifold generation still need integration. These methods do not query map geometry, select materials, schedule physical bodies or change the visible rider.
