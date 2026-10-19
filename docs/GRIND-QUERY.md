# Static grind edge queries

`GrindEdgeQuery.Collect` queries caller-supplied `GrindEdgeChunk` trees in the rig's
Y-up coordinate frame. It returns up to 40 `GrindEdgeRecord` values into a reusable
array. `GrindEdgeTreeBuilder` produces these trees from supplied bounds and records.
`GrindEdgeLookup` joins the collector and overlap filter. The scene's marked-rail
adapter now uses this path to supply gameplay contacts.

Each chunk owns copied node, chain, bounds and record arrays. A node has a chain
head, eight octants and a mask distinguishing leaf chain heads from child nodes.
An index of -1 ends a chain or marks an empty leaf. Tree construction and geometry
invalidation belong to the producer; rebuild a chunk when its geometry changes.
Bounds supplied for an edge can differ from its endpoint bounds.

The builder takes outer chunk bounds and a declared edge count. Insert each index
once, in the producer's order, then call `Build` to get an immutable chunk. It
expands the tree bounds to a cube around the supplied center; the outer chunk
bounds stay separate for query rejection. At most 65,535 edges fit in one chunk.
The producer must partition larger inputs into chunks.

Node capacity is half the edge count, rounded down, plus one. Edges prepend to
their selected chain. Only entries that can fit another child level contribute
to its split threshold. A fourth such entry splits the leaf when a node is
available. Redistribution visits the old chain forward and prepends to new
chains, which can reverse their order. Redistribution does not recursively split
crowded children; a later insertion can trigger another split. Exhausting the
node pool keeps subsequent records in existing chains.

The query rejects squared diagonals at or below 1/65536. Chunk and edge bounds
overlap inclusively. Child bounds overlap through the middle 20 percent of each
axis. Split pruning uses strict comparisons, so a touching query remains eligible.
For each node, leaf heads are gathered in ascending octant order and consumed in
reverse, with the node's own chain last. Chain links run forward. These leaf
chains precede pending internal nodes, which are consumed last-in, first-out.
Chunks retain registration order, and the 40-record limit applies across chunks.

Optional vertical exclusion skips records with bit 31 set. Copying a record
replaces the output slot's endpoints, metadata and vertical bit, while preserving
that slot's lower 31 flag bits. Records beyond the returned count are untouched.
Consumers must use the returned count; a rejected query does not clear the array.

The pending-node workspace holds 211 entries without per-query allocation. Inputs
must be acyclic trees and chains with valid indices. Construction checks index
ranges; traversal has bounds to stop malformed cycles and capacity overflow.

Verification: 26 native builder comparisons match cubic bounds, capacity, free
node state, allocated nodes, split counters, linked order and split eligibility.
Ten builder regressions cover split timing, pool exhaustion, insertion order,
separate chunk/tree bounds and invalid input. These checks do not prove the full
live builder-to-gameplay pipeline.

The 69 native query comparisons cover root chains, internal children, three-
level trees, multiple chunks, touching/pruned bounds, filtering, capacity and
nonzero reused output flags. Twelve regressions run in the Core suite. These
checks do not establish every tree depth, moving-provider behavior, automatic
mesh-edge generation or playable-map acceptance.

## Live marked rails

`GrindRail` collects its entire valid polyline or box top edges. `RailEdgeSource`
converts Z-up world metres to Y-up lookup metres using the proper cyclic mapping
(X,Y,Z) to (Y,Z,X). Endpoint bounds and a one-metre expanded chunk union feed the
builder. These are host-authored straight segments; authored curve bounds may
instead cover more than their endpoints. Unchanged endpoints reuse the immutable
tree. Changes to points, object transforms or box dimensions rebuild it. This
requires scanning each registered polyline during collection; large moving
polylines still need performance work. Each source currently supports 65,535 edges.

`GrindRails` queries chunks in registration order, including vertical records,
then applies the overlap filter using the board's position as reference. Filtering
compacts whole records, preserving metadata and flags. Endpoints are converted
back to world metres before reaching contacts. The board accepts forty candidates;
the previous eight-segment source and sixteen-candidate board limits are removed.

The lookup search region is still the host's estimated box extending 60 inches
around the board. Predicted asymmetric query bounds and the recovered attachment
candidate producer are not integrated. The marked-rail validity cutoff and
contact capture/friction remain estimates. This path does not automatically
classify mesh edges or implement dynamic edge providers.

All 76 combined lookup comparisons pass in the native engine. Seven new Core
regressions cover coordinate conversion, suppression, metadata/flags, capacity,
empty queries and cache invalidation. A live twelve-segment component reaches the
board's forty-slot buffer, both before and after moving it. Existing native 50-50
and boardslide acceptance gates pass. These checks do not establish all-map or
full grind-controller parity.

## Predicted contact samples

`GrindEdgeContactSampler` accepts a quadratic trajectory and one edge in Y-up
metres. A supplied radius and contact offset move the edge's projected-up plane.
The sampler solves the trajectory/plane quadratic and projects the selected
crossing back onto the edge. The normal is derived from the original edge vector;
a capped copy controls direction calculations and endpoint extension. Extension
therefore stops growing with arbitrarily long edges.

The single-root path requires positive time. The two-root path chooses the later
root without a positive-time check. There is no linear fallback for zero normal
acceleration. The projected point must lie strictly inside the extended segment;
touching its boundary is rejected. A successful sample records the edge point,
trajectory point, direction, projected velocity direction, perpendicular distance,
time, folded approach angle and truncated sample index. The producer-owned edge
index is preserved. Failure leaves the entire prior sample unchanged.

Lengths, clearance, sample rate and angle parameters are supplied by the caller.
The sampler reuses the established polynomial angle and explicit-rounding math.
It allocates no per-sample buffers. All 93 native comparisons match fifteen float
fields, return status, sample/edge indices and logical validity. Twelve regressions
cover geometry, root handling, boundary exclusion, output ownership and clearance.

This stage is not yet called by the live marked-rail adapter. Sampling and ranking
are now joined below. Collision classification, candidate correction and activation
remain before it can replace the remaining estimated grind contacts.

## Contact sample selection

`GrindEdgeSampleQueue` now provides the ranking stage for supplied samples. It
copies up to forty inputs, scans them in retained order, removes only the chosen
sample and preserves survivor order. An eight-point `GrindEdgeSelectionCurve`
provides comparison margins from height differences. Initial separation, height
and angle values are caller-supplied; the near/far separation threshold is passed
per scan.

Samples at or beyond the threshold compete by strictly smaller separation.
Samples below it compete when the old angle minus their angle strictly exceeds
the curve value at their height minus the last close winner's height. A close
winner replaces the retained height and angle and sets comparison separation to
-1. Far winners change only the separation comparison. The rule is order-dependent;
negative curve margins can permit replacement even when the angle is unchanged.
It is not a nearest-edge sort or a stable sort by angle.

Selection success is separate from sample validity: the selected sample's flag
is copied unchanged. An empty queue preserves all output. A nonempty scan with
no winner clears only output validity and retains every pending sample. Successful
removal keeps the relative order of all other samples. Scans allocate no buffers.

All 375 native calls across 51 sequences match chosen outputs and remaining
sample order, including the preceding sampler's recorded contacts. Eleven new
regressions cover ties, strict thresholds, curve ownership, replacement, empty
and unsuccessful scans, validity and capacity. The queue accepts supplied samples
or constructs them directly from edges as described below. Classification,
candidate correction and activation remain unfinished.

## Population and rebuilding

`GrindEdgeSampleQueue.Populate` replaces the pending queue from a declared prefix
of up to forty edge records. It samples each segment in input order and retains
only successful contacts. Each contact keeps its source array index, so a rejected
edge does not shift the identity of later contacts. Record metadata and flags are
not sampling gates. `SourceEdgeCount` records the supplied prefix length separately
from `RemainingCount`, which counts successful contacts still awaiting selection.

Rebuilding discards every old pending contact, including when the new input is
empty or entirely rejected. It does not clear the caller's last selected output;
subsequent empty selection preserves it. The existing forty-slot buffer is reused,
and population allocates no per-edge storage. Invalid input counts throw before
changing either count or pending samples.

Thirty native sequences cover thirty-six builds and 110 subsequent selection calls,
matching 6,102 logical sample field checks, source counts and pending order. Cases
include partially consumed and fully drained rebuilds, full capacity, rejected
edges, trajectory boundaries and unsuccessful selection with retained candidates.
Nine new regressions cover source indices, rebuilds, empty results, prefixes,
invalid counts and capacity. These are internal pipeline checks; live collision
classification and arbitrary-map grindability remain unverified.

## Surface probe geometry

`GrindSurfaceProbePlan` prepares the collision queries used to inspect an edge's
cross-section. Inputs are a straight edge, reference position, far-probe reach,
an optional downward origin and caller-supplied dimensions. All vectors remain
in the solver's Y-up metre frame. A reusable seven-slot buffer avoids allocation
on each build; consumers use `Count` and `GetProbe`, so stale slots are inaccessible.

Direction comes from the edge vector. Up is the edge-relative projection of world
up, computed by two cross products before normalization. A degeneracy gate uses
that unnormalized up-vector length, so it depends on edge length as well as tilt.
Equality at the gate is accepted. Below the gate, no probes are exposed and the
center remains the supplied reference. Otherwise the reference is projected onto
the infinite edge line without clamping to endpoints.

The first two probes cross the surface at opposite near-side offsets. The next
two use the supplied far reach and scaled vertical extent. Probe five crosses
the center. Probe six sweeps sideways above the edge, using its own radius. The
optional seventh probe runs straight down in world Y from its supplied origin,
even when the edge slopes. Reversing endpoints reverses the side ordering.

All 47 native comparisons match 2,243 float-word checks: frame vectors and 260
active probe start/end positions and radii. Ten regression cases cover ordering,
projection, reversal, inclines, optional/rejected rebuilds, gate equality, zero
length and zero reach. This verifies geometry preparation only. Engine collision
dispatch, engine material mapping and full activation remain unfinished; these
probes are not yet wired into the live contacts. Supplied-hit classification is
implemented below.

## Supplied-hit classification

`GrindSurfaceClassifier` consumes a `GrindSurfaceSection` and seven ordered
`GrindSurfaceHit` slots. Missing collisions use `HasHit=false`. Both near-side
hits, or an overhead hit, produce a blocked cross-section. A single near-side hit
produces a ledge when its corresponding far-side fraction is strictly below the
supplied threshold; otherwise it produces a wide rail. Neither near-side hit
produces a thin rail. These categories precede gameplay-specific acceptance.

The classifier retains far hit positions or falls back to the far probe endpoints.
It derives a high-side vector from world up and edge direction, reversing it for
an unblocked right-only hit. Curb detection requires a ledge, both far hits,
strict normal alignment and strict high/low height ranges. Material selection
prioritizes the near pair, then the far pair, then the center hit; ties within a
pair choose the second hit. Thin rails use center-hit audio with a fixed physical
material, and curbs have separate material values. Packed hit materials still
need an engine adapter.

Classification replaces its invalid, curb, stair and optional-query bits while
retaining unrelated flags and the previous blocked bit. Stair detection examines
only the near pair. Optional normal testing subtracts the original direction
scaled by its normalized projection; it does not substitute a normalized vector
for that subtraction. An absent downward hit or a hit at least one metre below
the center sets the drop bit.

Contact-angle limits use the two far points, separate length/reciprocal operations,
polynomial arc cosine and supplied angular clearance. A point coincident with the
center produces a zero angle through ordered vector-clamp semantics, preserving
finite limits. A generic NaN-propagating clamp would change that result.

All 229 native comparisons match 4,580 float words, categories, audio/physical
materials and flags. They include every combination of hit presence, strict
thresholds, material ties, retained flags, nonunit directions, inclined frames
and coincident points. Twenty-eight new regressions cover these rules. This stage
assumes a completed valid investigation: a rejected probe plan must not be treated
as seven misses. Inactive results and normal orientation are now implemented
below. Actual world dispatch, contact-direction handoff, correction and
live activation remain to be integrated.

## Normal orientation and inactive results

`GrindSurfaceNormal` accepts the classified up vector, edge direction and two
angle limits. An increasing interval that strictly straddles a half-turn returns
the supplied up vector unchanged. Reversed or equal limits use their arithmetic
midpoint. Other increasing intervals choose the limit nearest a half-turn, with
the second winning equal distance. Rotation applies a polynomial half-angle
quaternion to the negated up vector, preserving operation order and the supplied
axis length. Neither axis nor output is normalized.

`GrindSurfaceClassifier.Inactive` produces the result for an investigation that
was not dispatched. It places fallback points on either side of the supplied
center along X using the supplied reach, assigns Z to the three frame vectors,
sets zero angle limits, blocked kind, default materials and the invalid flag.
Previous output state is discarded. This result is distinct from a completed
investigation whose seven probes all missed.

Native comparisons cover 270 normal selections and six inactive results, including
all 229 preceding classifier outputs, strict interval boundaries, reversed/equal
limits, nonunit and zero axes, spatial inputs and signed reach. All 930 float-word
checks plus inactive category/material/flag fields match. Twelve new regressions
cover these rules. The methods remain internal building blocks; they do not yet
replace the live grind scheduler. Direction-limit logic is described below.


## Contact direction limits

`GrindSurfaceDirections` consumes the query-success flag, classified surface,
reference/point pair and previous contact. It returns `GrindSurfaceContact` with
normal, kind, fallback status, side, two permitted directions and high-side.
Failed queries preserve every previous vector while setting blocked/fallback;
successful queries with invalid classification replace the frame with explicit
axis defaults. Valid contacts clear fallback and retain the classified kind.

Thin rails choose their rotation side from the reference relative to the point.
Other categories choose it from the classified high-side; both tests are strictly
positive, so zero takes the negative branch. Wide-rail sides use the cross product
of supplied up and direction; ledge/blocked sides use the selected normal instead.
The two caller-supplied angles rotate the permitted directions with the shared
polynomial quaternion helper. Neither axes nor results are normalized.

All 274 native cases match 4,110 float words plus category/fallback fields,
including previous classifier outputs, inactive results and nonunit geometry.
Fourteen new regressions cover the handoff rules. The methods are internal and
still need material mapping and gameplay scheduling. The connected probe path
below now supplies classifications from world collision; final contact-direction
application is not yet connected to gameplay.

## Surface investigation through map geometry

`GrindSurfaceInvestigation.TryEvaluate` now joins the probe planner, world mesh
query and hit classifier. It submits six ordered probes, or seven when the
downward probe is enabled, using the supplied filter ID, zero exclusion and
surface masks, and both facing flags. The sixth probe retains its configured
rounded radius. Setting both flags does not make the triangle primitive
universally two-sided. The classifier receives hit presence from accepted
triangle counts and the selected position, normal, fraction and packed material.
Missing optional results are cleared between calls.

A degenerate edge produces the inactive classification and returns false;
an investigation with valid geometry but no hits still returns true. Previous
classification flags are supplied explicitly, preserving the classifier's
retained-bit rules. The reusable investigator owns its probe and hit buffers;
ordinary queries allocate no new arrays.

`MapCollisionSource.TryInvestigate` feeds its cached instances into this path,
updating body frames once per investigation. Inputs and output vectors use Z-up
metres at the adapter boundary; probes and classification use the internal Y-up
frame. Filter IDs and explicit packed material assignments come from the map
owner. This method does not replace the gameplay scheduler or infer map edges.

All 33 connected native comparisons match 660 classification float words plus
category, material, flags, active state and probe count. The same 33 cases through
actual engine meshes and the map adapter also match, with zero numeric difference
in this set. Cases cover thin/wide rails, ledges, curbs, stairs, empty results,
filters, retained flags, reversed/degenerate edges, the optional downward probe,
and rounded overhead obstruction. Nine regression cases include reuse after
seven-probe, empty-world and degenerate-edge calls. Dynamic gameplay scheduling,
automatic surface ingestion, arbitrary maps and live grind activation remain
unfinished; these fixtures do not establish full gameplay parity.

## Candidate contact frames

`GrindSurfaceContactQuery` connects the investigation to normal and approach-limit
selection. This caller centers probes on the candidate point, uses a separate
approach reference to choose the side, requests six probes without the optional
downward probe, and starts with zero classification flags. A geometrically
invalid investigation preserves the previous contact vectors while marking the
result blocked/fallback. A completed investigation replaces the frame, including
when its classified kind is blocked; that is distinct from query failure.

`MapCollisionSource.EvaluateContact` updates cached body frames once and handles
all input/output vector conversion, including the previous contact. Callers use
Z-up metres and receive normals, side, high-side and the two permitted directions
in that same frame. Geometry refresh, surface assignments and filter ownership
remain the map owner's responsibility.

All 56 connected caller cases match 840 Core float words plus category, fallback
and probe count. The same cases through real engine meshes have zero numeric
difference in this set. They include both approach sides and the zero boundary,
degenerate edges, missing/filtered hits, rounded obstructions and independently
positioned candidate/reference points. A separate native check preserves five
distinct previous vectors through failure and coordinate conversion. Eight new
regressions cover point selection, approach side, optional-probe reuse and
failure/success handoffs. Candidate eligibility, live scheduling and grind
activation/attachment still need integration; this does not replace the current
gameplay grind behavior yet.

## Contact velocity correction

`GrindContactCorrection` converts a selected contact displacement into an added
velocity using its integer sample index and a caller-supplied sample interval.
Nonpositive indices preserve the incoming velocity. The correction is limited
by its XYZ speed using a strict squared-length comparison; the resulting scale
applies to all four vector lanes. A negative limit remains signed when limiting
is necessary. The fourth lane does not contribute to the length test.

Scalar duration and limit calculations preserve their rounded intermediates;
the vector calculation uses explicit fused operations and two reciprocal/root
refinements. All 87 native comparisons match 348 float words. Cases cover sample
rounding, threshold neighbors, positive/zero/negative limits, independent fourth
lanes and varied magnitudes. Fourteen new regression cases cover the time basis,
signed limit, unchanged early return, fourth lane and reusable instance.

This helper now participates in the candidate acceptance loop below. Its
comparison fixtures do not establish live activation or visual smoothness.
Gameplay input producers and activation scheduling still need integration.

## Accepted contact normals

`GrindCandidateNormal` computes the normal stored with an accepted contact and
the edge's upward direction in the query's Y-up frame. Transverse velocity biases
the desired normal. The angular limit rotates from the supplied contact normal;
a small rotation axis keeps the desired vector. The last projection removes
only a positive component along the edge, using its supplied direction after
testing against a normalized direction. Zero-length results remain zero.

`EdgeUp` computes an upward perpendicular from the edge direction, using world
up for degenerate edges. Both operations reuse the existing explicit polynomial,
rotation and normalization math. The contact point and provider are not inputs
to these two calculations.

All 133 paired native comparisons match 798 spatial float words, including
nonunit directions, signed tilt limits, zero vectors, near-vertical geometry and
threshold-adjacent inputs. Fifteen regressions cover transverse motion, signed
rotation, asymmetric projection, fallback direction and solver reuse. One sign
selection instruction remains unexercised in the reference run; the source
comparison is retained in the implementation. The numerical candidate loop is
connected below; live activation and visual continuity are still unverified.

## Candidate acceptance loop

`GrindCandidateAcceptance` rebuilds the sample queue from supplied edges and a
quadratic trajectory, then consumes candidates in ranked order. Height and edge
direction gates precede endpoint updates. Vertical approach rejection precedes
map investigation; a blocked contact preserves the previous ledge and side
outputs. Other contacts replace those outputs before horizontal, separation and
correction-angle gates. Rejection continues to the next ranked candidate.

Ledge separation scales depend on correction direction and support side. Long
corrections include both supplied deck extents. Accepted candidates update the
velocity, quantized sample time, normal and edge-up direction and set the active
flag. Failure preserves that flag and any partial updates already made; callers
must not treat a false return as a reset. The loop reuses its buffers and math
helpers without per-call allocations.

`MapCollisionSource.TryAccept` converts supplied edges, input vectors and the
entire retained state between Z-up metres and the internal Y-up frame. It updates
cached body transforms once before the candidate loop. Map topology refresh,
packed surface assignments and ownership filters remain explicit.

All 44 native comparisons match 1,496 float words plus acceptance, sample index,
contact category, retained flags and queue/query counts. The same 44 fixtures
through actual engine meshes match with zero numeric difference. Three further
checks cover distinct-vector conversion in both directions and unchanged state
after an empty map-adapter call. Twelve Core regressions cover retained outputs,
strict threshold boundaries, rejection, retry, rebuild and ledge acceptance.

These fixtures execute the reference numerical path with allocation and async
service boundaries supplied by the harness. They do not verify concurrent
dispatch, live input production or gameplay cadence. The current riding path
still uses its existing grind behavior; automatic edges, live activation and
full-map acceptance remain unfinished.

## Candidate production

`GrindCandidateProducer` now connects static edge lookup to the numerical
acceptance loop. It predicts position from the supplied time and quadratic
trajectory, searches a box extending two metres horizontally, half a metre below
and four metres above that point, and suppresses overlapping edges using the
separate approach reference. Empty lookup skips acceptance entirely, preserving
its pending samples and prior candidate state.

The producer distinguishes found edges, retained acceptance and returned score.
It clears found-edge status on each lookup. Success sets acceptance and returns
the supplied score, including zero or negative scores; failure returns zero
without clearing prior acceptance. A caller must not infer acceptance from score
alone, or confuse either flag with final gameplay activation.

`MapCollisionSource.ProduceCandidate` supplies cached map geometry and converts
world-space input and candidate state. The supplied static edge chunks already
use the query's Y-up frame, as produced by the existing rail edge source. Mesh
geometry and edge-registry geometry are separate inputs; this does not generate
grind edges automatically from map triangles.

All 56 connected native cases match 2,296 float words, including prediction bounds
and score, plus output flags, sample state and query counts. The same cases through
actual engine meshes have zero numeric difference. Seven new regressions cover
prediction, signed/zero scores, found-but-rejected contacts, retained acceptance
and skipping sample rebuild after an empty lookup. The reference fixture runs
the original static lookup and numerical acceptance together, with the documented
allocation/async service boundaries. A debugger continuation also skips the
lookup call's no-op return slot; it does not replace computation.

Dynamic edge providers, selection among scored candidates, capture eligibility
and live scheduling still need integration. Current gameplay still uses the
existing grind path, and full-map/animation acceptance remains unfinished.

## Selected-contact capture gate

`GrindCaptureEligibility` preserves the distinction between its capture output
and its boolean return value. Suppression returns false without changing capture.
Otherwise capture becomes true only when the producer accepted a candidate and
the selected index is zero. The return value requests another eligible selection
pass only when the update count is exactly one, the signed candidate count is
greater than one, capture is false, and the selected/reference direction angle
is at least the supplied minimum. Equality passes. A false return can still
change capture; callers must always retain the updated output.

The gate reuses the existing polynomial angle calculation. Directions below its
length threshold produce zero angle. Update count is unsigned 16-bit; candidate
count and selected index are signed 16-bit values. The selected direction comes
from the chosen candidate frame, rather than the accepted contact normal alone.

All 107 complete native comparisons match both boolean outputs; 104 unsuppressed
cases also match the angle's float bits. The original comparison runs the gate
and all its computational dependencies without service substitutions. Nineteen
new regressions cover suppression, capture updates on failure, index selection,
signed counts, exact update count and angle boundaries. Final scoring/selection
and repeated-query scheduling remain unfinished; this gate is not yet connected
to live riding.

## Selected trajectory preparation

`GrindTrajectoryPreparation` copies the selected position, velocity, acceleration
and auxiliary vector. Nonpositive sample indices preserve the complete state.
For positive indices, position uses the rounded subtract-then-add anchor offset;
replacing that arithmetic with a direct assignment changes large-coordinate results.
Acceleration and auxiliary data stay unchanged.

The direction's Y component selects an eight-knot gain. Absolute projected contact
distance uses only XYZ, while displacement and velocity corrections retain all four
lanes. The existing contact correction converts displacement using sample time and
applies the signed speed limit. Its spatial length controls scaling of every lane.
Ordered curve endpoint comparisons are separate from the sample queue's existing
endpoint behavior, preserving both callers' unordered-input paths.

All 85 original/native cases match 1,360 float words. They exercise 197 of 201
instructions; the nonpositive interpolation-width fallback remains unexercised.
Fifteen regressions cover copied state, signed limits, fourth lanes, projection,
curve selection, endpoint behavior and rounded anchoring. The unordered-endpoint
regression follows the recovered branch semantics; nonfinite runtime comparisons
are not included in the 85-case claim. This helper is not yet connected to live
riding. Scoring, selection and query scheduling still need completion.

## Candidate score bias

`GrindCandidateBias` updates only the supplied signed count of candidates. It
leaves nonpositive counts unchanged. Each candidate must meet the inclusive
minimum sample index and have all four time-vector lanes nonnegative. Time X
drives fused height prediction from position, velocity and half acceleration.

An eligible candidate with zero stored bias receives the supplied height bias
only when its predicted Y is strictly above the threshold. Existing nonzero bias
stays intact regardless of height. The resulting bias is added to the score on
every eligible call, so repeated passes accumulate retained bias. Rejected
candidates preserve both values. Inputs and entries beyond count remain unchanged.

All 66 original/native sequences match 99 calls and 1,386 float words, including
repeat calls and all seven score/bias slots. Original comparisons run actual
integer parameter lookup, missing-field fallback and a private uncontended lock.
The host receives resolved tuning values; it does not recreate binary lookup
storage. The caller covers 139 of 143 instructions; four loop-setup instructions
are unreachable after the earlier nonpositive-count gate. Twenty regressions
cover count/sample/time gates, strict height limits, signed retained bias,
accumulation and prediction. Nonfinite time behavior has a branch-derived test
but is outside the finite runtime fixture claim.

The first comparison exposed a runtime lookup-layout mistake in the private
fixture. Correcting the fixture resolved the mismatch without changing the host
gate or weakening tests. Complete candidate scoring, rejection, selection and
query cadence remain unfinished, and this pass is not yet connected to live riding.

## Fallback rejection and map correction

`GrindCandidateRejection` first checks the candidate direction's absolute Y.
Passing candidates issue a downward mesh query from the offset contact point.
An invalid lane in the four-component time vector substitutes a zero point for
the probe origin. A miss or a failed normal-dot gate returns zero without changing
trajectory or retained height. Query filters and both surface directions are
passed to the existing mesh query implementation.

A hit supplies absolute vertical distance. Preliminary evaluation returns a
distance-scaled score minus the penalty and leaves correction state unchanged.
The full pass checks the trajectory's vertical/horizontal displacement ratio and
minimum hit distance. These failure branches retain distinct penalty scores.
An eligible hit evaluates the four-point height curve, raises retained height
when prediction exceeds it, and applies sample-time velocity correction. A
nonpositive sample can still raise retained height while preserving velocity.

The correction's fourth component is explicit input because the originating
routine reads that component from caller-owned scratch without initializing it.
Six additional runtime cases verify nonzero values, signed limits and branches
that skip correction. Acceleration, position and opaque auxiliary trajectory data
stay unchanged. The host does not infer a physical meaning for the scratch lane.

`MapCollisionSource.ScoreRejection` supplies cached geometry, updates body frames
and converts world-space metres to the query frame and back. Spatial conversion
preserves fourth components and opaque auxiliary data; the time vector is not
rotated. Retained height remains a vertical scalar.

All 85 original/native cases match 2,016 float words and 81 queries. The same cases
through actual engine mesh colliders have zero numeric difference, with three
coordinate checks. There are 24 new regressions. The reference run executes
submission, triangle collision, completed-result waiting/retrieval, rejection
and correction. Manager/filter acquisition, batch allocation, asynchronous
scheduling and release are explicit service boundaries, not verified lifecycle
behavior. Of 424 caller instructions, 410 are exercised; ten are bypassed service
setup/call instructions and four form an unexercised curve-width fallback.

The normal-dot tuning value is a supplied caller setting in these fixtures; its
live configuration binding remains unfinished. Complete candidate scoring,
selection, repeated-query scheduling and live riding integration remain open.

## Edge-distance fallback

`GrindCandidateFallback` scores distance to the retained edge registry when direct
candidate acceptance is unavailable. Missing edges evaluate a supplied default
distance. A found but empty registry instead uses the square root of the supplied
initial squared minimum. The active count prevents stale entries from contributing.

All four prediction-time lanes must be nonnegative to use the candidate point;
otherwise the distance reference is the origin. Each edge direction is normalized
only when its computed length strictly exceeds the configured threshold. Distance
is the minimum of the infinite-line projection and both endpoint distances. The
projection is deliberately not clamped to the segment, including beyond its ends.
Short and zero-length edges still contribute endpoint distances. The initial
squared minimum caps the distance before the existing ordered eight-point curve
converts it to a score. Point fourth components do not affect spatial distance.

All 85 original/native cases match exact score bits, with no provider or candidate
record mutation in the reference and no registry mutation in the host. The complete
routine runs without service substitutions; these cases exercise 167 of 174
instructions. The first-output branch and nonpositive curve-width branch are not
covered by the original fixtures. A separate regression covers the first-output
branch using a shifted curve. Nonfinite runtime inputs remain unverified.

This pass has no engine dependencies or per-call allocations. It now participates
in the combined scorer below. Final selection and live activation remain open.

## Combined candidate scoring

`GrindCandidateScoring` processes up to seven candidates in order. It clears each
active score, correction bias and rejection flag before checking all four time
lanes and the signed sample minimum. A valid time clears `AllTimesInvalid` even
when the sample gate rejects the candidate. Inactive slots and rejected candidates'
retained frames stay unchanged. Nonpositive signed counts leave all slots intact.

The first candidate on update one can run the connected producer, subject to
caller flags. Later candidates reuse its edge registry. Missing or explicitly
overridden production scores use edge-distance fallback. Nonpositive scores run
map rejection; negative values request its preliminary path. The returned bias
is stored separately rather than added immediately. A positive bias sets the
candidate flag; a negative bias selects the supplied upward direction. Kind
bonuses apply at scores at most one, with distinct direction-copy behavior.

Frame position uses the time vector's first lane. Frame velocity uses sample
index times the configured interval. Directional weighting is computed before
contact correction, while the time curve uses the potentially updated time.
The special-state term preserves orientation gating, retained-acceptance clearing,
horizontal direction weighting and the strict near-apex timing boundary. The
final additions retain the originating operation order.

`MapCollisionSource.ScoreCandidates` supplies cached engine geometry, refreshes
body transforms, converts world-space candidates and caller vectors to query
coordinates, and converts candidate outputs back. Edge chunks and scorer frames
remain in query coordinates. Time lanes, opaque trajectory auxiliary data and
explicit scratch fourth components are preserved. Its fixed seven-slot scratch
array avoids per-call allocations.

All 80 original/native cases match 22,320 float values, checked state flags and
104 collision batches. The same comparisons through actual engine mesh colliders
pass exactly, with 640 coordinate round trips. Eighteen new regressions cover
count/time/sample gates, frame timing, kind bonuses, separate correction bias,
multiple map queries and retained slots. All 1,375 Core tests pass.

The original caller exercises 732 of 746 instructions; fourteen instructions in
nonpositive curve-width fallbacks remain unexercised. Allocation, manager/filter
acquisition and asynchronous dispatch remain explicit service boundaries. Native
comparison exposed a harness error where later collision jobs reused an exhausted
claim counter. Fresh counters fixed the fixture; host behavior was unchanged.
The two earlier retry cases affected by that harness pattern were rerun against
the original with fresh counters: final caller outputs are unchanged, and the
corrected collision-result captures are retained separately.
Raw reports also retain scratch-address differences in an uninitialized query
fourth lane and its derived hit lane. Exact relocation and interpolation checks
account for those differences; scores and candidate/frame values match bitwise.

Nonfinite runtime inputs, arbitrary fourth lanes through successful contact
production, dynamic edge providers, live ownership/tuning bindings and automatic
geometry refresh remain unverified. This scorer does not yet replace live riding:
final selection, capture/trajectory handoffs and repeated-query cadence are next.

## Query-direction fan

`GrindQueryFan` generates the velocity directions used to request candidate
trajectories. It caps the unsigned requested count at seven and always copies
the first direction, including when the count is zero. Unused output slots are
retained for subsequent calls. The caller supplies reusable storage, the angular
math profile, the spread curve and all tuning values.

The lateral and horizontal forward axes derive from the input velocity and query
up axis. The speed used for spread can come from a separate velocity; the original
velocity still limits every generated direction's magnitude. Two angular spreads
form an elliptical set around that velocity, preserving polynomial evaluation,
operation order and the accumulated phase step. Zero and vertical directions
retain the original degeneracy behavior. Spatial length excludes the fourth lane,
while vector operations preserve its effect on output data.

All 88 original executions match the instruction runner, including every retained
frame column and untouched slot. Native execution matches all 2,552 direction and
height float values plus counts. Eleven regressions cover count capping, copying
with zero requested queries, zero/vertical motion, both sides of the fan, alternate
spread speed, speed ceilings, storage bounds and reuse. All 1,386 Core tests pass.
The comparison exercises 543 of 547 instructions; four nonpositive curve-width
fallback instructions remain unexercised. Nonfinite inputs are not accepted as
verified cases.

The request builder below now uses this helper. Live input production, final
selection orchestration and repeated-query scheduling remain unfinished. These
direction comparisons do not establish smooth gameplay or all-map acceptance.

## Trajectory request construction

`GrindQueryRequestBuilder` connects the fan to `TrajectoryQueryService`. Two
eight-point curves select the origin offset from the supplied up vector and
vertical motion. The projected clearance subtracts the query radius, clamps
against the supplied offset cap and blends toward that cap. An override replaces
the origin and retained reference, while local offsets still use the original
reference points. Four-lane state vectors and ordered frame transforms survive.

Prediction duration uses the first direction's vertical velocity, the configured
height/gravity expression and its upper duration limit. Ordinary submissions
advance each origin by its direction times lead time and use the supplied service
acceleration. Retained trajectory reuse requires a strict distance gate, a
non-opposing direction and a bounded speed ratio; near-zero current speed skips
the ratio test. Accepted reuse submits one retained position/velocity/acceleration
tuple with both step controls set to one. It can replace an empty fan.

`GrindQueryRequestState` owns seven reusable direction and target slots. The
builder resets selection flags and height, retains unused directions and special
history, and sets readiness even when no requests were submitted. Readiness is
distinct from a target's completion and from a successful collision.

All 60 native cases match 353 submitted requests and 8,009 float words exactly,
including queue counts, readiness and retained state. Inputs cover curve ranges,
origin overrides, transformed offsets, missing optional values, count capping,
both submission queues and reuse boundaries. Ten new regressions pass; all
1,429 tests pass. The caller comparison covers 396/406 instructions; curve-width
fallbacks and one below-first-knot path remain unexercised. Duration, reuse and
retained submission each have full instruction coverage in these fixtures.

These cases validate construction and submission, not a complete riding update.
Live input production, final contact selection and repeated-query scheduling
remain unfinished. Nonfinite values and queue overflow are not equivalence
claims. A missing radius can produce a queued zero-radius request; executing it
still encounters the trajectory worker's explicit positive-radius guard.

## Final candidate selection

`GrindCandidateSelection` connects scoring, clearing pending edge samples, bias,
selection, trajectory preparation, capture eligibility and repeat submission.
It operates on supplied candidate records and the matching reusable query state.
Its sample queue must be the same queue used by the scoring producer. A missing
ready flag or incomplete first target preserves selection and clears only the
repeated-update flag. An eligible call increments the unsigned update counter,
clears readiness and scores before draining pending samples and applying bias.

Equal finite scores keep the earlier slot; nonpositive counts still select slot
zero. The selected candidate and all four frame columns are retained separately
from newly generated queries. An invalid selected time retains the previous
prepared trajectory and kind, stores the fallback direction and clears active
and repeated state. A valid time prepares the trajectory, evaluates capture and
optionally rebuilds queries from the retained first frame column. Update two is
assigned after regeneration returns. The builder can return ready with no queued
requests; readiness, completion and successful contact remain separate states.

All 22 native comparisons match 8,037 float words, state fields, 34 collision
batches and regenerated requests. Twelve new regressions cover waiting, update
wrapping, tie order, height bias, sample cleanup, invalid time, nonpositive counts,
suppression and repeats. All 1,441 tests pass with clean native compilation.

Raw padding is not part of the host model, and boolean flags normalize nonzero
byte values. The following result-transfer work extends the original selection
boundary through subsequent updates. Existing queue capacity guards and
synchronous dispatch limitations still apply.

## Consecutive candidate updates

`GrindCandidateSelection.SubmitRequests` builds requests and starts tracking the
affected target slots. `Evaluate` transfers changed contacts before testing
readiness. Repeat submissions use the same path. Seeded candidate arrays remain
caller-owned until submission starts tracking their slots.

`GrindCandidateQueryTransfer` distinguishes new requests from new successful
contacts. Submission resets contact fields and copies position, velocity,
acceleration, duration, radius, step controls and the request flag. Successful
delivery copies contact position, face and surface normals, sample/time, contact
matrix, packed material and geometry identity. Material bits determine candidate
kind. Request-vector fourth lanes survive transfer. Scoring corrections remain
intact until another request replaces them; refreshing unchanged data or marking
a miss complete cannot overwrite them. Tracked late results still reach their
slot when the current active count has decreased.

Fifteen three-update sequences produce 90 checked selection/delivery snapshots.
Both Core geometry and real engine mesh colliders match all 71,163 compared float
words per route, plus candidate metadata, queue counts and retained selection.
Cases cover both queues, empty and retained repeats, hits, misses, owner filters,
cancellation, flat geometry, ledges and obstruction retries. Geometry identities
are explicitly mapped between the single source instance and the host instance.
Fifteen additional engine checks verify contact-frame axis conversion and exact
candidate round trips. Eight new regressions bring the suite to 1,449 passes.

`MapCollisionSource.SelectCandidate` and `ProcessTrajectories` use query-space
metres and refresh cached body transforms before selection or delivery. Caller
code still owns initial inputs, the edge registry, query cadence and activation.
Live riding, automatic edge extraction, general nonzero contact fourth lanes,
asynchronous behavior and full gameplay parity remain unverified.

## Query input preparation

`GrindQueryInputPreparation.TryBegin` accepts a new input only after the pending
query flag clears. A completed target alone does not clear that guard. Starting
a cycle resets the update count, selected index, three candidate directions and
active/accepted flags, while retaining selection and capture history. The stored
input supplies subsequent selection anchors and repeated requests.

`GrindQueryMotionPreparation` conditions velocity using the supplied up vector,
orientation and movement state. An upright input keeps its velocity. A sufficiently
upward direction on tilted support selects orientation-limited correction and
blending while retaining speed. Otherwise secondary movement or the designated
movement state bypasses projection scaling. Other inputs scale the component
parallel to the supplied up vector. The special-motion flag remains distinct
from the query builder's retained-trajectory flag.

All 55 native cases match 297 submitted requests and 7,711 float words, including
pending guards, threshold boundaries, missing optional values, zero velocities,
fourth lanes and randomized inputs. Ten new regressions bring the suite to 1,459
passes. The comparisons cover all instructions in initialization and conditioning;
the fixture explicitly initializes unused temporary vector lanes to zero.

The initialized lifecycle now has a combined comparison: nineteen scenarios run
initialization followed by three selection/delivery updates. All 133 snapshots
match 106,441 float words and retained metadata through both Core geometry and
actual engine mesh colliders. Cases include both queues, cancelled owners, misses,
zero through seven requests, retained trajectories, tilted velocity conditioning
and the pending-input guard. Two scoring investigation batches execute per route;
trajectory mesh jobs are separate from that count. Nineteen contact-frame round
trips also pass on the engine route.

The fixtures supply initial board frames, velocities, movement flags and edge
registries. They explicitly dispatch collision jobs between selection updates;
they do not verify asynchronous scheduling or movement-state cadence in gameplay.
Live producers, attachment activation and animation acceptance remain open.

## Query input assembly

`GrindQueryInputAssembler.Assemble` fills board-derived fields from
`GrindQueryBoardState` while preserving caller flags, position overrides and
external query context. It copies both velocities, reference points, the local
frame and lead time; motion flag bit 13 selects seven directions instead of one.
The second basis vector of the supplied world frame becomes the query up vector.
The basis vectors occupy matrix rows in the Core representation. Inputs use the
query pipeline's Y-up metre coordinates, not engine-space positions.

Twenty-three combined scenarios run assembly before initialization and three
selection/delivery updates. The represented input fields match 1,081 float words
plus flags and counts. All 161 subsequent snapshots match 128,825 float words
and metadata on both Core geometry and actual engine mesh routes. Twenty-three
contact-frame round trips also pass. Eight new regressions cover bit selection,
fourth lanes, frame translation and preservation of caller-owned context; the
suite passes all 1,467 tests.

Board states are supplied by the fixture. The shared assembler does not choose
movement-specific overrides or schedule selection and attachment. Those original
caller responsibilities still need live integration. Unconsumed world-frame and
auxiliary fields are outside the host request representation; these comparisons
do not claim a complete binary object model or full-game parity.
