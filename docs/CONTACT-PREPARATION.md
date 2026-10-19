# Contact preparation

`ContactManifoldPoint` carries a narrow-phase point pair, normal and material properties. `ContactInputs.FromManifold` derives its relative point velocity and both friction directions from the current bodies. `PhysicalSolverWorld.CompileManifoldContact` connects this preparation to the existing compiler, solver, integration and contact-reporting path. Prepare the world before compiling a contact.

Sliding contacts use the cross product of relative point velocity and the normal as the first tangent. Contacts below the squared-slip cutoff use a projected coordinate axis; the axis changes with the normal's X component. The second direction comes from the normal and the unnormalized first tangent, followed by the same reciprocal-length scale. Ordered fused arithmetic and signed zeros are retained.

The finite arithmetic model preserves the largest-subnormal cutoff and single-precision squared-slip rounding even when the engine flushes float subnormals. Its reciprocal-square-root estimate is exact before the existing refinement interface. This is an explicit arithmetic boundary, not hardware-equivalent execution.

## Validation

The original bounded contact writer executes its prologue, point-velocity calculation, tangent selection, material stores and body-data copy. All 243 instructions in those ranges and their shuffle/comparison helpers execute. There are 122 cases, including stationary contacts, normal-only motion, both fallback axes, angular motion, moving nonresponding surfaces, randomized inputs and adjacent values on both selection boundaries.

In-engine preparation matches 5,368 exact comparisons across geometry, relative velocity, material fields, body centers, accelerations and mode flags. The first native attempt exposed a flushed cutoff that produced invalid stationary tangents; its failed report is retained. Eleven additional runtime tests include the full prepared-contact, solve, integration and registered-owner reporting path.

## Remaining integration

This consumes supplied manifold points. It does not generate body contacts from map geometry, run the broadphase or narrowphase, choose material properties, or schedule the physical rider. Existing explicitly supplied tangent frames retain their separate path. Visible physical pose rendering and full gameplay acceptance remain unfinished.
