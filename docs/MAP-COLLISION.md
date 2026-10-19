# Map collision queries

Live board contacts currently use engine collision queries. Maps must provide
collision geometry; rendering a mesh alone does not make it skateable. Automatic
grind-edge discovery and the recovered surface pipeline's world dispatch remain
unfinished.

`Core.Collision.TriangleRayQuery` is an internal directed-segment query for the
geometry adapter under development. It accepts three vertices, a start point,
a full segment delta, determinant/boundary tolerances and retained contact data.
It returns a hit flag and updates position, normal, barycentric coordinates and
fraction. Geometry may use any consistent coordinate frame and length unit.

The determinant test is strictly greater than the supplied minimum, rejecting
the opposite winding. Barycentric and segment tests use determinant-scaled
tolerance. A negative tolerance admits small excursions beyond edges and segment
endpoints; accepted fractions are not clamped. Barycentric X/Y are weights for
vertices B/C, with Z zero. A successful normal uses ordered cross-product and
inverse-square-root refinement.

Early rejection preserves the previous contact. Once barycentric checks pass,
the unscaled segment numerator is written to Fraction before the interval test;
an interval failure retains that numerator and preserves every other field.
Callers must honor the returned hit flag before treating the output as a contact.

All 73 native comparisons match 730 float words and hit status, including winding,
parallel/degenerate segments, tolerance boundaries, spatial inputs and retained
output. Sixteen regression cases cover these contracts. The query is not yet
wired into live contacts. Mesh collection/indexing, material
mapping and dynamic geometry invalidation still need integration and acceptance.

`Core.Collision.TriangleSweepRoots` supplies the vertex and infinite-edge roots
needed by rounded triangle queries. A root retains its numerator and denominator
without division. Miss and not-approaching results preserve the previous pair;
an initial overlap can return the pair `(0, 1)`. The edge query accepts the supplied
edge vector and squared length without normalizing or recomputing them.

Vertex queries include tangent and segment-endpoint hits. Entering edge queries
exclude their endpoint boundary; exiting edge queries select the later root with
strict endpoint checks. Requiring a root bypasses the edge overlap shortcut.
Signed zero and operation order are retained. Callers must use the returned
status before consuming a root, and must restrict infinite-edge roots to the
appropriate triangle feature.

All 139 native root comparisons match 278 float words and status. Fifteen
regression cases cover overlap, approach rejection, retained pairs, endpoint
differences, entering/exiting roots and non-unit edges. These helpers do not yet
change live board contacts by themselves.

`Core.Collision.TriangleSweepQuery` now composes directed rays and rounded
triangle contacts. It first tests the face, then selects the closest triangle
feature and advances between edges and vertices. Radius plus margin determines
the sweep boundary; only radius offsets the final position. Rounded queries
accept both face orientations. Initial overlap reports squared penetration in
the third barycentric field and can preserve the incoming face normal when the
distance to the feature is zero.

Rounded rejection updates the normal and can retain a partially advanced
fraction, while leaving position and barycentric data unchanged. Stationary
segments can return face results even when distant; world dispatch must apply
its own segment gate. The feature iteration limit preserves partially written
output. Callers must honor the result before consuming a contact.

Scalar arithmetic retains single-precision rounding and subnormal intermediates
separately from the engine's vector arithmetic. This prevents very small
triangles from changing hit status when the engine flushes tiny float values.
Invalid initial normals retain positive quiet NaNs, including on rejection.

All 542 native query comparisons match 5,420 float words and status: 73 directed
rays and 469 rounded inputs, including feature transitions, margins, finite
degenerate geometry and small-scale arithmetic. This is measured case coverage,
not a claim that every possible input or live map is verified. The geometry
adapter, world traversal, material mapping and dynamic invalidation
still need integration and acceptance.

`Core.Collision.MeshTriangleQuery` accepts local triangle vertices, a packed
material, an affine transform, caller-computed local query bounds and a retained
mesh contact. It rejects triangles outside those bounds, transforms the vertices
with ordered multiply-add operations, and runs the triangle sweep. Accepted
contacts pass surface exclusion and front/back approach filters. Its output
normal is the transformed triangle's face normal, including for rounded contacts
on edges and corners. Mirroring changes the face orientation.

The four exclusion words retain their packed layout: the last word serves both
audio categories 96-127 and the physical-material mask. Fractions are clamped
before comparison. Only strictly nearer contacts replace position, normal,
transform, material and instance identity; equal or farther accepted contacts
still increment the hit count. Callers initialize the retained fraction and
count and preserve triangle order when ties matter.

All 109 native mesh cases match 2,507 float words plus material, identity and
count. These cover translation, rotation, mirroring, nonuniform scale, shear,
surface exclusions, approach flags, local bounds and overlapping triangles.
Nineteen regression cases cover filtering and selection contracts. The stage
has also received 50 triangles collected from five solid shapes in a paused
lab scene: 22 sampled ray contacts agree with engine queries within 0.001 inch
(maximum measured position difference 0.000123 inch).

This stage consumes individual triangles; it does not implement the world
dispatcher, mesh tree traversal, compressed storage or the segment gate. The
scene probe uses neutral materials and tests sampled rays in one scene.
Material mapping, streamed/changed collider invalidation, moving-body queries,
rounded scene sweeps and connection to wheel contacts remain unfinished.

`Core.Collision.MeshWorldQuery` implements linear instance dispatch around that
stage. It initializes a fresh result, rejects segments whose largest absolute
component does not strictly exceed the supplied movement threshold, and applies
instance filter and exclusion checks. Either filter value `-1` is a wildcard;
exclusion bits still apply. The radius-expanded world query bounds are converted
to local center/extents using the supplied inverse transform, preserving the
ordered arithmetic and testing local instance bounds inclusively.

Before querying triangles, `MeshBoundsQuery` transforms segment endpoints into
the local frame and tests padded mesh bounds. It extends nearly parallel segment
components, refines reciprocals, and requires a strictly nonempty interval.
This pruning can reject tangent overlaps that the triangle primitive accepts
alone. Geometry supplies its own mesh bounds separately from instance bounds.
Instances and triangles retain caller order, including for equal-distance ties.
The canonical miss has zero position, query-frame +Y normal, maximum fraction,
zero material/identity/count and an identity basis with a zero fourth component.
Adapters remain responsible for coordinate-frame conversion.

All 127 processed contacts from 128 native worker scenarios match 2,921 float
words plus material, identity and count. The original worker's isolated atomic
claims were checked separately; the host query API evaluates one request and
does not reproduce worker-thread scheduling. Sixteen regression cases cover the
segment boundary, wildcard filters, exclusions, local bounds, ordering and fresh
results. A paused scene probe dispatches across five shapes and 50 triangles;
all 22 sampled rays select the expected object and agree in position within
0.001 inch, with no skipped samples.

The current dispatcher scans ordered instances and triangles. World/mesh tree
acceleration, compressed mesh storage, persistent collider collection and
invalidation, material mapping and live wheel integration remain unfinished.
The scene probe bakes a static snapshot into world coordinates; it does not prove
moving-body, streamed-map, rounded-sweep or all-map acceptance.

`Engine.Collision.MapCollisionSource` now owns a persistent geometry snapshot.
`Refresh` discovers solid mesh, hull and heightfield shapes, triangulates their
body-local geometry and rebuilds cached triangles only when vertices, indices
or caller-supplied material IDs change. It detects edits even when shape identity
and bounds remain unchanged, and removes entries no longer in the world.
Unsupported analytic shapes and shapes without triangles are counted explicitly.
Sphere and capsule tessellations are not substituted for analytic collision.

`Trace` accepts and returns Z-up metres. The adapter converts local geometry,
body frames and requests to the query's Y-up frame, then converts contacts back.
Shape-local transforms and scale are already included by engine triangulation;
only the owning body transform is applied afterward. Each query updates body
transforms and rejects removed, disabled or trigger shapes without rebuilding
geometry. The selected engine shape remains attached to the result.

Geometry refresh is explicit. The engine's shape-dirty event is internal and
`Triangulate` allocates fresh arrays, so the adapter does not poll full geometry
on every query or frame. A map owner must call `Refresh` after shape additions,
mesh edits or material changes. New/reactivated shapes omitted by the previous
refresh need another refresh to enter the snapshot. Automatic refresh scheduling
and gameplay ownership are still unfinished; this is not an automatic streaming
solution. Material IDs come from a caller resolver, not a guessed conversion of
engine surface names or a claimed triangle-to-surface mapping.

Twelve isolated native checks pass for cache reuse, translation/rotation,
disabled/re-enabled shapes, same-handle mesh edits, unchanged-bounds edits,
material changes, new hulls and removal. Engine rays can still hit a shape whose
solid contacts are disabled; the adapter intentionally requires solid contact
participation. A paused lab probe uses this adapter to collect five shapes and
50 triangles, reuses unchanged geometry, and passes 22 sampled rays with matching
shape selection and position differences below 0.001 inch. No unsupported shapes
or skipped rays occur in that scene. Heightfields, analytic shapes, automatic
streaming, rounded scene sweeps and live board contact integration remain
unverified or unfinished.

`SetQueryFilter` assigns a filter ID and unsigned exclusion mask to a collected
shape. `TraceFiltered` supplies the corresponding request values to the world
query. Either filter ID `-1` is a wildcard; any shared exclusion bit still
rejects the instance. The unfiltered `Trace` entry point retains its wildcard
and zero-mask behavior. Metadata changes take effect on the next query without
retriangulation and persist across geometry refreshes while the shape remains
collected. Removed and subsequently recollected shapes start with defaults;
callers must assign their metadata again. Uncollected or invalid shapes cannot
be configured. Gameplay ownership must supply these masks; the adapter does not
infer rider or board ownership from names or tags.

Seventeen isolated native checks cover filter matching, both wildcard paths,
exclusion precedence, the unsigned high bit, selection behind excluded shapes,
metadata changes, refresh, movement, disable/removal and newly collected shapes.
These establish adapter behavior, not live rider/board exclusion assignment.

Engine surface ingestion is still unresolved for arbitrary live shapes. Model
mesh descriptions expose resolved per-triangle surfaces, but live shapes have
a write-only surface table and can override the model assignments. Native
sampling confirmed that an all-hits ray can return only the nearest triangle
within a mesh, so it cannot recover every overlapping triangle's material.
The adapter continues to require explicit packed material IDs; it does not
silently assign sampled or guessed materials to unobserved faces.

The adapter's `TryInvestigate` entry point now feeds cached geometry into the
[connected surface investigation](GRIND-QUERY.md#surface-investigation-through-map-geometry).
It updates body transforms once, converts the edge/reference/downward origin
from Z-up metres into the query frame, and converts all classification vectors
back. Thirty-three native cases through real engine meshes match the connected
reference classifications, including rounded overhead probes. Surface IDs are
explicitly supplied in these fixtures; automatic material ingestion and live
gameplay scheduling remain unfinished.

`EvaluateContact` extends the connected path through contact-normal and approach
direction selection. Candidate and approach-reference positions stay separate;
the previous contact is converted as well so failed investigations preserve its
vectors in the caller's Z-up frame. Fifty-six native mesh cases match the
reference contact frames. This entry point still needs a live candidate owner
and activation scheduling; its existence does not establish gameplay parity.

`TryAccept` extends this path through ranked candidate rejection/retry, contact
correction and output normals. It converts the complete retained state and
supplied candidate edges, and updates body frames once per loop. Forty-four
native cases through actual engine meshes match the reference numerical outputs;
three distinct-vector/empty-call checks also pass. Geometry refreshes, material
ingestion, live candidate production and riding integration remain unfinished.

`ProduceCandidate` now connects predicted static-edge lookup to this acceptance
path. Edge chunks use their internal query frame; input and retained candidate
vectors use world-space metres. Fifty-six native cases match through actual
engine meshes, including displaced prediction windows and retained failure flags.
The adapter does not create edge chunks from triangles or choose the final live
contact. Dynamic providers, material ingestion, refresh scheduling, ownership and
activation still need completion.

## Trajectory surface collection

The internal `TriangleBoundsQuery` tests a closed box against a triangle using
nine edge axes, coordinate bounds and the face plane. Touching boundaries are
included, and degenerate triangles retain point/segment overlap behavior. Ordered
products preserve the distinct rounding of the first edge-axis radius.

All 322 native comparisons match, including translated boxes, separated corners,
touching planes, degenerate triangles, varied scales and deterministic mixed
geometry. Ten additional regressions cover the geometric behavior. This primitive
now feeds the trajectory surface collector described below.

`TrajectoryWorldQuery` tests ordered curved-motion segments through the mesh
query, reconstructs hit time and sample index, and collects at most 64 nearby
faces. Surface collection uses its own instance-filter rules; contact exclusion
masks do not suppress faces from the subsequent normal calculation. The first
64 intersecting faces count toward capacity before normal selection. The query
owns reusable normal storage and allocates no arrays during evaluation.

`MapCollisionSource.EvaluateTrajectory` accepts position, velocity and acceleration
in world-space metres. It updates cached body transforms, converts the vectors to
the query frame, and converts the contact vectors and instance matrix back. Its
caller supplies the trajectory query and sampling parameters. Radius and step
controls must be positive; zero duration returns an invalid contact.

The combined path matches 108 native Core queries across 103 captured jobs,
including multi-face meshes, a 64-face capacity boundary, filter and material
masks, near-zero acceleration, initial contact, rotation and affine scale.
107 queries also pass through actual engine meshes; maximum numeric difference
is below 0.000002 and sample/material/shape selection matches. These include
translated and rotated bodies. The affine-scale case is covered in Core only;
the engine fixture does not reproduce its separate scaled instance matrix.
Twelve additional regressions cover timing, empty/reused results, filters,
surface capacity and stalled-step rejection.

Three additional captured jobs explicitly distinguish normal collection from
contact rejection: an excluded tilted instance and a masked tilted material still
affect the nearby-face normal, while an incompatible instance filter removes it.
These cases pass both native Core and engine-mesh comparisons.

This is an internal connected geometry query, not the live riding controller.
Live request scheduling, final candidate selection, automatic
materials and geometry refresh scheduling remain unfinished. Internal mesh-tree
and world-tree traversal have not been reproduced by the linear host dispatcher.

## Trajectory request delivery

`TrajectoryQueryService` retains two ordered queues. Selector 1 uses the first;
other selectors use the second. Submission clears the target's previous contact
and completion flag, retains its request and byte flag, and activates its owner.
Owner filter -1 remains unrestricted for geometry and uses cancellation slot 7.
Service submissions use empty surface masks; instance filters and exclusions
remain part of the request. Cancellation applies across both queues, and a new
submission reactivates that owner for already queued requests too.

Processing delivers valid contacts and marks misses complete without inventing
a contact. Cancelled targets stay incomplete. Queue counts clear after delivery;
owner activity remains until cancellation. Reusing a target resets it before the
next query, while repeated target references retain the latest request metadata.
The service owns reusable arrays and allocates no storage per request.

`MapCollisionSource.ProcessTrajectories` updates cached body transforms once and
processes the service against that geometry. Its requests and results use the
internal Y-up query frame in metres. This differs from `EvaluateTrajectory`,
whose individual request and result use the caller's world frame.

102 native Core target comparisons and 38 engine-mesh target comparisons across
26 map cases match all 5,460 compared float words and target metadata with zero
difference. Eleven regressions cover routing, misses, reuse, cancellation,
reactivation, shared targets, mask clearing and capacity rejection. Existing
tests remain unchanged; all 1,419 tests pass.

The service executes synchronously; asynchronous dispatch, contested locks and
live gameplay cadence are not reproduced. Owner indices outside -1..7 and full
queues are rejected before target mutation. The reference's ignored batch-full
failure is intentionally not claimed as matching this checked host boundary.
Request construction is now connected to this service; see the request builder
in [Grind queries](GRIND-QUERY.md#trajectory-request-construction). Live input
production and riding integration remain open.

## Selection after trajectory delivery

`SelectCandidate` runs the final candidate selector against the same cached mesh
instances used by `ProcessTrajectories`. Both methods take query-space data in
metres and update body transforms first. The selector tracks new requests and
transfers delivered contact metadata before its next readiness check; unchanged
or missed deliveries preserve scoring corrections. Candidate coordinate adapters
also convert the stored contact matrix alongside point and direction fields.

Fifteen consecutive-update scenarios match 90 selection/delivery snapshots
through real engine mesh colliders. All 71,163 compared float words and state
fields match, with the single source geometry identity mapped to the sole host
identity. Both queues, owner cancellation/filtering, hits, misses, flat surfaces,
ledges and obstruction retries are covered. These are internal lifecycle checks;
the live controller still needs input production, automatic edge generation,
cadence and activation. Existing geometry-refresh and shape-coverage limits apply.
