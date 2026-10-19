# Rig metadata

Animation graphs may include an optional `Rig` object. The asset loader resolves
its names against the clip skeleton, validates the data and retains an internal
`EffectorRigAsset`. Graphs without it keep loading normally. This supplies rig
data; the recovered rig controller is not yet connected to gameplay.

| Field | Value |
| --- | --- |
| `Version` | `1` |
| `ClipAxes` | Three signed one-based axis indices, describing clip X/Y/Z in rig coordinates |
| `Bones` | Solver bones in their required solver order |
| `Effectors` | Four slots: left foot, right foot, left hand, right hand |

Each bone has `ClipBone`, `BindRotation` (four X/Y/Z/W floats), `BindPosition`
(three floats in metres) and `BodySize` (three nonnegative dimensions in metres).
Names resolve against the clip skeleton, independently of the display avatar.
Bind values and dimensions stay in rig coordinates. Bind quaternions are not
normalized, and transform axes are not orthonormalized during conversion.

Each effector has `HelperBone`, `EndBone` and `ReferenceBone`. The helper names
a clip bone; endpoint and reference must name bones in solver order. Empty
`ReferenceBone` means no linked bone. A toe endpoint and its ankle reference
remain separate even when both belong to one foot.

For example, `ClipAxes: [3, 1, 2]` means clip X = rig Z, clip Y = rig X and
clip Z = rig Y. Signed permutations are supported if they preserve handedness.
Duplicate axes and reflections are rejected. A matrix conversion changes both
its input and output basis; changing only its translation is insufficient.
No unit scaling is performed by this mapping.

Hierarchy initialization takes already-composed model-space clip matrices,
converts them to rig space and applies the validated solver mapping and bind
data. Bone-local animation transforms must be composed first. Per-rider mutable
poses, contact inputs and request state must remain separate from shared assets.

Data checks reject missing names, duplicate solver mappings, unsupported versions,
invalid component counts, nonfinite transforms and negative body dimensions.
Physical dimensions alone do not define body mass, inertia or collision shapes.
Those adapters, live frame inputs and update scheduling still need integration.
