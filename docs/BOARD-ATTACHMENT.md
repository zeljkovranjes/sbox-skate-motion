# Board attachment correction

The internal `BoardAttachmentController` retains a correction between an animated
hand and its authored helper. It is verified independently and is not connected
to gameplay. Callers must supply poses in rig coordinates and preserve the update
order relative to pose gathering, other modifiers and cache updates.

`RequestFromHand` forms `hand.Compose(helper.RigidInverse())`, requests that frame
for 15 updates and separately requests its vertical offset for 15 updates.
The helper inverse uses transposed axes; it is not a general affine inverse.
Fresh requests apply at full strength and keep their countdowns for that update.
`RequestFrame` refreshes only the frame countdown. `RequestHeight` changes only
rig Y and its independent countdown.

Without a fresh request, each positive countdown uses the squared fraction
`((remaining - 1) / remaining)^2`, then subtracts one from the countdown. Frame
decay blends the rotation columns toward identity, orthonormalizes them in Z/Y/X
order and decays translation X/Z. Height decay changes only translation Y.
The frame is applied before the current board pose and all four authored targets.
Request flags clear after every update, even when no correction is active.

These are update counts, not seconds. There is no elapsed-time multiplier or
countdown clamp. Gameplay scheduling must be established before using this
controller to replace the existing held-board placement. An expired frame stays
in state but is not applied when both countdowns are inactive.

The caller supplies the current animation's poses on each update; the controller
modifies them in place. It does not own clip sampling, select a hand, map gameplay
events, attach a rendered object or determine pickup/drop timing. Each rider needs
its own controller, separate from shared animation assets.

Verification covers 108 native cases at both requested and final state, including
both hand choices and up to 17 updates. Each stage compares 74 spatial/count fields
and two request flags. Thirteen Core regressions cover lifecycle, composition,
independent vertical handling and invalid target counts. These checks do not
establish a visually smooth live transition or complete animation acceptance.

## Vertical request producer

`VerticalAttachmentController` runs before the retained attachment update. It
selects one of four response modes from category transitions and supplied signals,
keeps elapsed time, height, velocity and previous-update measurements, and requests
only rig Y through `BoardAttachmentController.RequestHeight`. It leaves an earlier
request intact when it does not generate a replacement. Mode/category identifiers
remain internal inputs; the gameplay adapter must establish their real producers.

Three supplied four-point curves control transition, tracking and impulse response.
Spring modes integrate velocity with fixed scalar steps of 1/60 and 1/120; impulse
mode samples its curve using normalized elapsed time and bounded speed magnitude.
Tracking can move its baseline by an authored maximum per update. Forced requests
override cancellation and use a separate attachment countdown, then stop the
vertical controller. Inactive updates still retain the latest projected speed and
category. The previous-height sentinel starts at `float.MaxValue`.

The scalar controller expects current vertical separation, projected speed and
cached height in rig coordinates. These inputs must come from the correct pose,
normal and cache sources; substituting engine Z or raw animation for a modified
pose is not equivalent. Fixed scalar integration and frame-counted attachment
decay make the original caller cadence part of the remaining verification.

All 322 native prescribed updates match ten scalar fields and four state/flag
fields. Sixteen regressions cover initialization, transition priority, sampling
history, independent pending requests, cancellation, impulse response and curves.
The recovered modules remain internal and do not alter gameplay yet.

## Descending alignment producer

`DescendingAttachmentController` requests alignment only when supplied vertical
motion is negative and the blocking signal is clear. Other updates retain any
earlier request. It transforms the supplied target and displacement into rig
coordinates, takes horizontal displacement from that vector, and replaces its
rig-Y displacement with the positive target-to-board height difference or zero.

An owned eight-point response curve weights `LimbPoseBlender.BlendTransform`
between the board and this target. The result composed with the board's rigid
inverse supplies a relative correction. Frame translation Y is cleared before
the frame request; the negated relative Y is requested separately for 15 updates.
The input board pose stays unchanged. Rotation blending uses the existing shared
implementation, including its half-turn handling.

All 140 prescribed native cases match 26 spatial/count fields and two request
flags. Thirteen Core regressions cover positive/negative/zero/NaN gating, pending
request ownership, local height clamping, transformed offsets, relative frame
composition and curve boundaries. The actual motion/signal/response producers
and gameplay scheduling still require integration; this controller alone does not
establish playable descending behavior or smooth transitions.

## Ordered request sequence

`BoardAttachmentSequence` runs the hand request, selects the grind or descending
branch, runs vertical correction, then applies the retained correction once.
`BoardAttachmentInput` supplies hand/helper poses and the dispatch gates alongside
the existing stage inputs. Descending mode selects that branch unless suppressed;
when both gates are set, neither motion branch runs. Suppressing descending has
no effect when descending mode is off. A bypass preserves the grind controller's
published state; calling its disabled path would clear that state incorrectly.

Each update takes freshly gathered mutable board and four authored targets. The
sequence supplies that current board to both motion branches so stale copies in
input records cannot change the pivot. Requests leave poses untouched until the
final application. Vertical correction can replace height and its countdown
without replacing the preceding request's rotation or horizontal offset. The
caller updates its pose cache afterwards and retains controller state per rider.

48 three-update reference scenarios cover all dispatch choices, absent and both
hand requests, excluded hands, blocked descending, vertical overrides and release.
All 144 native updates match 355 float fields and ten flag/index/state fields each,
including all four modified targets. Nine new regressions cover branch priority,
retention, request order, independent height, fresh poses and invalid target counts.
These checks establish the supplied-input sequence; live message production,
geometry selection, scheduling and visible gameplay handoffs remain unfinished.
