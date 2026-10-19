# Ordered controller gestures

`GesturePatternMatcher` advances supplied two-dimensional stick samples through caller-supplied ordered paths. Each path retains progress, cumulative error, gap and span state. Completed paths compete using their length, error and timing; stable ordering resolves ties. The selected path reports its identity, quality, error and span.

The matcher retains the selected endpoint while subsequent samples stay within its tolerance. After a match, the next update clears active paths without recognizing another gesture. Initial sample gating, timing modes and the quality curve remain independent of gameplay actions.

The engine replay matches 5,922 updates across 182 synthetic scenarios: 17,766 result words and 25,968 retained-state words, with no differences. Twelve new unit cases exercise progress, resets, timing, hold behavior and winner selection. Authored paths are caller supplied; none ship in the library.

The rider adapter now loads caller-supplied gesture profiles, filters both stick lanes, delivers retained holds before new matches and places accepted matches into the controller command queue. Matching identities use ASCII case-insensitive comparison; unrelated matches preserve the previous hold-tracked identity. A queue-capacity check occurs before recognition state changes. Existing profiles without gesture data retain their previous behavior.

Callback replay matches 72 sequences, 2,520 events and 2,904 queued-command comparisons. A scoped probe through the actual engine input sampler and rider adapter exercises all 285 locally supplied paths over 1,401 updates, producing 322 paired trick/speed request frames and 67 hold request frames without pairing or nonfinite failures. It restores input overrides afterward and does not mutate the scene.

The adapter currently supplies one recognition notification per rider input update. Exact original notification cadence, hardware filtering, movement-context ownership and full trick physics/animation consumers remain unfinished. Command delivery alone does not establish completed trick behavior; the existing board motor still supplies its current trick response.
