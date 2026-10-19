# Grind approach reconstruction

`GrindApproachSolver` is an internal, engine-independent stage for an already
active approach request. It accepts a board pose, linear/angular velocity, a
normal, edge endpoints and a time step in one consistent rig coordinate frame.
Its five local board points and seven candidate definitions are supplied and
copied at construction, allowing board dimensions to vary. It does not discover
map edges, activate requests, or apply a complete attachment correction yet.

Prediction updates velocity before position and rotates the board axes without
rotating its world position. Each predicted pose transforms all five board
points. The board direction projected into the edge plane supplies a signed
angle sample. Angular coefficients, units, thresholds and correction limits
are caller-supplied; there are no game-specific defaults in this module.

Each point keeps its first strict plane crossing. Touching the plane at a sample
without a sign change is excluded. Candidates have ordered priority, timing,
distance and angle gates; the last two compete by distance. The eligibility
angle uses the rounded crossing sample, while the retained angle uses the
candidate index. Correction velocity and accumulated position have separate
length limits. A failed selection clears the valid flag but preserves retained
indices and correction history. Suppression belongs to the enclosing controller described below. Edge-change
resets remain to be integrated.

State owns reusable buffers for up to twelve samples. Updates allocate no
trajectory arrays. The caller must keep input frames and vectors finite and
use a consistent coordinate convention; this rig stage does not perform engine
unit conversion. Its frame axes come from the supplied normal and edge, so
slopes are not replaced with a fixed horizontal rail.

Native comparison of the complete controller covers 147 prescribed cases:
301 float fields, four flags and three retained indices per case. Cases include inclined,
reversed, translated, vertical and nearly degenerate edges, board rotation,
motion and retained correction. All comparisons are exact for this fixture set.
Fifteen Core regressions cover integration order, custom points, crossing
boundaries, candidate modes, correction limits, retained state and input ownership.
The complete controller comparison includes disabled/suppressed cases, pending
requests, nonidentity model/world frames and displaced board pivots.

`GrindAngularCorrector` follows a successful selection. It uses supplied edge
metadata vectors and the predicted tip direction for pitch, the selected target
and response for yaw, and the current board frame for roll. Yaw caps the response
before adding and capping the old rate. Pitch adds to its old rate before a cap.
Roll replaces its old rate, then all three rates accumulate into angular position.
Invalid selection leaves all angular history and diagnostics unchanged.

`LimbSignedAngle` normalizes with one reciprocal-square-root refinement and
evaluates the supplied angle polynomial. Scalar wrapping keeps the negative
half-turn endpoint, while fractional-period wrapping keeps the positive one.
The roll vector subtracts its scalar dot product from each component. These
details and the floating-point quarter-turn fold are intentional parts of the
verified algorithm. Sixteen additional regressions cover angular limits,
orientation, boundaries, candidate-specific pitch, state retention and ownership.

`GrindAttachmentController` joins prediction, selection, angular correction and
final frame construction. Disabled requests preserve retained history. Suppressed
requests clear positional and angular offsets but keep rates, selection history
and pending attachment requests. An active update without a candidate also keeps
pending requests. A selected approach requests both frame and height retention
for fifteen updates; the separate attachment controller applies/decays them later.
Published request validity is separate from retained candidate validity.

The final frame applies rotation about the authored board axis, then edge axis,
then projected up axis, between model/world rotations. Products round before
their additional terms. Frame translations are discarded before correction
around the mutable board pivot; positional correction uses the world-to-model
rotation. Thirteen additional regressions cover complete request ownership,
suppression, update sequences, pivots, rotation order and discarded translations.

Live input production, activation transitions and
cadence, and integration with world collision geometry are unfinished. Current
gameplay still uses the existing marked-rail adapter. These comparisons do not
establish complete grinding, arbitrary-map acceptance or animation acceptance.

The [attachment sequence](BOARD-ATTACHMENT.md#ordered-request-sequence) places
this branch after hand alignment and before vertical correction and retained
application. A dispatch bypass preserves published request state; it is distinct
from a disabled grind update. The combined sequence matches 144 native updates,
including release and disabled updates after each initial request.

`GrindEdgeFilter` accepts up to forty nearby segments in the rig's Y-up coordinate
frame and returns retained input indices in order. It suppresses some nearly
parallel, overlapping segments based on perpendicular spacing, projected height
and distance from the supplied reference point. Collinear duplicates are retained;
this is not a generic mesh weld or deduplication pass. The caller keeps endpoints
and metadata, and must supply space for every possible retained index.

All76 native comparisons match reference selections, including exact boundaries,
reversed/vertical/zero-length edges, capacity limits and nearly parallel groups.
Twelve regressions cover the filter contract. Marked rails now use the combined
tree/filter path with coordinate conversion. Triangle-edge production and the
recovered attachment controller's live inputs remain separate work; the filter
does not enable arbitrary-map grinding on its own.

`GrindEdgeRecord` builds an endpoint pair from four supplied coefficient vectors
and an affine frame. The first endpoint uses the fourth vector; the second uses
the grouped sum of all four. It preserves metadata and low flag bits, replacing
the high bit according to the transformed horizontal span in the rig Y-up frame.
The classification includes zero-length edges. Nonuniform scale, reflection and
shear remain part of the supplied frame; it is not normalized into a rotation.

All68 native comparisons match endpoint bits, metadata and flags. Nine regressions
cover transform order, strict classification thresholds and coefficient grouping.
Supplying these records from a live map remains unfinished; these tests do not
establish collision-shape scale handling or automatic mesh-edge extraction.

The [predicted contact sampler](GRIND-QUERY.md#predicted-contact-samples) now
reconstructs trajectory/edge-plane crossings and bounded edge projection. All 93
native comparisons pass. Its output is an upstream sample for the separate
ranking/classification pipeline; it is not interchangeable with the approach
solver's seven board-contact definitions. That upstream pipeline remains unfinished.
