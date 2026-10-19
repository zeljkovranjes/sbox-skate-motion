# World contact collection

`PhysicalWorldContactCollector` accepts solved constraints with retained body
identities, normals, points, impulses and packed surface words. Register each body
with its physical owner and canonical bone before collecting a world update.
Registration changes occur outside the per-frame collection path.

Collection clears retained owner counts and visits constraints in their supplied
order. Each registered side receives at most 16 records. First-side records keep
the normal and low surface half; second-side records reverse the normal and use
the high half. Relative velocity follows that same orientation. Contacts between
the same owner or two owners sharing a non-null actor identity are suppressed.
An unregistered opposite body still contributes velocity, mass and impulse data.

`PhysicalRiderPostsolve.UpdateFromWorld` consumes only the owner's populated
prefix. It builds actor contact inputs, classifies contacts, advances collision
countdowns and updates pose feedback. Reused buffers avoid per-frame contact
array allocations. Contact lists must be collected once per completed world step,
before invoking postsolve for each rider.

Validation covers 34 sequences and 132 updates in the engine, with 25,376 exact
comparisons. Another 32 sequences cover 96 connected updates and 80,768 compared
values with zero numeric difference. Regressions cover side orientation, owner
and actor rejection, independent capacity limits, reassignment and clearing.

These checks supply solved constraints and actor inputs. Native scene collision
ingestion, automatic registration lifecycle and gameplay scheduling remain
unfinished. The collector does not generate collision contacts or discover map
geometry. Allocation paths are managed host storage, not a verified allocator
translation. This work does not enable experimental physical pose rendering.
