# Controller input

The internal input processor retains consecutive resolved controller readings.
It preserves independent left/right sticks, analog trigger pressure and twelve
button flags. Axes arrive in the caller's coordinate convention; this layer does
not invert, clamp or apply a deadzone.

Holding both bumpers selects the left bumper unless the caller enables retention
and the previous sample selected exactly one bumper. Context determines that
retention flag; it is not a user binding preference.

Trigger hold timers accumulate only at exactly full pressure. Face-button hold
timers reset when released. Stick-click timers measure time since each press and
release, resetting on the relevant edge. Stick-activity timers compare magnitude
strictly against a supplied threshold, without changing the stick sample.

The engine-native comparison covers 48 scenarios and 328 updates, matching 7,872
float words and 656 flag words, including activity-threshold boundaries in twelve
directions. Thirteen regressions cover bumper history, press
and release timing, partial triggers, independent face buttons, activity boundaries
and reserved state.

Off-board requests now preserve jump edges, independent full-pressure trigger
edges for dropping, throwing and retrieving the board, body-tweak activation and
unaltered right-stick axes. Drop, throw and retrieve requests can coexist; movement
state must choose which request applies. Holding a trigger does not repeat its
request. Looking omits a zero axis; body-tweak axes remain available at zero.

The native off-board comparison covers 62 scenarios and 239 updates, matching
2,151 action-presence checks and 624 emitted float words. Ten additional
regressions cover edge timing, simultaneous requests and signed axes.

Manual input keeps radial balance pressure and braking as separate requests.
Braking uses supplied threshold and scale parameters; horizontal pressure can
produce braking without a balance request. Handplant axes, either foot lift and
dismount are independent. Dismount preserves both its held state and initial edge.

Directional gestures retain separate starts and holds. Either bumper suppresses
them; releasing a bumper while holding a direction does not create a new edge.
Bail controls retain independent gesture and body-axis gates, recovery edges and
push-off. A deliberate bail requires full triggers, both stick clicks and a new
edge on one of those four controls; the caller can suppress it.

These five additional groups match 343 native updates: 8,232 action-presence
checks and 1,119 emitted float words. Eighteen new regressions cover combinations,
edge history, suppression, signed manual pressure and independent mode gates.

Object movement now applies separate eight- and sixteen-point directional response
curves to sideways motion, forward motion and rotation. It wraps the left-stick
angle before sampling the curves, then adds direct right-stick rotation and clamps
only the combined rotation. Translation stays independent and is not clamped.
The caller supplies curve data, angle coefficients and left-stick magnitude.

Object requests match 391 native updates and all 1,173 emitted float words.
Thirteen new regressions cover interpolation, repeated points, angle seams,
independent responses and final rotation bounds.

Riding buttons now preserve independent left/right pushes, initial hold windows,
stance-based automatic pushing, braking, air/ground grab pressure, mount toggles
and catch requests. Movement restrictions apply separately to each request.
Sprint requires a positive push hold and no brake hold. The no-input request is a
gameplay result after restrictions, not a general measure of device activity.

These seventeen requests match 489 native updates, including 8,313 presence checks
and 1,049 emitted values. Twenty new regressions cover timing boundaries, stance,
pressure, independent suppression and brake-versus-sprint combinations.

Riding analog requests retain stick angles and radial pressure, body spin, board
adjustment, grind balance and nudges, anticipation, pumping, hard turns, crouching
and slide starts. Turn and slide restrictions are independent. Left and right
slide requests can overlap; neither overwrites the other. Crouch can be present
with a zero value when the stick angle requests it without trigger pressure.
Directional response constants and angle coefficients are caller supplied.

These twenty-three requests match 615 native updates across 180 scenarios,
including 14,145 presence checks and 3,557 exact float words. Seventeen new
regressions cover polar math, overlap, pressure, suppression and angle boundaries.

Walking requests preserve signed movement along the character's horizontal facing,
the two unchanged world axes and independent stick magnitude. Optional obstruction
projection removes movement into a supplied surface and stops the directional
request above a supplied alignment limit. It does not alter the raw axes or stick
strength. Tiny facing vectors use a strict normalization-length threshold.

These four always-present requests match 765 native updates across 195 scenarios:
3,060 presence checks and 3,060 exact float words. Ten new regressions cover facing
normalization, threshold boundaries, obstruction direction and projection limits.

Queued commands preserve six-word identifiers, case folding, null termination and
the 36-byte name limit. Direct requests bypass trick restrictions. String trick
requests retain separate finger, late, flip, slide and basic-trick gates; flips
require grab pressure without pushing. Accepted requests retain gesture speed.
The hold-pattern request is consumed once.

The shared request buffer preserves insertion order and the first value supplied
for each action. Repeated commands must not overwrite the first gesture speed.
Its storage is allocated by the caller before updates; insufficient capacity
raises an error instead of silently dropping a distinct request.

Native comparisons cover 899 queued records across 841 updates, with 3,522 key
and presence checks, 732 exact float words, matching action order and hold-state
consumption. An initial comparison used the last value for repeated actions and
failed 35 value checks; the shared buffer fixes that behavior against unchanged
captures. Eighteen new regressions bring the suite to 1,798 passing tests.

The joined router now owns retained samples, timers and one reusable request
buffer. It evaluates the auxiliary, riding, walking and queued requests in order;
queued restrictions receive the current effective push and grab state. Both-foot
input inserts the shared pushing request after the first foot request. Requests
already produced by analog input retain priority over queued commands with the
same identifier.

The joined native comparison starts from raw supplied readings and movement
context, without supplying intermediate action results. Across 243 scenarios and
905 updates, all 20,904 ordered actions, 21,720 state float words, 1,810 flag words
and hold-state checks match. Eight additional regressions cover retained edges,
request precedence, current-state handoffs and allocation-free repeated updates.
All 1,806 package tests pass.

The engine sampler reads six public analog channels and twelve configured button
actions. It converts both vertical axes to up-positive values, preserves trigger
pressure and returns neutral input when controller use is inactive, input is
suppressed or no controller is connected. It uses the engine's current player
scope. It does not add a deadzone, clamp, camera transform or button priority.

The hosting project's input settings must contain these actions without keyboard
bindings. Their names describe button positions; contextual actions are selected
later by the router.

| Action | Engine gamepad code |
|---|---|
| SkatePadLeftStick | LeftJoystickButton |
| SkatePadRightStick | RightJoystickButton |
| SkatePadLeftBumper | SwitchLeftBumper |
| SkatePadRightBumper | SwitchRightBumper |
| SkatePadUp | DpadNorth |
| SkatePadDown | DpadSouth |
| SkatePadLeft | DpadWest |
| SkatePadRight | DpadEast |
| SkatePadWest | X |
| SkatePadNorth | Y |
| SkatePadSouth | A |
| SkatePadEast | B |

Controlled engine inputs now pass through the sampler before the joined router.
All 905 captured updates still match the complete ordered requests and retained
state. Additional native checks cover all 4,096 button combinations, inactive,
suppressed and disconnected input, and axis selection for two player scopes.
These checks supply engine state directly; they are not physical-device tests.

The installed engine filters axes before exposing them through `GetAnalog`.
Its joystick deadzone is configurable; trigger deadzone is fixed at 0.125 in this
build. Ninety native integer-axis samples verify the filtering and endpoints.
The adapter cannot recover pressure removed by that filtering, and the engine's
axis getter currently allocates. Core allocation checks do not cover this API.

The playable movement adapter still uses its provisional bindings. Connecting the
sampler and router to live movement states, natural context production and local
owner callbacks remains unfinished. Physical-device testing, state-dependent
gameplay responses and animation acceptance are outstanding.

The frame processor now owns request clearing, controller-versus-override
selection, queued-command consumption and forced requests. Blocking controller
processing preserves retained samples, timers and the pending hold request, but
still consumes queued commands. Forced braking only blocks controller processing
when the separate movement-state gate is enabled. A pending forced bail is
consumed even when suppressed. Forced bail and brake requests replace an existing
value without moving its position in the host request buffer.

Forty-nine native scenarios cover 197 consecutive updates with a reused request
pool. The host matches 5,697 request values, 4,728 retained float words, 394 input
flag words and all queue, hold and owner-flag checks. Collection comparisons use
keys and values, not physical allocator order. Eight additional regressions bring
the suite to 1,814 passes. Empty and disabled override providers are covered in
the native capture; active override generation is still a caller responsibility.
Natural movement-state producers and live gameplay consumers remain unfinished.

## Hierarchical state selection

`HierarchicalStateSelector` selects a state and transition from immutable tables.
It checks each transition priority across the entire active path before trying a
lower priority. Descendants win ties; stored transition order breaks ties within
a state. An explicit transition may enter an inactive destination, while ordinary
child selection skips inactive states.

Entry guards and staying guards remain distinct. A transition's explicit target
uses entry guards even when already active. Common ancestors use staying guards;
new ancestors use entry guards. The guard evaluator receives the owning state so
state-dependent conditions can retain their context. No guard means success.

The selector supports retention, root reselection and retained-ancestor
reselection. It returns the selected leaf and transition without invoking state
exit, entry or update callbacks. These callbacks, concrete condition producers,
motion behavior activation and the live controller adapter remain unfinished.
The current playable graph still uses the earlier implementation.

Native comparison covers all 234 current state IDs plus the uninitialized state
under eight supplied condition patterns: 1,880 selections match. Those cases
select 65 distinct transitions from a table of 128. Concrete condition producers
are excluded; the comparison supplies captured entry/staying expression results.
The second graph now covers all 2,088 current states plus initialization under
eight supplied condition patterns: 16,712 native selections match, including
retained-ancestor reselection. Those cases select 477 distinct transitions from
a table of 2,390. This verifies selection with supplied condition values; it
does not establish live condition production or animation continuity.

## State transition paths

`StatePathPlanner` builds root-to-leaf paths and separates retained, exited and
entered states. A state remains retained only while both paths agree and its
ID precedes the supplied reset boundary. A boundary of -1 permits the entire
common prefix to remain. A self-transition can therefore restart its parent
and leaf even though the selected leaf ID stays unchanged.

The caller keeps the state records immutable. Each parent must precede its
children. Planning reuses buffers sized from the deepest path and returns
read-only spans valid until the next plan. An uninitialized current state is
allowed; a missing selected state is rejected explicitly. Entry/exit callbacks
and timer updates remain the caller's responsibility.

All 254 valid native path comparisons match. Four additional missing-selected
diagnostics are rejected by the host contract rather than reproducing an
uninitialized result. A broader instruction-reference replay matches 23,266
paths in the editor. Ten new regressions cover initialization, branch changes,
self resets, state-ID boundaries, reuse, capacity, invalid inputs and allocation
behavior; all 1,839 tests pass. The planner is not connected to the playable
graph yet, and full lifecycle and animation acceptance remain unfinished.

## Request conditions

`InputRequestCondition` checks the current request buffer using all six key words.
With comparison disabled, an existing request succeeds even when its value is
zero or NaN. Missing requests fail. Each evaluation retains its supplied owner
state, including evaluations that fail, so later state-dependent logic can use
the correct context. Construction starts that context at zero.

`ScalarConditionComparison` supplies equality, inequality, strict bounds,
negated bounds and absolute-magnitude comparison. Optional absolute input does
not change the threshold. Negated bounds intentionally accept unordered NaN
values; replacing them with ordinary inclusive comparisons changes behavior.
Disabled or unknown scalar comparison modes fail when evaluated directly.

All 800 native scalar cases and 93 request cases match, including every owner
update. The host buffer contains valid requests only; native collection end-node
metadata is excluded when translating the sentinel diagnostic. A separate
initialized-object observation checks 64 initial owner values and 18 named keys;
that observation is not a gameplay or predicate-execution test.

Nine new regressions cover comparison boundaries, NaN, absolute input, unknown
modes, presence, key collisions, reuse, invalid collections and allocation.
All 1,848 tests pass. These conditions still need wiring into the playable graph
alongside its remaining context producers, callbacks and timer lifecycle.
Physical-controller and complete animation acceptance remain unfinished.

## Behavior lifecycle

`StateBehaviorLifecycle` manages entered and exited state ranges, retained timers
and behavior instances. Entry follows the supplied state path and each state's
stored behavior range, while active storage stays sorted by definition ID. Exit
visits active definitions in reverse order, removes those owned by exited states,
then removes their timers. Definitions without an owning state survive ordinary
state exit. Entry resets that state's timer to zero even on repeated entry.

Factories may return an absent instance while leaving the definition active.
Callbacks still receive that absence. Disabling entry/exit callbacks preserves
instance creation, storage changes and timer changes; explicit transition actions
still execute in supplied order. Callback enable changes take effect during the
same phase. A repeated definition keeps its stored instance but passes the first
active slot's instance to the entry callback.

The caller supplies immutable ranges and definitions plus retained callbacks.
Creation transfers one owned instance reference; -1 represents absence. Storage
retains unused final slots after removal until another assignment overwrites
them. Entry and exit callbacks must use a copied owner context. Temporary
reference-copy operations are collapsed; comparison covers callback arguments
and phase-final reference counts, not every intermediate reference operation.

All 88 native sequences match 296 phase snapshots, 652 callback events and 2,368
reference-count values. Twelve final-update events are explicitly outside this
class and excluded from callback comparison. Timer increments separately match
280 original cases and 1,445 float values from supplied starting timers. Nine
new regressions pass; the full suite passes all 1,857 tests on recheck. An initial
run recorded an existing router allocation-test failure; the unchanged isolated
test and unchanged full suite both passed afterward.

Final per-state update scheduling remains separate. Its instruction reference
covers 2,322 state schedules; an original replay of 114 representative schedules
matches all 3,434 dispatches, covering every observed body target. Behavior bodies
are excluded from that dispatch comparison. The joined graph below now orders
selection, path planning, condition invalidation, lifecycle phases and final
updates. Runtime teardown, playable controls and full animation acceptance remain pending.

## Behavior update schedules

`StateBehaviorScheduler` runs an immutable operation list for the selected state.
Operation IDs are caller-defined; each operation receives the corresponding
stored definition and instance. The scheduler publishes `CurrentInstance` before
the callback and preserves the final value afterward, including callback changes.
An empty schedule or disabled update preserves the existing context.

Schedules address a fixed prefix of retained storage, including inactive slots
when supplied by the caller. They do not derive their length from the currently
active definition count. Absent instances pass through to the operation callback.
Insufficient storage is rejected before any operation executes or context changes.

Native comparison matches all 3,434 dispatches from 114 original state captures.
A separate replay covers all 2,322 extracted schedules and 71,435 operations;
this broader reference replay is not full original runtime coverage. All checks
use supplied operations and tagged storage, with concrete behavior bodies excluded.
Six new regressions cover ordering, shared context, disabled updates, fixed storage,
invalid inputs and allocations; that milestone passed all 1,863 tests. Actual
operation implementations and playable animation acceptance remain pending.

## Joined graph updates

`HierarchicalStateGraph` retains the selector, path planner and behavior lifecycle.
Each update invalidates condition validity before publishing its new delta time,
sets the selection flag during guard evaluation, partitions the resulting paths,
then advances existing timers. It publishes previous and current state before
exit callbacks, transition actions, entry callbacks and the final state update.
The supplied invalidator must preserve cached values while clearing validity.

No explicit transition retains the common path. Explicit self/descendant targets
use the target's parent as the reset boundary; other targets use their own ID.
Newly entered timers reset to zero after old timers advance. Callback enable
changes take effect during the same update. Disabled callbacks still allow
selection, timers, factories, retained storage changes and transition actions.

The caller binds the final state callback to `StateBehaviorScheduler`, supplying
the lifecycle's full `Storage` span rather than its shorter `Active` span. This
preserves unused retained slots required by fixed operation schedules. All graph
tables and callbacks remain immutable after construction. Missing selections
are rejected before state publication or lifecycle changes; guard exceptions
also clear the selection flag. These failures do not roll back invalidation or dt.

Native comparison matches 24 captured sequences and 144 consecutive updates,
including 832 callback events, 432 timer words and 1,152 phase-final reference
counts. Fixtures contain five states, supplied guards, nonnull/null factories and
inert final callbacks. To keep host tables immutable, the replay replaces a
rewritten transition with five fixed guarded targets on each leaf. It compares
all recorded state/cache fields at callbacks and after each update. Concrete
predicate production and behavior bodies are excluded from this comparison.

Ten new regression cases cover publication order, retained timers, reset paths,
callback enable changes, scheduler storage, selection failures and allocation.
Live rider context, actual operation bodies, runtime teardown, physical-device
testing and complete animation acceptance remain unfinished. This graph update
does not change the playable controller bindings.

## Clearing active behaviors

`StateBehaviorLifecycle.ClearActive` removes every active definition in reverse
order, including definitions that have no owning state. Each enabled exit callback
receives the stored instance, including absence. Callback enable changes are
observed before each exit. Removal still occurs when callbacks are disabled.

This operation preserves all timers and retained storage, including unused slots.
It does not release those stored references. Repeated clearing is empty; later
entry runs factories again and resets only entered-state timers. A full graph
restart must separately establish the correct state and lifetime ownership; it
must not treat this method as resource destruction or a complete gameplay reset.

Six regressions cover unowned and absent instances, reverse order, retained
timers/storage/reference counts, re-entry, disabled callbacks, callback enable
changes and allocations. All 1,879 tests pass. Concrete behavior destruction,
graph owner cleanup and playable reset wiring remain unfinished.

Native comparison matches 96 captured sequences and 264 entry/exit/clear
snapshots, including 120 clear calls, 1,128 callback events, 2,112 reference-count
values and 12,408 stored definition/instance pairs. The host allocates storage by
definition count; unused captured slots beyond that count are checked as absent.
The captures also contain 96 destruction snapshots, explicitly excluded from the
host comparison because graph destruction is not implemented here. Callbacks
are supplied and inert; callback-driven collection mutations are not covered by
these original captures.

## State timer conditions

`StateTimeCondition` publishes its supplied owner before resolving it. Direct
owners are encoded state handles. Indirect owners use caller-provided state-handle
and parent getters; the first nonzero handle wins, otherwise lookup continues
through the parent chain until it reaches an absent owner. The caller supplies
valid, terminating owner chains. The direct-owner mode is checked again after
each parent step, so getter-driven mode changes affect subsequent resolution.

`StateBehaviorLifecycle.GetElapsedByHandle` treats zero as absent and decodes a
nonzero handle using the complemented low sixteen bits. A state ID can be encoded
as `~(uint)state`. Missing timers and keys outside the host table return -1.
The condition rejects negative time before optional absolute comparison; zero,
negative zero and unordered NaN values continue into `ScalarConditionComparison`.
Disabled numeric comparison returns false even when the timer exists.

The owner and timer getters are retained delegates. Live graph bindings must
provide the correct context and encode selector state IDs explicitly. State
selection reads the existing timers before the graph advances them for the
current update. Clearing active behaviors preserves those timers; re-entry
resets entered timers and changes subsequent condition results.

All 456 native cases match the predicate result, retained owner, resolved handle,
elapsed float bits and owner getter order. Forty-eight cases use indirect owners,
with 72 getter calls. Both timer lookup variants are covered, including empty and
populated trees, missing keys, distinct upper handle bits, comparison modes,
absolute input, infinities and NaNs. Starting timers and owner chains are supplied;
the native comparison does not test live context production or timer accumulation.

Ten new regressions cover owner publication, chain traversal, missing and negative
time, numeric edge cases, handle decoding, clearing/re-entry, mode changes,
allocation and evaluation before timer advancement in the joined graph. All
1,889 tests pass. Live state bindings and full gameplay animation timing remain
unfinished; isolated timer parity does not establish transition acceptance.

## Condition result caching

`ConditionResultCache` retains validity and result bits by condition index.
Both true and false results are cached. A valid result is reused even when the
caller supplies another node or owner; the predicate receives those arguments
only on a miss. Invalidation clears validity while preserving stored values.

The predicate runs before its validity and value bits are published. Writes use
the current words after the callback returns, preserving changes made by nested
evaluation or invalidation. The caller can bind `Invalidate` to the graph's
invalidation callback and use `Evaluate` from its guard evaluator. Invalid
condition indices are rejected before any predicate call or cache change.

Native comparison matches 548 sequences and 3,288 accesses, including 1,644
predicate calls and 1,096 invalidations. The fixtures use captured single-condition
guard paths, supplied boolean answers and changing node/owner arguments. They
compare cached results, every validity/value word, arguments and cache contents
at each predicate call, and preservation of values during invalidation. Guard
negation is applied by the private replay; compound expression evaluation is
outside this cache class and is covered by the compound evaluator below.

Eight new regressions cover true/false reuse, changed context, word boundaries,
nested evaluation, callback invalidation, failed predicates, invalid indices,
allocation and graph invalidation with timer conditions. Nested mutation and
exception cases are host/source checks; the original cache captures use inert
supplied predicate results. All 1,897 tests pass. Live condition registries,
actual state bindings and complete gameplay/animation acceptance remain unfinished.


## Compound conditions

`ConditionDecisionEvaluator` owns cloned decision and root arrays. Each decision
contains a condition index and false/true targets. Targets reference earlier
decisions or terminal results, so every path ends without a traversal allocation.
Entry and staying guards select separate root tables. A retained predicate
delegate can bind directly to `ConditionResultCache.Evaluate`.

Only the chosen branch runs. Repeated condition accesses remain in the program,
and a decision still evaluates its condition when both targets are identical.
This preserves callback effects, including invalidation between repeated checks.
Node and owner arguments pass unchanged to every access. The evaluator does not
invalidate the cache itself; graph update ordering remains caller-owned.
Malformed targets and negative condition indices fail during construction.
Invalid guard indices fail before any predicate runs.

Native comparison matches six sequences and 4,512 guard accesses across every
entry/staying ID in two supplied programs. All 1,866 primitive calls, arguments,
callback order, cache snapshots and six invalidations match. The same evaluator
also matches 12,032 previously captured cached guard results. Primitive answers
are supplied; actual condition bodies and live context production are excluded.
Recovered program data remains private and is not shipped in this package.

Nine regressions cover short-circuit order, context forwarding, separate roots,
cache sharing, terminal roots, repeated accesses after invalidation, equal-branch
side effects, immutable input data, invalid programs/guards, failed predicates
and allocation. Callback mutation and failure are host checks, not native mutation
acceptance. All 1,906 tests pass. Live condition registries, concrete behavior
implementations, controls and complete gameplay/animation acceptance remain
unfinished. These checks do not establish smooth visual transitions.


## Playable rider integration

Set SkateRider.ControllerProfile to a JSON path in the package's local data folder.
It supplies arc coefficients, quadrant constants, activity/manual thresholds,
button/analog tuning, object response curves and walking factors. The lab uses a
separately supplied profile; no reference data is shipped in this library.
An empty profile leaves rider controller input neutral. Keyboard input remains
available. The board-only SkateControls component retains its existing bindings.

The rider samples the controller every frame, including during interactions, so
held buttons do not become fresh presses when an interaction ends. The retained
frame processor/router feeds the current motor. West/South select physical left/
right push feet, East brakes, North toggles riding, and the left stick steers or
walks. On foot, West requests a jump; either full trigger retrieves a nearby board,
and a full left-trigger edge drops a carried board. Engine deadzones still apply.
Right-trigger throwing, sprint, camera controls, full grab selection and other
advanced consumers remain incomplete. Current motor gates are not a complete
movement-state implementation.

SkateInput.PushFeet and NewPush preserve explicit feet and the router's new-push
window through animation and the client-to-host input RPC. Strength-only callers
keep the legacy stance-selected foot and rising edge. Nine new Core regression
cases cover both feet/stances, simultaneous requests, switching feet while held,
repeated request windows and legacy input. All 1,915 tests pass.

Native scene checks complete both-stance push/brake/dismount/pickup/carry/drop/
mount sequences. Separate steering, walking jump and held-action checks pass.
The running character now selects distinct left/right push states. Physical
hardware, remote clients and the full animation sweep remain untested. An abrupt
mirrored braking transition and pickup hand alignment remain open visual issues.


## Playback expiry handoff

StateGraphPlayer now supplies AnimationPlaybackStatus to the graph: remaining
time at the frame being produced and whether its fade will still be active.
The expiry condition waits for actual blending when requested, then accepts a
finished animation or remaining time at/below the requested threshold. Graph-only
callers retain their existing state-time fallback. Current looped playback does
not assert a finished flag; full clip lifecycle metadata remains separate work.

This removes a reproduced intro wrap caused by independently accumulated graph
and playback clocks. The rendered ankle moved 8.93 inches, with 8.74 inches already
present before leg IK. Three fixed live traces have maximum braking ankle steps
below 2.36 inches. Both stance interaction chains pass again with no diagnostic
continuity flags across 862 poses. Twelve new regression cases pass, including
400 frame-rate/start-time combinations; the old player fails the continuity
regression at 30, 60 and 90 Hz. All 1,927 tests pass. This is an adapter timing
correction; complete scheduler parity and full animation acceptance remain open.


The numeric expiry test preserves the unordered comparison outcome: it rejects
only when the threshold is strictly less than the remaining time, so a NaN does
not take that rejection branch. The blend gate still applies first. All 800
native host comparisons match an instruction reference with supplied provider
values, including threshold boundaries, signed zero, infinities and NaNs. This
does not execute the complete original runtime or validate its providers.
