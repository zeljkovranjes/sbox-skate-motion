# Solved contact reporting

`PhysicalSolverWorld.SolveIntegrateAndCollect` connects constraint solving, retained body movement, contact reporting and per-owner collection. The caller prepares the world, supplies compiled constraints and a reusable output buffer, and receives the number of reported contacts. The buffer must hold every input contact; insufficient capacity fails before consuming the prepared step.

Compilation retains the contact materials, effective inverse masses and the reporting request from either body's motion flags. Reporting requires bit 8 and a strictly positive solved normal impulse. Rejected rows do not occupy output slots; accepted rows preserve input order. Each update clears stale owner lists even when no contact is reported.

The reported position uses each body's updated position plus its compiled offset, weighted by the effective inverse masses retained at compilation. The normal force is the normal basis times the first impulse channel. The tangential force combines the two tangent bases and their impulse channels. Both use inverse step squared; the separation channel is excluded. Rider feedback consumes the tangential force. The full tangential vector also retains its signed fourth component for exact comparisons.

The exporter preserves the arithmetic order, including fused operations and reciprocal refinement. Valid loaded rows require a nonzero total effective inverse mass. The adapter does not invent a fallback point for an invalid zero-mass loaded row.

## Validation and remaining work

The native runtime matches 72 export cases across 11,195 bit-exact comparisons. The connected solver, integration, export and owner-collection check matches 24 cases across 3,864 comparisons with zero numerical error. These cases include disabled reporting, zero solve passes, multiple solve passes, separation corrections and several step lengths. Regression tests also cover reporting flags independent of response flags, force separation, stale clearing and capacity failure.

Inputs remain synthetic. The comparison uses finite arithmetic callbacks and synchronous transfer in its external reference runner; it is not hardware arithmetic parity. Zero-speed limit calculations use explicitly masked scratch values, checked with a second scratch value for output independence. Production collision ingestion, body registration lifecycle, complete world scheduling and live rider pose integration remain unfinished. The experimental physical pose renderer remains disconnected.
