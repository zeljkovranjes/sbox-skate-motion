# Primitive pair separation axes

`CollisionPairAxes` provides five internal entry points over complete 44-word shape descriptors. The first four output words contain the repeated core separation; the next four contain its direction. Round radii remain separate for later manifold acceptance and expansion. Other output words and both input descriptors remain unchanged.

`SpherePair` uses the center difference and two inverse-root refinements. Coincident centers retain a zero direction. `GeneralPair` visits each shape's face axes in descending order, then ordered edge crosses above the squared-length threshold. Two single-edge shapes add perpendicular candidates, and an empty list tries center/edge and center-difference directions before using the vertical fallback. Batched projection intervals choose the largest separation, with strict comparisons preserving the first candidate on ties.

`BoxPair` preserves the specialized candidate list: descending face axes followed by all nine ordered edge crosses. Cross normalization has one refinement and retains zero crosses; the final cross is repeated as the fourth candidate in the last group. Signed scaling writes the final normal. Coincident aligned boxes can therefore select a zero cross and return zero core separation rather than a negative interval gap. The port retains that behavior instead of substituting the general routine's filtered list.

`TriangleBoxPair` starts with the triangle normal, then descending box face axes and nine ordered edge crosses. Cross normalization uses one refinement and retains zero crosses. Triangle interval projection compares the second and third vertices before the first, preserving strict comparisons and ties. `BoxTrianglePair` reverses descriptor order and toggles the sign bit in all four direction words.

The routines validate complete buffers and supported descriptor metadata before writes. There is no complete pair dispatcher yet: cylinder-specific routines remain unported. These kernels do not own live collision scheduling.

## Validation

Native comparisons match 1,097 general/sphere cases and 256 box cases across 135,300 exact words, including unchanged descriptors and output guards. All 1,353 original cases repeat with reversed order and poisoned scratch. Original execution covers all 28 sphere instructions, 537 of 541 generic instructions and all 593 box instructions. The four generic instructions not reached handle an empty candidate list after the mandatory fallback.

The composed host axis, shape-feature and contact path matches 80 earlier original cases across 57,680 words. Another 45 cylinder cases are explicitly excluded because their specialized axes are unfinished. Ten unit cases cover sphere centers and radius independence, coincident defaults, collinear and perpendicular segments, both orders, box metadata independence, retained zero crosses and validation without mutation.

These are supplied-descriptor instruction and native Core comparisons under the retained arithmetic model. They do not establish hardware behavior, complete manifold generation, map scheduling or visual gameplay acceptance.

Triangle/box native comparisons match 512 cases across 51,200 words. All cases repeat in reverse order with poisoned scratch, covering all 543 triangle/box instructions and all 22 reversed-wrapper instructions. Eight additional unit cases cover both sides of the triangle, reverse direction bits, retained zero crosses and validation without mutation.

## Bounded cylinder candidate helper

`CollisionCylinderAxes.AppendSegmentCandidate` appends one candidate from a point/axis pair and a bounded second segment. It preserves endpoint clamping, two inverse-root refinements, count advancement and unrelated output rows. Endpoint projection deliberately uses the second endpoint directly. Complete cylinder pair dispatch still needs the remaining circle helpers and candidate assembly.

Native checks match 584 supplied helper cases across 30,368 words, repeated with reversed order and poisoned scratch. Nine unit cases cover bounded endpoints, retained zero crosses, exact direction rounding and invalid buffers without mutation. Instruction replay covers 127 of 170 instructions. The remaining 43 form the parallel fallback: the retained arithmetic profile flushes its subnormal comparison threshold to zero, so that branch cannot be reached by nonnegative squared lengths. Its translation is not dynamically verified, and these results do not establish physical hardware behavior.

## Circle and segment candidate search

`CircleSegmentCandidate` projects segment endpoints onto a circle, brackets directional slopes and runs at most ten weighted refinements when the slope signs straddle zero. It selects the final endpoint with strict comparisons, then chooses normalized separation, a tangent-derived direction or the circle axis. Circle projection and final normalization use one inverse-root refinement. Input validation happens before output writes.

Native checks match 1,236 supplied cases across 39,552 words, including coincident endpoints, zero radius, translated geometry and oblique axes. All cases repeat with reversed order and poisoned scratch; execution covers all 332 instructions. Eight new unit cases cover degenerate directions, search rounding, output guards and incomplete buffers. Circle/circle candidates and complete cylinder pair assembly remain unfinished. These supplied-input comparisons do not establish complete collision scheduling or live gameplay parity.

## Paired-circle candidates

`TryCircleCircleCandidate` preserves the supplied seed direction, radial feasibility rejection, near-plane projection path and bounded angular search. The tolerance uses the smaller radius. The search retains endpoint reflection, area-based initialization and strict comparisons. Rejected candidates leave all output words unchanged; accepted candidates choose separation, crossed tangents or the first circle axis.

Native checks match 2,624 supplied cases across 107,584 words, including 1,314 accepted and 1,310 rejected candidates. All cases repeat with reversed order and poisoned scratch, covering all 602 instructions. Ten new unit cases cover exact directions, coincident/opposite axes, zero/unequal radii, rejection guards and incomplete buffers. Complete cylinder candidate assembly and final manifold integration are described below. The earlier bounded segment helper's parallel fallback is still dynamically unverified under the retained arithmetic profile.

## Complete cylinder pairs and primitive dispatch

`CylinderPair` and `ReversedCylinderPair` compose the verified circle/segment, bounded segment and circle/circle helpers. They retain the original ordered cap and edge candidates for sphere, capsule, triangle, box and cylinder counterparts, including signed zeros and degenerate axes. Other face axes append in ascending order. Projection selects the largest interval gap with strict comparisons, so the first candidate wins ties; reversal toggles all four direction sign bits.

All 2,304 supplied cylinder pairs match 389,376 words in the native runtime, including candidate counts, every candidate row, final separation/direction, input descriptors and output guards. Original captures repeat with reversed case order and poisoned registers/scratch. Main routine coverage is 1,562 of 1,594 instructions, with all 33 wrapper instructions covered. The mandatory first candidate makes the empty-list path unreachable; the sphere fallback remains dynamically uncovered under the retained arithmetic profile. Earlier bounded-segment parallel fallback limits also remain.

`Pair` selects the specialized sphere, box, triangle/box and cylinder routes for all five supported kinds. The connected axis, feature and contact path matches all 125 supplied cases and 90,125 words, without exclusions. This establishes the represented primitive narrow-phase stages; final manifold radius expansion, filtering and output layout are connected below; live map scheduling remains unfinished.

## Final primitive manifolds

`CollisionContactManifold.Assemble` connects pair separation to feature selection and contact assembly, refines a single contact normal only when its projection gap permits it, applies second-triangle filtering, then expands both rounded radii. The output retains shape identities, material words, count, reversed normal and interleaved contact points. Radius/margin rejection leaves caller storage unchanged. Triangle rejection can return zero after writing the generated manifold; callers must use the returned accepted count.

The complete outer routine matches 600 supplied primitive cases and 1,080 triangle-context variants, totaling 398,160 native word comparisons. All 1,680 repeat with case order reversed and poisoned scratch/registers; all 331 outer instructions execute. Rejected cases with output writes are explicitly covered. Eleven unit cases cover radius offsets, exact margin inclusion, metadata and guards, one-sided triangle acceptance, first/second shape asymmetry, coincident centers and invalid inputs. No existing tests changed.

The triangle filter preserves supplied adjacency cosine slots, flag masks, edge/vertex classification, normal policy and contact projection; those supplied values still require production from live map geometry. The existing generic contact kernel deliberately rejects a demonstrated contact-buffer overflow rather than reproducing memory corruption. Broadphase/body ownership, material lookup, batch ordering and live map scheduling remain outside this primitive entry.

## Supplied map batches and contact collection

`CollisionMapContactBatch.Generate` traverses triangles, bodies and manifold points in supplied order. It retains the distinct disabled/type/activity gates, both packed category filters, material max/max/min selection, triangle bounds overlap and velocity-dependent margin arithmetic. It writes full contact records, including friction directions, relative velocity, stable identities and physical body caches. A rejected manifold's internal buffer is not treated as an accepted contact stream.

`CollisionContactCollector` preserves allocation before writing and completion after writing. Duplicate rejection drops the accepted count while retaining the written backing record. A full collector can reduce geometric patches before checking capacity again or flush its inline records before reusing storage. Total-capacity failure increments rejected count; inline-full failure without streaming has a distinct flag-only path.

`CollisionContactOutputStream` reserves an ordered destination range atomically, clamps overflow and copies only the available prefix. Flush accounting retains the complete candidate count even when the output truncates. This is the host adapter for ordered record publication; the original transport's transfer timing is not claimed.

Duplicate suppression matches 320 cases and 1,035,840 native words, covering all 181 instructions. Full allocation, geometric reduction, flush and output match 112 cases and 1,394,704 native words. All 237 allocation/flush/output instructions execute; all cases repeat with reversed order and poisoned scratch. The source execution uses an explicit synchronous transfer-bus model, including failed conditional-store retries, rather than claiming hardware scheduling evidence.

These inputs still need the live world traversal, adjacency/category/material producer, body identity binding, moving bounds and refresh schedule. No incomplete live solver graph was enabled.

## Engine triangles and batching

`CollisionWorldTriangleConstruction` builds the full decoded triangle descriptor
from three vertices, an affine transform, surface/group data and optional authored
edge codes. Missing edge data uses its explicit boundary mode. It preserves
destination padding and matches 210 reference cases, including the current map's
50 triangles, across 29,400 output words in both Core and the editor.

`CollisionMapTriangleQueue` generates a full 20-triangle batch before accepting
the next triangle. Explicit generation retains its pending count; callers finalize
each queue once. Collector exhaustion stops contact generation for that batch but
does not stop queuing the next triangle. Descriptor assignment preserves padding
rather than copying the entire allocation. The ordinary resident-memory route
matches 56 cases and 234,320 words; optional input-transfer timing is not modeled.

The engine contact bridge enumerates cached shapes in order, requires explicit
physical body bindings and uses world-space metres with Z up. Query geometry is
converted back to the solver frame, with affine direction W=0 and translation W=1.
Each shape retains its own context, material IDs and identity. Null bindings skip
unowned shapes. Full enumeration uses the supported admission-override route;
it does not claim equivalence for an unported spatial acceleration structure.

The live board owner now supplies seven current body caches and recovered moving
bounds. Stationary scene shapes, including keyframed map colliders, use the static
world binding; moving props and rail contacts retain their native path. Surface
mapping and the full physical rider/scheduler remain host integration work. Geometry refresh remains
explicit, while ordinary calls update moving body transforms. These component
checks do not establish full playable-map or hardware behavior parity.

## Generated record consumption

`GeneratedContactRecord` reads the supplied physical snapshot rather than
reconstructing it from current body poses. `CompileGeneratedRecord` requires both
stable identities to resolve to unique world bindings. Raw decoding matches 375
reference records exactly. The 376 compiled/solved/world cases match within the
existing floating model tolerance, with maximum observed compiler and solver
errors below 4.8e-7 and 2.1e-7 respectively; this is not bitwise solver parity.

The generated solver entry permits nonnegative sliding friction above static
friction, retaining the captured coefficients. Generic solver validation remains
unchanged. Shared projection and integration arithmetic avoids a second solver
implementation. This resolves all 136 previously rejected cases in the supplied
unordered-coefficient family.

## Live board ownership

The existing board tick applies pilot and external forces before generating map
contacts, then compiles generated and retained generic contacts into one workspace.
Generated rows use their cached inverse masses and projection; generic rows retain
the previous validation and workspace semantics. The existing joints, drives and
body integration run once. Native prop reactions and deck publication follow once.

Collision output grows synchronously across fifty-contact flushes. This is a host
memory policy rather than a claim to reproduce the external job reservation or DMA
schedule. Geometry dimensions, mass distribution, pilot and surface friction remain
explicit host adaptations. The visible rider pose writer has not been replaced.

Two representative live flows completed with 50 enumerated triangles, up to 16
generated contacts and 568 finite sampled poses. The observed keyframed-map filter
error was fixed before this result. Pickup alignment and three ollie temporal
diagnostics remain open. Neither these flows nor the isolated native coast proves
large-map performance or full behavior parity.
