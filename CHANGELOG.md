# Changelog

## 2026-10-19
Title: Measured Handling, Board Sync and Smooth Animation
### Added
# Physics
Measured handling - SkateSettings.Recovered (the default for boards without a profile and for the Balanced preset) rides on measured response curves: pops launch at a nearly fixed 3.3 to 3.46 m/s whatever the windup (less on steep ground and at very high speed) with a little sideways steer, each push adds a set amount of speed while the pushing foot is down, steering follows stick and speed curves and eases off while pushing and in a manual, manuals need the stick held a moment and pitch 6 to 24 degrees with a speed-scaled wobble and their own slowing, and rolling friction rises only at very high speed
Landing rules - a landing tilted further from the ground than its speed allows is a crash, running into something side-on while standing upright at a moderate speed is a runout (Skateboard.RunoutRequested; the rider steps off) instead of a bail, and the rider's legs absorb a landing so the board does not spring back into the air
Air control - steering in the air sets a spin rate about world up (strongest just after take-off), and toward the landing the board lines itself up with its travel, forwards or backwards
Animated tricks - with an animated rider, flips and spins are the animation's: the pop gives the deck no trick spin, the rider carries the board through the air, and a caught trick lands exactly on the physical board
Push contact - Skateboard.PushContact: an animated rider reports when its pushing foot is on the ground, so the push adds speed while the foot is down
Head-first bail - a rider upside down in the air whose body reaches the ground bails instead of passing through it
Grind lock-on - a rider falling past a rail within 0.9 m is carried onto it, arriving with no sideways speed; while grinding the board is held over the rail's line and turned along it (across it in a boardslide), grind friction builds up over the first four seconds, and a deck twisted far off its grind balance is a crash
Rider recovery - a bailed rider gets up by themselves after lying still (SkateRider.RecoverAutomatically)
Controller pump - crouching on a controller (left stick back or a trigger) pumps, as the pump key does
Powerslides - holding slide while rolling turns the board across its travel toward the steering side and lets it slide, and letting go turns it back in line (SkateState.PowerSlide)
Reverts - a quick tap of slide spins the board half way round so it rolls on backwards (SkateState.Reverting)
Fakie pushing - a push while rolling backwards drives the way the board is rolling instead of braking it (SkateState.Fakie)
Body flips - leaning toward the toes or heels while grabbing in the air (SkateInput.Lean; push or brake on keyboard, the left stick on a controller) turns rider and board over together in a front or back flip that comes round once by the landing when held, keeps turning for at least 0.3 s, and stops part way round when let go early; it needs most of a second of air (SkateState.BodyFlip)
Wall rides - a board in the air heading for a steep wall at a glancing angle, at least 0.6 m up, turns its wheels to the wall (with a small lift for a low contact), is held to the wall while it still travels along it and is not yet sliding down it (1.5 s at most), then steps off and turns upright to land on its wheels (SkateState.WallRiding); the rider rides on in the riding blend's wall pose, body upright, instead of playing a landing
Bail reason - Skateboard.BailReason tells why the board last bailed: impact, landing tilt, grind balance or requested
Difficulty - SkateProfile.Difficulty sets how forgiving the measured handling is: Easy pushes quicker, pops full height from any windup and locks onto rails from further away; Hard pushes slower, pops low from a quick windup, locks on only close to a rail and gives little help lining up a landing; Normal is the measured handling as it was
Measured pumping - holding pump crouches and letting go stands the rider up; rising on ground that curves up under the board gains speed, most when slow and in a tight transition, up to 10 m/s^2, nothing on flat ground or ground curving away. The rider also rides lower on steep ground and rises as it flattens, and with SkateProfile.AutoPump on, dropping into a transition pumps a little without the button (70% on Normal, all of it on Easy, none on Hard); off by default, so a board coasting with no input never gains speed. The crouch shows in the animation (SkateState.Crouch)
Hippy jump - holding push and brake together crouches (SkateInput.HippyJump; both push buttons on a controller), and letting go jumps the rider straight up off the board, higher after a longer crouch, while the board rolls on beneath; the rider lands back on the deck when it is still underneath and otherwise runs out
# Animation
Switch riding - riding backwards for a moment turns the rider round to ride switch (SkateRider.RidingSwitch), and from then on the clips play mirrored and facing the board's tail, so pushes, reverts and riding look right in either stance; the turn into the new stance eases in and the board shown in the air stays on the real one. A graph names its turn-round clips in StanceSwapClips
Grabs - each grab plays its own grab and tweak, a held grab lets go just before the landing so its release brings the board back underfoot, and grabbing during a body flip keeps the grab pose instead of the dark-side idle
Landing prediction - the animation's time to land is measured from the ground below and the board's vertical speed
Powerslide and hippy jump animation - the slide plays its into, cycle and out, a revert its spin, and the hippy jump its crouch, jump, air and landing, with the feet off the deck while in the air
Transitions - clip changes are inertialized: each bone's difference from the pose shown last eases out while the new clip plays its own motion, so a fast-moving bone (a flipping board) keeps moving through the change and no blend ever snaps between nearly opposite rotations
Landing blends - big drops land with the heavy absorbing crouch, one of three landing variations is chosen at each touchdown, and a landing absorbs for half a second before a held crouch can cut it short
Landing types - a landing that turned in the air plays the spin landing and one that came down off its line plays the sketchy landing
Pushing foot - the planted foot stays where it touched the ground, the push plays at the speed that sweeps it back as fast as the board moves, and its leg never snaps straight (the reach to a planted foot eases toward the leg's full length)
Pickup - the board is drawn toward the hand every frame, the deck is kept out of the holding hand (measured from the rider's own hand), and the catch gives a little; SkateRider.HandDeckGap reports the gap between hand and deck
# Rider
Sprint - holding SkateSprint while running lifts the top speed to 10 m/s, held a moment after letting go and then easing back to a run over a few seconds (SetWalkInput's sprint, the sprint button on a controller)
Walking - walking and running follow stick speed curves (a gentle walk, a run at full stick), speed changes at a steady rate, the body turns no faster than a rate that falls with speed, and jumps keep part of the ground speed
Runout - stepping off a moving board runs it out with the board's momentum instead of stopping dead
### Improved
# Animation
Blend phase - a blend whose weights change keeps its clips at the same point of their cycle
Smooth placement - the rider and board are shown between physics steps, and animation inputs read from the board follow that smoothed pose
### Fixed
# Animation
Board snapping - the board no longer snaps or turns over at the end of a flip or when it is handed back to physics on landing
Animation hitches - clips whose poses are stored in eight-frame blocks no longer hold a frame and jump at every block boundary when the pose file selects continuous sampling
Landing skipped - a push held at touchdown no longer cuts the landing off; a transition's time-in-parent test counts time in the state that owns it
Flip dark-side flash - a flipping board passing through upside down no longer plays the dark-side idle for a frame
Knee flips and snaps - every leg correction keeps the animation's knee direction, held steady while the leg is nearly straight
Held poses - a state playing a single held pose shows it through its fade before a transition taken at its end, instead of leaving (and starting again) every frame
Stale clip endings - a state entered as another clip ends no longer reads that clip's ending as its own
Interrupted moves - a state that may not be interrupted keeps its whole path: nothing earlier above it takes over, so a hippy jump's crouch or a push from fakie plays through
First frames - the first frames after starting no longer pass an unset pose to the board display or the foot corrections
# Rider
Ragdolls from a stale pose - a bail moments after the board was placed somewhere else no longer starts the ragdoll where the rider was last shown (inside whatever is there, flinging it away); the pose moves to where the board let the rider go, and motion handed to it is always finite
Ragdolls under the ground - a bail from a pose partly inside the world lifts the ragdoll out before it starts instead of letting it fall forever

## 2026-10-18
Title: Keyboard Controls, Clipping Fixes and Behavior Ownership
### Added
# Input
Keyboard and mouse - steering keys ramp the trucks in and out, holding the SkateFlick button (right mouse in the lab) turns the mouse into the trick stick, and releasing Jump with SkateFlipLeft or SkateFlipRight held pops a kickflip or heelflip; bind the three new actions
# Camera
Mouse orbit - SkateCamera orbits with the mouse (MouseOrbit); while riding it swings back behind the rider once the mouse rests, on foot it stays where it was turned
# Animation
Held board clearance - a carried board moves out from the body when the rider's proportions would put a leg or hip through it, and the holding arm reaches out with it; body sizes are measured from the rider's own mesh, so any humanoid model fits
Board display - Skateboard.SetDisplayPose, DisplayedTransform, DeckSurface, DeckBounds and BoardBounds show the whole board where a trick animation holds it and expose the deck's real surface
Foot soles - each shoe (heel to toe, edge to edge, sole to instep) is measured from the rider's own mesh and kept out of the deck, so heels no longer sink into the board on any model
Flip clearance - while a trick's board is shown in the air it gives way to the feet along its own up axis instead of the legs bending away, so the feet and shins stay clear of the deck, trucks and wheels with the board on edge and the knees keep their animation
FootDeckGap - SkateRider reports the lowest gap between a foot and the deck each frame, for checking custom riders and boards
Pushing foot - a foot pushing beside the board stands on the real ground under it instead of hovering at the clip's own board height
# Physics
Map seams - shared triangle edges, and ramp edges resting on a floor, get edge codes so wheels roll across seams and onto ramps instead of hitting each one like a small wall
# Animation
Compiled behavior registration - keep independent instance families distinct and reject missing runtime bindings
Owner timer conditions - connect kickturn decisions and entry/exit resets to the shared timer
Owner event timers - preserve graph-clock updates and teleport resets separately from direction-confirmation timing
Ground query submission - transform retained probes, process cached map meshes and feed complete support selection
Body tilt binding - preserve independent smoothing instances, shared activation flags and animation output order
Intent behavior - forward present request values through the shared physical scalar queue
Contact history - retain orientation and delayed metadata publication before movement classification
Shared scalar output - preserve graph assignment and trick-height queue order across entry, update and exit
Playback actor context - share selectors, flags, held-board feedback and pending reference state with retained playback
Ground query results - refine completed support and edge results into ordered samples and final support selection
Physical mode behavior - apply entry-time mode changes to the retained character state
Base motion factory - consume indexed references before owner posture and retain base playback rate and publication order
Physical movement classification - preserve state counters and contact history while feeding riding direction updates
Ground query pipeline - connect geometric admission and candidate ordering to retained final selection
Posture profiles - load resolved maps with skeleton identity checks and preserve explicitly unpaired bones
Physical riding observations - derive direction and projected speed from physical body and character state
Ground contact selection - retain candidate intervals, slope history and query publication
Authored posture - transform paired bones, attribute values and auxiliary stream pairing together
Riding direction state - retain direction confirmation and teleport timing through trick state changes
Ground query segments - construct ordered surface segments from supplied contact candidates
Channel motion factory - construct rated authored motions with retained owner references and independent storage
Gameplay animation flags - preserve manual, trick and powerslide state across compiled entry and exit
Keyed channel owner - preserve layer priority, replacement, fade requests and ordered storage release
Effective animation input - advance the active channel tree and publish its attributes before physical input
Head owner connection - send retained head requests and torso parameters to the shared animation owners
Walking placement inputs - predict the desired point from selected physical velocity and retain the query target
Head channel behavior - retain entry and exit state, channel transitions and torso parameter updates
Overlay attribute output - merge enabled channel records with overlay priority
Walking placement output - feed solved placement into the character feedback reference
Physical input phase - advance animation clocks and publish evaluated attributes before physics reads them
Offboard character preparation - preserve previous-frame targets and feedback before adopting solved walking motion
Walking owner preparation - feed retained contact motion, angular history and supported ground placement into solved locomotion
Compiled playback binding - connect retained playback instances and entry actions to the base animation owner
Evaluated attribute publication - rebuild the retained attribute cache from supported playback nodes
Walking frame adoption - apply solved orientation, stance and optional turn correction to the physical rig
Contact motion reference - carry linear moving-contact history into walking motion and cadence
Evaluated attribute inputs - playback actions read retained evaluated records and queue parameters without resampling current trees
Walking frame preparation - feed solved motion into gait cadence with explicit contact and reference inputs
Handling phase binding - retain the input physical frame and use the current canonical spine at output
Playback instance lifetime - preserve the first-update action suppression and explicit entry reset
Playback parameter actions - preserve ordered sampled and table-backed values, fallback rules and explicit actor bindings
Physical input connections - feed retained animation attributes into contact-aware gait cadence and compute handling angles from supplied physical observations
Compiled graph connection - preserve explicit state IDs, ordered behavior callbacks and separate transition-action bindings
Playback entry behavior - preserve entry-action order, one-use transition overrides, motion variants and fallback requests
Physical gait producer - preserve ordinary movement and settling cadence with explicit physical inputs; contact correction and live binding remain pending
Owner sampling context - share interpolation, trajectory and directional threshold inputs across retained base and handling trees
Optional tree player - connect ordered state requests to retained animation trees with explicit reference and parameter bindings; live activation remains pending
Channel transitions - preserve per-bone weight selection when transition storage is reused
Retained base trees - preserve outgoing animation clocks through sequences, transitions and nested insertion; playable state-graph connection remains in progress
Motion tree construction - load nested directional blends and construction-time selectors with explicit reference poses and ordered parameter modes
Playback metadata - retain transition type, clock synchronization, nested placement and explicit zero duration during conversion
Retrieval profile - add opt-in authored timing and motion curves; the lab keeps this disabled pending board-target correction
Board probe adapter - connect board requests to engine mesh queries and the retained result cache
# Physics
Live map contacts - feed current board bodies and motion-aware bounds through map triangle generation into the existing board solve
Mixed contact ownership - preserve native prop and rail responses while removing duplicate stationary-map contacts
Contact telemetry - report generated contacts together with retained native contacts
# Animation
Non-jump owner ordering - connect placement, force, direction, companion, query and displacement phases for supplied owner inputs
Probe result consumption - decode completed wheel, ground and forward queries into retained motion caches
# Physics
Map triangle ingestion - connect cached engine geometry to decoded triangle construction and ordered contact batches with explicit physical body bindings
Triangle batching - preserve the twenty-triangle boundary, final remainder, descriptor padding and collector exhaustion behavior
Generated contact input - retain raw physical caches and bind stable contact identities to solver bodies before compilation
# Animation
Non-jump force connection - join angular and contact forces, queued speed correction and ordered query submission while retaining cache and queue lifetimes
# Physics
Map contact batches - connect supplied triangles and bodies through filtering, materials, speculative margins and full contact output in source order
Contact collection - preserve duplicate rejection, geometric reduction, capacity handling and ordered batch output
# Animation
Board input publication - read the physical board binding and retain derived frames and control caches before motor processing
# Physics
Primitive manifolds - connect separation, feature contacts, normal refinement, triangle filtering and radius expansion with exact output and rejection behavior
Triangle boundaries - preserve edge and vertex filtering, normal correction and contact projection for supplied flags and context
# Animation
Joined jump motor - run launch, character placement, body publication and direction/filter/tilt/history in the original order for supplied finite inputs; live scheduling and other motor routes remain unfinished
# Physics
Cylinder separation - connect cap, edge and circle candidates for all supported primitive pairs, including reversed shape order
Primitive collision dispatch - connect all five primitive kinds through axis selection, feature extraction and contact generation in supplied-input checks
# Animation
Jump character connection - retain surface context, apply launch velocity, update the riding rig and publish body velocity again in the required order; full live scheduling remains unfinished
# Physics
Circle pair candidates - preserve acceptance, bounded search and direction fallbacks for cylinder collision assembly
# Animation
Physical jump velocity - apply surface-dependent launch velocity to supplied bodies from processed trick attributes, with lateral correction and cooldown state; live authored triggers remain unfinished
# Physics
Circle and segment candidate - preserve bounded search, direction fallbacks and exact rounding for cylinder collision assembly
# Animation
Physical trick attributes - combine queued scalar requests and sampled records in order before the existing physical processor; live scheduling and motor activation remain unfinished
# Physics
Bounded cylinder candidate - preserve endpoint selection and rounding in the segment helper; complete cylinder pairs remain unfinished
# Animation
Trick output consumption - flush ordered overrides into directional playback selection and expose queue reset; live graph scheduling and physical attribute transfer remain unfinished
# Physics
Triangle and box separation axes - preserve specialized candidate and projection order for both shape orders; cylinder paths and complete live manifolds remain unfinished
Pair separation axes - preserve candidate order and arithmetic for general primitive, sphere and box pairs; triangle/box and cylinder-specific paths remain unfinished
# Animation
Trick height outputs - retain repeated queued requests and replace owner-scoped channel values through the authored behavior binder; queue lifecycle and live motor activation remain unfinished
# Physics
Polygon contact dispatch - assemble every supported point, segment and polygon pair, retaining closest-feature normals for separated faces; complete live manifolds remain unfinished
# Animation
Trick behavior binding - connect retained continuation monitors and entry-time height selection to authored lifecycle schedules while forwarding unsupported behaviors explicitly; live activation remains unfinished
### Improved
# Visuals
Generated trucks - the built-in truck has a hanger, kingpin and baseplate, so the wheels visibly hang from the deck
# Input
Walking - movement keys walk relative to the camera and the rider turns smoothly toward the walking direction instead of snapping to it
# Physics
Carving - full lock at speed now carves within the wheels' grip (SkateSettings.CarveAcceleration) instead of skidding and losing most of its speed, and steering reverses immediately
# Animation
Mounting and dismounting - the body eases between the ground and the deck instead of jumping when the step-on or runout clip starts
Smooth placement - the rider and board are shown interpolated between physics steps (Skateboard.RenderTransform), and animation inputs read from the board (speed, deck height and tilt) follow that smoothed pose, so the body never steps at the physics rate under an animation that moves every frame
Clip changes - a new clip always fades in over at least 0.1 seconds, and a clip asked for again crossfades from the current pose instead of restarting with a pop
Pushing foot - the planted foot stays where it touched the ground while the board rolls on, and the push plays at the speed that sweeps it back as fast as the board moves, so it no longer slides or shakes over the ground
Big landings - drops land with the heavy absorbing crouch instead of always using the light landing (whose hip sway read as a shift to one side); one of three landing variants is chosen at each touchdown, and the fall speed is kept from the air so a landing that touches one wheel first still reads as the drop it was
### Fixed
# Animation
Flip tricks - feet no longer pass through the board while it flips: the shown board follows the trick animation and the feet stay on the deck model's actual surface, kicktails included
Board parts - trucks and wheels stay attached to the deck at take-off, landing and while the board is carried
Flip smoothness - the legs no longer snap at the catch: an in-air foot correction that would jump several inches (the clip's foot target is only meaningful while its own foot IK is engaged) is ignored, and corrections change smoothly
Landing rock - a board rocking on its wheels just after a landing no longer replays the air pose for a frame
Pushing leg - the knee no longer snaps or jitters as the foot steps off the deck and onto the ground: the lift onto the deck and the hold on the ground now ease off instead of switching in one frame
Generated deck surface - procedural decks cannot be mesh-traced, so the deck's surface (used to keep feet on it) was never found; the generated deck now answers from its own shape
Board lookup - foot and carry placement found no board through the new display interface and silently switched off
Landing skipped - a push held at touchdown cut the landing off at once: a transition's "time-in-parent" test now counts time in the state that owns the transition, as the graph's timings (land then push after 0.1 seconds, idle variations after 3 seconds) expect
Flip dark-side flash - a flipping board passing through upside down no longer plays the dark-side idle for a frame after the trick; dark side up needs the deck upside down and steady
Knee flips - every leg correction keeps the knee bending the way the animation bends it, so a stretched leg no longer flips its knee for a frame
Board pull on the ground - the in-air foot pull no longer drags a foot that has stepped off the deck onto the ground
# Physics
Ramps - a board rolling into a quarterpipe kept barely half its height (56 of 118 inches) and twisted its trucks; it now climbs fully and returns straight
Thin ramps - a board rolling back under the open back of a bank no longer catches the sheet and stops
# Physics
Steering grip - resolve an unset shape friction override through its surface instead of making the wheels frictionless
Board contact shapes - use spherical wheels and longitudinal capsule trucks while preserving caller wheel radius
# Physics
Generated friction - accept recovered nonnegative friction coefficients without imposing the generic solver coefficient-order restriction; preserve the original values and shared arithmetic
# Physics
Contact capacity - reject overflowing polygon boundaries before committing output instead of corrupting contact metadata

## 2026-10-17
Title: Face Contacts and Trick Height
### Added
# Input
Gesture commands - load caller-supplied paths and route recognized gestures, timing quality and retained holds through the rider controller queue; full trick physics and animation remain unfinished
Ordered gestures - match supplied controller paths with retained progress, stable winner selection, timing quality and endpoint hold tracking; live trick recognition remains unfinished
# Physics
Clipped face contacts - preserve ordered edge intersections and interior vertex pairs for quadrilateral faces; general polygon fallback and live map manifolds remain unfinished
# Animation
Trick height selection - retain entry-time configured/requested height priority, attribute fallback and distinct manual/grind scaling; authored live state activation remains unfinished
# Physics
Polygon and segment contacts - clip face intersections, retain separated-edge pairs and provide replacement normals; polygon/polygon assembly and live map manifolds remain unfinished
# Animation
Trick continuation state - retain catch and follow-up request decisions across enter, update and exit; live animation-state activation remains unfinished
# Physics
Point and segment contacts - assemble ordered contact pairs with endpoint classifications and parallel overlap handling; polygon-pair assembly and live map manifolds remain unfinished
Primitive contact features - select points, segments and ordered faces for all five primitive shapes, preserving contact-side winding, strict thresholds and descriptor identities; full contact generation remains unfinished
Primitive projections - compute scalar and batched contact-geometry bounds for all primitive shapes, preserving distinct box rounding and untouched output fields; live contact generation remains unfinished
Physical shape construction - prepare sphere, capsule, box, cylinder and triangle descriptors with transforms, feature metadata and cached triangle normals; live contact generation remains unfinished
Generated contacts - prepare supplied points with deterministic runtime arithmetic, preserving stationary friction axes and estimate precision; live geometry ingestion remains unfinished
Contact preparation - the physical world now derives friction directions and relative point velocity from supplied manifold points, including stationary contacts and tiny sliding speeds; live contact generation remains unfinished
Contact registration - the shared world now enables body reporting on registration, restores base flags on removal and registers the rig's 24 canonical bodies with stable owner and bone identities
Solved contact reporting - the shared solver now exports loaded contacts after body movement, preserves normal and tangential forces separately and feeds rider contact collection; live collision ingestion remains unfinished
World contacts - route solved constraints to each rider, reject contacts within the same actor, preserve side-specific surfaces and feed the bounded result into postsolve feedback; live collision ingestion remains unfinished
Contact state - derive movement-specific reference frames and gates, then advance collision suppression between contact classification and pose feedback
Contact feedback - classify ordered body contacts, retain pin proximity, select weighted correction directions and feed postsolve feedback; live collision production and scheduling remain unfinished
Postsolve feedback - connect animation-error measurement with weighted movement history and direct or contact-filtered correction; live contact production and scheduling remain unfinished
Physical readback - retain solved body poses, velocity changes, weighted motion and feedback errors for integration checks
Map wheel contacts - refine mesh wheel hits using the selected shape triangles while preserving engine surface friction
Combined rig construction - assemble animation state, all 30 bodies and both joint sets from the loaded rig, retaining shared identities and authored response gains
Physical pose targets - construct four independent nonresponding bodies and load both selected drive presets with the rig
Authored joint construction - load structural speeds and angular locks alongside body settings and joint ranges
# Physics
Rider body assembly - construct all canonical and feedback bodies from authored geometry, masses and offsets
# Physics
Authored rig settings - load body masses, drive weights, joint ranges and contact properties alongside rig bindings
# Physics
Coordinated rig modes - reset retained feedback, refresh registered joint limits and update solver properties through one owner
# Physics
Rider body modes - switch retained masses, damping, collision properties and structural joint ranges while preserving the current pose
# Physics
Rider collision masks - preserve both authored body-pair exclusion tables and the mode that suppresses all pairs
# Physics
Compound rider body - retain both mass variants, inertia modes and the authored identity offset in body construction
# Physics
Compound mass - combine child shape mass, center of mass and principal inertia axes for physical rider construction
# Physics
Compound geometry - build the authored cylinder frame and sphere dimensions while retaining pose volume and mass input
# Physics
Feedback bodies - construct both retained capsule bodies with their distinct masses, inertia scaling, damping and disabled collision flags
# Physics
Physical body construction - build authored primitive shapes with both retained mass variants and selectable inertia adjustments
# Animation
Live drop layer - board drops now play the supplied release motion over locomotion in both stances; physical handoff timing remains unfinished
Live retrieval layer - pickup requests now play authored reach, hold and return motions over locomotion, with directional attribute queries, masked blending and reusable trees
Live raw poses - optional raw pose and attribute files now feed the rider's existing animation path; clip compatibility is checked before binding
Live interaction checks - both stances complete push, brake, dismount, pickup, carry, drop and mount with 865 captured poses; transition timing and full animation acceptance remain unfinished
# Development
Structural rig ownership - structural joints now retain ordered world membership, resolve canonical bodies by identity, compile active endpoints and release independently of unrelated joints
Structural angular ranges - cone and twist limits now compile through the shared physics world, with independent speed channels and aligned-joint handling; live ownership remains unfinished
Loaded structural rig data - optional versioned metadata now retains supplied joint orientations and angular ranges through the asset loader and canonical hierarchy; malformed or mismatched records fail during loading
Physical mass construction - sphere, capsule, cylinder and rounded-box properties now produce mass, inverse inertia and local body corrections, preserving density selection and extent floors; complete live shape binding remains unfinished
Structural joint construction - supplied body offsets and bind poses now produce compact parent-child joints with independent swing/twist limits, per-joint scaling and global mode overrides; live physical binding remains unfinished
Complete rig drive ownership - root, feedback and mapped child attachments now register together in the shared solver using physical body identity, with retained pose updates and explicit release
Physical joint initialization - supplied canonical bind poses and body mappings now produce both attachment frames and their distinct initial drive settings, preserving unmapped entries and authored tuning
Physical attachment lifetime - the shared solver retains registrations, appends replacements in solve order and releases individual slots without confusing target updates with physics membership
Connected physical poses - joint output frames and drive settings now feed the shared body solver without treating bone indices as body indices; live registration remains unfinished
Shared physical workspace - connected bodies now share solver reactions before their physical poses update; gameplay binding remains unfinished
Physical body binding - hand and target inputs retain controller motion while response flags determine whether constraints can move them; corrected the target/deck endpoint order
Physical hand connections - the board solver now uses separate hand bodies, retained attachment frames and drive settings, with reactions available to the character; live character binding remains unfinished
Connected board state - retrieval, holding, placement and release now share motion clocks and target/hand drives; 118 native operations match, while live physical body binding remains unfinished
Board placement - retains the initial orientation while applying supplied placement offsets, with reset motion state and strict range boundaries; live body binding remains unfinished
Physical board tick - joins ownership decisions with ordered distance-limit checks, state entry and motion dispatch; live physical callbacks remain unfinished
Held board feedback - calculates independent hand and reference directions with near-zero suppression and preserved body flags; live application remains unfinished
Held board constraints - retains both hand frames and selected drive settings through activation, movement and release; 96 native operations match, with live body binding still unfinished
Held board frames - calculates the hand attachment in the board's local axes using the existing pose math; live constraint binding remains unfinished
Retrieval initialization - supplies distance-based pickup duration and retained start-position adjustment, including separate mounting limits; 88 native cases match, with playable handoff integration still unfinished
Board retrieval motion - combines the supplied motion curve, progress clock and pose blending toward a moving attachment; physical actuators and playable handoff integration remain unfinished
Physical board ownership - preserves release-marker precedence, retrieval completion and contact interruption in 69 native cases; physical attachment and playable handoff integration remain unfinished
Attribute bank loading - separate versioned metadata now binds to existing pose containers, preserving encoded keys and ordered records; missing metadata fails explicitly, while empty tables remain queryable
Clip attribute queries - clip playback can now query ordered scalar, vector, curve and encoded values using retained timing and wrap history; 227 native cases pass, while live attribute-bank loading and composite querying remain unfinished
Directional parameter refresh - child changes can now refresh blend samples without a parent-input change, preserving sorted callback order and playback phase; 144 native operations pass, with live factory queries still unfinished
Reference pose trees - body composition now shares the active animation clock and auxiliary traversal without repeated composition; 175 body samples and 96 stream cases pass native checks, while live character integration remains unfinished
Pose tree ownership - pending replacements now keep body and auxiliary stream children aligned through sequence and transition reuse; 128 native ownership cases pass, while live authored-tree construction and rendering remain unfinished
Connected pose trees - sequence, transition, channel and directional nodes now share playback state across body preparation and auxiliary streams; 96 native cases pass, while live factory and renderer binding remain unfinished
Multi-input pose streams - preserves grouped weights and repeated-value reduction for up to five sampled inputs; 60 native cases pass, while live tree ownership and rendering remain unfinished
Connected auxiliary streams - blends indexed poses before reference composition and restores the reference when a stream is absent; 100 native cases pass, while live character binding remains unfinished
Indexed auxiliary poses - preserves discrete frame-clock rounding and supplies stable command-buffer snapshots; both banks pass all 124 native pose checks, while live character binding remains unfinished
Continuous pose sampling - version 5 files support adjacent-frame interpolation for auxiliary skeletons; two user-supplied banks pass 132 native pose checks, while reference composition, model binding and live renderer integration remain unfinished
Loaded pose streams - version 4 files retain paired names and signed-time keys and feed commands from the skeletal playback clock; all 3,324 clips load with unchanged pose data, while actual indexed bank binding and live scheduling remain unfinished
Authored pose streams - signed-time tracks now generate blend commands through reusable paired storage into pose buffers; 64 stream sequences, 37 lookup cases and 42 connected pose cases pass, while runtime metadata loading and live scheduling remain unfinished

### Fixed
# Animation
Moving rider instability - removed the incomplete physical pose override from the live animation path; movement and board handling use the established clip retargeting and knee correction again
# Physics
Body update precision - preserve ordered integration, orientation normalization, inertia rotation and speed limiting; captured connected-world simulations now match exactly
# Physics
Shared solver precision - preserve fused accumulation when solving structural joints and animation drives; captured responses and retained impulses now match exactly
# Physics
Joint response precision - preserve fused response accumulation and ordered axis projection as bodies move; the connected multi-step comparison now passes its numerical tolerance
# Physics
Animation drive precision - preserve endpoint response, frame basis and motion accumulation order when compiling connected body drives; remaining multi-step drift is still under investigation
# Physics
Joint frame precision - linear constraints now use stored physical axes and source-ordered prediction and effective-mass accumulation; combined multi-step precision checks remain open
# Physics
Relative rotation precision - ordered quaternion products prevent false motion at identical targets; angular-axis refinement preserves solver precision across a complete rig
Pose constraint accuracy - signed coefficients retain upper-then-lower projection, and frame composition and anchor separation preserve calculation order
Physical rotation axes - retained body axes now reach connected constraints and survive integration, resolving the strict repeated-step comparison failures
# Animation
Pose blending - preserves quaternion sign and normalization rounding after axis conversion for both pair and multi-pose blends
Pose composition - preserves the scalar rotation's addition order after axis conversion; auxiliary reference and composed poses now match all 3,264 checked float words, with live model binding still unfinished

## 2026-10-16
Title: Pose Command Execution
### Added
# Input
Playable controller - the rider now consumes retained controller requests for riding, walking and board handling from a local data profile
# Development
Pose command execution - connects compact pose selections and blend commands to reusable buffers, with frame selection retaining timing history; 89 native dispatch/timing cases and 22 connected buffer cases pass, while live scheduling remains unfinished
Reusable pose stack - prepared poses now share reusable storage with ordered multi-pose blending; 320 native multi-pose checks and existing connected-buffer checks pass, while attribute scheduling and live renderer integration remain unfinished
Connected pose buffers - sequence, transition, layer and composition preparation can now combine raw clip and constant reference outputs in preparation order; native checks cover 1,536 combinations and 175 connected samples, while live scheduling remains unfinished
Connected clip playback - joins authored clocks, pose preparation and reusable raw pose sampling; all 3,324 clips prepare in the editor and 240 backend pose comparisons pass, while live tree and renderer binding remain unfinished
Interval pose sampling - preserves cycle movement in version 3 clips and samples motion deltas from previous/current times; native checks cover 40 intervals and 512 arithmetic cases, while live graph binding remains unfinished
Raw pose buffers - retain every scale lane independently of bone influence through referenced sampling, copying and blending; 1,024 native blend cases and 175 sampled poses pass, while live version 2 graph binding remains unfinished
Referenced pose playback - connected raw sampling to the selected reference pose without repeated composition or discarded scale; 175 composed samples pass native checks, while live character binding remains unfinished
Raw pose playback - added a version 2 reader and sampler retaining uncomposed rotations, masks, scale and references; all 3,324 clip records and 175 sampled poses pass native checks, while live character binding remains unfinished
Reference pose composition - added ordered transform composition and two-child playback/preparation wrappers; 32,256 native transform checks and 224 control operations match, while live asset and interaction binding remain unfinished
Pending tree ownership - retains current and pending overlays, promotes successors before release and preserves interrupted replacement ordering; 128 native cleanup cases match, while the live asset factory and pose binding remain unfinished
Joined channel playback - connected base clocks and composite overlays with separate completion output; 2,160 native operations match, while tree release and live interaction pose binding remain unfinished
Channel envelope lifecycle - new channels reset fade clocks while replacements preserve entry history and recover partial fade-outs; 1,024 native cases match, with tree ownership and live interaction binding still unfinished
Weighted skeleton buffers - authored masks now control position and rotation blending while preserving mask history; 32,568 native values match, with live pickup/drop channel integration still unfinished
Authored bone weights - optional clip metadata now survives sampling, copying and blending; all 3,324 keyed clips pass native weight checks, while pickup/drop layer binding remains unfinished
Composite pose control - added sequence selection, transition preparation and weighted overlay operations tied to playback state; 2,304 native operations match, while pose-buffer binding and live interaction layering remain unfinished
Weighted pose blending - added bounded per-bone weights from either input pose; 33,828 native components match, with weight production and live interaction-channel binding still unfinished
Live pose blending - connected ordered position and normalized linear rotation interpolation to the character; both-stance controller chains pass with 947 captured poses, while pickup/drop layering and full animation acceptance remain unfinished
Clip pose preparation - preserves authored time ranges and consumes wrap history after sampling; 3,072 native operations match with supplied callbacks, while actual sampling and live interaction layering remain unfinished
Directional pose control - preserves child preparation order, activation gates and separate immediate/deferred weights; 1,536 native operations match with supplied callbacks, while leaf preparation and live rendering remain unfinished
Pose blending - added ordered translation/scale interpolation and normalized linear rotations with sign correction; 33,096 native components match, while pose preparation and live layering remain unfinished
Directional playback - selects authored neighbors and preserves phase through angle and rate changes; 3,520 native operations match with fixed sample metadata, while live pose layering remains unfinished
Clip timing - added elapsed and phase control, strict endpoint completion, repeated wraps and phase-preserving rate changes; 6,144 native operations match and composite handoffs have regressions, with live interaction layering still unfinished
Queued playback - added completion-driven handoff with unused frame time, ordered transition updates, phase/time matching and a separate fade curve; 3,360 native control operations match, with live layering still unfinished
Interaction fades - added retained channel entry, exit and recovery weights with playback holds; 3,072 native operations match, while queued playback and live layering remain unfinished
Board handling - added retained pickup, drop and throw phases with playback-driven exits; 2,016 native updates match, while the separate animation channel and live physical handoff remain unfinished
Compound conditions - added ordered entry/staying decisions that preserve short-circuiting and repeated condition accesses; 4,512 shared-cache and 12,032 cached native results match, while actual condition bindings and playable integration remain unfinished
Condition cache - added shared true and false results by condition index with ordered publication and validity-only invalidation; 3,288 native accesses match, while compound guards and live bindings remain unfinished
State timer conditions - added encoded-state and parent-owner lookup with elapsed-time comparisons; 456 native cases match, while live state bindings and animation timing acceptance remain unfinished
Behavior clearing - added reverse active-behavior exits while preserving timers and retained storage; 264 native snapshots match, with destruction and playable reset integration unfinished
Joined state graph - connected selection, timers, state publication and ordered behavior callbacks; 144 native updates match, with concrete behavior implementations and playable integration unfinished
Behavior update schedules - added ordered per-state operations with shared instance publication; 114 native schedules match, with concrete behavior bodies and playable integration unfinished
State behavior lifecycle - added retained timers, ordered entry and exit callbacks, null instances and explicit transition actions; 88 native sequences match, with playable orchestration unfinished
Controller request conditions - added presence checks, numeric comparisons and retained owner context; 893 native predicate cases match, with live control integration unfinished
State transition paths - added retained, exited and entered ancestor planning with explicit reset boundaries; 254 native path cases match, while live lifecycle integration remains unfinished
Motion state selection - extended native comparison to 16,712 selections, including retained-ancestor reselection, with supplied condition values
State selection - added hierarchy-wide transition priorities and entry/staying guards; 1,880 native selections match with supplied condition values, while live graph lifecycle integration remains unfinished
Controller frame lifecycle - added request clearing, suspended-input history, queue consumption and forced brake/bail requests; 197 native updates match, with live movement integration unfinished
Controller sampling - added six analog channels and twelve button positions; controlled engine input matches 905 router updates and every button combination, with live movement and physical-device acceptance unfinished
Joined controller requests - connected retained input, timers and all request groups in one ordered update; 905 native updates match, with live gameplay and device integration unfinished
Queued controller requests - added trick restrictions, one-use hold requests and ordered action collection that preserves the first value; 841 native updates match, with live controller integration unfinished
Walking stick requests - added horizontal facing response, obstruction projection and independent stick strength; 765 native updates match, with live locomotion integration unfinished
Riding stick requests - added directional turning, overlapping slides, crouch pressure and independent grind controls; 615 native updates match, with live controller integration unfinished
Riding controller requests - added separate pushing feet, hold windows, stance-based automatic pushing, grabs, brakes and mount restrictions; 489 native updates match, with live controller integration unfinished
Object controller requests - added directional response curves, independent translation and combined-stick rotation bounds; 391 native updates match, with live object interaction unfinished
Additional controller requests - added manual pressure, handplant controls, dismount edges, gestures and bail chords; 343 native updates match, with live gameplay integration unfinished
Off-board controller requests - added jump edges, full-pressure board-handling requests, looking and body control; 239 native updates match, with live locomotion and device integration unfinished
Controller state - added analog samples, separate button timers and contextual bumper selection; 328 native updates match, with gameplay routing and device integration unfinished
Hand owner requests - added retained side selection, retry limits and deactivation resets through generated contacts and pins; 246 native updates match, with character phase scheduling and live integration unfinished
Generated hand interactions - connected source requests and map queries through contact projection, fading and both hand pins; 195 native updates match, with live rider integration unfinished
Hand candidate pipeline - connected generated surface requests, mesh investigation and one-use candidate evaluation; 195 native updates and 426 triangle queries match, with live rider scheduling unfinished
Hand request preparation - connected contact reset, eligibility, source production and surface probes; 88 native updates and 366 triangle queries match, with query completion and live scheduling unfinished
Complete static hand sources - connected lookup, surface frames, direction requests, reach prediction and selection into retained world contacts; 80 native updates match, with submission and live scheduling unfinished
Hand reach regions - added speed-driven velocity bounds and four predicted apex corners; 103 native cases match, with joined source lookup and live integration unfinished
Hand source selection - added reach-region clipping, height/depth ranking and connected-edge contact placement; 104 native cases match, with reach-region production and live integration unfinished
Joined hand source lookup - connected static edge queries to surface frames and paired edge filtering; 36 native cases match, including capacity before rejection, with final contact selection unfinished
Hand source geometry - added surface frames and paired world/local edge filtering with retained flags and capacity limits; 29 native cases match, with lookup and final selection unfinished
Hand source eligibility - added strict speed, upward-motion and surface-angle checks using shared angle math; 120 native cases match, with source selection and live scheduling unfinished
Joined hand interactions - connected activation, retained candidates, hand selection and reset flags through contact projection to hand pins; 64 combined native updates match, with live scheduling unfinished
Hand contact candidates - added geometry-based trajectories, attachment frames and scores with one-use query results; 105 native updates match, with source geometry production and live scheduling unfinished
Retained hand contacts - connected hand selection, target projection, radius contraction and weight fading to hand-pin production; 260 native updates match, with candidate generation and live scheduling unfinished
Hand pin targets - added bounded requests around raw animated arm anchors with independent hand slots; 96 native updates match, with upstream contact production and live scheduling unfinished
Animation-to-rig inputs - connected retained authored poses, cached frames and current contact flags; 56 ground replays match, with live graph scheduling unfinished
Combined animation update - joined rider-body preparation, post-attribute attachments, cache publication and reset drives; 72 complete native replays match, with live graph and physical-character handoff unfinished
Animation pose inputs - connected independent motion extraction, mapped contact bones and retained pose publication; 72 native stage replays match, with full physical-body and live rider integration unfinished
Physical animation inputs - added ordered attribute processing, bone-selected contacts and retained jump controls; 432 native comparisons match, with live graph and rider integration unfinished
Airborne character update - connected placement, board targets, velocity feedback and the physical rig while preserving the next placement anchor; 80 native updates match, with live input production and animation integration unfinished
Airborne placement - added predicted-position anchoring, retained local rotation and orientation resets; 52 native updates match, with live movement and animation integration still unfinished
Ground character update - added heading refresh, physical-deck rig targets and state-specific output flags; 56 native updates match, with live rider adapters and scheduling still unfinished
Joined riding update - connected deck-based placement, root publication, feedback targets, board handoff and the physical rig; 60 native updates match, with live movement scheduling unfinished
Rider placement frames - added deck-based anchor prediction, retained anchor overrides and heading resets; 37 native updates match, with movement scheduling and live animation integration unfinished
Board target drives - added independent position and angle control, speed-limited following and release; connected a separate immovable target to the board solver, with gameplay input scheduling still unfinished
Physical body initialization - resets reused body motion, mass properties and accumulators while preserving supplied defaults; 30 native cases match, with live body allocation and rider integration unfinished
Character update - joined placement, shared frame ownership, board target and velocity feedback, and the complete physical rig sequence; 56 native updates match, with live renderer/body adapters still unfinished
Board frame handoff - writes the physical target on every update, including disabled correction and release; 73 retained native updates match
Board body feedback - derives shared velocity from the reference body's position and timestep; 18 native cases match, with live rider scheduling unfinished
Board orientation correction - added curve-driven retained rotation and release behavior; 86 native comparisons match, with physical target handoff and live rider integration unfinished
Character placement - added weighted-pose anchoring, world/model transforms and helper-frame resets; 27 native comparisons match, with the board-frame provider and live renderer integration unfinished
Character feedback targets - added retained height changes and normalized paired targets before the complete physical update; 168 producer updates and 56 joined native updates match, with live board-frame integration unfinished
Physical rig coordination - connected preparation, reset, rig solving, joint output and body correction across retained updates; 56 native updates match, with live rider integration unfinished
Physical pose reset - added full skeleton reset, retained animation-cache handling and mass-scaled correction; 26 native comparisons match, with complete rider scheduling unfinished
Physical pose preparation - added body target transforms, inverse inertia updates and bounded feedback; 46 native comparisons match, with live rider integration unfinished
### Improved
# Development
Body frame recovery - rebuilds one missing axis and preserves cached target transforms independently of physical normalization
### Fixed
# Animation
Push exits - per-clip looping metadata keeps one-shot blend leaves at their final pose while transitions finish, preventing an observed end-to-start leg jump
Pushing knees - preserve the animated bend plane during board foot placement; a reproduced lower-leg flip falls from 173.5 to 2.6 degrees in the exact pose replay, with broader animation acceptance still open

## 2026-10-15
Title: Leg Stability and Walking Animation
### Added
# Development
Physical joint output - added pose-derived attachment frames, masked strength blending and retained joint drives; 41 native comparisons match, with live rider-body integration unfinished
Board query inputs - added pose and motion input assembly with flag-controlled direction counts and preserved caller overrides; 161 lifecycle snapshots match through both geometry routes, with live movement-controller integration unfinished
Initialized contact lifecycle - verified input preparation through consecutive selection and map-query delivery; 133 snapshots match on both geometry routes, with live movement-state production and attachment activation unfinished
Query input preparation - added the pending-request guard, selection resets and orientation-dependent velocity conditioning; 55 native comparisons and ten regressions pass, with live movement-state production unfinished
Consecutive candidate updates - connected query completion and full candidate metadata to subsequent selection through cached map meshes; 90 Core and 90 engine-mesh snapshots match, with live riding integration unfinished
Final candidate selection - connected scoring, bias, retained trajectory output and repeat submission; 22 native comparisons and twelve regressions pass, with result transfer and live riding integration unfinished
Trajectory request construction - connected direction fans, origin curves, duration and retained-trajectory reuse to request queues; 60 native comparisons and ten regressions pass, with live selection and repeat scheduling unfinished
Trajectory request queues - connected ordered submission, owner cancellation and completed results to cached map geometry; 140 native target comparisons and eleven regressions pass, with live request construction and riding integration unfinished
Trajectory map queries - connected curved-motion segments, sample-time reconstruction and nearby-face normals to cached geometry; 108 Core and 107 engine-mesh comparisons pass, with live request submission and riding integration unfinished
Triangle bounds - added closed-box overlap tests for trajectory surface collection; 322 native comparisons and ten regressions pass, with trajectory queries and live scheduling unfinished
Query-direction fan - added seven-slot direction generation with speed limits, curve-scaled spread and retained unused slots; 88 native comparisons and eleven regressions pass, with query submission and live scheduling unfinished
Combined candidate scoring - connected production, map rejection, distance fallback and directional/timing scores; 80 engine-mesh comparisons and eighteen regressions pass, with final selection and live activation unfinished
Edge-distance fallback - preserved infinite-line projection, endpoint distances, short-edge gates and ordered score curves; 85 native comparisons pass, with full scoring and live selection unfinished
Fallback map rejection - connected direction gates, downward mesh queries, penalty scores and vertical correction; 85 native mesh comparisons and 24 regressions pass, with full scoring and live selection unfinished
Candidate score bias - added signed sample gates, predicted-height comparison and retained bias accumulation; 66 native sequences and twenty regressions pass, with full scoring and live scheduling unfinished
Selected trajectory preparation - added anchored position and curve-scaled velocity correction while retaining other trajectory data; 85 native comparisons and fifteen regressions pass, with final selection and live scheduling unfinished
Selected-contact capture - added suppression, selected-index capture and repeat-selection eligibility with signed count and angle boundaries; 107 native comparisons and nineteen regressions pass, with live scheduling unfinished
Candidate production - connected predicted static-edge lookup to map-based acceptance while preserving separate scores and retained flags; 56 native mesh comparisons and seven regressions pass, with final selection and live activation unfinished
Candidate acceptance - connected ranked samples, map queries, rejection and retry, correction and output normals; 44 native mesh comparisons and twelve regressions pass, with live inputs and activation unfinished
Accepted contact normals - added velocity bias, signed tilt limits and edge-up fallback; 133 paired native comparisons and fifteen regressions pass, with whole-candidate acceptance and live activation unfinished
Contact velocity correction - added sample-time conversion and signed speed limiting; 87 native comparisons and fourteen regressions pass, with complete candidate acceptance and live activation unfinished
Map contact frames - connected surface investigation to contact normals and approach limits; fifty-six native comparisons and eight regressions pass, with candidate acceptance and gameplay activation unfinished
Map surface investigation - connected edge probes, cached map geometry and classification; thirty-three native comparisons and nine reuse/filter regressions pass, with gameplay scheduling and material ingestion unfinished
Map query filters - added per-shape filter IDs and exclusion masks, with seventeen native checks; gameplay ownership assignment and live wheel integration remain unfinished
Map geometry cache - added explicit collider refresh, body-frame updates and coordinate conversion; twelve lifecycle checks and 22 sampled map rays pass, with refresh scheduling, materials and wheel integration unfinished
World mesh dispatch - added segment gates, instance filters, transformed query bounds and padded mesh pruning; 127 native contacts and 22 sampled scene rays pass, with persistent geometry updates and wheel integration unfinished
Mesh contact selection - added transformed triangle queries, surface filters and ordered nearest-contact selection; 109 native comparisons and 22 sampled live-map ray comparisons pass, with world dispatch and wheel integration unfinished
Rounded triangle queries - added face, edge and vertex traversal with margin handling and retained failure output; 542 native comparisons pass, including small geometry, with world dispatch and live map integration unfinished
Rounded contact roots - added vertex and infinite-edge roots with distinct endpoint and overlap behavior; 139 native comparisons and fifteen regressions pass, with live map integration unfinished
Triangle ray contacts - added directed triangle queries with boundary tolerances and retained failure output; 73 native comparisons and sixteen regressions pass, with rounded queries and live map integration unfinished
Contact directions - added kind-specific approach limits and distinct failed-query/invalid-contact handoffs; 274 native comparisons and fourteen regressions pass, with world dispatch and gameplay integration unfinished
Surface normal orientation - added angular-limit normal selection and explicit invalid results for inactive investigations; 276 native comparisons and twelve regressions pass, with live scheduling unfinished
Surface hit classification - added ordered rail/ledge/blocked decisions, materials, retained flags and contact-angle limits; 229 native comparisons and 28 regressions pass, with live query integration unfinished
Surface probe geometry - added ordered cross-section and optional downward probes with incline handling and degeneracy gates; 47 native comparisons and ten regressions pass, with collision dispatch and classification unfinished
Contact sample population - joined trajectory sampling and ordered selection, preserving source edge indices across rejected samples and pending-list rebuilds; 36 native builds, 110 selections and nine regressions pass, with gameplay integration unfinished
Edge contact ranking - added ordered selection and removal with height/angle curves and distinct empty/no-winner output behavior; 375 native calls and eleven regressions pass, with pipeline integration unfinished
Predicted edge contacts - added trajectory-plane crossings, bounded edge projection and retained sample data; 93 native comparisons and twelve regressions pass, with ranking and gameplay integration unfinished
Static grind trees - added insertion, leaf splitting and redistribution with separate chunk and tree bounds; 26 native comparisons and ten regressions pass, with live geometry production still pending
Static grind queries - added ordered loose-tree collection with reused-buffer flag preservation; 69 native comparisons and twelve regressions pass, with live map integration still pending
Grind edge records - added transformed endpoints and horizontal-span classification; 68 native comparisons and nine regressions pass, with live map input production still pending
Grind edge filtering - added ordered suppression of nearby overlapping edges; 76 native comparisons and twelve regressions pass, with automatic map-edge production still pending
Attachment update order - joined hand, grind/descending, vertical and retained application stages; 144 native updates and nine regressions pass, with live geometry and gameplay scheduling still pending
Complete grind attachment - joined prediction and angular correction with final board-pivot alignment and request gates; 147 native comparisons and thirteen regressions pass, with live geometry/input integration pending
Grind angular correction - added candidate-specific pitch, capped yaw, roll replacement and retained angular position; 94 native comparisons and sixteen regressions pass, with map-edge integration pending
Grind approach prediction - added supplied-board trajectory sampling, edge crossing, candidate selection and positional correction; 94 native comparisons and fifteen regressions pass, with map-edge integration pending
Gait checks - tests cover walking input, board possession and named condition arguments
Leg stability checks - regression cases cover straight legs, hip-coincident targets, knee hint continuity and tilt transitions
Tilt controller - added a curve-driven per-step controller with acceleration limits, activation resets and stance handling; physics-frame integration remains pending
Limb position solver - added configurable polynomial math, animated bend-plane reconstruction and target retries, with separate native verification and ten regression tests
Limb rotations - added segment-frame rotation and translation with degenerate-frame rejection, 288 native comparison cases and ten regression tests
Limb chain solver - joined reach limiting, position solving and bone updates, including residual displacement and distinct failure paths; twelve new regression tests and native comparisons cover the combined behavior
Effector weights - added per-invocation foot and hand weighting, reach-box fades and pinned/released overrides, verified with 360 native comparisons and twelve regression cases
Effector modes - added pin/release transitions and independent foot/hand defaults, verified with 484 native comparisons and twelve regression cases including sequential weight updates
Effector targets - added pinned/releasing pose preparation, accumulated release offsets, orientation decay and linked-bone targets, verified with 202 native comparisons and eleven regression cases
Pose preparation - added the four-effector preparation stage, inclusive reach bounds and preserved inactive/reference caches, verified with 313 native comparisons and sixteen regression cases
Pose blending - added configurable pose interpolation, half-turn axis recovery and separate transform translation blending, verified with 196 native comparisons and fourteen regression cases
Four-effector blending - combined authored and animated pose normalization, root-frame positioning, linked-pose blending and pin/release overrides, verified with 149 native comparisons and nineteen regression cases
Foot reach adjustment - added cached-contact height smoothing, clearance correction and retained/per-update status flags, verified with 250 native comparisons and twenty-two regression cases
Final bone application - added hierarchy/bind-pose chain selection, linked-bone placement and residual cache updates, verified with 129 native comparisons and eighteen regression cases; live rig integration remains pending
Rig sequence - joined the six stages over persistent state, preserving previous-update hand positions and request ownership; 108 native update comparisons and eleven regression cases cover the combined controller
Rig frames - added supplied-frame and animated-root frame selection, checked against 134 native cases and eight regressions; live transform producers remain pending
Skeleton initialization - added explicit clip-to-solver mappings, parent links through unmapped bones, ordered child links and bind transforms; 36 native comparisons and thirteen regressions pass, with live rig integration pending
Pose inputs - separate untouched animation bones from bind-composed solver poses and helper targets; 40 native comparisons and ten regressions pass, with live pose-cache integration pending
Pose cache - added independent mutable-bone snapshots, weighted position changes and root-relative displacement history, with first-update suppression; 48 native comparisons and thirteen regressions pass, with live input and update-order integration pending
Pose weights - added ordered scalar normalization with 40 native comparisons and eleven regressions, preserving zero-total and overflow behavior; physical weight production and gameplay integration remain pending
Bind-data checks - verified both supplied rig datasets through hierarchy initialization, including separate toe endpoints and linked ankle references; runtime data loading remains pending
Rig metadata loading - graphs can supply named solver bones, bind transforms, body dimensions and separate helper/endpoint/reference bindings; the native asset loader matches both reference datasets
Rig coordinate conversion - preserves unnormalized quaternion and affine matrix data across proper signed axis permutations; 21 new regressions cover conversion, name resolution, metadata ownership and invalid inputs
Board attachment controller - added hand/helper alignment, independent vertical decay and retained frame countdowns; 108 native cases and thirteen regressions pass, with gameplay handoff integration pending
Vertical attachment requests - added phase/history-driven height correction, supplied response curves, independent request cancellation and forced release handling; 322 native comparisons and sixteen regression cases cover the controller, with gameplay input integration pending
Descending attachment requests - added blended board alignment with separate vertical correction and preserved pending requests when inactive; 140 native comparisons and thirteen regressions pass, with gameplay input integration pending
### Fixed
# Animation
Carried boards - follow the configured animated hand and board orientation, including the mirrored stance, instead of staying at a fixed offset
Board interactions - preserve the board object's scale while moving it during pickup and drop
# Animation
Braking transitions - clip expiry now follows actual playback and blending, preventing the reproduced intro wrap and sudden ankle jump; both stance interaction chains pass again
# Input
Push feet - explicit left and right requests now survive the animation handoff and client-to-host input forwarding instead of selecting the same foot
# Grinding
Rail collection - full marked polylines now feed the tree query and overlap filter, with forty board candidates and cache rebuilds when world endpoints change; native 50-50 and boardslide checks pass
# Animation
Foot targets - authored board-relative helper bones drive feet when present, including mirrored poses; clips without helpers keep their mapped toes
Walking states - walking speed and input reach the state graph independently of skateboard steering
Carry poses - carrying reports board possession so the graph can select its holding-board idle and walking clips
State conditions - named arguments select the matching gait instead of treating every gait as the same boolean
Knee stability - hint blending no longer switches bend planes abruptly; degenerate targets and straight vertical legs keep valid transforms
Tilt poses - crossing the wall-tilt threshold blends the body pose instead of replacing it in one frame
### Known Issues
# Animation
Live acceptance - the reproduced braking wrap is fixed, while pickup hand alignment, physical-device testing and complete animation acceptance remain open
# Animation
Map edge discovery - rolling and deck contacts use scene collision, but grinding still requires marked rail components; automatic discovery of grindable mesh edges is unfinished
Push contact - the standing foot check passes with the current local clip set, but the pushing ankle does not meet the floor-height gate
Board handoff - pickup/drop timing and held-board placement remain estimated; selecting carrying clips does not yet align the board with the hand
Terrain gait - the adapter supplies flat-ground gait categories; slope and stair classification remain unfinished
Tilt integration - the verified parameter controller is not yet connected to gameplay; the current roll-based adapter remains an estimate
Limb integration - the verified complete rig controller still needs live request/pose/contact bindings and update cadence before it can replace the gameplay IK

## 2026-10-14
Title: Grinds, Pumping, Multiplayer and Clip Animation
### Added
# Animation
Connected attribute queries - animation trees now retain child selection, overlay fallback, directional value blending and partial results on failed queries; 336 native cases pass, while multi-input queries and live character integration remain unfinished
Clip sets - SkateRider plays a clip set and tagged graph from the data folder, retargeted onto its avatar
Animation graph - priority states choose the best-matching tagged motion with crossfades, variant rotation and steering lean blends
Retargeting - limbs follow the source pose's directions regardless of rest pose; leg IK keeps soles on the deck or ground
State graphs - a graph can carry a hierarchical state graph (entry/staying tests, transitions, priorities, non-interruptible states, mirror and switch-stance clips) that decides what plays from rider intents and physics, so activities follow authored rules such as braking only while moving
Blends - a graph can define named blends (1-D, keyed select, threshold switch) over clips; motions may name a blend, which mixes its clips in phase from numeric parameters the director publishes
Foot contact - each clip's lowest foot height on and beside its deck is lined up with the real deck top and ground, so pushing feet reach the floor and standing feet neither float nor sink
Any humanoid - clip animation drives any humanoid CharacterModel: bones are found by rig naming presets, name words or the hierarchy, and the model's facing, size and A- or T-pose are measured from its rest pose
Animation director - riding, pushing, braking, slides, windups, tricks, landings, manuals, grinds, walking, carrying, mounting, dismounting, pickup and drop drive the graph
PlayAnimation - any tagged motion can be requested from code for a set time
Stance mirroring - riders whose stance differs from the clips' authored stance are mirrored
Activity sequencing - looping activities play an intro, a cycle and an exit; leaving the ground cuts exits short
Air grabs - SkateInput.Grab and two grab actions play nose, tail, mute, stale, crail and other grabs in the air
Gestures - SkateGesture cycles the clip graph's gestures; AnimationActions lists what any family offers
Get-ups and runouts - recovering from a bail plays a get-up matching how the rider lies; fast dismounts run out
Bumps - deck impacts that do not bail play a short knock
Styles - AnimationStyle prefers a style set across pushes, idles, manuals, grinds, tricks and grabs
Pop height - holding the windup longer plays the high pop and air clips
# Skating
Flip and spin tricks - rolling the stick during the windup adds board spin: varial kickflip and heelflip, hardflip, inward heelflip, 360 flip, laser flip, frontside shuvit and 360 shuvits, each with its own flip and spin
# Skating
Grinds - GrindRail marks rails, ledges and coping; boards lock into 50-50, 5-0, nosegrind and boardslide with grind friction and rider balance, and can ollie out
Grind events - GrindStarted, GrindEnded and SkateState.Grind report the current grind
Pumping - holding SkatePump through a transition adds speed; it adds nothing on flat ground
Collision event - Collided reports deck impacts with their approach speed
# Multiplayer
Host authority - the host simulates boards, clients forward input to the board's driver and read synced speed, wheel count, bail, balance and grind state
# Development
Skate Debugger - draws wheels, deck, contacts, velocity and state while playing
Skate Replay - records inputs and replays them from the same start, reporting the largest deviation; replays are deterministic in the lab
Benchmarks - per-board step timing and a Core benchmark; one board costs about 0.4 ms per 60 Hz tick
Code facade - Skating.CreateRail adds a grind rail from code
### Fixed
# Animation
Push foot - the pushing foot now touches the ground through the whole push
Held push - holding push plays one continuous push cycle instead of restarting
Idle legs - knees no longer flip when a leg straightens, and the standing stance no longer swaps clips each loop
Feet in the deck - soles stay on the deck top and grounded feet stay outside the deck
# Physics
Thin ramps - the board no longer catches the underside of one-sided map meshes and sinks through the floor on a quarterpipe return
Rider ragdoll - a bailed rider's own ragdoll no longer pushes their board through the floor
Resting corners - a deck corner that comes to rest past a thin wall is pushed back out
Ramp heading - riding straight up a faceted quarterpipe no longer turns the board about one degree; the solver no longer favours the left wheels
### Known Issues
# Physics
Hard carves - a hard carve at speed loses slightly more speed than before (5.7 to 2.6 m/s in the lab carve)

## 2026-10-13
Title: Native Skateboard
### Added
# Skating
Bails - hard impacts, unsafe landings and the bail action release the rider into a native ragdoll with incoming linear/angular motion and unload the board
Recovery - a settled rider can stand in clear space, pick up the board, drop it and remount
Manual balance - partial right-stick input or keyboard actions lift one axle using rider torque, then return to four wheels on release
Balance state - SkateState.Balance reports physical rear or nose balance after the opposite axle clears the surface
Articulated board - contact, joint, drive and integration routines now move the deck, trucks and four wheels against scene collision geometry
Physical wheels - generated and custom wheel visuals now follow the simulated body poses
Map contacts - four wheel traces use the active scene's collision meshes and moving surfaces
Board models - generated deck, trucks and rolling wheels, plus custom deck and wheel assignments
Rider - walking, pickup, drop and remount APIs with Citizen animation and foot targets
Controls - keyboard and controller input with estimated handling presets
# Development
Physics checks - 528 core test cases and an isolated native replay scene
Physics telemetry - on-demand snapshots expose all seven bodies, contact geometry, joint error and kinetic energy in SI units
Contact solving - static and sliding friction projection with coupled linear, angular and correction responses, covered by fixed numeric fixtures
Contact preparation - inertia, friction and correction rows, tested with tilted contacts and static bodies
Body integration - pose update, damping, speed limits and low-motion tracking
Joint and drive solving - coupled linear/angular joint and drive responses solved after contacts each pass
Joint anchor setup - translational limits and motion allowance with rotated bodies and offset anchors
Wheel axle constraints - quaternion axle joints; an isolated spinning wheel stays supported under gravity at 30, 60 and 120 Hz
Drive preparation - position, spring and speed-limited responses, including exact half turns; spring settling and acceleration caps are tested
Contact input conversion - contact velocity and acceleration inputs support falling wheels and transfer moving-surface motion at three rates
Source checkout - generated engine asset caches are excluded from version control
### Fixed
# Physics
Dynamic contacts - native solid response is disabled while the board solver owns the deck, preventing a second infinite-mass response to props and ragdolls
Moving props - overlap queries recover the actual contact face instead of treating the prop's top as ground
Rotating contacts - vertex motion queries and separate terrain support reduce missing floor contacts beneath a ragdoll
Wheel support - multiple surfaces contacting one wheel count once and opposing normals remain finite
Truck steering - stronger estimated drives resist wheel loads instead of allowing large unintended truck turns; native ramp drift and speed retention improve
Rolling contacts - sweep travel no longer enters friction as a tangential contact offset; wheel spin matches forward speed and radius in the native push run
Thin-wall collision - multiple deck contacts stop the native 30 m/s test at the board nose
Overlap recovery - connected bodies separate together without converting correction into launch velocity
Mesh transitions - lifted probes reject back-side contacts that could throw the board off a thin ramp
Carry lifecycle - releasing the skating adapter restores native motion before pickup freezes the carried board
Pickup rejection - an unreachable or fast-moving board is rejected before disabling or resetting its simulation
Walking input - partial movement input uses proportionate stopping friction and more responsive controller acceleration
Flat-ground stability - wheel suspension now solves all four loads together
Low-rate stability - removed duplicate angular damping that destabilized a thin deck at 30 Hz
# Presentation
Carry hand - the wrist follows the model's hand frame with fingers directed toward the board edge
Rider feet - generated targets use the model's ankle orientation and height so shoes sit on the deck in regular and goofy stance; authored targets remain unchanged
Materials - generated geometry loads the built-in vertex-color material
Default rider - built-in shirt, jeans and sneakers are applied with the native Dresser
### Known Issues
Manual coverage - flat entry/hold/release passes; curves, trick chaining and weight-shift poses remain unfinished
Rider and custom rigs - two complete carry/walk/drop/remount cycles and stationary-bail recovery pass; obstructed interactions, custom rigs and varied terrain remain unfinished
Collision coverage - the unchanged quarterpipe return and wall settling gates currently fail after bail/contact changes; broader collision stability remains unfinished
Handling - steering drift and transition speed loss still need tuning
Advanced mechanics - grinds, pumping, full ragdoll/landing stability and multiplayer are incomplete
