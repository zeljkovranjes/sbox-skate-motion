# Generated contact arithmetic

`ContactInputs.FromGeneratedManifold` prepares supplied contact points using integer word operations for the retained runtime arithmetic profile. `PhysicalSolverWorld.CompileGeneratedContact` connects that preparation to the existing contact compiler and world solver. The world must be prepared first.

The arithmetic preserves rounding toward zero, input and result subnormal flushing, separate multiply/add rounding, exceptional encodings, reciprocal estimate tables and integer interpolation. Native floating-point mode changes do not affect these operations. Vectors store the resulting words; their ordinary arithmetic operators are not used during preparation.

This matters for friction directions. At rest, the flushed cutoff selects a zero tangent, and multiplying it by the extended inverse-root estimate produces zero. Sliding directions retain estimate precision rather than being normalized with the host square root. The earlier finite-model `FromManifold` entry remains a separate contract, described in [contact preparation](CONTACT-PREPARATION.md).

## Validation

Inside the editor, 1,808 arithmetic cases match 65,088 exact word comparisons against the retained reference profile. These cover nine operations, exceptional encodings, large exponent gaps and seeded random words. A separate 122-case contact-writer replay matches 5,368 comparisons across contact geometry, motion, materials and body fields. Twelve added unit cases cover numerical boundaries, stationary and sliding directions, moving nonresponding surfaces, and compilation through solving and registered-owner reporting.

## Remaining integration

These comparisons validate this arithmetic profile and bounded preparation. They do not establish hardware parity. Contact compilation and solving still use their existing arithmetic models. Shape generation, broadphase selection, map ingestion into this path and physical rider scheduling remain incomplete; visible rider rendering has not been switched to this path.
