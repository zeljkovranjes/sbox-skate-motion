# Physical contact registration

`PhysicalSolverWorld.RegisterContactBody` registers a world body for an owner's contact feedback and enables reporting bit 8. Registering the same body again replaces its owner and bone. `UnregisterContactBody` removes its routing entry and retains only the low three motion-mode bits, including when no entry existed. Prepared body snapshots retain the changed reporting flag for subsequent contact compilation.

The collector's lower-level registration methods remain routing adapters for supplied contact streams. World registration adds the physical flag side effects. Call it before compiling constraints; already compiled rows retain the reporting request they had at compilation.

`PhysicalRigContactBinding` registers canonical bodies 0 through 23. It resolves bodies by identity, so solver-array order can differ from bone order. The two feedback-only bodies and the four targets are outside this registration set. Construction validates every canonical body before changing membership; release is idempotent. The physical animation runtime now retains this binding and exposes its contact owner for the subsequent feedback connection.

Registration does not itself produce contacts or activate bodies in broadphase collision detection. Live geometry ingestion, complete physics-world ownership, activation, scheduling and physical pose rendering remain unfinished.

## Validation

The original registration, replacement, removal, tree lookup and tree balancing routines were executed for 24 sequences containing 1,128 operations. Native world registration and subsequent collection match 208,512 comparisons, including body flags, contact order, bones, surfaces, normals, positions, relative velocities and opposite-owner identity. Integer fields and floating-point bits are compared exactly.

The original canonical registration loop was also executed for twelve permuted body layouts. Native rig binding matches 1,512 comparisons, including all 24 canonical bone identities and exclusion of feedback-only bodies. The reference runner uses a bounded allocation arena and preallocated owner lists; general allocator behavior and live scheduling are outside this evidence.
