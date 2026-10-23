using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core.Animation;

namespace SkateMotion.Engine;

/// <summary>
/// Plays clip-set animation on a skinned model: the director picks motions from gameplay, the graph samples
/// them, and the retargeter writes final bone transforms as overrides. Any humanoid model works: its bones are
/// found by name and hierarchy, and its facing and size are measured from the rest pose. The model is placed at
/// the clip root, which is the board frame while riding and the rider's feet otherwise.
/// </summary>
internal sealed class ClipRiderAnimation
{
	private readonly SkinnedModelRenderer _renderer;
	private readonly GameObject _rider;
	private readonly GameObject _board;
	private readonly MotionGraph _graph;
	private readonly StateGraphPlayer _states;
	private readonly BoundStateGraphTreePlayer _treeStates;
	private readonly StateFacts _facts = new();
	private readonly RiderStateFacts _riderFacts = new();
	private readonly RiderAnimationDirector _director;
	private readonly AnimationParameters _parameters = new();
	private readonly SkeletonPose _pose;

	// The state graph and the requested-motion graph each pose into their own buffer, and a change from one to the other
	// is eased: a request (a bump, a gesture, getting up) and the graph show quite different poses, and swapping one for
	// the other in a frame turned the whole body round at once. The graph keeps running under a request, so the rider
	// comes back to where gameplay has got to.
	private readonly SkeletonPose _statePose;
	private readonly SkeletonPose _motionPose;
	private readonly System.Numerics.Vector3[] _sourcePositions;
	private readonly System.Numerics.Quaternion[] _sourceRotations;
	private float _sourceTime = SourceBlend;

	// The graph's top-level placeholder, entered only when no other state can be: its clip keeps whatever default
	// animation the original host had, so the body keeps its last pose instead.
	private const string Placeholder = "Default";
	private bool _shownRequest;
	private bool _sourceShown;

	// Whether the requested-motion graph was run last frame: run again after a frame or more without, it starts afresh.
	private bool _graphRunning;

	// Seconds a change between the graph and a requested motion is eased over.
	private const float SourceBlend = 0.2f;

	// The locomotion pose with the board-handling overlay on top. The locomotion players blend each change of clip from
	// their own last pose; given one the overlay was already laid over, a change under the overlay would lay it on twice
	// and the masked bones (spine, arms) would jump.
	private readonly SkeletonPose _layered;
	private readonly BoardHandlingAnimationLayer _handling;
	private readonly CarriedBoardAttachment _carryAttachment;
	private readonly HandPoseLayer _fingers;

	// A breath laid over the clips' pose while riding on the ground, so a held pose never looks frozen.
	private readonly RiderBreathing _breathing;
	private readonly ClipSet _clipSet;
	private string _requestedClip;
	private float _requestedTime;
	private RiderActivity _previousActivity;
	private readonly PoseRetargeter _retargeter;
	private readonly int _anchor;
	private readonly int _carryHand;
	private readonly int _mirroredCarryHand;
	private readonly System.Numerics.Vector3[] _positions;
	private readonly System.Numerics.Quaternion[] _rotations;
	private readonly (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius)[] _feet;
	private readonly float[] _footWeight;

	// Current push of each foot out of the deck (inches along the deck's up axis).
	private readonly float[] _deckPush;

	// Feet still rising onto the deck at MountRise from getting on: they finish rising at that rate once riding.
	private readonly bool[] _rising;

	// On foot, how far (inches) each foot is lifted out of the ground now: a lift is taken up and let go at FootLiftRate
	// (inches per second), so a clip starting with its feet below the ground (a landing) does not snap the legs straight.
	private readonly float[] _footLift;
	private const float FootLiftRate = 75f;

	// Current ground-plant shift of each foot (inches, up positive).
	private readonly float[] _plant;

	// How much higher (inches) the deck ridden stands over the ground than the clips' board over theirs, and how fast
	// (per second) it is followed: a foot the clip puts down on its ground has that much further to reach.
	private float _groundRise;
	private const float GroundRiseRate = 4f;

	// Ground lock of each foot: whether it is locked, how fully (eased), and where it touched down (world).
	private readonly bool[] _locked;
	private readonly System.Numerics.Vector3[] _kneeHint;
	private readonly System.Numerics.Vector3[] _ball;
	private readonly bool[] _ballSeen;
	private readonly float[] _lock;
	private readonly Vector3[] _lockPoint;
	private readonly System.Numerics.Vector3[] _footCorrection;

	// Where each foot's pull last put it (the shown board then, the foot's place in the world, the clip's own ankle in the
	// model's frame, when): on the ground the pull rides with the shown board as it moves under the rider, from a record
	// no older than CarryGap seconds.
	private readonly (Transform Board, Vector3 Target, System.Numerics.Vector3 Ankle, float At)?[] _footCarry;
	private const float CarryGap = 0.1f;

	// Off the board, where each foot's ankle was last shown, against the body (the model's frame) and on the board as drawn
	// then. Getting on it is taken from the board when the board shown riding is within EntryBoardJump (inches) of where it
	// was drawn, so it moves on with a rolling board, and from the body otherwise (a board put down from the hand jumped 5.9
	// in under the feet in a frame; held in the world instead, the feet of a rider running onto it fell 1.8 in behind the
	// body). Each foot then steps from there to its riding place over EntryTime
	// seconds, carried with the board from the first frame riding (_entryAt, in the shown board's frame): the pull onto the
	// deck is taken up in full at once and the foot goes over to it as a smooth step, lifted off the deck by up to
	// EntryLift inches (all of it once the step goes EntryLiftFull inches or more) so it is set down rather than slid. The
	// clips' own feet move on for a fifth of a second after riding begins (a drop-in's stand hands over to its ride before
	// it ends), so the step lasts past that. Dropping in, the front foot's lift off the ground and its push out of the deck
	// both ended in the first frame riding: it fell 5.7 in through the deck and popped back out 4.3 in in a frame six frames
	// later; eased over instead it glided 11 in along the deck at up to 65 in/s, and stepped over a time set by the way it
	// first had to go it still went 10 in in 0.15 s. A foot further than EntryReach (inches) from its riding place is not
	// stepped.
	private readonly System.Numerics.Vector3?[] _entryFrom;
	private readonly Vector3?[] _entryOn;
	private readonly float[] _entryWay;
	// The step moves along the deck quickest at first, so the leg is not left stretched behind a body the clip moves on,
	// and over the deck in an arc: from the start's height to the riding place's, lifted by EntryLift and by EntryOver of
	// the height between them, so a back foot standing past the tail's end steps up onto the tail (stepped along the deck
	// as it rose, it went through the kicked tail, its ball 4.5 in under the top) and no foot rests just over the deck
	// while it moves along it (rising as it dropped to a lower riding place, a front foot slid 65 in/s). Only a board the
	// clips set down off the hand is stepped onto (a drop-in, at its lip): getting on from the hand or from the ground has
	// its own way onto the deck, the feet rising out of it at MountRise as the pull fades in, and stepped there the legs
	// snapped 3 to 6 in as the board eased from the hand under them.
	private const float EntryOver = 0.5f;

	// A step up onto a higher riding place (a back foot standing past the tail's end, onto the tail) rises to it by EntryRise
	// of the way through, ahead of moving along the deck; arced instead, the back leg reached up behind the body and its
	// knee turned 22 degrees in a frame on a short deck. Whether a foot steps up is settled on its first frame.
	private const float EntryRise = 0.4f;
	private readonly bool[] _entryUp;
	private Transform? _entryBoard;
	private const float EntryBoardJump = 2f;
	private readonly Vector3?[] _entryAt;
	private readonly float[] _entryAge;
	private readonly float[] _entryLift;
	private const float EntryTime = 0.35f;
	private const float EntryLift = 1.5f;
	private const float EntryLiftFull = 2f;
	private const float EntryReach = 16f;

	// A foot standing on the deck stays where it stands on it (in the shown deck's frame) for as long as the clip keeps it
	// standing on its board, as a skater's feet do. The clips' stances differ (an ollie's wind-up has the front foot 4-5 in
	// further back than riding's; a landing's feet come down wherever the trick put them), and followed, the feet slid
	// back along the deck into each clip's stance: 4-5 in at every wind-up, and 0.6 s after every landing a step of a 0.8
	// in lift back to the riding stance that read as a slide. A foot landing turns as it came down for LandingPlant
	// seconds; standing, it turns as the clip turns it (a push's front foot pivoting), easing over StandTurn. Only when
	// the clip's place has moved far enough (see DeckStance.OutOfStance) does the foot step to it, over StepTime, lifted by
	// up to StepLift. Once the clip lifts the foot off its board (a push, a pop, a flip) or the board leaves the ground, the
	// foot eases off the place it stood over StandRelease rather than jumping to the clip's.
	private const float LandingPlant = 0.6f;
	private const float StepTime = 0.25f;
	private const float StepLift = 1.5f;
	private const float StandTurn = 10f;
	private const float StandRelease = 0.12f;
	private const float PlantStanding = 0.3f;

	// How each foot eases off the place it stood: its offset from the clip's place there (model frame) and its turn
	// against the deck as it let go, and how far it still has to go (1 to 0).
	private System.Numerics.Vector3[] _letGoOffset;
	private System.Numerics.Quaternion[] _letGoTurn;
	private float[] _letGoWeight;
	private readonly Vector3?[] _stepFrom;
	private readonly System.Numerics.Quaternion[] _stepTurnFrom;
	private readonly float[] _stepProgress;
	private readonly Vector3?[] _plantedAt;
	private readonly float[] _plantAge;
	private const float PlantWindow = 0.15f;
	private readonly System.Numerics.Quaternion?[] _plantedTurn;
	private readonly int[][] _footTree;
	private readonly float[] _sinceAirborne;
	private readonly bool[] _plantPending;
	private readonly int[] _framesLanded;

	// Which feet a landing planted, held where they came down for as long as the landing plays (or within PlantWindow,
	// before the graph is in it), whether or not the clip stands them on its own board: planted only once the clip stood
	// them, they followed its pull until then, and after a varial kickflip, whose clip keeps its feet off its board for a
	// fifth of a second, the front foot went 4.4 in along the deck and 3 in back, rising 2 in. A held foot's height
	// (_landingHeight, inches in the shown deck's frame) is the clip's, rising no quicker than DeckStance.LandingRise.
	private bool[] _landingHeld;
	private float[] _landingHeight;

	// How high a stepping foot is lifted over the deck this frame (see Planted), added in OutOfDeck.
	private float[] _stepLift;

	// Size of an in-air foot correction (inches) that is trusted fully, and where it is ignored; and how fast a
	// correction follows its target (per second).
	private const float TrustedCorrection = 2f;
	private const float IgnoredCorrection = 6f;
	private const float CorrectionRate = 30f;

	// Beyond this (inches) a foot correction is never followed, on the ground or a rail as in the air (fading out from
	// IgnoredCorrection): a landing clip whose board is far from its feet (a spin's) put the pull's targets 10 in above
	// the hips, and the legs folded up to them.
	private const float ImplausibleCorrection = 12f;

	// Seconds the feet's rules take to change (how far the pull and the push out of the deck are trusted, the leg's reach
	// limit, the rail's footprint fade): changed in one frame, a grind's end or a landing snapped the legs.
	private const float SwitchEase = 0.15f;

	// Seconds over which the feet's pull comes to be trusted on a rail (see SwitchEase).
	private const float RailTrustEase = 0.05f;

	// How far into the rail's and the air's foot rules the feet are (0 to 1), and each foot's distrust of the pull and of the
	// push out of the deck (see Trusted), all eased over SwitchEase.
	private float _railWeight;
	private float _airWeight;
	private readonly float[] _pullDistrust = new float[ 2 ];
	private readonly float[] _pushDistrust = new float[ 2 ];

	private static float Approach( float value, float target, float step ) =>
		value < target ? MathF.Min( value + step, target ) : MathF.Max( value - step, target );

	// Share of a foot correction this size (inches) followed at all: all of it up to IgnoredCorrection, none past
	// ImplausibleCorrection.
	private static float Plausible( float size ) =>
		1f - Smoothstep( (size - IgnoredCorrection) / (ImplausibleCorrection - IgnoredCorrection) );

	private readonly IBoardDisplay _skateboard;

	// The clips' board fitted onto the board ridden, and how far each hand (left, right) is pulled onto the real board.
	private readonly RiderBoardFit _fit;
	private readonly float[] _handFit = new float[ 2 ];

	// How fast (per second) a hand's pull onto the board ridden eases in and out, and the most (inches) it moves the hand.
	private const float HandFitRate = 12f;
	private const float HandFitReach = 10f;

	// A grabbing hand's hold on the shown deck (see Hold): whether it holds, how far over to the hold it is (0 to 1), and
	// where on the deck (the hand in the shown deck's frame) it took hold. It takes hold once the clip's hand is that near
	// its board (see RiderBoardFit.Nearness) and lets go under HoldOff; taking hold eases over HoldTake (the hand is
	// already there), letting go over HoldRelease.
	private readonly bool[] _holding = new bool[ 2 ];
	private readonly float[] _hold = new float[ 2 ];
	private readonly Transform[] _held = new Transform[ 2 ];
	private const float HoldOn = 0.8f;
	private const float HoldOff = 0.45f;

	// A hand that took hold in a grab keeps it, whatever the clip's hand does, until this long before the board lands:
	// the grab is let go ahead of the landing (RiderAnimator.GrabRelease) and its release clip takes the clip's hand off the
	// board at once, so off a flat ollie a hand held the board for 60-90 ms.
	private const float HoldLetGo = 0.15f;
	private const float HoldTake = 0.08f;

	// Furthest (inches) a hand taking hold is moved onto the deck.
	private const float HoldClose = 2.5f;
	private const float HoldRelease = 0.12f;

	// Every bone moves on continuously from what was shown (see PoseInertializer).
	private readonly PoseInertializer _continuity = new();
	private float _boardShown;
	private Transform _animatedBoard;
	private readonly HashSet<string> _stanceSwapClips;
	private bool _shownSwitch;
	private int _plays;
	private readonly System.Numerics.Vector3[] _shownPositions;
	private readonly System.Numerics.Quaternion[] _shownRotations;
	private readonly System.Numerics.Vector3[] _flipPositions;
	private readonly System.Numerics.Quaternion[] _flipRotations;
	private Transform _shownModel;

	// The physical board as the pose was last shown, riding (null off the board): turning round, what was last shown is
	// carried on with the board from there, so the body keeps going with it through the turn's first frame.
	private Transform? _shownBoard;
	private bool _shownMirror;
	private bool _shownValid;
	private float _flipTime = FlipEase;

	// How long (seconds) showing the other stance eases in, and how long turning the body upright on a wall takes.
	private const float FlipEase = 0.3f;
	private const float WallTurn = 0.2f;
	private float _wallWeight;
	private float _settle;

	// Furthest (inches) the shown board gives way to the feet, and how fast it eases back (per second).
	private const float SettleLimit = 3f;
	private const float SettleRelease = 10f;

	// How fast (per second) the shown board gives way further to the feet.
	private const float SettleFollow = 60f;

	// Points along each shin kept out of the shown board in the air.
	private const int ShinSamples = 4;
	private float _anchored;

	// Keeps the body's placement continuous where what it is placed from jumps: getting on or off, a step taken at once.
	private readonly PlacementSmoother _placement = new();
	private readonly BodyClearance[] _clearance = new BodyClearance[ 2 ];
	private readonly Dictionary<int, TwoBoneIk> _arms = new();

	// The way each arm's elbow pointed (off the shoulder-to-hand line, model space) the last time it was reached with,
	// and when, by hand bone: a reach a moment later keeps it where the clip's arm is too straight to say.
	private readonly Dictionary<int, (System.Numerics.Vector3 Pole, float Time)> _elbows = new();
	private const float ElbowMemory = 0.1f;

	// Fastest (radians per second) an arm reaching with the engine swings its elbow round the shoulder-to-hand line: the
	// clip's own bend plane, or the remembered one while the arm is nearly straight, otherwise turned the arm round in a
	// frame or two with the hand still (up to 18 degrees a frame), as the arm bent again or the clip's plane flipped.
	private const float ElbowSwing = 2f * MathF.PI;

	// Inches the held board is moved out to clear the body; the way it is moved (in the model's frame) and the hand moved
	// with it, kept so the arm's reach eases back in once the board has left the hand.
	private float _clearancePush;
	private float _clearanceSpeed;
	private Vector3 _clearanceAway;
	private int _clearanceHand = -1;

	// How quickly (the spring's frequency, radians per second) the arm's reach out from the body follows a growing push
	// and a shrinking one. Followed at a rate the arm jumped out up to an inch in a frame and swung out and back in with
	// each stride of a run; followed by a critically damped spring it sets off and settles smoothly, and coming back
	// slowly it stays out through a run's strides.
	private const float ClearanceOut = 16f;
	private const float ClearanceIn = 4f;

	// The rider's hands measured from its mesh, by hand bone: direction to the fingertips in the hand bone's frame,
	// length and thickness (inches).
	private readonly Dictionary<int, (System.Numerics.Vector3 Axis, float Length, float Radius)> _hands = new();

	// How far (deck units, along its up axis) the held deck is moved to keep the holding hand out of it: positive puts
	// the deck above the hand.
	private float _handPush;

	// Rate at which a hand clearance no longer needed eases back out (per second), and how much shorter (in hand
	// thicknesses) the other side of the deck must be before the deck changes sides of the hand.
	private const float HandRelease = 8f;
	private const float HandSideBias = 1f;

	// Points along the holding hand, wrist to fingertips, tested against the deck.
	private const int HandSamples = 6;

	// Each hand's fingers as joint chains from the knuckle out, and how thick they are (inches): with them the holding
	// hand's gap to the deck is measured as it is posed, wrapped round the deck's edge, rather than as a flat paddle.
	private readonly Dictionary<int, (int[][] Chains, int Thumb, float Radius)> _fingerChains = new();
	private readonly List<(Vector3 Point, float Radius)> _handPoints = new();

	// Which finger (its chain's index) each of the hand's points belongs to.
	private readonly List<int> _handChains = new();

	// Points along each finger segment, how far past the last joint a fingertip reaches (a share of that last segment),
	// and a finger's thickness (radius) as a share of its length.
	private const int SegmentSamples = 3;
	private const float FingertipReach = 0.6f;
	private const float FingerThickness = 0.1f;

	// How fast (per second) the held deck follows where the posed fingers leave room for it.
	private const float HandFollow = 20f;

	// The carried board's place in the holding hand (the hand bone's frame), the hand holding it and how long it has
	// held it; for its first GripSettle seconds the place follows the clips' at HeldFollow per second (the hand has the
	// board before the clip's grip is round it: taken at once it was held 5 in off), and is kept from then.
	private Transform _heldInHand;
	private bool _heldSteady;
	private int _heldHand = -1;
	private float _heldFor;
	private const float GripSettle = 0.5f;
	private const float HeldFollow = 12f;

	// The give of a hand catching the flying board: the board's arrival speed carries hand and board on, easing back
	// over this time constant (seconds), at most this share of the forearm's length.
	private const float CatchTime = 0.06f;
	private const float CatchReach = 0.3f;

	// Board position and velocity in the clip frame while it flies in, and the catch under way (seconds since the
	// board arrived, its velocity then; a negative time means none).
	private System.Numerics.Vector3 _flightPosition;
	private System.Numerics.Vector3 _flightVelocity;
	private bool _flightSeen;
	private float _catchElapsed = -1f;
	private System.Numerics.Vector3 _catchVelocity;
	private Transform _model;
	private bool _lastMirror;

	// Seconds over which the clip's board bone eases off the real deck when getting off.
	private const float AnchorBlend = 0.25f;

	// Seconds for the shown board to follow the animation's board after take-off, and to return to physics on landing.
	private const float BoardTakeOver = 0.04f;
	private const float BoardHandBack = 0.15f;

	// Fastest (inches per second) the shown board moves beyond the physical one while taking over, and the reach from a
	// deck's middle to its ends (inches) when the deck model gives none.
	private const float HandOverSpeed = 60f;
	private const float DeckReach = 16f;

	private const float MaximumLift = 6f;

	// Inches past the deck's edge over which its push on a foot fades out.
	private const float EdgeFade = 1.5f;

	// Inches kept between a sole and the deck surface.
	private const float SoleMargin = 0.05f;

	// Sole gap above the ground (inches) within which a foot beside the deck is set on the ground, and the distance
	// over which that hold then fades as the clip lifts the foot.
	private const float PlantSnap = 2f;
	private const float PlantFade = 1.5f;

	// How fast (per second) a foot settles onto the ground or lets go of it.
	private const float PlantRate = 12f;

	// How fast (per second) a foot's ground lock lets go, and the furthest (inches) the body may carry the leg from
	// a locked foot before the held spot is dragged along.
	private const float LockRelease = 8f;

	// Soft reach: past this share of the leg's length a planted foot's distance from the hip is eased toward full
	// length instead of reaching it, so the knee never snaps straight (a two-bone leg's knee angle changes without
	// bound as it nears full stretch).
	private const float SoftReach = 0.9f;

	// A foot on a rail is pressed onto the deck by at most PressMost (inches), fully while the clip lifts it no more than
	// PressLift over where it stands, fading out over PressLiftFade more.
	private const float PressMost = 6f;
	private const float PressLift = 0.25f;
	private const float PressLiftFade = 0.35f;

	// Riding on the ground a foot the clip stands on its board is laid flat on the deck under it (turned by at most FlatMost
	// radians) and pressed onto it (see OutOfDeck), fully while the clip holds its toe no more than GroundPressLift (inches)
	// over its standing height, fading out over GroundPressFade more.
	private readonly float[] _groundStand = new float[ 2 ];
	private const float GroundPressLift = 0.5f;
	private const float GroundPressFade = 0.5f;
	private const float FlatMost = 0.35f;
	private readonly float?[] _clipStand = new float?[ 2 ];
	private readonly float[] _clipLift = new float[ 2 ];

	// Deepest crouch with the board held dark side up (inches): the board is shown from the clip, placed from the rider as
	// it stands, so the crouch lowers only the rider; a longboard's darkslide stance was out of reach at MaxHipDrop.
	private const float DarkHipDrop = 8f;

	// Seconds after getting on a rail the crouch holds rather than rising: a slide's pitch comes in over its first quarter
	// second, and the crouch let go as the clip landed only to be needed again as the tail dropped.
	private const float CrouchHold = 0.3f;
	private float _onRailFor;

	// Least angle (radians) between shin and foot a foot on the board is bent to on a rail or in the air, unless the clip
	// bends it further itself: a person's ankle flexes about 25 degrees past square.
	private const float LeastAnkle = 65f * MathF.PI / 180f;

	// On a rail a grind's clip can stand the rider higher over the board than the board ridden lets a leg reach down (a
	// crook's back foot to a tail lying lower than the clip's): the rider is lowered (in inches, at most MaxHipDrop, at
	// HipDropRate) until each foot reaches the deck within the leg's soft reach, and rises back as it comes off the rail.
	// Without it the back foot floated 1.9 in over the tail.
	// (Any deeper sank the front foot into the deck.)
	private const float MaxHipDrop = 4f;
	private const float HipDropRate = 100f;
	private const float HipRiseRate = 20f;
	private float _hipDrop;
	private float _reachShort;

	// On the ground too a foot put down onto it (a brake's dragging foot, a push) can be out of the leg's reach, the clip's
	// skater reaching it with a leg nearly straight and the rider's legs proportioned otherwise: the rider is lowered (at
	// most GroundHipDrop, at GroundDropRate) until it reaches within the leg's soft reach, the leg on the deck bending, and
	// rises back as the foot lifts. Without it a braking foot stood still hung 1-1.2 in over the ground.
	private const float GroundHipDrop = 3f;
	private const float GroundDropRate = 30f;
	private float _groundShort = float.NegativeInfinity;
	private const float LockReach = 16f;

	// Gap above the ground (inches) at which a descending foot is pinned, so it does not skid the last of the way.
	private const float LockGap = 3f;

	// Push playback-rate matching: slowest sweep worth matching (inches per second), the rate limits, and how fast
	// the rate follows (per second).
	private const float MinimumSweep = 5f;
	private const float MinimumRate = 0.6f;
	private const float MaximumRate = 3f;
	private const float RateSmoothing = 15f;

	// How fast (per second) a foot's push out of the deck eases off once it is no longer needed.
	private const float DeckPushRelease = 12f;

	// How fast (per second) a push out of the deck lets go while the foot is still over the deck: a foot sliding off a
	// raised kick onto the flat stays on the deck instead of floating over it as the push eased (1.8 in in a smith).
	private const float DeckPushFollow = 40f;

	// Fastest (inches per second) a push lets go while the foot is still over the deck, however large it is.
	private const float DeckPushFollowSpeed = 60f;

	// Fastest (inches per second) a getting-on foot is raised out of the deck.
	private const float MountRise = 60f;

	// How far above and below the foot (inches) the ground is looked for.
	private const float PlantReach = 24f;

	// Seconds for a foot's pull to the board to fade fully in or out.
	private const float FootBlend = 0.15f;

	// State action that releases the feet from the board (grab tweaks, handplants).
	private const string ReleaseFeet = "attribute:disable-feet-target-ik";

	// Graph action of the states that hold the board dark side up.
	private const string SetDark = "set-dark";

	// Graph action of the states that step off the board from a grab in the air.
	private const string AirDismounting = "air-dismounting";

	/// <summary>True while the graph holds the board dark side up (a dark catch, dark air or ground, a darkslide).</summary>
	internal bool Dark { get; private set; }

	/// <summary>The graph holds the board dark side up on the ground (its dark ground state, which has no clip of its own
	/// and answers only with a runout or a jump out): a board that cannot be ridden on its grip.</summary>
	internal bool DarkOnGround { get; private set; }

	/// <summary>The lip of the board's last hand plant (where it left the ramp), in world space.</summary>
	internal Vector3? PlantLip { get; set; }

	// A hand plant's hold on the clips' pose: 0 to 1 as the hand plants, back to 0 as it lets go, and which hand plants.
	private float _plantWeight;
	private int _plantHand = -1;

	// Seconds a hand plant takes to turn the rider upside down on the lip, and to let go.
	private const float PlantEase = 0.2f;

	// The turn (in the world, against the deck) that stands a hand plant's model upright, kept from the last frame the hand
	// planted while the plant eases out (see Update).
	private Rotation _plantTurn = Rotation.Identity;
	private bool _plantTurnHeld;

	// The deck's turn tipped grip side down by the shortest way. The plant clips hold the planting hand above the deck
	// along its grip side, so this hangs the rider upside down under the board, the hand toward the lip below.
	private static Rotation Inverted( Rotation deck )
	{
		var up = deck.Up;
		var axis = Vector3.Cross( up, Vector3.Down );
		var sine = axis.Length;
		if ( sine < 0.0001f )
			return Vector3.Dot( up, Vector3.Down ) > 0f ? deck : Rotation.FromAxis( deck.Forward, 180f ) * deck;
		return Rotation.FromAxis( axis / sine, MathF.Atan2( sine, Vector3.Dot( up, Vector3.Down ) ) * 180f / MathF.PI ) * deck;
	}

	internal ClipRiderAnimation( SkinnedModelRenderer renderer, RiderAnimationAssets assets, GameObject rider, GameObject board )
	{
		_renderer = renderer;
		_rider = rider;
		_board = board;
		_graph = new MotionGraph( assets.Clips, assets.Graph );
		if ( assets.Graph.BasePlayback is not null )
			_treeStates = new BoundStateGraphTreePlayer( assets.Clips, assets.Graph );
		else if ( assets.Graph.StateMachine is not null )
			_states = new StateGraphPlayer( assets.Clips, assets.Graph.StateMachine );
		_director = new RiderAnimationDirector( string.Equals( assets.Graph.Stance, "goofy", StringComparison.OrdinalIgnoreCase ) );
		_pose = new SkeletonPose( assets.Clips.Skeleton.Count );
		_statePose = new SkeletonPose( assets.Clips.Skeleton.Count );
		_motionPose = new SkeletonPose( assets.Clips.Skeleton.Count );
		_sourcePositions = new System.Numerics.Vector3[ assets.Clips.Skeleton.Count ];
		_sourceRotations = new System.Numerics.Quaternion[ assets.Clips.Skeleton.Count ];
		_layered = new SkeletonPose( assets.Clips.Skeleton.Count );
		if ( assets.Graph.BoardHandling is not null )
			_handling = new BoardHandlingAnimationLayer( assets.Clips.RawPoses, assets.Graph );
		_anchor = string.IsNullOrEmpty( assets.Graph.AnchorBone ) ? -1 : assets.Clips.Skeleton.IndexOf( assets.Graph.AnchorBone );
		if ( _states is not null )
		{
			_states.Board = _anchor;
			_states.Legs = StanceBones.Legs( assets.Clips.Skeleton );
		}
		_fit = new RiderBoardFit( assets.Graph.Board );
		var bones = renderer.Model.Bones.AllBones.OrderBy( bone => bone.Index ).ToArray();
		var names = bones.Select( bone => bone.Name ).ToArray();
		var parents = bones.Select( bone => bone.Parent?.Index ?? -1 ).ToArray();
		var bindPositions = bones.Select( bone => SkateUnits.ToCore( bone.LocalTransform.Position ) ).ToArray();
		var bindRotations = bones.Select( bone => SkateUnits.ToCore( bone.LocalTransform.Rotation ) ).ToArray();
		Rig = new HumanoidBones( names, parents, bindPositions );
		if ( !Rig.Complete )
			throw new InvalidOperationException( $"{renderer.Model.Name} has no recognisable humanoid legs, hips and head." );
		// Finger banks add their bones below the clip skeleton's hands; the model's fingers then retarget like limbs.
		_clipSet = assets.Clips;
		_travel = new ClipRootTravel( assets.Clips.Skeleton );
		_fingers = assets.Hands.Count > 0 ? new HandPoseLayer( assets.Clips.Skeleton, assets.Hands ) : null;
		_breathing = new RiderBreathing( assets.Clips.Skeleton );
		_retargeter = new PoseRetargeter( _fingers?.Skeleton ?? assets.Clips.Skeleton, names, parents, bindPositions, bindRotations,
			Rig.Map(), Rig.Facing, Sole( renderer.Model.Bounds, Rig.Facing ) );
		_carryHand = assets.Graph.CarryHand switch
		{
			"left_hand" => Rig[ "left_hand" ],
			"right_hand" => Rig[ "right_hand" ],
			_ => -1
		};
		_mirroredCarryHand = assets.Graph.CarryHand switch
		{
			"left_hand" => Rig[ "right_hand" ],
			"right_hand" => Rig[ "left_hand" ],
			_ => -1
		};
		var hasCarryTarget = _anchor >= 0 && _carryHand >= 0 && _mirroredCarryHand >= 0
			&& _retargeter.SourceBone( _carryHand, false ) >= 0
			&& _retargeter.SourceBone( _mirroredCarryHand, true ) >= 0;
		if ( assets.RetrievalProfile is not null && hasCarryTarget && assets.Rig is not null
			&& assets.Rig.SolverToClip[ 0 ] == _anchor )
			_carryAttachment = new( assets.Rig, assets.Graph.CarryHand == "left_hand" );
		RetrievalProfile = _handling is not null && _carryAttachment is not null ? assets.RetrievalProfile : null;
		_positions = new System.Numerics.Vector3[ bones.Length ];
		_rotations = new System.Numerics.Quaternion[ bones.Length ];
		_shownPositions = new System.Numerics.Vector3[ bones.Length ];
		_shownRotations = new System.Numerics.Quaternion[ bones.Length ];
		_flipPositions = new System.Numerics.Vector3[ bones.Length ];
		_flipRotations = new System.Numerics.Quaternion[ bones.Length ];
		_stanceSwapClips = new HashSet<string>( assets.Graph.StanceSwapClips ?? new() );
		_feet = new[] { "left", "right" }.Select( side => Foot( parents, side ) ).Where( foot => foot.Leg is not null ).ToArray();
		_footOnTop = new bool?[ _feet.Length ];
		_footWeight = new float[ _feet.Length ];
		_plant = new float[ _feet.Length ];
		_locked = new bool[ _feet.Length ];
		_kneeHint = new System.Numerics.Vector3[ _feet.Length ];
		_ball = new System.Numerics.Vector3[ _feet.Length ];
		_ballSeen = new bool[ _feet.Length ];
		_lock = new float[ _feet.Length ];
		_lockPoint = new Vector3[ _feet.Length ];
		_deckPush = new float[ _feet.Length ];
		_rising = new bool[ _feet.Length ];
		_footLift = new float[ _feet.Length ];
		_locks = new FootLock[ _feet.Length ];
		for ( var i = 0; i < _locks.Length; i++ )
			_locks[ i ] = new FootLock();
		_contactBefore = new System.Numerics.Vector3[ _feet.Length ];
		_contactSeen = new bool[ _feet.Length ];
		_plantedAt = new Vector3?[ _feet.Length ];
		_plantAge = new float[ _feet.Length ];
		_sinceAirborne = new float[ _feet.Length ];
		_plantPending = new bool[ _feet.Length ];
		_framesLanded = new int[ _feet.Length ];
		_plantedTurn = new System.Numerics.Quaternion?[ _feet.Length ];
		_stepFrom = new Vector3?[ _feet.Length ];
		_stepTurnFrom = new System.Numerics.Quaternion[ _feet.Length ];
		_stepProgress = new float[ _feet.Length ];
		_shownStand = new Vector3[ _feet.Length ];
		_shownStandTurn = new System.Numerics.Quaternion[ _feet.Length ];
		_footTree = new int[ _feet.Length ][];
		for ( var i = 0; i < _feet.Length; i++ )
			_footTree[ i ] = Enumerable.Range( 0, parents.Length ).Where( bone => Below( parents, bone, _feet[ i ].Ankle ) ).ToArray();
		_footCorrection = new System.Numerics.Vector3[ _feet.Length ];
		_footCarry = new (Transform Board, Vector3 Target, System.Numerics.Vector3 Ankle, float At)?[ _feet.Length ];
		_entryFrom = new System.Numerics.Vector3?[ _feet.Length ];
		_entryOn = new Vector3?[ _feet.Length ];
		_entryWay = new float[ _feet.Length ];
		_entryUp = new bool[ _feet.Length ];
		_entryAt = new Vector3?[ _feet.Length ];
		_entryAge = new float[ _feet.Length ];
		_entryLift = new float[ _feet.Length ];
		foreach ( var side in new[] { "left", "right" } )
		{
			var (upper, lower, end) = (Rig[ $"{side}_arm" ], Rig[ $"{side}_forearm" ], Rig[ $"{side}_hand" ]);
			if ( upper >= 0 && lower >= 0 && end >= 0 )
				_arms[ end ] = new TwoBoneIk( parents, upper, lower, end );
			if ( end >= 0 && BodyClearance.Hand( renderer.Model, Rig, side ) is { } hand )
				_hands[ end ] = (SkateUnits.ToCore( hand.Axis ), hand.Length, hand.Radius);
			if ( end >= 0 && FingerChains( renderer.Model, side ) is { } fingers )
				_fingerChains[ end ] = fingers;
		}
		// Interfaces are not component types to the lookup, so the board's components are filtered instead.
		_skateboard = board.Components.GetAll<Component>( FindMode.EverythingInSelf ).OfType<IBoardDisplay>().FirstOrDefault();
	}

	/// <summary>The humanoid bones found on the model.</summary>
	internal HumanoidBones Rig { get; }

	/// <summary>The model's bone name for a humanoid role (hips, head, left_shoulder...), or null.</summary>
	internal string BoneName( string role )
	{
		var bone = Rig[ role ];
		return bone < 0 ? null : _renderer.Model.Bones.AllBones.FirstOrDefault( b => b.Index == bone )?.Name;
	}

	// The soles are the bottom of the model's bounds, measured along its up axis.
	private static float Sole( BBox bounds, System.Numerics.Quaternion facing )
	{
		var inverse = System.Numerics.Quaternion.Conjugate( facing );
		return bounds.Corners.Min( corner => System.Numerics.Vector3.Transform( SkateUnits.ToCore( corner ), inverse ).Z );
	}

	private (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) Foot( int[] parents, string side )
	{
		var upper = Rig[ $"{side}_upleg" ];
		var lower = Rig[ $"{side}_leg" ];
		var ankle = Rig[ $"{side}_foot" ];
		var ball = Rig[ $"{side}_toe" ];
		if ( upper < 0 || lower < 0 || ankle < 0 )
			return (null, -1, -1, 0f, 0f, [], 0f);
		if ( ball < 0 )
			ball = ankle;
		var bones = _renderer.Model.Bones.AllBones;
		var shin = BodyClearance.Radius( _renderer.Model, bones.First( bone => bone.Index == lower ).LocalTransform.Position,
			bones.First( bone => bone.Index == ankle ).LocalTransform.Position );
		return (new TwoBoneIk( parents, upper, lower, ankle ), ankle, ball, _retargeter.BindPosition( ankle ).Z,
			_retargeter.BindPosition( ball ).Z, FootSole.Measure( _renderer.Model, _retargeter, ankle, ball ), shin);
	}

	/// <summary>Name of the graph state currently playing.</summary>
	internal string State => _treeStates?.Player.Graph.State ?? _states?.Graph.State ?? _graph.State;

	/// <summary>Name of the clip playing now.</summary>
	internal string Motion => _treeStates?.Player.ClipName ?? _states?.ClipName ?? _graph.ClipName;

	/// <summary>
	/// Lowest gap (inches) between any part of either foot or shin and the shown deck, on the side of the deck the foot is on,
	/// after this frame's foot placement; negative means a foot is inside the deck, null when no foot is over it.
	/// </summary>
	internal float? FootDeckGap { get; private set; }

	/// <summary>
	/// Per foot (left, right): the lowest gap (inches) between its sole and the shown deck, on the side of the deck it is
	/// on, after this frame's foot placement; null off the deck, while not riding, or while the clip lets the feet go of
	/// the board (a grab tweak, a handplant). For checking that each foot stands on the deck.
	/// </summary>
	internal float?[] SoleDeckGaps { get; } = new float?[ 2 ];

	/// <summary>
	/// Per foot (left, right): how high (inches) the clip holds its ball of the foot over the clip's own board, this frame;
	/// null while not riding. A foot the clip lifts (stepping it along the board, an ollie's) is higher than it stands.
	/// </summary>
	internal float?[] ClipToeHeights { get; } = new float?[ 2 ];

	/// <summary>True while a foot stands on the ground beside the board (held where it touched down).</summary>
	internal bool FootOnGround => System.Array.IndexOf( _locked, true ) >= 0;

	/// <summary>Authored board pose aligned to the carrying hand after retargeting, when configured.</summary>
	internal Transform? CarriedBoardPose { get; private set; }
	internal BoardRetrievalProfile RetrievalProfile { get; }
	internal bool RetrieveRequested => _handling?.Result.RetrieveRequested ?? false;

	/// <summary>True once a drop or throw playing has reached the moment its hand lets go of the board.</summary>
	internal bool BoardReleased => _handling?.ReleasePassed ?? false;

	/// <summary>How far the rider should turn (degrees, left positive) for a reach to cover the board being picked up.</summary>
	internal float ReachTurn => _handling?.ReachTurn ?? 0f;

	/// <summary>The board-handling motion playing over the body (a reach, drop or throw), or null.</summary>
	internal string HandlingMotion => _handling is { Active: true } handling ? handling.Motion.ToString() : null;

	internal void Request( IReadOnlyDictionary<string, string> tags, float seconds ) => _director.Request( tags, seconds );

	// A body pose the first poses blend from (model-space bone transforms), and how far into the blend it is; and whether
	// it is still in the world's space, to be taken into the model's frame where the model is next placed.
	private Transform[] _fadeFrom;
	private float _fadeFor;
	private float _fadeTime;
	private bool _fadeFromWorld;

	internal void BlendFrom( Transform[] modelPose, float seconds )
	{
		if ( modelPose is null || modelPose.Length != _positions.Length || !(seconds > 0f) )
			return;
		_fadeFrom = modelPose;
		_fadeFor = seconds;
		_fadeTime = 0f;
		_fadeFromWorld = false;
	}

	/// <summary>
	/// Forgets what the clips last showed, for a body that has been shown by something else since (a ragdoll lying where it
	/// fell): nothing eases on from that pose, long gone, any more, and a fade from the body's own (see
	/// <see cref="BlendFromWorld"/>) takes over from it. Eased on from it (the riding pose from before a bail, a get-up's
	/// requested motion faded from the last request's pose seconds before), a get-up face down swung the hips 43 in out and
	/// back against the body's root over its first 0.2 s and stopped them dead.
	/// </summary>
	internal void ForgetShown()
	{
		_sourceShown = false;
		_sourceTime = SourceBlend;
		_graphRunning = false;
		_shownValid = false;
		_flipTime = FlipEase;
		_continuity.Reset();
		_shownFollow.Reset();
		_placement.Reset();
		foreach ( var hold in _locks )
			hold.Reset();
	}

	/// <summary>As <see cref="BlendFrom"/>, from each bone's place in the world.</summary>
	internal void BlendFromWorld( Transform[] worldPose, float seconds )
	{
		BlendFrom( worldPose, seconds );
		_fadeFromWorld = _fadeFrom is not null;
	}

	internal IReadOnlyList<string> Actions( string family, string style ) => _graph.Actions( family, style );

	internal void Update( in RiderAnimationInput input, float delta, Transform root )
	{
		if ( _fadeFrom is not null )
			_fadeTime += delta;
		if ( input.Activity != RiderActivity.Riding )
		{
			Switch = false;
			_shownSwitch = false;
		}
		// Riding switch the rider faces the board's tail: rolling forwards for that stance is the board rolling backwards.
		// The clips show the stance they started in (a turn-round clip ends in the new stance), so their mirroring and
		// facing change only when the next clip starts.
		var stance = Switch ? input with { Speed = -input.Speed, Switch = true } : input;
		_director.Update( _shownSwitch ? stance with { Goofy = !input.Goofy } : stance, delta, _parameters );
		var mirror = _parameters.Number( "mirror" ) > 0.5f;
		var stanceMirror = mirror != (_shownSwitch != Switch);
		var graphed = _treeStates is not null || _states is not null;
		if ( _treeStates is not null )
		{
			_riderFacts.Fill( _facts, stance, stanceMirror, delta );
			_treeStates.Update( _facts, _parameters, delta, _statePose );
		}
		else if ( _states is not null )
		{
			_riderFacts.Fill( _facts, stance, stanceMirror, delta );
			// Given here as well as at construction: a player made before a reload added them has none.
			_states.Legs ??= StanceBones.Legs( _clipSet.Skeleton );
			_states.Update( _facts, _parameters, delta, _statePose );
		}
		var requested = !graphed || _director.Requesting;
		var carryHand = mirror ? _mirroredCarryHand : _carryHand;
		_travelRate = (_states?.Graph.Play?.Speed ?? 1f) * (_states?.RateScale ?? 1f);
		_travelling = !requested && _states is not null && _travel.Update( _states.Clip, _states.Time, delta * _travelRate,
			_parameters, carryHand >= 0 ? _retargeter.SourceBone( carryHand, mirror ) : -1, _anchor );
		// A request taking over from the graph starts its motion afresh (see MotionGraph.Forget): faded instead from the pose
		// the requested motion last showed, seconds before, the body went that way and back as the ease from what the graph
		// showed went out.
		if ( requested )
		{
			if ( !_graphRunning )
				_graph.Forget();
			_graph.Update( _parameters, delta, _motionPose );
		}
		_graphRunning = requested;
		// The board is dark side up while the graph is in a state that holds it so; the facts carry that into the next
		// frame, so the graph's dark states can follow one another.
		Dark = input.Activity == RiderActivity.Riding
			&& ((_treeStates?.Player.Graph ?? _states?.Graph)?.HasAction( SetDark ) ?? false);
		_riderFacts.GraphDark = Dark;
		_riderFacts.GraphHippyJump = State?.Contains( "HippyJump." ) == true;
		// Stepping off from a grab, the rider is on the board in the air until the graph's let-go has played out.
		_riderFacts.GraphAirDismounting = (_treeStates?.Player.Graph ?? _states?.Graph)?.HasAction( AirDismounting ) ?? false;
		DarkOnGround = Dark && _facts.Physics == "ground";
		// A graph's first frames as its first state blends in can give no usable pose, and its placeholder state no pose of
		// the rider's: the body keeps the last one it showed (a new rider stays hidden until it has one) rather than
		// dropping into its bind pose, the T, or standing up straight.
		if ( !(requested ? _motionPose : _statePose).Usable || State == Placeholder )
		{
			_continuity.Reset();
			return;
		}
		ShowSource( requested ? _motionPose : _statePose, requested, delta );
		if ( TrackStance() )
		{
			mirror = !mirror;
			_parameters.Set( "mirror", mirror );
		}
		if ( _shownSwitch )
			root = new Transform( root.Position, root.Rotation * Rotation.FromYaw( 180f ) );
		_layered.CopyFrom( _pose );
		if ( _handling is not null )
		{
			var offset = root.Rotation.Inverse * (_board.WorldPosition - root.Position);
			var yaw = MathF.Atan2( offset.y, offset.x );
			var pitch = MathF.Atan2( offset.z, MathF.Sqrt( offset.x * offset.x + offset.y * offset.y ) );
			var changed = input.Activity != _previousActivity;
			var handlingInput = new BoardHandlingInput( false, input.HoldingBoard, false,
				input.Activity == RiderActivity.PickingUp || input.HoldingBoard,
				changed && input.Activity == RiderActivity.Dropping, changed && input.Activity == RiderActivity.Throwing,
				changed && input.Activity == RiderActivity.PickingUp, yaw, pitch, mirror );
			_handling.Update( handlingInput, delta, _layered );
		}
		_previousActivity = input.Activity;
		// The rider breathes over the clips before the pose is put on the model, the board and the grips, so the foot and
		// hand fits below still hold; a board-handling motion wants the hands, so the breath eases out for it.
		_breathing.Update( input, _handling is { Active: true }, delta );
		_breathing.Apply( _layered );
		var pose = _fingers?.Update( _layered, FingerClip( delta, out var fingerTime ), fingerTime, _parameters, _handling, delta ) ?? _layered;
		// Every bone carries on from what was shown, its speed changing by no more than a budget a frame (see
		// PoseInertializer): a clip handing over, a key's corner at a high frame rate, a layer or the fingers' poses switching
		// in or a pose jumping shows as a quick ease rather than a snap. The fits below then put the feet and hands on the board.
		_continuity.Apply( pose, delta );
		_retargeter.Apply( pose, mirror, _positions, _rotations );
		_fit.Update( _skateboard, _retargeter.Scale );
		var riding = input.Activity == RiderActivity.Riding;
		if ( riding && !input.Airborne && _anchor >= 0 && _skateboard?.Dimensions is { RideHeight: > 0f } size )
		{
			var clipDeck = (_retargeter.SourcePosition( _anchor ).Z - _retargeter.SourceGround) * _retargeter.Scale;
			var rise = MathF.Max( 0f, size.RideHeight * SkateUnits.InchesPerMetre - clipDeck );
			if ( float.IsFinite( rise ) )
				_groundRise = MathX.Lerp( _groundRise, rise, 1f - MathF.Exp( -GroundRiseRate * delta ) );
		}
		MatchPushToGround( riding, input.Speed, delta );
		MatchAirTime( input, delta );
		MatchGrabReach( input, delta );
		for ( var i = 0; i < _feet.Length; i++ )
			_kneeHint[ i ] = KneeDirection( _feet[ i ], _kneeHint[ i ], _positions );
		// While riding, the clip's board bone sits on the real deck rather than the clip's travel root. Getting on it is put
		// there at once, the placement below easing the body across from where it was shown; getting off it eases out.
		_anchored = riding ? 1f : Math.Max( 0f, _anchored - delta / AnchorBlend );
		// Off the board the clip's board no longer means the real one: getting off eases out from where the deck was held
		// as the rider left it, so a clip change meanwhile (a hippy jump handing over to its runout) does not move the body.
		if ( riding && _anchor >= 0 )
			_heldDeck = _retargeter.SourceOffset( _anchor, mirror );
		var deck = _anchor >= 0 ? _heldDeck * _anchored : System.Numerics.Vector3.Zero;
		// Clip-frame space: the root transform with the deck offset; the renderer adds the rig's own facing.
		// Riding a wall the clips hold the board up the wall with the body upright: the model turns so the clip's board
		// lies on the real deck, rather than the body lying along the wall with it.
		var wallStep = delta / WallTurn;
		_wallWeight = Math.Clamp( _wallWeight + (riding && input.WallRiding ? wallStep : -wallStep), 0f, 1f );
		var frame = root.Rotation;
		if ( _wallWeight > 0f && _anchor >= 0 )
		{
			var clipBoard = SkateUnits.ToEngine( _retargeter.SourceRotation( _anchor, mirror ) );
			frame = Rotation.Slerp( root.Rotation, root.Rotation * clipBoard.Inverse, Smoothstep( _wallWeight ) );
		}
		// A hand plant's clips hold the rider upside down on the planted hand with the body upright in the world: the model
		// turns upright rather than lying along a board that left a vertical wall, and below it is moved so the planting
		// hand is on the lip. Both ease in as the hand plants and out as it lets go.
		var planting = riding && input.Handplant is "into" or "cycle" && PlantLip is not null;
		if ( planting && _plantWeight <= 0f )
			_plantHand = Rig[ "left_hand" ] < 0 || Rig[ "right_hand" ] < 0 ? -1
				: _positions[ Rig[ "left_hand" ] ].Z <= _positions[ Rig[ "right_hand" ] ].Z ? Rig[ "left_hand" ] : Rig[ "right_hand" ];
		_plantWeight = Math.Clamp( _plantWeight + (planting ? delta : -delta) / PlantEase, 0f, 1f );
		// While the hand plants the model turns toward the deck turned over; as it lets go it eases back along the turn
		// that had as the hand left the lip, kept against the deck. Turned toward the deck turned over each frame instead,
		// the turn swung round as the deck came level leaving the lip (the shortest way from its up to down is any way at
		// level): the model's heading went through 41 degrees in one frame and the planting hand's fingers 4.3 on the snap
		// meter as the plant let go.
		if ( planting || !_plantTurnHeld )
		{
			_plantTurn = Inverted( root.Rotation ) * root.Rotation.Inverse;
			_plantTurnHeld = planting;
		}
		if ( _plantWeight > 0f )
			frame = Rotation.Slerp( frame, _plantTurn * root.Rotation, Smoothstep( _plantWeight ) );
		else
			_plantTurnHeld = false;
		// Getting on or off, the body is placed from the deck instead of the rider's own position (or back), and on foot it
		// can be stepped up or down at once: either way it carries on from where it was shown and settles. Getting on it
		// keeps its height (the clip stands on the ground the board stands on) while the rider is moved onto the deck.
		var placed = root.Position - frame * SkateUnits.ToEngine( deck );
		var model = new Transform( SkateUnits.ToEngine( _placement.Update( SkateUnits.ToCore( placed ), riding,
			!riding && !input.Airborne, input.Activity == RiderActivity.Mounting, System.Numerics.Vector3.UnitZ, delta ) ), frame );
		var travelling = _travel.Velocity;
		if ( mirror )
			travelling.Y = -travelling.Y;
		var rootTravel = travelling * (_retargeter.Scale * _travelRate);
		var footTravel = HeldFootTravel( rootTravel, mirror, delta );
		LandingTravel = !riding && _travelling && State?.Contains( ".Landing." ) == true
			? model.Rotation * SkateUnits.ToEngine( footTravel ?? rootTravel ) : null;
		var fromStand = _travel.FromStand;
		if ( mirror )
			fromStand.Y = -fromStand.Y;
		TravelOffset = _travelling ? SkateUnits.ToEngine( fromStand * _retargeter.Scale ) : null;
		if ( _plantWeight > 0f && _plantHand >= 0 && PlantLip is { } lip )
			model = model.WithPosition( model.Position
				+ (lip - model.PointToWorld( SkateUnits.ToEngine( _positions[ _plantHand ] ) )) * Smoothstep( _plantWeight ) );
		if ( _hipDrop > 0f )
			model = model.WithPosition( model.Position + Vector3.Down * _hipDrop );
		model = HippyFlight( model, input, mirror, delta );
		var flipped = _shownValid && mirror != _shownMirror;
		EaseFlip( model, flipped, delta );
		// The pose before the fits, against each bone's parent, for Settle to follow the shown pose over.
		_unfittedValid = Finite( model ) && Finite( _positions[ 0 ] ) && LocalPose( ref _unfitted );
		// Turned round, the eased pose carries on from what was last shown, with the feet's pulls onto the deck and their
		// pushes out of it and down to the ground already in it: those start again from nothing rather than being added a
		// second time, and a pull carried with the board, whose clip ankle was kept in the model's frame from before the
		// turn, is dropped. Kept, the feet were thrown 16 in along the deck in a frame (a knee straightening 36 degrees) as
		// a turn round rolling back down a bank handed over to riding the other way.
		if ( flipped )
		{
			for ( var i = 0; i < _feet.Length; i++ )
			{
				_footCorrection[ i ] = System.Numerics.Vector3.Zero;
				_footCarry[ i ] = null;
				_deckPush[ i ] = 0f;
				_plant[ i ] = 0f;
				_plantedAt[ i ] = null;
				_stepFrom[ i ] = null;
				if ( _letGoWeight is not null )
					_letGoWeight[ i ] = 0f;
			}
		}
		var facing = SkateUnits.ToEngine( _retargeter.Facing );
		_renderer.WorldTransform = new Transform( model.Position - model.Rotation * (Vector3.Up * _retargeter.Ground),
			model.Rotation * facing.Inverse );
		// On a rail the physical board is shown, as on the ground: the rail carries it, so the clip's board giving way to
		// the feet there would sink it into the rail. A darkslide's clip holds the board upside down on the rail, so its
		// board stays shown there as in the air.
		// A board held dark side up shows the clip's board, upside down, on the ground and on a rail as in the air.
		// The board shown from the clip is placed from the rider as it stands, before the crouch, which lowers the rider onto
		// it rather than sinking it into the rail; the rider rises back as the crouch goes after the rail.
		var boardShown = ShowAnimatedBoard( _hipDrop > 0f ? model.WithPosition( model.Position + Vector3.Up * _hipDrop ) : model,
			mirror, riding && (Flying( input ) || Dark), delta, riding );
		// Off the board in a hippy jump the feet leave the deck too.
		var released = input.OffBoard || ((_treeStates?.Player.Graph ?? _states?.Graph)?.HasAction( ReleaseFeet ) ?? false);
		_railWeight = Math.Clamp( _railWeight + (string.IsNullOrEmpty( input.Grind ) ? -delta : delta) / SwitchEase, 0f, 1f );
		_airWeight = Math.Clamp( _airWeight + (input.Airborne ? delta : -delta) / SwitchEase, 0f, 1f );
		// The crouch follows last frame's reach on the rail (none: it holds), and goes off the rail.
		// A board shown from the clip (dark side up, or handed back from the air) is placed from the rider as it stands, so the
		// crouch never sinks it into the rail.
		var crouch = string.IsNullOrEmpty( input.Grind )
			? riding && !input.Airborne && float.IsFinite( _groundShort ) ? Math.Clamp( _hipDrop + _groundShort, 0f, GroundHipDrop ) : 0f
			: float.IsFinite( _reachShort ) ? Math.Clamp( _hipDrop + _reachShort, 0f, Dark ? DarkHipDrop : MaxHipDrop ) : _hipDrop;
		// Down quickly, as a slide's pitch comes in under a foot that cannot reach the tail; back up more slowly.
		_onRailFor = string.IsNullOrEmpty( input.Grind ) ? 0f : _onRailFor + delta;
		var crouchRise = !string.IsNullOrEmpty( input.Grind ) && _onRailFor < CrouchHold ? 0f : HipRiseRate;
		var dropRate = string.IsNullOrEmpty( input.Grind ) ? GroundDropRate : HipDropRate;
		_hipDrop = Approach( _hipDrop, crouch, (_hipDrop < crouch ? dropRate : crouchRise) * delta );
		_reachShort = float.NegativeInfinity;
		_groundShort = float.NegativeInfinity;
		for ( var i = 0; i < _feet.Length; i++ )
		{
			if ( riding )
			{
				_locks[ i ].Reset();
				OnBoard( i, model, mirror, released, delta, boardShown, !string.IsNullOrEmpty( input.Grind ), input.Airborne );
				continue;
			}
			// Off the board a foot the deck held lets go of it over FootBlend, not in a frame: on a board longer or wider
			// than the clips' the deck holds the feet further from the clips' own pose.
			ReleaseFoot( i, delta );
			LiftOnFoot( i, input.Airborne, delta );
			HoldPlanted( i, model, mirror, !input.Airborne, State, delta );
		}
		TakeLegs( input, model, delta );
		HoldStance( input, model, delta );
		// The first frames after starting can carry a pose or board that is not real yet; the corrections wait for one.
		if ( riding && Finite( model ) && Finite( boardShown ) && Finite( _positions[ 0 ] ) )
		{
			boardShown = SettleBoard( model, boardShown, delta, !string.IsNullOrEmpty( input.Grind ) );
			_deckInModelBefore = _deckInModel;
			_deckInModel = model.ToLocal( boardShown );
			boardShown = DropForFlip( model, boardShown, delta, input.Airborne && string.IsNullOrEmpty( input.Grind ) );
			for ( var i = 0; i < _feet.Length; i++ )
			{
				OutOfDeck( i, model, boardShown, delta, !string.IsNullOrEmpty( input.Grind ), input.Airborne );
				PlantOffDeck( i, model, boardShown, !input.Airborne && !input.OffBoard, delta );
				OutOfRails( i, model, delta );
				OverTheEdge( i, model, boardShown, delta, input.Airborne && string.IsNullOrEmpty( input.Grind ) );
			}
			// A grab's hold is kept until just before the landing (see HoldLetGo), its release clip playing meanwhile.
			FitHands( model, boardShown, mirror, input.Airborne && State?.Contains( ".GrabsTweaks." ) == true
				&& input.TimeToLand > HoldLetGo, input.Handplant is not null, delta );
		}
		else
		{
			_settle = 0f;
			_flipDrop = 0f;
			_flipDropRate = 0f;
			LetGoOfDeck();
			// Getting on, the feet are kept out of the deck as they step onto it, so riding does not start by pushing them
			// out in one frame; off the board a push left from riding eases out instead of the leg dropping in one frame.
			if ( Finite( model ) && Finite( boardShown ) && Finite( _positions[ 0 ] ) )
			{
				for ( var i = 0; i < _feet.Length; i++ )
				{
					if ( input.Activity == RiderActivity.Mounting )
						OutOfDeck( i, model, boardShown, delta, rising: true );
					else
						ReleaseDeckPush( i, model, boardShown, delta );
				}
			}
		}
		SoleDeckGaps[ 0 ] = SoleDeckGaps[ 1 ] = null;
		if ( !riding )
			ClipToeHeights[ 0 ] = ClipToeHeights[ 1 ] = null;
		FootDeckGap = riding ? LowestFootGap( model, boardShown ) : null;
		if ( released )
			SoleDeckGaps[ 0 ] = SoleDeckGaps[ 1 ] = null;
		_model = model;
		Catch( input, model, delta );
		ReachForCall( input, model, mirror, delta );
		CarriedBoardPose = !riding && (input.HoldingBoard || input.Activity == RiderActivity.PickingUp)
			? CarryPose( model, mirror, input.HoldingBoard && input.Activity is not (RiderActivity.PickingUp
				or RiderActivity.Dropping or RiderActivity.Throwing), !input.HoldingBoard, delta,
				input.Activity == RiderActivity.Mounting, input.Airborne ) : null;
		if ( CarriedBoardPose is null )
		{
			EaseClearanceOut( delta );
			_offHand = 0f;
			_ontoClipBoard = 0f;
			_mountingFor = 0f;
		}
		// Off the board each foot's place in the world and on the board as drawn is kept for its step onto the riding stance
		// (see EntryTime).
		if ( !riding )
		{
			var drawn = CarriedBoardPose ?? boardShown;
			_entryBoard = Finite( drawn ) ? drawn : null;
			for ( var i = 0; i < _feet.Length; i++ )
			{
				var ankle = _positions[ _feet[ i ].Ankle ];
				_entryFrom[ i ] = Finite( model ) && Finite( ankle ) && _offHand > 0.5f ? ankle : null;
				_entryOn[ i ] = _entryFrom[ i ] is not null && _entryBoard is { } then
					? then.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( ankle ) ) ) : null;
				_entryAt[ i ] = null;
				_entryAge[ i ] = 0f;
			}
		}
		if ( Finite( model ) && Finite( _positions[ 0 ] ) )
			Settle( delta, model, mirror );
		_shownValid = Finite( model ) && Finite( _positions[ 0 ] );
		if ( _shownValid )
		{
			System.Array.Copy( _positions, _shownPositions, _positions.Length );
			System.Array.Copy( _rotations, _shownRotations, _rotations.Length );
			_shownModel = model;
			_shownMirror = mirror;
			_shownBoard = riding ? _skateboard?.RenderTransform : null;
		}
		// The model is shown from the first pose on (see RiderAnimator), never in its bind pose: the frame it appears its
		// bones take the pose at once, rather than at the next animation update.
		var appearing = _shownValid && !_renderer.GameObject.Enabled;
		if ( appearing )
			_renderer.GameObject.Enabled = true;
		var scene = _renderer.SceneModel;
		if ( scene is null )
			return;
		// Taking another body's place, the pose eases across from that body's own.
		var fade = _fadeFrom is null ? 1f : Smoothstep( Math.Clamp( _fadeTime / _fadeFor, 0f, 1f ) );
		if ( fade >= 1f )
			_fadeFrom = null;
		if ( _fadeFrom is not null && _fadeFromWorld )
		{
			var modelAt = _renderer.WorldTransform;
			for ( var bone = 0; bone < _fadeFrom.Length; bone++ )
				_fadeFrom[ bone ] = modelAt.ToLocal( _fadeFrom[ bone ] );
			_fadeFromWorld = false;
		}
		for ( var bone = 0; bone < _positions.Length; bone++ )
		{
			var transform = new Transform( SkateUnits.ToEngine( _retargeter.ToModel( _positions[ bone ] ) ),
				SkateUnits.ToEngine( _retargeter.ToModel( _rotations[ bone ] ) ) );
			if ( _fadeFrom is not null )
			{
				var from = _fadeFrom[ bone ];
				transform = new Transform( Vector3.Lerp( from.Position, transform.Position, fade ),
					Rotation.Slerp( from.Rotation, transform.Rotation, fade ) );
			}
			scene.SetBoneOverride( bone, in transform );
		}
		if ( appearing )
			scene.Update( 0f );
	}

	// The shown pose settles as one after every fit, as the clips' pose did before them (see PoseInertializer): a fit
	// coming in or letting go (a foot planted, pressed onto the deck or lifted, a hand taking hold or letting go, the
	// crouch), a knee turning over or a target jumping eases over rather than snapping, and a bone the fits stop dead
	// carries on a little and settles. Each bone's place and turn against its parent are followed, in metres; a frame
	// after a gap (the rider hidden, a hitch) starts again from the pose as given. A board carried in the hand goes where
	// the hand goes, its place in the hand kept.
	// It is followed over the pose before the fits, whose own motion (already continuous, see StateGraphPlayer) goes
	// through as the fits leave it: followed alone, a clip's kick quicker than the budget was held back (a varial
	// kickflip's front knee turned 2, 8 and then 21 degrees in successive frames as the board left the ground, and the
	// foot was thrown up to 6 in off the clip's and back). Following only what the fits change instead swung a get-up's
	// hips out and back: there the legs and hips are taken from the on-foot body, which stands still while the clip's
	// under them moves.
	// The skeleton's roots are followed in the model's frame, which turns round with the stance (riding switch faces the
	// tail) and moves with the deck's place in the clip: on the frame it does, the roots are carried into the new frame.
	// Followed on in the old frame they were eased back to where it had them, and the whole body jumped 5-6 in in a frame
	// as a turn round rolling back down a bank, or a push from fakie, handed over to riding the other way.
	private readonly PoseInertializer _shownFollow = new();
	private SkeletonPose _settled;
	private SkeletonPose _unfitted;
	private bool _unfittedValid;
	private int[] _boneParents;
	private int[] _boneRoots;

	// On the frame the stance turns round, how the model's frame moved under the pose (a point in the last frame's model
	// is Turn * point + Shift in this one, inches): the follower's roots are carried across by it (see Settle).
	private (System.Numerics.Quaternion Turn, System.Numerics.Vector3 Shift)? _reframe;
	private float _settledAt = float.NegativeInfinity;
	private const float SettleGap = 0.25f;

	// The pose as _positions and _rotations hold it now (inches, in the model's frame) against each bone's parent, in
	// metres, into pose (made to fit if it does not); false while the rig's bones are not known.
	private bool LocalPose( ref SkeletonPose pose )
	{
		if ( _boneParents is null && _renderer.Model is { } rig )
			_boneParents = rig.Bones.AllBones.OrderBy( bone => bone.Index ).Select( bone => bone.Parent?.Index ?? -1 ).ToArray();
		if ( _boneParents is not null && _boneRoots is null )
			_boneRoots = Enumerable.Range( 0, _boneParents.Length ).Where( bone => _boneParents[ bone ] < 0 ).ToArray();
		var count = _positions.Length;
		if ( _boneParents is null || _boneParents.Length != count )
			return false;
		if ( pose is null || pose.Positions.Length != count )
			pose = new SkeletonPose( count );
		for ( var bone = 0; bone < count; bone++ )
		{
			var parent = _boneParents[ bone ];
			if ( parent >= bone )
				return false;
			if ( parent < 0 )
			{
				pose.Positions[ bone ] = _positions[ bone ] / SkateUnits.InchesPerMetre;
				pose.Rotations[ bone ] = System.Numerics.Quaternion.Normalize( _rotations[ bone ] );
				continue;
			}
			var undo = System.Numerics.Quaternion.Conjugate( System.Numerics.Quaternion.Normalize( _rotations[ parent ] ) );
			pose.Positions[ bone ] = System.Numerics.Vector3.Transform( _positions[ bone ] - _positions[ parent ], undo )
				/ SkateUnits.InchesPerMetre;
			pose.Rotations[ bone ] = System.Numerics.Quaternion.Normalize( undo * _rotations[ bone ] );
		}
		return true;
	}

	private void Settle( float delta, Transform model, bool mirror )
	{
		if ( !LocalPose( ref _settled ) )
			return;
		var count = _positions.Length;
		if ( Time.Now - _settledAt > SettleGap )
			_shownFollow.Reset();
		_settledAt = Time.Now;
		var hand = mirror ? _mirroredCarryHand : _carryHand;
		Transform? handBefore = hand >= 0 && CarriedBoardPose is not null ? HandOnModel( model, hand ) : null;
		if ( _reframe is { } frame && _boneRoots is not null )
			_shownFollow.Reframe( _boneRoots, frame.Turn, frame.Shift / SkateUnits.InchesPerMetre );
		_reframe = null;
		_shownFollow.Apply( _settled, delta, _unfittedValid && _unfitted?.Positions.Length == count ? _unfitted : null );
		_unfittedValid = false;
		for ( var bone = 0; bone < count; bone++ )
		{
			var parent = _boneParents[ bone ];
			var place = _settled.Positions[ bone ] * SkateUnits.InchesPerMetre;
			if ( parent < 0 )
			{
				_positions[ bone ] = place;
				_rotations[ bone ] = _settled.Rotations[ bone ];
				continue;
			}
			_positions[ bone ] = _positions[ parent ] + System.Numerics.Vector3.Transform( place, _rotations[ parent ] );
			_rotations[ bone ] = System.Numerics.Quaternion.Normalize( _rotations[ parent ] * _settled.Rotations[ bone ] );
		}
		if ( handBefore is { } before && CarriedBoardPose is { } carried )
		{
			var moved = HandOnModel( model, hand ).ToWorld( before.ToLocal( carried ) );
			if ( Finite( moved ) )
				CarriedBoardPose = moved;
		}
	}

	// A bone's place and turn in the world, as the model shows it.
	private Transform HandOnModel( Transform model, int bone ) =>
		new( model.PointToWorld( SkateUnits.ToEngine( _positions[ bone ] ) ), model.Rotation * SkateUnits.ToEngine( _rotations[ bone ] ) );

	// The clip whose hand streams pose the fingers, and how far into it the body is: the state graph's own, or a
	// requested motion timed from when it started.
	private AnimationClip FingerClip( float delta, out float time )
	{
		if ( _states is not null && !_director.Requesting )
		{
			time = _states.Time;
			return _states.Clip;
		}
		var name = _graph.ClipName;
		_requestedTime = name == _requestedClip ? _requestedTime + delta : 0f;
		_requestedClip = name;
		time = _requestedTime;
		return name is null ? null : _clipSet.Find( name );
	}

	/// <param name="reaching">Whether the hand reaches for a board still on its way: nothing is in it yet.</param>
	private Transform? CarryPose( Transform model, bool mirror, bool carried, bool reaching, float delta, bool mounting = false,
		bool airborne = false )
	{
		var hand = mirror ? _mirroredCarryHand : _carryHand;
		_lastMirror = mirror;
		if ( _anchor < 0 || hand < 0 )
			return null;
		var sourceHand = _retargeter.SourceBone( hand, mirror );
		if ( sourceHand < 0 )
			return null;
		var boardPosition = _retargeter.SourcePosition( _anchor );
		var boardRotation = _retargeter.SourceRotation( _anchor );
		if ( _carryAttachment is not null )
		{
			var corrected = _carryAttachment.Update( _retargeter, _anchor, sourceHand );
			boardPosition = corrected.Translation;
			var rotation = PhysicalPoseOutput.Rotation( corrected.Rotation );
			boardRotation = new( rotation.X, rotation.Y, rotation.Z, rotation.W );
		}
		var pose = RetargetedAttachment.Place( boardPosition,
			boardRotation, _retargeter.SourcePosition( sourceHand ),
			_positions[ hand ], _retargeter.Scale, mirror );
		pose = _fit.Held( pose, _positions[ hand ] );
		var held = new Transform( model.PointToWorld( SkateUnits.ToEngine( pose.Position ) ),
			model.Rotation * SkateUnits.ToEngine( pose.Rotation ), _board.WorldScale );
		// The grip is kept first, then the deck fitted between the thumb and fingers of the hand as posed now; while the grip
		// still settles (the pickup's clip handing over to the carry's) the fingers keep the sides they are on.
		// A called board flies to the hand itself, not to where a pickup clip has its own board (by the feet, as its hand
		// reaches down for it): its middle comes to a hand's breadth off the palm, toward where the clip has it.
		if ( reaching && _called )
		{
			var palm = model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) );
			var off = held.Position - palm;
			if ( off.Length > CatchInHand )
				held = held.WithPosition( palm + off.Normal * CatchInHand );
		}
		held = HoldInHand( held, model, hand, carried, delta );
		held = OutOfHand( held, model, hand, carried && _heldFor >= GripSettle, delta );
		held = CatchGive( held, model, hand );
		// Reaching for a board still on its way, the hand holds nothing yet: where the board is called to is not pushed clear
		// of the body, nor the arm with it (the clips' own board, far off as the reach begins, would swing the arm out).
		// Off the hand (set down at a lip) the board needs no room made for it beside the body.
		if ( reaching || _offHand > 0.5f )
			EaseClearanceOut( delta );
		var clear = reaching || _offHand > 0.5f ? held : ClearOfBody( held, model, hand, mirror, delta );
		return OntoClipBoard( OffHand( clear, mirror, sourceHand, carried, delta ), model, mirror, sourceHand, mounting, airborne,
			delta );
	}

	// The clips place the board by their own board bone and the hand by its arm, and each clip holds it a little
	// differently (up to 1.5 in apart between standing, running and the air), so followed clip by clip the deck slid in
	// the hand and through the fingers, and a change of clip eased each bone on its own. While the board is simply carried
	// it keeps the place in the hand the clips settle it into as the hand takes it, as a grip does, and goes wherever the
	// hand goes; picking it up and putting it down it goes where the clips put it.
	private Transform HoldInHand( Transform held, Transform model, int hand, bool carried, float delta )
	{
		var grip = new Transform( model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) ),
			model.Rotation * SkateUnits.ToEngine( _rotations[ hand ] ) );
		var placed = grip.ToLocal( held );
		// A clip whose own board is not in its hand (a pickup's, the board still down by the feet as the called board
		// arrives) says nothing of the grip: the board is held where it came into the hand until a clip has it there, and
		// the grip settles only on placements in the hand. Settled on the pickup clip's, a called board was carried 72 in
		// from the hand for as long as it was carried.
		var inHand = Finite( placed ) && placed.Position.Length <= GripReach;
		if ( !carried || !_heldSteady || hand != _heldHand || !Finite( placed ) )
		{
			_heldInHand = placed;
			if ( carried && !inHand && Finite( placed ) )
			{
				var caught = grip.ToLocal( _board.WorldTransform );
				_heldInHand = Finite( caught ) && caught.Position.Length <= GripReach ? caught
					: placed.WithPosition( placed.Position.Normal * GripReach );
			}
			_heldSteady = carried && Finite( placed );
			_heldHand = hand;
			_heldFor = 0f;
			return carried ? grip.ToWorld( _heldInHand ) : held;
		}
		if ( _heldFor < GripSettle && inHand )
		{
			_heldFor += delta;
			var follow = 1f - MathF.Exp( -HeldFollow * delta );
			_heldInHand = new Transform( Vector3.Lerp( _heldInHand.Position, placed.Position, follow ),
				Rotation.Slerp( _heldInHand.Rotation, placed.Rotation, follow ), placed.Scale );
		}
		return grip.ToWorld( _heldInHand );
	}

	// Farthest (inches) a held board's middle is from the wrist: a placement further off has the board out of the hand.
	private const float GripReach = 14f;

	// The clip's skater curls its fingers round the rail. With finger poses a rider with finger bones curls its own the
	// same way, its hand lying as the clip's does, thumb over the deck and fingers under it; the deck eases along its
	// own up axis only as far as keeps every finger on its side of the deck (a thumb above caps how far the deck may
	// rise, fingers beneath how far it may sink). Otherwise the rider keeps the fingers its model has, which at the
	// clip's grip would pass through the deck, and the deck moves the shorter way until the hand measured from the
	// rider's mesh (wrist to fingertips, with its thickness) lies against the deck instead of in it: a larger move
	// applies at once so the hand never shows inside the deck, a smaller one eases in, and the deck stays on its side
	// of the hand unless the other side is clearly shorter.
	// Gripped (the board held in a settled grip), the thumb goes over the deck and the fingers under it.
	private Transform OutOfHand( Transform held, Transform model, int hand, bool gripped, float delta )
	{
		// Nothing pushes any more: the push eases out, but never back into the hand.
		var push = MathX.Lerp( _handPush, 0f, 1f - MathF.Exp( -HandRelease * delta ) );
		if ( _fingers is not null && _fingerChains.ContainsKey( hand ) )
		{
			var room = HandRoom( held, model, hand, _handPush, gripped );
			var wanted = room is not { } bounds ? 0f
				: bounds.Lowest <= bounds.Highest ? Math.Clamp( 0f, bounds.Lowest, bounds.Highest )
				: (bounds.Lowest + bounds.Highest) * 0.5f;
			// In a settled grip, a finger inside the deck goes out to the nearest place clear of every finger at once (eased,
			// the thumb showed through it for a few frames); otherwise the deck eases.
			var inside = gripped && room is { } now && now.Lowest <= now.Highest && (_handPush < now.Lowest || _handPush > now.Highest);
			_handPush = inside ? Math.Clamp( _handPush, room.Value.Lowest, room.Value.Highest )
				: MathX.Lerp( _handPush, wanted, 1f - MathF.Exp( -HandFollow * delta ) );
			return Lifted( held );
		}
		if ( HandOverlap( held, model, hand ) is { } overlap )
		{
			// The hand is inside the deck at pushes between minus the sink and the rise; inside that range the deck
			// moves out the shorter way, keeping its side unless the other side is shorter by more than the bias.
			var (rise, sink, bias) = overlap;
			if ( push > -sink && push < rise )
			{
				var up = rise - push;
				var down = push + sink;
				var raise = _handPush > 0f ? up <= down + bias : _handPush < 0f ? up + bias < down : up <= down;
				push = raise ? rise : -sink;
			}
		}
		_handPush = push;
		return Lifted( held );
	}

	private Transform Lifted( Transform held ) =>
		MathF.Abs( _handPush ) < 0.001f ? held : held.WithPosition( held.PointToWorld( new Vector3( 0f, 0f, _handPush ) ) );

	// How far (deck units) the deck must rise for its underside to clear the holding hand and sink for its top to
	// clear it (both positive while the hand is inside the deck), and a hand thickness in deck units; null when no
	// part of the hand is over the deck.
	private (float Rise, float Sink, float Bias)? HandOverlap( Transform board, Transform model, int hand )
	{
		if ( _skateboard is null || !_hands.TryGetValue( hand, out var shape ) )
			return null;
		var wrist = model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) );
		var axis = model.Rotation * SkateUnits.ToEngine( System.Numerics.Vector3.Transform( shape.Axis, _rotations[ hand ] ) );
		var normal = board.Rotation.Up;
		var across = Vector3.Cross( normal, axis );
		across = across.Length > 0.001f ? across.Normal : board.Rotation.Left;
		var offsets = new[] { Vector3.Zero, normal, -normal, across, -across };
		var rise = float.MinValue;
		var sink = float.MinValue;
		for ( var i = 0; i <= HandSamples; i++ )
		{
			var centre = wrist + axis * (shape.Length * i / HandSamples);
			foreach ( var offset in offsets )
			{
				var local = board.PointToLocal( centre + offset * shape.Radius );
				if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
					continue;
				rise = MathF.Max( rise, local.z - surface.Bottom );
				sink = MathF.Max( sink, surface.Top - local.z );
			}
		}
		if ( rise == float.MinValue )
			return null;
		var bias = (board.PointToLocal( wrist + normal * shape.Radius ) - board.PointToLocal( wrist )).Length * HandSideBias;
		return (rise, sink, bias);
	}

	// The pushes (deck units along its up axis) between which every point of the posed fingers stays outside the deck: a
	// point above caps the push, one beneath sets its least. Null when no finger is over the deck. Each finger keeps to
	// one side as a whole, the side its first point over the deck is on at the current push (nearest the palm, which
	// holds the deck's edge): a thumb tip curling down toward the deck's middle plane stays a thumb over the deck, where
	// counting it as beneath once past that plane asked the deck to rise over it and closed the grip on the deck's middle.
	private (float Lowest, float Highest)? HandRoom( Transform board, Transform model, int hand, float push, bool gripped )
	{
		if ( _skateboard is null )
			return null;
		var lowest = float.MinValue;
		var highest = float.MaxValue;
		var points = HandPoints( model, hand );
		var thumb = _fingerChains.TryGetValue( hand, out var fingers ) ? fingers.Thumb : -1;
		var finger = -1;
		var above = false;
		for ( var i = 0; i < points.Count; i++ )
		{
			var (point, radius) = points[ i ];
			var local = board.PointToLocal( point );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
				continue;
			var thickness = (board.PointToLocal( point + board.Rotation.Up * radius ) - local).Length;
			// Each finger keeps the side of the deck its knuckle is on; a carried board is held as the clips hold it, the
			// thumb over the deck and the fingers under it, whichever side the clips' grip put them on for this rider's hand
			// (put over the thumb, the deck was carried resting on it).
			if ( _handChains[ i ] != finger )
			{
				finger = _handChains[ i ];
				above = gripped && thumb >= 0 ? finger == thumb : local.z - push >= (surface.Top + surface.Bottom) * 0.5f;
			}
			if ( above )
				highest = MathF.Min( highest, local.z - surface.Top - thickness );
			else
				lowest = MathF.Max( lowest, local.z - surface.Bottom + thickness );
		}
		return lowest == float.MinValue && highest == float.MaxValue ? null : (lowest, highest);
	}

	// Gap (deck units) between the posed hand and the deck: each point over the deck measured to the deck's face on
	// its side, less its thickness; negative inside. Null when no part of the hand is over the deck.
	private float? PosedHandGap( Transform board, Transform model, int hand )
	{
		if ( _skateboard is null )
			return null;
		float? gap = null;
		foreach ( var (point, radius) in HandPoints( model, hand ) )
		{
			var local = board.PointToLocal( point );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
				continue;
			var thickness = (board.PointToLocal( point + board.Rotation.Up * radius ) - local).Length;
			var clear = local.z >= (surface.Top + surface.Bottom) * 0.5f ? local.z - surface.Top : surface.Bottom - local.z;
			gap = MathF.Min( gap ?? float.MaxValue, clear - thickness );
		}
		return gap;
	}

	// The posed fingers as points with their thickness: each knuckle, along each finger and the fingertip past its last
	// joint.
	private List<(Vector3 Point, float Radius)> HandPoints( Transform model, int hand )
	{
		_handPoints.Clear();
		_handChains.Clear();
		if ( !_fingerChains.TryGetValue( hand, out var fingers ) )
			return _handPoints;
		void Segment( System.Numerics.Vector3 from, System.Numerics.Vector3 to, float radius )
		{
			for ( var i = 1; i <= SegmentSamples; i++ )
			{
				var point = System.Numerics.Vector3.Lerp( from, to, i / (float)SegmentSamples );
				_handPoints.Add( (model.PointToWorld( SkateUnits.ToEngine( point ) ), radius) );
			}
		}
		for ( var index = 0; index < fingers.Chains.Length; index++ )
		{
			var chain = fingers.Chains[ index ];
			_handPoints.Add( (model.PointToWorld( SkateUnits.ToEngine( _positions[ chain[ 0 ] ] ) ), fingers.Radius) );
			for ( var joint = 1; joint < chain.Length; joint++ )
				Segment( _positions[ chain[ joint - 1 ] ], _positions[ chain[ joint ] ], fingers.Radius );
			var last = _positions[ chain[ ^1 ] ];
			Segment( last, last + (last - _positions[ chain[ ^2 ] ]) * FingertipReach, fingers.Radius );
			while ( _handChains.Count < _handPoints.Count )
				_handChains.Add( index );
		}
		return _handPoints;
	}

	// A hand's fingers from the rig's finger roles, each a chain of at least two joints, which of them is the thumb (-1 for
	// none), and their thickness, a share of the longest finger's length; null without fingers.
	private (int[][] Chains, int Thumb, float Radius)? FingerChains( Model model, string side )
	{
		var named = HumanoidBones.Fingers
			.Select( finger => (Finger: finger,
				Chain: new[] { 1, 2, 3 }.Select( joint => Rig[ $"{side}_{finger}{joint}" ] ).Where( bone => bone >= 0 ).ToArray()) )
			.Where( entry => entry.Chain.Length >= 2 ).ToArray();
		if ( named.Length == 0 )
			return null;
		var chains = named.Select( entry => entry.Chain ).ToArray();
		var thumb = System.Array.FindIndex( named, entry => entry.Finger == "thumb" );
		var bones = model.Bones.AllBones;
		System.Func<int, Vector3> at = index => bones.First( bone => bone.Index == index ).LocalTransform.Position;
		var longest = chains.Max( chain =>
			Enumerable.Range( 1, chain.Length - 1 ).Sum( joint => (at( chain[ joint ] ) - at( chain[ joint - 1 ] )).Length ) );
		return (chains, thumb, longest * FingerThickness);
	}

	/// <summary>
	/// Gap (inches) between the holding hand and <paramref name="board"/> after this frame's pose: negative when the
	/// hand is inside the deck, null when no board is held or no part of the hand is over the deck.
	/// </summary>
	internal float? HandDeckGap( Transform board )
	{
		if ( CarriedBoardPose is null || _anchor < 0 )
			return null;
		var hand = _lastMirror ? _mirroredCarryHand : _carryHand;
		if ( hand >= 0 && _fingers is not null && _fingerChains.ContainsKey( hand ) )
			return PosedHandGap( board, _model, hand );
		if ( hand < 0 || HandOverlap( board, _model, hand ) is not { } overlap )
			return null;
		return overlap.Rise > 0f && overlap.Sink > 0f ? -MathF.Min( overlap.Rise, overlap.Sink )
			: MathF.Max( -overlap.Rise, -overlap.Sink );
	}

	// Follows the flying board while it is picked up; when it arrives in the hand the hand gives with it.
	private void Catch( in RiderAnimationInput input, Transform model, float delta )
	{
		if ( input.Activity == RiderActivity.PickingUp && !input.HoldingBoard )
		{
			var position = SkateUnits.ToCore( model.PointToLocal( _board.WorldPosition ) );
			_flightVelocity = _flightSeen && delta > 0f ? (position - _flightPosition) / delta : System.Numerics.Vector3.Zero;
			_flightPosition = position;
			_flightSeen = true;
			_catchElapsed = -1f;
			return;
		}
		if ( _flightSeen && input.HoldingBoard )
		{
			_catchVelocity = _flightVelocity;
			_catchElapsed = 0f;
		}
		_flightSeen = false;
		if ( _catchElapsed >= 0f )
			_catchElapsed = _catchElapsed + delta > CatchTime * 8f ? -1f : _catchElapsed + delta;
	}

	// Called to the hand from afar (a pickup begun with the board further than CallReach from the carrying hand: called
	// back, or picked up beyond reach), the board flies to the hand, and the hand reaches out for it, open, as it comes:
	// from CatchReachFrom seconds out (at the board's speed) the carrying arm reaches toward where its hand will hold the
	// incoming board, fully by CatchReachFull, never locking straight, and its fingers open flat; the board lands in the
	// open hand, which closes on it as the carry takes over, and the catch's give (see CatchGive) brings it in. Taken from
	// the clip alone, a pickup clip's other hand was raised while the board flew to where that clip has its own board, by
	// the hip, beside a hand that never moved for it.
	private bool _called;
	private bool _wasPickingUp;
	private float _callReach;
	private const float CallReach = 60f;
	private const float CatchInHand = 6f;
	private const float CatchReachFrom = 0.45f;
	private const float CatchReachFull = 0.12f;
	private const float CatchReachRelease = 0.15f;

	private void ReachForCall( in RiderAnimationInput input, Transform model, bool mirror, float delta )
	{
		var hand = mirror ? _mirroredCarryHand : _carryHand;
		var picking = input.Activity == RiderActivity.PickingUp && !input.HoldingBoard;
		if ( picking && !_wasPickingUp && hand >= 0 )
			_called = (_board.WorldPosition - model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) )).Length > CallReach;
		if ( !picking && !input.HoldingBoard )
			_called = false;
		_wasPickingUp = picking;
		var wanted = 0f;
		var target = System.Numerics.Vector3.Zero;
		if ( picking && _called && hand >= 0 && _flightSeen )
		{
			var board = SkateUnits.ToCore( model.PointToLocal( _board.WorldPosition ) );
			var toBoard = board - _positions[ hand ];
			var distance = toBoard.Length();
			var closing = MathF.Max( -System.Numerics.Vector3.Dot( _flightVelocity, distance > 1e-3f ? toBoard / distance : System.Numerics.Vector3.Zero ), 1f );
			var arriving = distance / closing;
			wanted = Smoothstep( (CatchReachFrom - arriving) / (CatchReachFrom - CatchReachFull) );
			// Where the hand will hold the board: a hand's breadth back from its middle, toward the hand.
			target = distance > CatchInHand ? board - toBoard / distance * CatchInHand : _positions[ hand ];
		}
		_callReach = wanted >= _callReach ? wanted : Approach( _callReach, wanted, delta / CatchReachRelease );
		if ( _callReach <= 0f || hand < 0 || !_arms.TryGetValue( hand, out var arm ) )
			return;
		if ( wanted > 0f )
			_reachTarget = target;
		Reach( arm, hand, System.Numerics.Vector3.Lerp( _positions[ hand ], _reachTarget, _callReach ), true );
		OpenFingers( hand, _callReach );
	}

	private System.Numerics.Vector3 _reachTarget;

	// Opens a hand's fingers toward the rig's own rest (a flat hand) by the given share: each joint's turn against the one
	// before it eases onto its rest turn, the joints past it following.
	private void OpenFingers( int hand, float share )
	{
		if ( !_fingerChains.TryGetValue( hand, out var fingers ) || share <= 0f || _renderer.Model is not { } rig )
			return;
		foreach ( var chain in fingers.Chains )
		{
			for ( var joint = 0; joint < chain.Length; joint++ )
			{
				var bone = chain[ joint ];
				if ( RestTurn( rig, bone ) is not { } known || known.Parent < 0 )
					continue;
				var (rest, parent) = known;
				var local = System.Numerics.Quaternion.Conjugate( _rotations[ parent ] ) * _rotations[ bone ];
				var opened = System.Numerics.Quaternion.Normalize( _rotations[ parent ] * System.Numerics.Quaternion.Slerp( local, rest, share ) );
				var turn = System.Numerics.Quaternion.Normalize( opened * System.Numerics.Quaternion.Conjugate( _rotations[ bone ] ) );
				var pivot = _positions[ bone ];
				_rotations[ bone ] = opened;
				for ( var after = joint + 1; after < chain.Length; after++ )
				{
					var next = chain[ after ];
					_positions[ next ] = pivot + System.Numerics.Vector3.Transform( _positions[ next ] - pivot, turn );
					_rotations[ next ] = System.Numerics.Quaternion.Normalize( turn * _rotations[ next ] );
				}
			}
		}
	}

	// A bone's rest turn against its parent in the rig, and that parent, cached.
	private readonly Dictionary<int, (System.Numerics.Quaternion Turn, int Parent)> _restTurns = new();

	private (System.Numerics.Quaternion Turn, int Parent)? RestTurn( Model rig, int bone )
	{
		if ( _restTurns.TryGetValue( bone, out var known ) )
			return known;
		foreach ( var each in rig.Bones.AllBones )
		{
			if ( each.Index != bone )
				continue;
			// Bones' rest transforms are relative to the root bone: the turn against the parent is the parent's undone.
			var parent = each.Parent;
			var turn = SkateUnits.ToCore( each.LocalTransform.Rotation );
			if ( parent is not null )
				turn = System.Numerics.Quaternion.Normalize( System.Numerics.Quaternion.Conjugate(
					SkateUnits.ToCore( parent.LocalTransform.Rotation ) ) * turn );
			var found = (turn, parent?.Index ?? -1);
			_restTurns[ bone ] = found;
			return found;
		}
		return null;
	}

	/// <summary>
	/// A body playing the model's own walk (the same model, never drawn) whose hips and legs the rider takes while it walks
	/// or carries the board on its feet, the body above the hips (spine, arms, hands, head) staying the clips' and carried
	/// with the hips; null keeps the clips' own legs.
	/// </summary>
	internal SkinnedModelRenderer LegSource { get; set; }

	/// <summary>Whether the rider's legs are the leg source's at all this frame (coming in or going out included).</summary>
	internal bool LegsShown => _legsShare > 0f;

	// How far over to the leg source's hips and legs the rider is (0 to 1), easing over LegsEase as it walks off the board
	// or onto it, picks it up or puts it down: the clips' own legs then (a pickup's crouch, a throw's step).
	private float _legsShare;
	private const float LegsEase = 0.25f;
	private int _hips = -1;
	private int[] _legBones;
	private int[] _bodyBones;
	private int[] _rigParents;
	private Transform[] _legPose;
	private bool _legsTaken;

	// The leg source's pose against each bone's parent (metres), followed for continuity before the legs take it: the
	// stock walk itself jumps, a foot going from mid-stride to the standing pose in a frame (14 in) as the walk stops, or
	// 9 in in a frame as a run's stride changes.
	private SkeletonPose _legLocal;
	private readonly PoseInertializer _legContinuity = new();

	/// <summary>
	/// Reads the leg source's whole skeleton on its model, from each bone's place against its parent, once a frame after
	/// every update (see Systems.RiderLegReader), when every skeleton is posed. Each bone carries on from what was shown
	/// (see PoseInertializer) before the legs take it.
	/// </summary>
	internal void ReadLegs()
	{
		_legsTaken = false;
		if ( LegSource?.SceneModel is not { } scene || !LegSource.GameObject.Enabled || !LegBones() )
		{
			_legContinuity.Reset();
			return;
		}
		if ( _legLocal is null || _legLocal.Positions.Length != _rigParents.Length )
			_legLocal = new SkeletonPose( _rigParents.Length );
		for ( var bone = 0; bone < _rigParents.Length; bone++ )
		{
			var local = scene.GetBoneLocalTransform( bone );
			_legLocal.Positions[ bone ] = SkateUnits.Metres( local.Position );
			_legLocal.Rotations[ bone ] = SkateUnits.ToCore( local.Rotation );
		}
		_legContinuity.Apply( _legLocal, Time.Delta );
		for ( var bone = 0; bone < _rigParents.Length; bone++ )
		{
			var local = new Transform( SkateUnits.Inches( _legLocal.Positions[ bone ] ), SkateUnits.ToEngine( _legLocal.Rotations[ bone ] ) );
			var parent = _rigParents[ bone ];
			_legPose[ bone ] = parent >= 0 && parent < bone ? _legPose[ parent ].ToWorld( local ) : local;
		}
		_legsTaken = true;
	}

	private void TakeLegs( in RiderAnimationInput input, Transform model, float delta )
	{
		var wanted = LegSource is not null && input.Activity is RiderActivity.Walking or RiderActivity.Carrying ? 1f : 0f;
		_legsShare = Approach( _legsShare, wanted, delta / LegsEase );
		// Taken only once ReadLegs has read the source whole (its bones found, see LegBones).
		if ( _legsShare <= 0f || !_legsTaken )
			return;
		var weight = Smoothstep( _legsShare );
		var drawn = _renderer.WorldTransform;
		// A bone of the source in this frame's model frame: its place on the source's model (as last read whole, see
		// ReadLegs), put where the rider is drawn.
		bool Source( int bone, out System.Numerics.Vector3 position, out System.Numerics.Quaternion rotation )
		{
			var world = drawn.ToWorld( _legPose[ bone ] );
			position = SkateUnits.ToCore( model.PointToLocal( world.Position ) );
			rotation = SkateUnits.ToCore( model.Rotation.Inverse * world.Rotation );
			return Finite( position ) && float.IsFinite( rotation.X + rotation.Y + rotation.Z + rotation.W );
		}
		if ( !Source( _hips, out var hipsAt, out var hipsTurn ) )
			return;
		// The hips go over to the source's, carrying the body above them; the legs take the source's own.
		var hipsWas = _positions[ _hips ];
		var turnWas = _rotations[ _hips ];
		var hipsNow = System.Numerics.Vector3.Lerp( hipsWas, hipsAt, weight );
		var turnNow = System.Numerics.Quaternion.Normalize( System.Numerics.Quaternion.Slerp( turnWas, hipsTurn, weight ) );
		var carry = System.Numerics.Quaternion.Normalize( turnNow * System.Numerics.Quaternion.Conjugate( turnWas ) );
		foreach ( var bone in _bodyBones )
		{
			_positions[ bone ] = hipsNow + System.Numerics.Vector3.Transform( _positions[ bone ] - hipsWas, carry );
			_rotations[ bone ] = System.Numerics.Quaternion.Normalize( carry * _rotations[ bone ] );
		}
		_positions[ _hips ] = hipsNow;
		_rotations[ _hips ] = turnNow;
		foreach ( var bone in _legBones )
		{
			if ( !Source( bone, out var at, out var turn ) )
				continue;
			_positions[ bone ] = System.Numerics.Vector3.Lerp( _positions[ bone ], at, weight );
			_rotations[ bone ] = System.Numerics.Quaternion.Normalize( System.Numerics.Quaternion.Slerp( _rotations[ bone ], turn, weight ) );
		}
	}

	// On foot, the feet the body stands on are held where they were put down, stepping over when the pose takes one
	// further (see StanceLock), as the original holds a walking body's planted foot: the stock walk takes up a change
	// of the body's speed a few frames late, and coming to a stop from a run the body stood still while its legs ran
	// on, the feet sliding up to 340 in/s for a tenth of a second; stepping off the board into a run, the clip's
	// planted foot swept 77-226 in/s across the ground under a body getting going at 35-76. Not while getting on (the
	// clip steps up onto the deck), bailed, or landing, whose own hold keeps the feet it plants (see HoldPlanted). Both
	// feet are held while both are down, where the original holds one only: here the body goes as the controls ask
	// rather than as its legs carry it, and setting off from a stand, at a walking pace at once, slid both feet 3.5 in
	// under it. A foot is planted while its ball is within StanceClearance (metres) of the ground the model stands on,
	// its heel no more than HeelRise up, and it is held by its ball: a foot rolls off the ground over its ball, and let
	// go as its heel began to rise, the ball of a walk's planted foot was swept 220-500 in/s across the ground as the
	// walk handed over to a run.
	private readonly StanceLock _stance = new();
	private readonly System.Numerics.Vector3[] _stanceFree = new System.Numerics.Vector3[ 2 ];
	private readonly System.Numerics.Vector3[] _stanceShown = new System.Numerics.Vector3[ 2 ];
	private readonly bool[] _stancePlanted = new bool[ 2 ];
	private const float StanceClearance = 0.02f;
	private const float HeelRise = 0.06f;

	/// <summary>On foot, how many feet are held where they were put down (see HoldStance).</summary>
	internal int FeetHeld => _stance.Held;

	private void HoldStance( in RiderAnimationInput input, Transform model, float delta )
	{
		var onFeet = _feet.Length == 2 && !input.Airborne && State?.Contains( ".Landing." ) != true
			&& input.Activity is not (RiderActivity.Riding or RiderActivity.Mounting or RiderActivity.Bailed)
			&& Finite( model ) && Finite( _positions[ 0 ] );
		if ( !onFeet )
		{
			_stance.Reset();
			return;
		}
		var clearance = StanceClearance * SkateUnits.InchesPerMetre;
		var heelRise = HeelRise * SkateUnits.InchesPerMetre;
		for ( var i = 0; i < 2; i++ )
		{
			var foot = _feet[ i ];
			_stancePlanted[ i ] = _positions[ foot.Ball ].Z - foot.BallHeight <= clearance
				&& _positions[ foot.Ankle ].Z - foot.AnkleHeight <= heelRise;
			// The hold works in metres; the model is in inches.
			_stanceFree[ i ] = SkateUnits.Metres( model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Ball ] ) ) );
			_stanceShown[ i ] = _stanceFree[ i ];
		}
		_stance.Update( _stanceShown, _stancePlanted, delta );
		for ( var i = 0; i < 2; i++ )
		{
			var foot = _feet[ i ];
			var moved = SkateUnits.ToCore( model.Rotation.Inverse * SkateUnits.Inches( _stanceShown[ i ] - _stanceFree[ i ] ) );
			// Kept out of the ground under it, as the clips' feet are (see LiftOnFoot): the stock run's feet dip up to 0.6 in
			// into it.
			var sole = MathF.Min( _positions[ foot.Ankle ].Z - foot.AnkleHeight, _positions[ foot.Ball ].Z - foot.BallHeight ) + moved.Z;
			moved.Z += Math.Clamp( -sole, 0f, MaximumLift );
			if ( moved.LengthSquared() < 1e-6f )
				continue;
			foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + moved, _kneeHint[ i ] );
		}
	}

	// The hips, the legs (each thigh and every bone below it) and the body above the hips (every other bone below them).
	private bool LegBones()
	{
		if ( _legBones is not null )
			return _hips >= 0;
		_legBones = System.Array.Empty<int>();
		_bodyBones = System.Array.Empty<int>();
		_hips = Rig[ "hips" ];
		var thighs = new[] { Rig[ "left_upleg" ], Rig[ "right_upleg" ] };
		if ( _hips < 0 || thighs[ 0 ] < 0 || thighs[ 1 ] < 0 || _renderer.Model is not { } rig )
		{
			_hips = -1;
			return false;
		}
		var parents = rig.Bones.AllBones.OrderBy( bone => bone.Index ).Select( bone => bone.Parent?.Index ?? -1 ).ToArray();
		_rigParents = parents;
		_legPose = new Transform[ parents.Length ];
		bool Under( int bone, int above )
		{
			for ( var at = bone; at >= 0; at = parents[ at ] )
			{
				if ( at == above )
					return true;
			}
			return false;
		}
		_legBones = Enumerable.Range( 0, parents.Length ).Where( bone => Under( bone, thighs[ 0 ] ) || Under( bone, thighs[ 1 ] ) ).ToArray();
		_bodyBones = Enumerable.Range( 0, parents.Length )
			.Where( bone => bone != _hips && Under( bone, _hips ) && !_legBones.Contains( bone ) ).ToArray();
		return true;
	}

	// The catch's give: the arrival velocity carried on and eased back, t * exp( -t / CatchTime ), its furthest reach
	// limited to a share of this rider's forearm. The holding arm reaches the moved hand.
	private Transform CatchGive( Transform held, Transform model, int hand )
	{
		if ( _catchElapsed < 0f || !_arms.TryGetValue( hand, out var arm ) )
			return held;
		var forearm = System.Numerics.Vector3.Distance( _positions[ arm.Lower ], _positions[ hand ] ) * CatchReach;
		// The give peaks at speed * CatchTime / e; a faster arrival is scaled down to reach no further than the limit.
		var speed = _catchVelocity.Length();
		var peak = speed * CatchTime / MathF.E;
		var velocity = peak > forearm && peak > 0f ? _catchVelocity * (forearm / peak) : _catchVelocity;
		var give = velocity * (_catchElapsed * MathF.Exp( -_catchElapsed / CatchTime ));
		if ( give.LengthSquared() < 1e-6f )
			return held;
		var before = _positions[ hand ];
		Reach( arm, hand, before + give, true );
		return held.WithPosition( held.Position + model.Rotation * SkateUnits.ToEngine( _positions[ hand ] - before ) );
	}

	// The board is placed for the clip's skater; a rider with other proportions may have legs or a torso where the
	// board hangs. The board moves out from the body just enough to clear this rider, measured from its own mesh,
	// and the holding arm reaches out with it so the grip stays; it eases back in once the body moves away.
	private Transform ClearOfBody( Transform held, Transform model, int hand, bool mirror, float delta )
	{
		if ( _skateboard?.BoardBounds is not { } box || !_arms.TryGetValue( hand, out var arm ) || Rig[ "hips" ] < 0 )
			return held;
		var side = mirror ? 1 : 0;
		_clearance[ side ] ??= new BodyClearance( _renderer.Model, Rig, hand );
		System.Func<int, Vector3> world = bone => model.PointToWorld( SkateUnits.ToEngine( _positions[ bone ] ) );
		var outward = held.PointToWorld( box.Center ) - world( Rig[ "hips" ] );
		var away = (outward - model.Rotation.Up * Vector3.Dot( outward, model.Rotation.Up )).Normal;
		var needed = _clearance[ side ].Push( held, box, away, world );
		// A larger push follows at once up to a rate (the body turning fast into the board would otherwise throw the arm
		// out in one frame); a smaller one eases in.
		FollowClearance( needed, delta );
		_clearanceAway = model.Rotation.Inverse * away;
		_clearanceHand = hand;
		if ( _clearancePush < 0.01f )
			return held;
		var shift = away * _clearancePush;
		var before = _positions[ hand ];
		Reach( arm, hand, before + SkateUnits.ToCore( model.Rotation.Inverse * shift ), true );
		// The board goes as far as the hand got: an arm drawn in short of the whole push takes it only so far.
		return held.WithPosition( held.Position + model.Rotation * SkateUnits.ToEngine( _positions[ hand ] - before ) );
	}

	// The push (inches) toward what the body needs, as a critically damped spring, in sub-steps short enough to stay
	// steady at any frame rate; never below nothing.
	private void FollowClearance( float needed, float delta )
	{
		var steps = Math.Max( 1, (int)MathF.Ceiling( delta * ClearanceSteps ) );
		var step = delta / steps;
		for ( var i = 0; i < steps; i++ )
		{
			var frequency = needed >= _clearancePush ? ClearanceOut : ClearanceIn;
			_clearanceSpeed += (frequency * frequency * (needed - _clearancePush) - 2f * frequency * _clearanceSpeed) * step;
			_clearancePush += _clearanceSpeed * step;
			if ( _clearancePush < 0f )
			{
				_clearancePush = 0f;
				_clearanceSpeed = 0f;
			}
		}
	}

	// Sub-steps per second of the clearance spring.
	private const float ClearanceSteps = 240f;

	// With no board in the hand to clear, the arm's reach out from the body eases back in as the spring has it rather than
	// dropping back to the clip's arm in one frame.
	private void EaseClearanceOut( float delta )
	{
		if ( _clearancePush <= 0f )
			return;
		FollowClearance( 0f, delta );
		if ( _clearancePush < 0.01f || !_arms.TryGetValue( _clearanceHand, out var arm ) )
		{
			_clearancePush = 0f;
			_clearanceSpeed = 0f;
			return;
		}
		Reach( arm, _clearanceHand, _positions[ _clearanceHand ] + SkateUnits.ToCore( _clearanceAway * _clearancePush ), true );
	}

	// Moves a hand to a place with its arm, the elbow pointing as it did a moment before: a carried board's arm hangs
	// nearly straight, where the clip's own bend is noise and the elbow swung round from one frame to the next.
	// Softened, the arm (the carrying one: the board held clear of the body, the catch's give) never locks straight: past
	// SoftReach of its length the target is drawn in toward the shoulder, toward the full length but never reaching it, and
	// never nearer the shoulder than the clip has the hand. Locked straight, the last 0.3 in of reach turned the elbow 17-21
	// degrees in a frame. A grab's hand fitted to the deck's edge reaches it as before.
	private void Reach( TwoBoneIk arm, int hand, System.Numerics.Vector3 target, bool soften = false )
	{
		var fresh = _elbows.TryGetValue( hand, out var last ) && Time.Now - last.Time < ElbowMemory;
		if ( soften )
			target = SoftenArm( arm, hand, target );
		arm.Solve( _positions, _rotations, target, fresh ? last.Pole : System.Numerics.Vector3.Zero );
		if ( fresh )
		{
			// A straight arm says nothing of where its elbow points: the remembered way is kept.
			var swung = arm.LimitSwing( _positions, _rotations, last.Pole, ElbowSwing * (Time.Now - last.Time) );
			_elbows[ hand ] = (swung != System.Numerics.Vector3.Zero ? swung : last.Pole, Time.Now);
			return;
		}
		var shoulder = _positions[ arm.Upper ];
		var line = _positions[ hand ] - shoulder;
		if ( line.LengthSquared() < 1e-8f )
			return;
		line = System.Numerics.Vector3.Normalize( line );
		var elbow = _positions[ arm.Lower ] - shoulder;
		var pole = elbow - line * System.Numerics.Vector3.Dot( elbow, line );
		if ( pole.LengthSquared() > 1e-8f )
			_elbows[ hand ] = (System.Numerics.Vector3.Normalize( pole ), Time.Now);
	}

	private System.Numerics.Vector3 SoftenArm( TwoBoneIk arm, int hand, System.Numerics.Vector3 target )
	{
		var shoulder = _positions[ arm.Upper ];
		var length = System.Numerics.Vector3.Distance( shoulder, _positions[ arm.Lower ] )
			+ System.Numerics.Vector3.Distance( _positions[ arm.Lower ], _positions[ hand ] );
		var soft = length * SoftReach;
		var span = length - soft;
		var offset = target - shoulder;
		var distance = offset.Length();
		if ( distance <= soft || span <= 0f || distance < 1e-4f || !float.IsFinite( distance ) )
			return target;
		var reach = soft + span * (1f - MathF.Exp( -(distance - soft) / span ));
		reach = MathF.Max( reach, MathF.Min( System.Numerics.Vector3.Distance( shoulder, _positions[ hand ] ), distance ) );
		return shoulder + offset * (reach / distance);
	}

	// While riding, each leg reaches for where the clip has its toes relative to the clip's board, carried by the
	// real board, so feet stay on the deck through bumps, tilts and flips and a pushing foot reaches the ground
	// beside it. The pull fades in and out over a short time and lets go while a state releases the feet.
	// In the air the clip's board (flipping, spinning, grabbed) is shown instead of the physical one, so the feet
	// and hands meet the board the animation was authored around; on the ground the physical board shows again.
	// The pose comes from the clip's board bone at the rider's measured scale, so any rider model fits.
	private Transform ShowAnimatedBoard( Transform model, bool mirror, bool airborne, float delta, bool riding = true )
	{
		if ( _skateboard is null || _anchor < 0 )
			return _board.WorldTransform;
		// The rider stands raised with the shown board out of a rail (see the root); the clip's board is handed over without
		// that raise, which the board's own rail clearance puts back.
		var animated = new Transform( model.PointToWorld( SkateUnits.ToEngine( _retargeter.SourceOffset( _anchor, mirror ) ) )
			- _skateboard.RailLift, model.Rotation * SkateUnits.ToEngine( _retargeter.SourceRotation( _anchor, mirror ) ) * SwitchTurn,
			_board.WorldScale );
		// Off the board, the clip's board no longer means the real one (the rider's own clip moves on): the board is handed
		// back from where it was shown as the rider let go of it, carried along as the physical board travels. Only its
		// travel: physics turning the board over as the rider steps off it (a board held dark side up is laid down dark
		// side up) would turn the shown board over with it, upright for a frame with its trucks and wheels in the floor.
		// So it is while the clips would turn the shown board over as they hand it back (a dark board's clip ending: the next
		// clip's board lies the other way up): it stays the way up it was shown.
		if ( airborne || _boardShown <= 0f )
			_letGo = null;
		var physical = _skateboard.RenderTransform;
		if ( Landing( airborne, riding, animated, physical, delta ) is { } landed )
			return landed;
		if ( !airborne && _boardShown > 0f && (!riding || _letGo is not null
			|| Vector3.Dot( animated.Rotation.Up, _skateboard.DisplayedTransform.Rotation.Up ) < 0f) )
		{
			if ( _letGo is null )
			{
				_letGo = _skateboard.DisplayedTransform;
				_letGoFrom = _skateboard.RenderTransform.Position;
			}
			animated = _letGo.Value.WithPosition( _letGo.Value.Position + (_skateboard.RenderTransform.Position - _letGoFrom) );
		}
		_animatedBoard = animated;
		// Taking the shown board over from physics, it moves no faster than HandOverSpeed beyond the physical one: a clip's
		// board far from the physical one (a board leaving a rail tipped onto a truck) takes over more slowly than the
		// usual take-over time. Handing back keeps its time, so the board never trails a rider moving off it.
		var rate = airborne
			? MathF.Min( delta / BoardTakeOver, HandOverSpeed * delta / MathF.Max( BoardGap( animated ), 0.01f ) )
			: delta / BoardHandBack;
		_boardShown = Math.Clamp( _boardShown + (airborne ? rate : -rate), 0f, 1f );
		_skateboard.SetDisplayPose( animated, _boardShown );
		_skateboard.HoldSlidePitch( riding && State?.Contains( ".Grinding." ) == true );
		// In the air the shown board's place on the physical one is kept, for a landing to carry it on from.
		if ( airborne && _boardShown > 0f && Finite( physical ) )
		{
			_shownOnBefore = _shownOn;
			_shownOn = physical.ToLocal( _skateboard.DisplayedTransform );
			_shownStep = delta;
		}
		else
		{
			_shownOn = _shownOnBefore = null;
		}
		return _skateboard.DisplayedTransform;
	}

	// Landing while riding, the shown board goes over to the physical one from where it was shown, carried on as it was
	// moving against the physical board and slowing (over LandCarry), and over a time long enough that it turns no faster
	// than HandBackTurn and moves no faster than HandBackMove beyond the physical board, easing in and out. Handed back
	// in a fixed 0.15 s from the clips' board, a board turned under the feet in the air (a board adjust held through the
	// landing, 72-80 degrees round) swung back at 500 degrees a second the moment it landed, the feet with it.
	// A board is the same end for end: one landing within OtherEnd of the physical board's other end (a shove-it caught
	// late) is handed over from that end, on round the way it was spinning, rather than turned back across its spin.
	private Transform? Landing( bool airborne, bool riding, Transform animated, Transform physical, float delta )
	{
		var landing = !airborne && riding && _boardShown > 0f && _letGo is null && Finite( physical )
			&& Vector3.Dot( animated.Rotation.Up, _skateboard.DisplayedTransform.Rotation.Up ) >= 0f;
		if ( !landing )
		{
			_landFrom = null;
			return null;
		}
		if ( _landFrom is null )
		{
			if ( _shownOn is not { } from )
				return null;
			_landMove = Vector3.Zero;
			_landTurn = System.Numerics.Vector3.Zero;
			if ( _shownOnBefore is { } before && _shownStep > 0f )
			{
				_landMove = (from.Position - before.Position) / _shownStep;
				_landTurn = TurnBetween( SkateUnits.ToCore( NearerEnd( before.Rotation, from.Rotation ) ), SkateUnits.ToCore( from.Rotation ) )
					/ _shownStep;
			}
			if ( TurnAngle( from.Rotation * HalfTurn ) < OtherEnd )
				from = from.WithRotation( from.Rotation * HalfTurn );
			_landFrom = from;
			var turn = TurnBetween( System.Numerics.Quaternion.Identity, SkateUnits.ToCore( from.Rotation ) ).Length() + _landTurn.Length() * LandCarry;
			var move = from.Position.Length + _landMove.Length * LandCarry;
			_landLength = MathF.Max( BoardHandBack, 1.5f * MathF.Max( turn / HandBackTurn, move / HandBackMove ) );
			_landTime = 0f;
			_landWeight = _boardShown;
		}
		_landTime += delta;
		var start = _landFrom.Value;
		var carried = LandCarry * (1f - MathF.Exp( -_landTime / LandCarry ));
		var dead = new Transform( start.Position + _landMove * carried,
			SkateUnits.ToEngine( TurnBy( _landTurn * carried ) ) * start.Rotation, start.Scale );
		animated = physical.ToWorld( dead );
		_boardShown = _landTime >= _landLength ? 0f : _landWeight * (1f - Smoothstep( _landTime / _landLength ));
		if ( _boardShown <= 0f )
			_landFrom = null;
		_animatedBoard = animated;
		_skateboard.SetDisplayPose( animated, _boardShown );
		_skateboard.HoldSlidePitch( State?.Contains( ".Grinding." ) == true );
		_shownOn = _shownOnBefore = null;
		return _skateboard.DisplayedTransform;
	}

	// The shown board in the physical board's frame over the last two frames in the air, and the time between them; a
	// landing's start, how it was moving then (inches a second, and radians a second as a rotation vector, in the physical
	// board's frame), how long it has been going, how long it takes, and the shown weight it started at.
	private Transform? _shownOn;
	private Transform? _shownOnBefore;
	private float _shownStep;
	private Transform? _landFrom;
	private Vector3 _landMove;
	private System.Numerics.Vector3 _landTurn;
	private float _landTime;
	private float _landLength;
	private float _landWeight;
	private const float LandCarry = 0.03f;
	private const float HandBackTurn = 6f;
	private const float HandBackMove = 40f;

	// A half turn about a board's own up, and how near (radians) a board must come to another's other end to be taken as
	// that end (as StateGraphPlayer takes a clip's board).
	private static readonly Rotation HalfTurn = Rotation.FromYaw( 180f );
	private const float OtherEnd = MathF.PI / 3f;

	// The angle (radians) a rotation turns by, the shorter way round.
	private static float TurnAngle( Rotation rotation ) => 2f * MathF.Acos( Math.Min( 1f, MathF.Abs( rotation.w ) ) );

	// A board's rotation, or the same board turned end for end, whichever is nearer another.
	private static Rotation NearerEnd( Rotation board, Rotation other ) =>
		TurnAngle( other.Inverse * board * HalfTurn ) < TurnAngle( other.Inverse * board ) ? board * HalfTurn : board;

	// The rotation vector (axis times radians) turning one rotation into another, the shorter way round.
	private static System.Numerics.Vector3 TurnBetween( System.Numerics.Quaternion from, System.Numerics.Quaternion to )
	{
		var turn = System.Numerics.Quaternion.Normalize( to * System.Numerics.Quaternion.Conjugate( from ) );
		if ( turn.W < 0f )
			turn = System.Numerics.Quaternion.Negate( turn );
		var axis = new System.Numerics.Vector3( turn.X, turn.Y, turn.Z );
		var sine = axis.Length();
		return sine < 1e-7f ? axis * 2f : axis * (2f * MathF.Atan2( sine, turn.W ) / sine);
	}

	private static System.Numerics.Quaternion TurnBy( System.Numerics.Vector3 turn )
	{
		var angle = turn.Length();
		return angle < 1e-7f || !float.IsFinite( angle ) ? System.Numerics.Quaternion.Identity
			: System.Numerics.Quaternion.CreateFromAxisAngle( turn / angle, angle );
	}

	// Where the shown board was as the rider let go of it, and where the physical board was then, while it is handed back.
	private Transform? _letGo;
	private Vector3 _letGoFrom;

	// Off the hand: dropping in, the clips let go of the board as they set it on the coping, and from where their hand lets
	// go it is placed where they have it against where their skater stands, from where the rider stands (placed from the
	// hand it moved with the arm, and stepping up it slid along the coping). How far it has gone over to that (0 to 1,
	// eased over OffHandEase as the clip's hand lets go of it or takes it again), the clip's own source distance (metres)
	// from hand to board up to which the board is in its hand, and the playing clip's root travel.
	private float _offHand;
	private const float OffHandEase = 0.25f;

	// Getting on from the hand, the board goes down under the rider where the clip has its board, not where the hand holds
	// it (at the end of a standing mount 5.4 in higher and 3.6 in across): put down from the hand it jumped 6-23 in in a frame,
	// and shown going on from the hand the feet, inside the carried deck, were thrown out of it. Over the mount clip's last
	// MountSettle seconds, or as the clip's hand lets go of its board, the board goes over from the hand to the clip's own
	// board placed from the body, so it is put down where it already is, the feet on it.
	private Transform OntoClipBoard( Transform held, Transform model, bool mirror, int sourceHand, bool mounting, bool airborne,
		float delta )
	{
		// In the air the board goes down when the mount has run AirMountPutDown, not at its clip's end: over that time it goes
		// over from the hand (timed by the clip's end, it was not yet over when put down and jumped 8.4 in).
		_mountingFor = mounting ? _mountingFor + delta : 0f;
		if ( _mountingFor <= delta )
			_mountInAir = mounting && airborne;
		var wanted = 0f;
		// A board set off the hand at a lip (a drop-in) is already where the clip has its board, on the coping.
		if ( mounting && BoardRestFrame is null && _offHand <= 0.5f && _states?.Clip is { } clip )
		{
			var left = (clip.Length( _parameters ) - _states.Time) / MathF.Max( _travelRate, 0.01f );
			var reach = System.Numerics.Vector3.Distance( _retargeter.SourcePosition( sourceHand ), _retargeter.SourcePosition( _anchor ) );
			var letGo = Smoothstep( Math.Clamp( (reach - HandHolds) / (ClipRootTravel.OutOfHand - HandHolds), 0f, 1f ) );
			var ending = _mountInAir ? Math.Clamp( _mountingFor / AirMountPutDown, 0f, 1f )
				: float.IsFinite( left ) ? 1f - Math.Clamp( left / MountSettle, 0f, 1f ) : 0f;
			wanted = MathF.Max( letGo, ending );
		}
		_ontoClipBoard = Approach( _ontoClipBoard, wanted, delta / MountSettleFollow );
		if ( _ontoClipBoard <= 0f )
			return held;
		var board = new Transform( model.PointToWorld( SkateUnits.ToEngine( _retargeter.SourceOffset( _anchor, mirror ) ) ),
			model.Rotation * SkateUnits.ToEngine( _retargeter.SourceRotation( _anchor, mirror ) ) * SwitchTurn, held.Scale );
		if ( !Finite( board ) )
			return held;
		var weight = Smoothstep( _ontoClipBoard );
		return new Transform( Vector3.Lerp( held.Position, board.Position, weight ),
			Rotation.Slerp( held.Rotation, board.Rotation, weight ), held.Scale );
	}

	// How far over (0 to 1) a board being got on with has gone from the hand to the clip's own board; the seconds before the
	// mount clip's end over which it goes, and the shortest time it may take to follow a change.
	private float _ontoClipBoard;
	private const float MountSettle = 0.3f;
	// Seconds since getting on from the hand began, whether it began in the air, and how long an air mount from the hand
	// runs before the board goes down (RiderInteraction's AirMountTime).
	private float _mountingFor;
	private bool _mountInAir;
	private const float AirMountPutDown = 0.25f;
	private const float MountSettleFollow = 0.1f;
	private const float HandHolds = 0.24f;
	private readonly ClipRootTravel _travel;
	private bool _travelling;
	private float _travelRate = 1f;

	/// <summary>
	/// While a landing on foot plays, the way and speed (inches per second, world) its clip's root travels: the clip carries
	/// the body on over its planted feet, and the walker going so keeps them planted. Null otherwise.
	/// </summary>
	internal Vector3? LandingTravel { get; private set; }

	/// <summary>Where the rider stands at a drop-in's lip, facing over it, while it drops in; null otherwise.</summary>
	internal Transform? BoardRestFrame { get; set; }

	/// <summary>How far through its own travel the playing clip is (0 to 1), or null for a clip that does not travel.</summary>
	internal float? TravelProgress => _travelling ? _travel.Progress : null;

	/// <summary>
	/// Where the playing clip's root is against where its skater stands still by the board (in the frame the rider stands in,
	/// inches), or null for no clip: stepping over a drop-in's lip it goes to the side, onto the board.
	/// </summary>
	internal Vector3? TravelOffset { get; private set; }

	// The board placed where the clip has it off its hand, against where its skater stands, from the stand at the lip;
	// eased over from the board in the hand as the clip's hand lets go of it.
	private Transform OffHand( Transform held, bool mirror, int sourceHand, bool carried, float delta )
	{
		var reach = System.Numerics.Vector3.Distance( _retargeter.SourcePosition( sourceHand ), _retargeter.SourcePosition( _anchor ) );
		var free = carried && _travelling && BoardRestFrame is not null
			? Smoothstep( Math.Clamp( (reach - HandHolds) / (ClipRootTravel.OutOfHand - HandHolds), 0f, 1f ) ) : 0f;
		_offHand = Approach( _offHand, free, delta / OffHandEase );
		if ( _offHand <= 0f || BoardRestFrame is not { } stand )
			return held;
		var travel = _travel.FromStand;
		if ( mirror )
			travel.Y = -travel.Y;
		var offset = _retargeter.SourceOffset( _anchor, mirror ) + travel * _retargeter.Scale;
		var rest = new Transform( stand.PointToWorld( SkateUnits.ToEngine( offset ) ),
			stand.Rotation * SkateUnits.ToEngine( _retargeter.SourceRotation( _anchor, mirror ) ), held.Scale );
		var weight = Smoothstep( _offHand );
		return new Transform( Vector3.Lerp( held.Position, rest.Position, weight ),
			Rotation.Slerp( held.Rotation, rest.Rotation, weight ), held.Scale );
	}

	// The board is in the air and the clips show it: leaving a rail without a trick (rolling off its end, dropping off its
	// side) it falls as physics has it, as it rode the rail, since the air clips' board, made for a board under the feet,
	// would swing the shown board about by the rail it just left; a pop, flip or grab from there shows the clips' board
	// as any trick does.
	private bool Flying( in RiderAnimationInput input )
	{
		_fromRail = !string.IsNullOrEmpty( input.Grind ) || (_fromRail && input.Airborne);
		var trick = input.Trick is not null || input.Grab is not null || input.LateTrick is not null
			|| input.Handplant is not null || input.BodyFlipping;
		return input.Flying && (!_fromRail || trick);
	}

	// Whether the board has come off a rail and not yet landed.
	private bool _fromRail;

	// How far apart (inches) a pose is from the physical board as shown: between their centres, and as their turn moves
	// the deck's ends.
	private float BoardGap( Transform pose )
	{
		var physical = _skateboard.RenderTransform;
		var reach = _skateboard.DeckBounds is { } deck ? deck.Size.x * 0.5f : DeckReach;
		var turn = (physical.Rotation.Inverse * pose.Rotation).Angle() * MathF.PI / 180f;
		return (pose.Position - physical.Position).Length + turn * reach;
	}



	private void OnBoard( int index, Transform model, bool mirror, bool released, float delta, Transform board, bool onRail,
		bool airborne )
	{
		var foot = _feet[ index ];
		var toeSource = _retargeter.SourceEffector( foot.Ball, mirror );
		if ( toeSource < 0 || _anchor < 0 )
			return;
		var step = delta / FootBlend;
		_footWeight[ index ] = Math.Clamp( _footWeight[ index ] + (released ? -step : step), 0f, 1f );
		var weight = _footWeight[ index ];
		var clipAnkle = foot.Leg.EndAngle( _positions, foot.Ball );
		if ( weight <= 0f )
		{
			_footCorrection[ index ] = System.Numerics.Vector3.Zero;
			_footCarry[ index ] = null;
			return;
		}
		// The clips' toes on their board, moved onto the same place on the board ridden: over its trucks, on its deck.
		var clipToe = _retargeter.SourceRelative( toeSource, _anchor, mirror );
		ClipToeHeights[ Math.Min( index, ClipToeHeights.Length - 1 ) ] = clipToe.Z;
		// Over its board the clips' toe is as high as their skater stands it, and the rider's ball stands at its own height:
		// the toe is taken down (or up) by the difference between the two standing heights. Kept as the clips have it, every
		// foot stood on the deck at the clip skater's height, and the default rider (ball 0.36 in over its sole, the clip's
		// toe 1.21 in) floated 0.9-1.1 in over the deck. A foot down beside the board or under it is left as it is.
		var raise = _retargeter.SourceRestHeight( toeSource ) - foot.BallHeight;
		if ( clipToe.Z > 0f && float.IsFinite( raise ) )
			clipToe.Z = MathF.Max( clipToe.Z - raise * _fit.Nearness( clipToe with { Z = 0f } ), 0f );
		// Where the clip stands this foot on its board in this grind (the lowest it has held it since getting on), and how
		// far over that it holds it now: a foot lifted to step or for a willy is not pressed onto the deck.
		var liftSlot = Math.Min( index, _clipLift.Length - 1 );
		var clipHeight = MathF.Abs( clipToe.Z );
		_clipStand[ liftSlot ] = !onRail ? null : _clipStand[ liftSlot ] is { } lowest ? MathF.Min( lowest, clipHeight ) : clipHeight;
		_clipLift[ liftSlot ] = clipHeight - (_clipStand[ liftSlot ] ?? clipHeight);
		// How fully the clip stands this foot on its board (see OutOfDeck): fully while it holds the toe over its deck no
		// more than GroundPressLift over where its skater stands, fading out over GroundPressFade more; a foot it lifts (a
		// wind-up, a step) is left where it is.
		_groundStand[ liftSlot ] = _fit.Nearness( clipToe with { Z = 0f } )
			* (1f - Smoothstep( (clipToe.Z - _fit.ClipsTop - foot.BallHeight - GroundPressLift) / GroundPressFade ));
		var onBoard = _fit.Foot( clipToe, _shownSwitch );
		var toeWorld = board.PointToWorld( SwitchTurn * SkateUnits.ToEngine( onBoard ) );
		var toeModel = SkateUnits.ToCore( model.PointToLocal( toeWorld ) );
		var ankle = _positions[ foot.Ankle ];
		var toe = _positions[ foot.Ball ];
		// The first frames riding can carry a pose or board that is not real yet: no pull comes of them, and a pull left
		// unreal by one starts again from nothing instead of staying unreal for good.
		var correction = toeModel - toe;
		if ( !Finite( correction ) )
			correction = System.Numerics.Vector3.Zero;
		if ( !Finite( _footCorrection[ index ] ) )
			_footCorrection[ index ] = System.Numerics.Vector3.Zero;
		// The pull keeps feet on the deck; a foot whose target is off the deck (down on the ground, pushing) belongs
		// to the ground and is left to the plant and lock, fading out past the deck's edge. On a rail there is no ground
		// to step to: a foot whose clip holds its toes out past the deck's edge stays with the board, where letting go
		// would drop it to the clip's own pose, made for its own board's tilt, through the deck and the rail.
		// Off the deck the foot goes as far past the real deck's edge as the clip has it past its own board's (a pushing
		// foot steps out past a wider deck instead of under it, where it could not be put down), and is then the plant's.
		if ( _skateboard is not null && !onRail && !airborne && _boardShown <= 0f )
		{
			var near = SurfaceNear( board.PointToLocal( toeWorld ), _skateboard.DeckBounds )?.Reach ?? 0f;
			var besideWorld = board.PointToWorld( SwitchTurn * SkateUnits.ToEngine( _fit.Beside( clipToe, _shownSwitch ) ) );
			var beside = SkateUnits.ToCore( model.PointToLocal( besideWorld ) ) - toe;
			correction = System.Numerics.Vector3.Lerp( Finite( beside ) ? beside : System.Numerics.Vector3.Zero, correction, near );
		}
		else if ( _skateboard is not null && !onRail )
			correction *= SurfaceNear( board.PointToLocal( toeWorld ), _skateboard.DeckBounds )?.Reach ?? 0f;
		// In the air the clip's foot target only means something while its own foot IK is engaged, and elsewhere it
		// can jump inches in a frame; a large correction there is ignored rather than followed, whichever board is shown
		// (the physical one stays shown leaving a rail). On a rail the shown board is where the feet stand, pitched as the
		// slide's clip holds it (SlideTip), so the feet are put on it in full: distrusted there, a 5-0's back foot stayed
		// 0.8 in inside the kicked tail and a salad's front foot floated 3.3 in over the deck; the leg's reach (KeepReach)
		// keeps it from stretching.
		// The distrust follows where the rider is over SwitchEase (into the air, onto or off a rail, a landing), and a target
		// out of all reach is never followed.
		// A foot the clip stands on its own board is put on the board ridden in full, in the air too: where the clip's feet
		// stand on its board their pull is the fit itself (over these trucks, on this deck), and left out in the air the
		// feet landed at the clip's own stance and slid to the fitted one over the next fifth of a second (3 in on an
		// ollie). A foot off its board (flicking a flip, lifted over a spinning board) is distrusted as before.
		var onItsBoard = _fit.Standing( clipToe, foot.BallHeight );
		if ( !float.IsFinite( onItsBoard ) )
			onItsBoard = 0f;
		var slot = Math.Min( index, _pullDistrust.Length - 1 );
		// A distrust left unreal (a first frame's pose) starts again from full distrust rather than staying unreal for good.
		if ( !float.IsFinite( _pullDistrust[ slot ] ) )
			_pullDistrust[ slot ] = 1f;
		_pullDistrust[ slot ] = Approach( _pullDistrust[ slot ],
			MathX.Lerp( (airborne ? 1f : _boardShown) * (1f - onItsBoard), 0f, onRail ? 1f : _railWeight ), delta / (onRail ? RailTrustEase : SwitchEase) );
		// A target out of all reach (the clip's own board far from its feet) gives way to the shown deck under the clip's
		// foot: the foot comes down, or up, onto it where the clip has it, its ball at its standing height over the top. A
		// BS noseslide's clip leaves its board bone far off, and its feet, pulled nowhere, floated 1.4 in over the deck.
		var plausible = Plausible( correction.Length() );
		if ( plausible < 1f && (onRail || !airborne) && _skateboard is not null )
		{
			var standing = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( toe ) ) );
			if ( _skateboard.DeckSurface( standing.x, standing.y ) is { } under && OnTop( foot, model, board ) )
			{
				var onDeck = board.PointToWorld( standing.WithZ( under.Top + foot.BallHeight ) );
				var down = SkateUnits.ToCore( model.PointToLocal( onDeck ) ) - toe;
				if ( Finite( down ) )
				{
					correction = System.Numerics.Vector3.Lerp( down, correction, plausible );
					plausible = 1f;
				}
			}
		}
		correction *= Trusted( correction.Length(), _pullDistrust[ slot ] ) * plausible;
		// On the ground the pull rides with the shown board as it moves under the rider (handed back from the clip's board at
		// a landing, turning back up to 24 degrees in a few frames), so the foot keeps its place on the deck and only the
		// pull's own change eases in: eased in from where the body put it, the foot slid across the deck (up to 120 in/s).
		// On a rail too: as a tailslide's pitch came in, the tail dropped 3.6 in in 34 ms under a back foot that eased after
		// it, floating 1.5 in.
		if ( (onRail || !airborne) && _footCarry[ index ] is { } last && Time.Now - last.At < CarryGap )
		{
			var carried = SkateUnits.ToCore( model.PointToLocal( board.PointToWorld( last.Board.PointToLocal( last.Target ) ) ) )
				- last.Ankle;
			if ( Finite( carried ) )
				_footCorrection[ index ] = carried;
		}
		// The correction itself changes smoothly, so no frame snaps a leg.
		var blend = 1f - MathF.Exp( -CorrectionRate * delta );
		_footCorrection[ index ] = System.Numerics.Vector3.Lerp( _footCorrection[ index ], correction, blend );
		// Turning round the pull starts again from nothing (see EaseFlip) and builds back as the turn eases in: followed at
		// the correction rate, the feet slid 1.8 in along the deck in a tenth of a second onto the other stance's places.
		if ( _flipTime < FlipEase )
			_footCorrection[ index ] = correction * Smoothstep( _flipTime / FlipEase );
		if ( BeginEntry( index, ankle + correction, model, board ) )
		{
			_footCorrection[ index ] = correction;
			_footWeight[ index ] = weight = 1f;
		}
		var target = ankle + _footCorrection[ index ] * weight;
		target = Planted( index, target, model, board, airborne, onRail || released, onItsBoard, delta );
		_footCarry[ index ] = (board, model.PointToWorld( SkateUnits.ToEngine( ankle + _footCorrection[ index ] ) ), ankle, Time.Now);
		target = Entering( index, target, model, board, delta );
		// On a rail and in the air the leg's reach is limited; the limit comes and goes over SwitchEase.
		var reachWeight = MathF.Max( _railWeight, _airWeight );
		if ( reachWeight > 0f )
		{
			// How far beyond the leg's soft reach the deck is on a rail: the rider is lowered by as much (see _hipDrop).
			var hip = _positions[ foot.Leg.Upper ];
			var length = System.Numerics.Vector3.Distance( hip, _positions[ foot.Leg.Lower ] )
				+ System.Numerics.Vector3.Distance( _positions[ foot.Leg.Lower ], ankle );
			if ( onRail && weight >= 1f )
				_reachShort = MathF.Max( _reachShort, System.Numerics.Vector3.Distance( hip, target ) - length * SoftReach );
			target = System.Numerics.Vector3.Lerp( target, KeepReach( foot, ankle, target, onRail ? DeckUp( model, board ) : default ), reachWeight );
		}
		if ( Finite( target ) )
			foot.Leg.Solve( _positions, _rotations, target, _kneeHint[ index ] );
		// The foot keeps its turn as the shin swings over it; pulled down onto a deck lower than the clip's, a salad's ankle
		// closed to 47 degrees. Past LeastAnkle (or the clip's own angle, if less) it tips onto its toes instead.
		if ( reachWeight > 0f && Finite( target ) )
			foot.Leg.OpenEnd( _positions, _rotations, foot.Ball, MathF.Min( LeastAnkle, clipAnkle ), reachWeight );
	}

	// Where a foot standing on the deck at a landing is held (see LandingPlant): its ankle target moved to the place on the
	// shown deck where it came down, and the foot turned as it came down against the deck, until LandingPlant has passed;
	// then the foot steps from there to the clip's place and turn (see StepTime).
	private System.Numerics.Vector3 Planted( int index, System.Numerics.Vector3 target, Transform model, Transform board,
		bool airborne, bool loose, float onItsBoard, float delta )
	{
		if ( _letGoWeight is null || _letGoWeight.Length != _feet.Length )
		{
			_letGoOffset = new System.Numerics.Vector3[ _feet.Length ];
			_letGoTurn = new System.Numerics.Quaternion[ _feet.Length ];
			_letGoWeight = new float[ _feet.Length ];
		}
		// A landing plants each foot the first frame it stands on the deck, within PlantWindow of touching down.
		if ( airborne )
		{
			_sinceAirborne[ index ] = 0f;
			_framesLanded[ index ] = 0;
			_plantPending[ index ] = true;
		}
		else
		{
			_sinceAirborne[ index ] += delta;
			_framesLanded[ index ]++;
		}
		var landing = _sinceAirborne[ index ] <= PlantWindow || _framesLanded[ index ] <= 3;
		var deck = SkateUnits.ToCore( model.Rotation.Inverse * board.Rotation );
		var foot = _feet[ index ];
		// A foot over the deck as the board comes down is planted there at once, and stays planted for as long as the landing
		// plays, however the clip has it against its own board meanwhile (see _landingHeld).
		if ( _landingHeld is null || _landingHeld.Length != _feet.Length )
		{
			_landingHeld = new bool[ _feet.Length ];
			_landingHeight = new float[ _feet.Length ];
			_stepLift = new float[ _feet.Length ];
		}
		var comingDown = !airborne && !loose && _plantPending[ index ] && landing && OverDeck( target, model, board, foot );
		var landed = !airborne && !loose && _landingHeld[ index ] && _plantedAt[ index ] is not null
			&& (landing || State?.Contains( ".Land." ) == true);
		if ( airborne || loose || (onItsBoard < PlantStanding && !comingDown && !landed) )
		{
			_landingHeld[ index ] = false;
			LetGoOfStand( index, target, model, board, deck );
			return EasedOff( index, target, deck, delta );
		}
		var wanted = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( target ) ) );
		var wantedTurn = System.Numerics.Quaternion.Normalize( System.Numerics.Quaternion.Conjugate( deck ) * _rotations[ foot.Ankle ] );
		// Only a foot that comes down over the deck is planted (its ball over the deck's outline); one beside it is the pull's.
		if ( _plantPending[ index ] && landing && !OverDeck( target, model, board, foot ) )
			_plantPending[ index ] = false;
		if ( _plantPending[ index ] && landing )
		{
			_plantedAt[ index ] = wanted;
			_plantedTurn[ index ] = wantedTurn;
			_plantAge[ index ] = 0f;
			_plantPending[ index ] = false;
			_stepFrom[ index ] = null;
			_landingHeld[ index ] = true;
			_landingHeight[ index ] = wanted.z;
		}
		else if ( _plantedAt[ index ] is null && !landing && OverDeck( target, model, board, foot ) )
		{
			// Standing on the deck otherwise (riding on, back on after a push): planted where it stands, turning with the clip.
			_plantedAt[ index ] = wanted;
			_plantedTurn[ index ] = wantedTurn;
			_plantAge[ index ] = LandingPlant;
			_stepFrom[ index ] = null;
			_landingHeld[ index ] = false;
		}
		if ( _plantedAt[ index ] is not { } planted )
			return EasedOff( index, target, deck, delta );
		_letGoWeight[ index ] = 0f;
		_plantAge[ index ] += delta;
		// Held by the landing, the foot comes down with the clip's but rises no quicker than DeckStance.LandingRise, and until
		// the clip stands it on its board it turns as the clip turns it (eased as a standing foot turns, see StandTurn).
		var height = wanted.z;
		_landingHeld[ index ] = _landingHeld[ index ] && (landing || State?.Contains( ".Land." ) == true);
		if ( _landingHeld[ index ] )
		{
			height = DeckStance.HeldHeight( wanted.z / SkateUnits.InchesPerMetre, _landingHeight[ index ] / SkateUnits.InchesPerMetre,
				delta ) * SkateUnits.InchesPerMetre;
			if ( onItsBoard < PlantStanding && _plantedTurn[ index ] is { } coming )
				_plantedTurn[ index ] = System.Numerics.Quaternion.Slerp( coming, wantedTurn, 1f - MathF.Exp( -StandTurn * delta ) );
		}
		_landingHeight[ index ] = height;
		var turn = _plantedTurn[ index ] ?? wantedTurn;
		var lift = 0f;
		if ( _plantAge[ index ] > LandingPlant )
		{
			turn = System.Numerics.Quaternion.Slerp( turn, wantedTurn, 1f - MathF.Exp( -StandTurn * delta ) );
			_plantedTurn[ index ] = turn;
			if ( _stepFrom[ index ] is null && DeckStance.OutOfStance( SkateUnits.Metres( wanted - planted ) ) )
			{
				_stepFrom[ index ] = planted;
				_stepTurnFrom[ index ] = turn;
				_stepProgress[ index ] = 0f;
			}
			if ( _stepFrom[ index ] is { } from )
			{
				_stepProgress[ index ] = MathF.Min( 1f, _stepProgress[ index ] + delta / StepTime );
				var progress = _stepProgress[ index ];
				var eased = progress * progress * (3f - 2f * progress);
				planted = Vector3.Lerp( from, wanted, eased );
				lift = StepLift * MathF.Sin( MathF.PI * progress );
				turn = System.Numerics.Quaternion.Slerp( _stepTurnFrom[ index ], wantedTurn, eased );
				if ( progress >= 1f )
				{
					// Planted again where it stepped to.
					_plantedAt[ index ] = wanted;
					_plantedTurn[ index ] = wantedTurn;
					_stepFrom[ index ] = null;
				}
			}
		}
		// The plant holds the foot's place along the deck, never its height: the height stays where the fit stands the foot
		// on the deck (lifted only as it steps). Held whole, a foot planted while it was still coming down onto the deck (riding
		// on, back on after a push) hovered there, 0.9 in over the deck standing and 1.9 in after a push, until it next stepped.
		// The step's lift is added over the deck (see OutOfDeck), not to the clip's height: the clips stand their ankles up
		// to 1.9 in under the deck's top, which the push out of the deck makes up, and lifted from there the step back to
		// the riding stance after a varial kickflip never rose off the deck, a 4.5 in slide.
		planted = planted.WithZ( height );
		_stepLift[ index ] = lift;
		_shownStand[ index ] = planted.WithZ( height + lift );
		_shownStandTurn[ index ] = turn;
		// The foot keeps its turn against the deck: it and the bones below it turn about the ankle onto it.
		TurnFoot( index, System.Numerics.Quaternion.Normalize( deck * turn * System.Numerics.Quaternion.Conjugate( _rotations[ foot.Ankle ] ) ) );
		var held = SkateUnits.ToCore( model.PointToLocal( board.PointToWorld( planted ) ) );
		return Finite( held ) ? held : target;
	}

	// Where each planted foot was last shown on the deck (the shown deck's frame) and turned against it, for letting go.
	private Vector3[] _shownStand;
	private System.Numerics.Quaternion[] _shownStandTurn;

	// A planted foot lets go: it eases off from where it was shown (see StandRelease).
	private void LetGoOfStand( int index, System.Numerics.Vector3 target, Transform model, Transform board, System.Numerics.Quaternion deck )
	{
		if ( _plantedAt[ index ] is not null )
		{
			var held = SkateUnits.ToCore( model.PointToLocal( board.PointToWorld( _shownStand[ index ] ) ) );
			if ( Finite( held ) && Finite( target ) )
			{
				_letGoOffset[ index ] = held - target;
				_letGoTurn[ index ] = deck * _shownStandTurn[ index ];
				_letGoWeight[ index ] = 1f;
			}
		}
		_plantedAt[ index ] = null;
		_plantedTurn[ index ] = null;
		_stepFrom[ index ] = null;
		_stepLift[ index ] = 0f;
	}

	// The clip's place for a foot, eased in from where it let go of the deck.
	private System.Numerics.Vector3 EasedOff( int index, System.Numerics.Vector3 target, System.Numerics.Quaternion deck, float delta )
	{
		if ( !(_letGoWeight[ index ] > 0f) )
			return target;
		_letGoWeight[ index ] = MathF.Max( 0f, _letGoWeight[ index ] - delta / StandRelease );
		var share = Smoothstep( _letGoWeight[ index ] );
		if ( share <= 0f )
			return target;
		var foot = _feet[ index ];
		var turn = System.Numerics.Quaternion.Normalize( _letGoTurn[ index ] * System.Numerics.Quaternion.Conjugate( _rotations[ foot.Ankle ] ) );
		if ( turn.W < 0f )
			turn = System.Numerics.Quaternion.Negate( turn );
		TurnFoot( index, System.Numerics.Quaternion.Slerp( System.Numerics.Quaternion.Identity, turn, share ) );
		return target + _letGoOffset[ index ] * share;
	}

	// Turns a foot and the bones below it about its ankle.
	private void TurnFoot( int index, System.Numerics.Quaternion keep )
	{
		if ( !float.IsFinite( keep.X + keep.Y + keep.Z + keep.W ) )
			return;
		var foot = _feet[ index ];
		var pivot = _positions[ foot.Ankle ];
		_rotations[ foot.Ankle ] = keep * _rotations[ foot.Ankle ];
		foreach ( var bone in _footTree[ index ] )
		{
			_positions[ bone ] = pivot + System.Numerics.Vector3.Transform( _positions[ bone ] - pivot, keep );
			_rotations[ bone ] = keep * _rotations[ bone ];
		}
	}

	// The first frame riding after getting on: whether this foot steps over from where it was shown (see EntryReach), and if
	// so where that is on the board and how high the step lifts. The push out of the deck kept off the board is already in
	// that place, so it starts again from nothing rather than adding to it.
	private bool BeginEntry( int index, System.Numerics.Vector3 target, Transform model, Transform board )
	{
		if ( _entryFrom[ index ] is not { } body || _entryAge[ index ] > 0f )
			return false;
		_entryFrom[ index ] = null;
		var from = _entryOn[ index ] is { } on && _entryBoard is { } then && (then.Position - board.Position).Length <= EntryBoardJump
			? board.PointToWorld( on ) : model.PointToWorld( SkateUnits.ToEngine( body ) );
		var held = SkateUnits.ToCore( model.PointToLocal( from ) );
		var way = System.Numerics.Vector3.Distance( held, target );
		if ( !Finite( held ) || !Finite( target ) || !float.IsFinite( way ) || way > EntryReach )
			return false;
		_entryAt[ index ] = board.PointToLocal( from );
		_entryWay[ index ] = way;
		_entryUp[ index ] = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( target ) ) ).z > _entryAt[ index ].Value.z;
		_entryLift[ index ] = EntryLift * Smoothstep( way / EntryLiftFull );
		_deckPush[ index ] = 0f;
		return true;
	}

	// The foot stepping from where it was shown before riding to its riding place (see EntryTime): along the deck as a
	// smooth step, lifted along the deck's up on the way.
	private System.Numerics.Vector3 Entering( int index, System.Numerics.Vector3 target, Transform model, Transform board, float delta )
	{
		if ( _entryAt[ index ] is not { } entry )
			return target;
		var held = SkateUnits.ToCore( model.PointToLocal( board.PointToWorld( entry ) ) );
		_entryAge[ index ] += delta;
		var progress = _entryAge[ index ] / EntryTime;
		if ( progress >= 1f || !Finite( held ) )
		{
			_entryAt[ index ] = null;
			return target;
		}
		// On the board: along the deck quickest at first (easing out), up ahead of along (see EntryRise) or over it in
		// an arc (see EntryOver).
		var start = entry;
		var end = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( target ) ) );
		_entryWay[ index ] = MathF.Max( _entryWay[ index ], (end - start).Length );
		_entryLift[ index ] = MathF.Max( _entryLift[ index ], EntryLift * Smoothstep( _entryWay[ index ] / EntryLiftFull ) );
		var along = 1f - (1f - progress) * (1f - progress);
		var up = _entryUp[ index ];
		var arc = _entryLift[ index ] + (up ? 0f : EntryOver * MathF.Abs( end.z - start.z ));
		var height = MathX.Lerp( start.z, end.z, Smoothstep( up ? progress / EntryRise : progress ) );
		var at = new Vector3( MathX.Lerp( start.x, end.x, along ), MathX.Lerp( start.y, end.y, along ),
			height + arc * MathF.Sin( MathF.PI * progress ) );
		var stepped = SkateUnits.ToCore( model.PointToLocal( board.PointToWorld( at ) ) );
		return Finite( stepped ) ? stepped : target;
	}

	// Whether the ball of a foot whose ankle goes to the target is over the shown deck's outline.
	private bool OverDeck( System.Numerics.Vector3 target, Transform model, Transform board,
		(TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
	{
		var ball = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( target + _positions[ foot.Ball ] - _positions[ foot.Ankle ] ) ) );
		return _skateboard?.DeckSurface( ball.x, ball.y ) is not null;
	}

	// Whether a bone lies below another in the hierarchy.
	private static bool Below( int[] parents, int bone, int above )
	{
		for ( var at = parents[ bone ]; at >= 0; at = parents[ at ] )
		{
			if ( at == above )
				return true;
		}
		return false;
	}

	// A foot the deck held, eased back to the clip's own pose as the rider leaves the board.
	private void ReleaseFoot( int index, float delta )
	{
		_plantedAt[ index ] = null;
		_footWeight[ index ] = Math.Max( 0f, _footWeight[ index ] - delta / FootBlend );
		_footCarry[ index ] = null;
		var correction = _footCorrection[ index ] * _footWeight[ index ];
		if ( _footWeight[ index ] <= 0f || !Finite( correction ) )
			return;
		var foot = _feet[ index ];
		foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + correction, _kneeHint[ index ] );
	}

	// A hand the clip has on its board (a grab, a hand on the nose or tail) holds the same place on the board ridden: its
	// own nose, tail or edge, however much longer, shorter or wider it is than the clips' board. The pull eases in and out
	// with how near the clip's hand is to its board (its wrist within a palm's span, see RiderBoardFit.Holding), and moves
	// the hand no further than HandFitReach. A hand that has reached the board then takes hold of it (see Hold), and no
	// hand is ever shown in the deck (see HandOutOfDeck).
	private void FitHands( Transform model, Transform board, bool mirror, bool grabbing, bool planting, float delta )
	{
		var blend = 1f - MathF.Exp( -HandFitRate * delta );
		for ( var side = 0; side < 2; side++ )
		{
			var hand = Rig[ side == 0 ? "left_hand" : "right_hand" ];
			var source = hand < 0 || _anchor < 0 ? -1 : _retargeter.SourceBone( hand, mirror );
			if ( source < 0 || !_arms.TryGetValue( hand, out var arm ) )
				continue;
			var onBoard = _retargeter.SourceRelative( source, _anchor, mirror );
			var near = Finite( onBoard ) ? _fit.Holding( onBoard ) : 0f;
			// A hand reaching down over the deck's edge (a melon's) has its wrist 6 in over the deck: its fingertip says it is on it.
			var tip = Rig[ side == 0 ? "left_middle3" : "right_middle3" ];
			var tipSource = tip < 0 ? -1 : _retargeter.SourceBone( tip, mirror );
			if ( tipSource >= 0 && _retargeter.SourceRelative( tipSource, _anchor, mirror ) is var onTip && Finite( onTip ) )
				near = MathF.Max( near, _fit.Holding( onTip, _fit.TipSpan ) );
			_handFit[ side ] += (near - _handFit[ side ]) * blend;
			if ( _handFit[ side ] >= 0.001f && Finite( onBoard ) )
			{
				var shift = model.Rotation.Inverse * (board.Rotation * (SwitchTurn * SkateUnits.ToEngine( _fit.Hand( onBoard ) - onBoard )));
				if ( shift.Length > HandFitReach )
					shift = shift.Normal * HandFitReach;
				if ( shift.Length > 0.001f )
					Reach( arm, hand, _positions[ hand ] + SkateUnits.ToCore( shift ) * _handFit[ side ] );
			}
			Hold( side, arm, hand, model, board, near >= HoldOn && (grabbing || planting), near >= HoldOff || grabbing, delta );
			HandOutOfDeck( side, arm, hand, model, board, delta );
		}
	}

	// A hand that has reached the board (a grab, a hand plant's board hand) takes hold of it where it is: from then until
	// the clip's hand leaves its board it stays on that place of the shown deck, turned with it, however the clip's hand
	// slides over its own board or the deck turns and tweaks under it, on any board. Fitted only, an indy's hand slid 6 in
	// along the toe edge while it held it and a melon's hung 9 in off the deck. Letting go eases back to the clip's hand.
	// The rider's grab input is no guide: it lets go ahead of a landing while the clip's hand is still on the board; a grab
	// keeps its hold to HoldLetGo before the landing instead.
	private void Hold( int side, TwoBoneIk arm, int hand, Transform model, Transform board, bool takes, bool keeps, float delta )
	{
		if ( !_holding[ side ] && takes && Finite( board ) )
		{
			var at = new Transform( model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) ),
				model.Rotation * SkateUnits.ToEngine( _rotations[ hand ] ) );
			if ( Finite( at ) )
			{
				// The hand closes onto the deck as it takes hold: a hand reaching it a little short (a melon's, 0.8 in off) holds
				// it, not the air beside it.
				if ( HandGap( board, model, hand ) is { } gap && gap.Length < HoldClose )
					at = at.WithPosition( at.Position + gap );
				_held[ side ] = board.ToLocal( at );
				_holding[ side ] = true;
			}
		}
		else if ( _holding[ side ] && !keeps )
		{
			_holding[ side ] = false;
		}
		_hold[ side ] = Approach( _hold[ side ], _holding[ side ] ? 1f : 0f, delta / (_holding[ side ] ? HoldTake : HoldRelease) );
		if ( _hold[ side ] <= 0f )
			return;
		var weight = Smoothstep( _hold[ side ] );
		var held = board.ToWorld( _held[ side ] );
		if ( !Finite( held ) )
			return;
		var place = SkateUnits.ToCore( model.PointToLocal( held.Position ) );
		Reach( arm, hand, System.Numerics.Vector3.Lerp( _positions[ hand ], place, weight ) );
		var turn = System.Numerics.Quaternion.Normalize( SkateUnits.ToCore( model.Rotation.Inverse * held.Rotation )
			* System.Numerics.Quaternion.Conjugate( _rotations[ hand ] ) );
		if ( turn.W < 0f )
			turn = System.Numerics.Quaternion.Negate( turn );
		arm.TurnEnd( _positions, _rotations, System.Numerics.Quaternion.Slerp( System.Numerics.Quaternion.Identity, turn, weight ) );
	}

	// The move (world) putting the hand's nearest point (a finger or the palm, with its thickness) against the shown deck,
	// from the deck model's outline and surfaces; null with no finger rig or nothing near the deck.
	private Vector3? HandGap( Transform board, Transform model, int hand )
	{
		if ( _skateboard is null || !_fingerChains.TryGetValue( hand, out var fingers ) || _skateboard.DeckBounds is not { } box )
			return null;
		var points = HandPoints( model, hand );
		var count = points.Count;
		var wrist = model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) );
		foreach ( var chain in fingers.Chains )
			points.Add( (Vector3.Lerp( wrist, model.PointToWorld( SkateUnits.ToEngine( _positions[ chain[ 0 ] ] ) ), 0.5f ),
				fingers.Radius * PalmThickness) );
		Vector3? best = null;
		var nearest = float.MaxValue;
		foreach ( var (point, radius) in points )
		{
			var local = board.PointToLocal( point );
			var x = Math.Clamp( local.x, box.Mins.x, box.Maxs.x );
			if ( _fit.HalfWidthAt( x, true ) is not { } left || _fit.HalfWidthAt( x, false ) is not { } right )
				continue;
			var y = Math.Clamp( local.y, -right, left );
			if ( _skateboard.DeckSurface( x, y ) is not { } surface )
				continue;
			var closest = new Vector3( x, y, Math.Clamp( local.z, surface.Bottom, surface.Top ) );
			var away = local - closest;
			var distance = away.Length;
			var thickness = (board.PointToLocal( point + board.Rotation.Up * radius ) - local).Length;
			if ( distance < 1e-4f || distance - thickness >= nearest )
				continue;
			nearest = distance - thickness;
			best = board.Rotation * (-away / distance * MathF.Max( 0f, distance - thickness ));
		}
		points.RemoveRange( count, points.Count - count );
		return best;
	}

	// Off the board (or with nothing to fit) every hold lets go and no hand is moved for its fingers.
	private void LetGoOfDeck()
	{
		for ( var side = 0; side < 2; side++ )
		{
			_holding[ side ] = false;
			_hold[ side ] = 0f;
			_handExit[ side ] = Vector3.Zero;
			_handWay[ side ] = -1;
		}
	}

	// No hand is shown in the deck: when its palm or fingers are in it the hand goes out by the shortest way, from the
	// shape of the deck model ridden so any board fits: up off its top or down off its underside (each finger staying on
	// the side its knuckle is on, so a hand round the edge is not closed onto the deck's middle), or out past its nearer
	// edge or end, which a hand round the edge takes. Along each of the deck's axes going out further follows at up to
	// HandExitRise (a hand taking the way out past the edge at once jumped 3 in in a frame) and coming back eases. Fitted only, an indy's fingers stood half an inch inside the deck,
	// and cleared along the deck's up axis alone the hand round its edge rose 2-3.5 in off it.
	private void HandOutOfDeck( int side, TwoBoneIk arm, int hand, Transform model, Transform board, float delta )
	{
		var wanted = Vector3.Zero;
		if ( _skateboard is not null && _fingerChains.ContainsKey( hand ) && Finite( board ) )
			wanted = HandExit( board, model, hand, side );
		var current = _handExit[ side ];
		var follow = 1f - MathF.Exp( -HandRelease * delta );
		for ( var axis = 0; axis < 3; axis++ )
		{
			var now = current[ axis ];
			var want = wanted[ axis ];
			var deeper = MathF.Abs( want ) >= MathF.Abs( now ) && (now == 0f || MathF.Sign( want ) == MathF.Sign( now ));
			now = deeper ? MathF.Sign( want ) * MathF.Min( MathF.Abs( want ), MathF.Abs( now ) + HandExitRise * delta )
				: MathX.Lerp( now, want, follow );
			current[ axis ] = MathF.Abs( now ) < 0.005f ? 0f : now;
		}
		_handExit[ side ] = current;
		if ( current.IsNearlyZero( 0.005f ) )
			return;
		Reach( arm, hand, _positions[ hand ] + SkateUnits.ToCore( model.Rotation.Inverse * (board.Rotation * current) ) );
	}

	// The shortest move (the shown deck's frame, inches) taking the hand's palm and fingers out of the deck, or zero.
	// Up and down move every point together, so they keep every finger on the side of the deck its knuckle is on and fail
	// when the hand is closed tighter than the deck is thick; out past an edge or an end only moves points away from the
	// deck. A way out once taken is kept until another is shorter by HandExitBias.
	private Vector3 HandExit( Transform board, Transform model, int hand, int side )
	{
		var points = HandPoints( model, hand );
		var wrist = model.PointToWorld( SkateUnits.ToEngine( _positions[ hand ] ) );
		var count = points.Count;
		// The palm, from the wrist to each knuckle.
		if ( _fingerChains.TryGetValue( hand, out var fingers ) )
		{
			foreach ( var chain in fingers.Chains )
			{
				var knuckle = model.PointToWorld( SkateUnits.ToEngine( _positions[ chain[ 0 ] ] ) );
				points.Add( (Vector3.Lerp( wrist, knuckle, 0.5f ), fingers.Radius * PalmThickness) );
				_handChains.Add( -1 );
			}
		}
		// Up: every point above its side's face; down: every point beneath it.
		var lowest = float.MinValue;
		var highest = float.MaxValue;
		Span<float> across = stackalloc float[ 4 ];
		var any = false;
		var finger = -2;
		var above = false;
		for ( var i = 0; i < points.Count; i++ )
		{
			var (point, radius) = points[ i ];
			var local = board.PointToLocal( point );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
				continue;
			var thickness = (board.PointToLocal( point + board.Rotation.Up * radius ) - local).Length;
			var middle = (surface.Top + surface.Bottom) * 0.5f;
			var chain = _handChains[ i ];
			if ( chain < 0 || chain != finger )
			{
				finger = chain;
				above = local.z >= middle;
			}
			if ( above )
				lowest = MathF.Max( lowest, surface.Top + thickness - local.z );
			else
				highest = MathF.Min( highest, surface.Bottom - thickness - local.z );
			if ( local.z >= surface.Top + thickness || local.z <= surface.Bottom - thickness )
				continue;
			any = true;
			var left = _fit.HalfWidthAt( local.x, true ) ?? local.y;
			var right = _fit.HalfWidthAt( local.x, false ) ?? -local.y;
			across[ 0 ] = MathF.Max( across[ 0 ], left + thickness - local.y );
			across[ 1 ] = MathF.Max( across[ 1 ], local.y + right + thickness );
			if ( _skateboard.DeckBounds is { } box )
			{
				across[ 2 ] = MathF.Max( across[ 2 ], box.Maxs.x + thickness - local.x );
				across[ 3 ] = MathF.Max( across[ 3 ], local.x - box.Mins.x + thickness );
			}
			else
			{
				across[ 2 ] = across[ 3 ] = float.MaxValue;
			}
		}
		points.RemoveRange( count, points.Count - count );
		_handChains.RemoveRange( count, _handChains.Count - count );
		if ( !any )
		{
			_handWay[ side ] = -1;
			return Vector3.Zero;
		}
		// The ways out: 0 up or down, 1 to the left, 2 to the right, 3 past the nose, 4 past the tail.
		var vertical = lowest == float.MinValue && highest == float.MaxValue ? 0f
			: lowest <= highest ? Math.Clamp( 0f, lowest, highest ) : float.NaN;
		Span<float> cost = stackalloc float[ 5 ];
		cost[ 0 ] = float.IsNaN( vertical ) ? float.MaxValue : MathF.Abs( vertical );
		for ( var way = 0; way < 4; way++ )
			cost[ way + 1 ] = across[ way ];
		var best = 0;
		for ( var way = 1; way < 5; way++ )
		{
			if ( cost[ way ] < cost[ best ] )
				best = way;
		}
		var kept = _handWay[ side ];
		if ( kept >= 0 && kept != best && cost[ kept ] < float.MaxValue && cost[ kept ] <= cost[ best ] + HandExitBias )
			best = kept;
		_handWay[ side ] = best;
		return best switch
		{
			0 => new Vector3( 0f, 0f, vertical ),
			1 => new Vector3( 0f, across[ 0 ], 0f ),
			2 => new Vector3( 0f, -across[ 1 ], 0f ),
			3 => new Vector3( across[ 2 ], 0f, 0f ),
			_ => new Vector3( -across[ 3 ], 0f, 0f )
		};
	}

	// How each hand is moved out of the deck (the shown deck's frame, inches), the way out it took (-1 none), how much
	// shorter another way must be to be taken instead (inches), and the palm's thickness against a finger's.
	private readonly Vector3[] _handExit = new Vector3[ 2 ];
	private readonly int[] _handWay = { -1, -1 };
	private const float HandExitBias = 0.3f;
	private const float HandExitRise = 120f;
	private const float PalmThickness = 1.6f;

	// In the air the feet keep out of the deck as it flips: a deck rolling over about its long axis sweeps out a cylinder
	// as wide as itself, and each foot is kept outside it (away from the axis, toward the hips for a foot above the deck),
	// by as much of that as the deck is rolling, against the body, past FlipRoll (fully past FlipRolling). The sweep does
	// not turn with the deck, so what it asks changes smoothly however fast the deck spins: it comes in as the flick spins
	// the board up and goes as the catch stops it. The board takes most of it, dropping away under the feet (see
	// DropForFlip) as a flipped board does, and the feet lift the rest; both come back as the catch stops the roll. Pushed
	// along the deck's up axis alone, the push went from one face's to the other's through nothing as the deck turned past
	// edge on, and a foot stood 1.4 in in the deck for four frames at a kickflip's catch; lifted over the nearer edge
	// instead the way out was up to 4.6 in and the shin jumped in a frame, and lifted out of the sweep by the feet alone,
	// at 200 in/s, the legs were thrown 3.6 in in three frames.
	// The lift and the drop follow what the sweep asks as critically damped springs (SweepSpring a second). What it asks
	// moves from frame to frame (how fast the deck rolls is read from one frame to the next, and which point of the sole is
	// deepest changes), and followed at a set speed (80 in/s) the lift set off and stopped at full speed in a frame and
	// turned with each frame's ask: the back foot's ball jumped 1.1 to 3.9 on the snap meter as a heelflip left the
	// ground, more or less with the frame's timing against the flick.
	private System.Numerics.Vector3[] _sweepLift;
	private System.Numerics.Vector3[] _sweepLiftRate;
	private const float FlipRoll = 3.5f;
	private const float FlipRolling = 10f;
	private const float SweepSpring = 40f;
	private const float DropShare = 0.6f;
	private const float DropLimit = 3f;

	// The shown deck in the model's frame this frame and the last: how fast it is rolling against the body.
	private Transform? _deckInModel;
	private Transform? _deckInModelBefore;

	// How far down (inches, the body's down) the shown board has dropped away from the feet for its flip.
	private float _flipDrop;
	private float _flipDropRate;

	// How far into the sweep a foot is (inches, scaled by how fully the deck is rolling) and the way out of it (the shown
	// deck's frame); nothing outside it or with the deck not rolling.
	private (float Need, Vector3 Away) SweepNeed( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board, float delta )
	{
		if ( _boardShown <= 0f || Dark || _skateboard is null || !Finite( board ) || !(delta > 0f)
			|| _deckInModel is not { } now || _deckInModelBefore is not { } before )
			return (0f, Vector3.Zero);
		// How fast the deck rolls about its own long axis against the body, radians a second.
		var turn = TurnBetween( System.Numerics.Quaternion.Identity, SkateUnits.ToCore( now.Rotation * before.Rotation.Inverse ) );
		var roll = MathF.Abs( System.Numerics.Vector3.Dot( turn, SkateUnits.ToCore( now.Rotation.Forward ) ) ) / delta;
		var share = Smoothstep( (roll - FlipRoll) / (FlipRolling - FlipRoll) );
		if ( share <= 0f )
			return (0f, Vector3.Zero);
		var deepest = 0f;
		var away = Vector3.Zero;
		foreach ( var (position, padding) in SolePoints( foot ) )
		{
			var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			if ( _skateboard.DeckBounds is { } box && (local.x < box.Mins.x || local.x > box.Maxs.x) )
				continue;
			var left = _fit.HalfWidthAt( local.x, true );
			var right = _fit.HalfWidthAt( local.x, false );
			if ( left is null && right is null )
				continue;
			var radius = MathF.Max( left ?? 0f, right ?? 0f ) + MathF.Max( padding, SoleMargin );
			var off = MathF.Sqrt( local.y * local.y + local.z * local.z );
			if ( off >= radius || radius - off <= deepest )
				continue;
			deepest = radius - off;
			// Away from the axis; a point on it goes toward the hips.
			var hip = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Leg.Upper ] ) ) );
			away = off > 0.01f ? new Vector3( 0f, local.y, local.z ) / off : new Vector3( 0f, hip.y, hip.z ).Normal;
		}
		return (deepest * share, away);
	}

	// The shown board drops away from the feet as it flips, by DropShare of the deepest foot's way out of its sweep (no
	// more than DropLimit), along the body's down: a flipped board turns over below the feet and rises to meet them at
	// the catch. It drops and comes back as a spring following that (see SweepSpring).
	private Transform DropForFlip( Transform model, Transform board, float delta, bool flying )
	{
		var wanted = 0f;
		if ( flying )
		{
			foreach ( var foot in _feet )
				wanted = MathF.Max( wanted, SweepNeed( foot, model, board, delta ).Need );
		}
		wanted = MathF.Min( wanted * DropShare, DropLimit );
		Spring( ref _flipDrop, ref _flipDropRate, wanted, SweepSpring, delta );
		if ( _flipDrop < 0.01f && wanted <= 0f && MathF.Abs( _flipDropRate ) < 0.1f )
			_flipDrop = _flipDropRate = 0f;
		if ( _flipDrop == 0f || _boardShown <= 0f )
			return board;
		// The shown pose blends physics and animation by the shown weight, so the animated pose carries the drop, with the
		// board's give-way to the feet as it was settled.
		var weight = MathF.Max( _boardShown, 0.25f );
		var settled = _animatedBoard.Position + board.Rotation.Up * (_settle / weight);
		_skateboard.SetDisplayPose( _animatedBoard.WithPosition( settled + model.Rotation.Down * (_flipDrop / weight) ), _boardShown );
		return _skateboard.DisplayedTransform;
	}

	private void OverTheEdge( int index, Transform model, Transform board, float delta, bool flying )
	{
		var foot = _feet[ index ];
		var (needed, away) = flying ? SweepNeed( foot, model, board, delta ) : (0f, Vector3.Zero);
		// Made here (not only where the rest are): a hot reload adds them to a rider already running.
		if ( _sweepLift is null || _sweepLift.Length != _feet.Length )
			_sweepLift = new System.Numerics.Vector3[ _feet.Length ];
		if ( _sweepLiftRate is null || _sweepLiftRate.Length != _feet.Length )
			_sweepLiftRate = new System.Numerics.Vector3[ _feet.Length ];
		// The way out in the model's frame, as far as the sweep asks.
		var wanted = needed > 0f ? SkateUnits.ToCore( model.Rotation.Inverse * (board.Rotation * away) ) * needed
			: System.Numerics.Vector3.Zero;
		Spring( ref _sweepLift[ index ], ref _sweepLiftRate[ index ], wanted, SweepSpring, delta );
		var lift = _sweepLift[ index ];
		if ( needed <= 0f && lift.Length() < 0.01f && _sweepLiftRate[ index ].Length() < 0.1f )
		{
			_sweepLift[ index ] = _sweepLiftRate[ index ] = System.Numerics.Vector3.Zero;
			return;
		}
		foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + lift, _kneeHint[ index ] );
	}

	// Moves a value toward a target as a critically damped spring of the given rate, exactly over the step: its speed
	// starts and ends at nothing and never steps, however the target jumps.
	private static void Spring( ref float value, ref float rate, float target, float spring, float delta )
	{
		if ( !(delta > 0f) )
			return;
		var offset = value - target;
		var push = rate + spring * offset;
		var fade = MathF.Exp( -spring * delta );
		value = target + (offset + push * delta) * fade;
		rate = (rate - push * spring * delta) * fade;
	}

	private static void Spring( ref System.Numerics.Vector3 value, ref System.Numerics.Vector3 rate,
		System.Numerics.Vector3 target, float spring, float delta )
	{
		if ( !(delta > 0f) )
			return;
		var offset = value - target;
		var push = rate + spring * offset;
		var fade = MathF.Exp( -spring * delta );
		value = target + (offset + push * delta) * fade;
		rate = (rate - push * spring * delta) * fade;
	}

	// A foot correction on a rail or in the air never straightens the leg: past SoftReach of the leg's length the target
	// is drawn in, toward the full length but never reaching it, so a rider whose legs are shorter than the clip skater's,
	// or a board the clip holds further off, leaves the knee bent instead of locked. It never draws the foot in nearer the
	// hip than the clip has it. Given the shown deck's up axis (model space), the foot is drawn in over the deck, as high
	// over it as it was, toward the hip: drawn straight toward the hip it rose off the deck, a darkslide's front foot
	// (fitted out to a longer board's truck) floating 1.5 in and a tailslide's back foot 1.7 in. Where the hip is too high
	// over the deck to reach it at all, it is drawn toward the hip.
	private System.Numerics.Vector3 KeepReach( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		System.Numerics.Vector3 ankle, System.Numerics.Vector3 target, System.Numerics.Vector3 deckUp = default )
	{
		var hip = _positions[ foot.Leg.Upper ];
		var length = System.Numerics.Vector3.Distance( hip, _positions[ foot.Leg.Lower ] )
			+ System.Numerics.Vector3.Distance( _positions[ foot.Leg.Lower ], ankle );
		var soft = length * SoftReach;
		var span = length - soft;
		var offset = target - hip;
		var distance = offset.Length();
		if ( distance <= soft || span <= 0f || distance < 1e-4f )
			return target;
		var reach = soft + span * (1f - MathF.Exp( -(distance - soft) / span ));
		reach = MathF.Max( reach, MathF.Min( System.Numerics.Vector3.Distance( hip, ankle ), distance ) );
		if ( deckUp != System.Numerics.Vector3.Zero && reach < distance )
		{
			// Over the deck by no more than DeckReachDraw; any reach still short is drawn toward the hip.
			var height = System.Numerics.Vector3.Dot( offset, deckUp );
			var across = offset - deckUp * height;
			var spread = across.Length();
			if ( spread > 1e-4f )
			{
				var room = reach * reach - height * height;
				var along = MathF.Max( room > 0f ? MathF.Sqrt( room ) : 0f, spread - DeckReachDraw );
				offset = deckUp * height + across * (along / spread);
				distance = offset.Length();
				if ( distance <= reach )
					return hip + offset;
			}
		}
		return hip + offset * (reach / distance);
	}

	// Furthest (inches) a foot out of reach on a rail is drawn in over the deck before it is drawn toward the hip.
	private const float DeckReachDraw = 1.5f;

	// The shown deck's up axis in the model's frame.
	private static System.Numerics.Vector3 DeckUp( Transform model, Transform board )
	{
		var up = SkateUnits.ToCore( model.Rotation.Inverse * board.Rotation.Up );
		return up.LengthSquared() > 1e-8f ? System.Numerics.Vector3.Normalize( up ) : System.Numerics.Vector3.Zero;
	}

	// No foot may pass into the deck: where a foot's ball or ankle is over the shown deck, padded by that foot's
	// own sole (on top) or instep (underneath) it must stay outside the deck model's actual surface there, else
	// the leg is moved out along the deck normal. Surfaces come from the deck model's triangles and the paddings
	// from the rider's rest pose, so any board and rider fit.
	// How far a foot must move along the deck's up axis to be clear of the deck (and, beneath it, the trucks and
	// wheels): positive lifts a foot on top, negative lowers one underneath.
	// With press (on a rail), a foot clear of the deck on its side gives how far it is off it instead of nothing: the push
	// toward the deck, negative for a foot over the top, positive for one under a board held dark side up.
	// Lays a foot on the deck under it by the given share: turned about its ankle, toes down or up along the foot, until
	// its ankle and ball stand as high over the deck under each as they stand over the floor flat (by at most FlatMost).
	private void LayFlat( int index, Transform model, Transform board, float share )
	{
		var foot = _feet[ index ];
		var ankle = _positions[ foot.Ankle ];
		var normal = SkateUnits.ToCore( model.Rotation.Inverse * board.Rotation.Up );
		var along = _positions[ foot.Ball ] - ankle;
		along -= normal * System.Numerics.Vector3.Dot( along, normal );
		if ( along.LengthSquared() < 1e-6f )
			return;
		var reach = along.Length();
		along /= reach;
		// The ankle and the ball over the deck under each, less their heights over the floor standing flat (the shoe's own
		// sole points will not do: its toe curves up off the floor standing flat).
		float? Gap( System.Numerics.Vector3 position, float standing )
		{
			var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			return _skateboard.DeckSurface( local.x, local.y ) is { } surface ? local.z - surface.Top - standing : null;
		}
		if ( reach < 1f || Gap( ankle, foot.AnkleHeight ) is not { } heel || Gap( _positions[ foot.Ball ], foot.BallHeight ) is not { } toes )
			return;
		// Turning by an angle (toes down) lowers the ball by its distance ahead of the ankle times the angle.
		var angle = Math.Clamp( (toes - heel) / reach, -FlatMost, FlatMost ) * share;
		if ( MathF.Abs( angle ) < 1e-4f )
			return;
		var axis = System.Numerics.Vector3.Normalize( System.Numerics.Vector3.Cross( normal, along ) );
		TurnFoot( index, System.Numerics.Quaternion.CreateFromAxisAngle( axis, angle ) );
	}

	private float RequiredPush( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board, bool onRail = false, bool airborne = false, bool press = false )
	{
		if ( _skateboard is null )
			return 0f;
		// The whole foot is either on top of the deck or under it (a flick passing beneath a flipping board); deciding
		// per point would let a deep heel count as "under" and stay inside.
		// On the ground the ankle decides; in the air both ways out are measured to pick the shorter. On a rail nothing
		// under the deck can be stood on: the feet stay on the side of the deck the rider's hips are on (the top of a board
		// the right way up, the underside of a darkslide's), however deep the clip sinks one into it, where the ankle
		// against the deck's middle would send a sunk foot down through the deck and into the rail, and the shorter way
		// out would flip a foot from one side to the other as a darkslide's board turns over.
		// Dark side up the hips decide in the air as well: leaving a darkslide, the shorter way out sent a foot through to
		// the deck's other side as the rail's weight went (a knee turned 21-24 degrees in a frame, ankles 204-239 in/s).
		if ( _boardShown <= 0f || onRail || Dark )
		{
			if ( FootIndex( foot ) is var ground and >= 0 )
				_footOnTop[ ground ] = null;
			// In the air over the physical board (leaving a rail) the hips decide too: the ankle flips a foot's side as an
			// air clip swings it across the deck.
			return SolePush( foot, model, board, onRail || airborne || Dark ? HipAbove( foot, model, board ) : Above( foot, model, board ),
				MathF.Max( _railWeight, Dark ? 1f : 0f ), press );
		}
		var top = SolePush( foot, model, board, true );
		var under = SolePush( foot, model, board, false );
		return Side( foot, model, board, top, under ) ? top : under;
	}

	// No leg passes into a rail: a slide's clip, made for its own board and rail, can hang a shin or a foot through the
	// rail ridden. The foot moves out of the rail the shorter way (up over its top or out past a side) by as much as the
	// deepest point of its shin and sole is in it, the leg following; getting in deeper applies at once, coming back out
	// eases, so the leg never snaps.
	private void OutOfRails( int index, Transform model, float delta )
	{
		var rails = _skateboard?.RailSolids;
		var foot = _feet[ index ];
		var wanted = Vector3.Zero;
		if ( rails is { Count: > 0 } )
		{
			var ankle = _positions[ foot.Ankle ];
			var knee = _positions[ foot.Leg.Lower ];
			var hip = SkateUnits.Metres( model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Leg.Upper ] ) ) );
			foreach ( var rail in rails )
			{
				// A leg from above the rail leaves it upward, one hanging beside it sideways.
				var fromAbove = rail.AboveTop( hip );
				// Along the shin the foot's move carries a point less the nearer the knee it is (the knee stays): the foot
				// moves as far as clears that point.
				for ( var i = 0; i < ShinSamples; i++ )
				{
					var share = i / (float)ShinSamples;
					Consider( rail, fromAbove, System.Numerics.Vector3.Lerp( ankle, knee, share ), foot.ShinRadius, 1f - share );
				}
				foreach ( var (position, padding) in SolePoints( foot ) )
					Consider( rail, fromAbove, position, MathF.Max( padding, SoleMargin ), 1f );
				// The shoe's body: round the ankle and the ball as far as each stands above the sole.
				Consider( rail, fromAbove, ankle, foot.AnkleHeight, 1f );
				Consider( rail, fromAbove, _positions[ foot.Ball ], foot.BallHeight, 1f );
			}
		}
		var current = _railPush[ index ];
		_railPush[ index ] = wanted.LengthSquared >= current.LengthSquared ? wanted
			: Vector3.Lerp( current, wanted, 1f - MathF.Exp( -DeckPushRelease * delta ) );
		if ( _railPush[ index ].LengthSquared < 0.0001f )
		{
			_railPush[ index ] = Vector3.Zero;
			return;
		}
		var shift = SkateUnits.ToCore( model.Rotation.Inverse * _railPush[ index ] );
		// The foot turns with the shin: nothing in the rail is to be stood on, and keeping its turn would fold the ankle.
		foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + shift, _kneeHint[ index ], true );

		void Consider( SkateMotion.Core.Grinding.RailSolid rail, bool fromAbove, System.Numerics.Vector3 position, float radius,
			float carried )
		{
			var world = SkateUnits.Metres( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			var exit = SkateUnits.Inches( rail.Exit( world, radius / SkateUnits.InchesPerMetre, fromAbove ) ) / carried;
			if ( exit.Length > MaximumRailPush )
				exit = exit.Normal * MaximumRailPush;
			if ( exit.LengthSquared > wanted.LengthSquared )
				wanted = exit;
		}
	}

	// How far (world inches) each foot is held out of the rails, and the most it is moved.
	private readonly Vector3[] _railPush = new Vector3[ 2 ];
	private const float MaximumRailPush = 6f;

	// Whether the leg's hip is on the deck's upper side: where the rider stands on a board, whichever way up it is.
	private bool HipAbove( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board ) =>
		board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Leg.Upper ] ) ) ).z >= 0f;

	// How far in from a truck's or wheel's edge (inches) a foot beneath the deck is pushed fully clear of it.
	private const float PartFootprintFade = 1f;

	// How far a foot must move along the deck's up axis to clear the deck on the given side.
	private float SolePush( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board, bool onTop, float fadeParts = 0f, bool press = false )
	{
		// Pressing, a foot off the deck gives its gap (the nearest point's) rather than nothing.
		var push = press ? onTop ? float.NegativeInfinity : float.PositiveInfinity : 0f;
		// Every measured point of the shoe (or the ankle and ball with their rest heights when the mesh could not be
		// measured) must clear the deck on the foot's side.
		var bounds = _skateboard.DeckBounds;
		foreach ( var (position, padding) in SolePoints( foot ) )
		{
			var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			if ( SurfaceNear( local, bounds ) is not { } near )
				continue;
			var (surface, reach) = near;
			// Past the edge the deck only reaches a foot coming over it, not one down beside it on the ground.
			if ( reach < 1f && (onTop ? local.z < surface.Bottom : local.z > surface.Top) )
				continue;
			var needed = onTop
				? surface.Top + (padding > 0f ? padding : SoleMargin) - local.z
				: surface.Bottom - (padding > 0f ? foot.BallHeight : SoleMargin) - local.z;
			needed *= reach;
			push = onTop ? MathF.Max( push, needed ) : MathF.Min( push, needed );
		}
		// A foot on the underside (a flipping board turned toward it) must also clear the trucks and wheels.
		if ( !onTop )
		{
			var parts = _skateboard.PartBounds;
			foreach ( var (position, padding) in SolePoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				foreach ( var box in parts )
				{
					if ( local.x < box.Mins.x || local.x > box.Maxs.x || local.y < box.Mins.y || local.y > box.Maxs.y )
						continue;
					// Coming in over a part's edge the push grows with how far in the foot is, rather than all at once at the
					// edge: a darkslide's feet stand beside the trucks, and crossing a truck's edge snapped a leg an inch. Only on a
					// rail, where the foot keeps its side: in the air this push is weighed against the way out over the deck, and
					// a faded truck there sent a foot to the underside, and the deck giving way to it snapped back.
					var inside = MathF.Min( MathF.Min( local.x - box.Mins.x, box.Maxs.x - local.x ),
						MathF.Min( local.y - box.Mins.y, box.Maxs.y - local.y ) );
					var share = MathX.Lerp( 1f, Math.Clamp( inside / PartFootprintFade, 0f, 1f ), fadeParts );
					push = MathF.Min( push, share * (box.Mins.z - (padding > 0f ? foot.BallHeight : SoleMargin) - local.z) );
				}
			}
		}
		if ( float.IsInfinity( push ) )
			push = 0f;
		return push;
	}

	// How much of a foot correction this size (inches) is followed: all of it up to TrustedCorrection, none past
	// IgnoredCorrection, as far as it is distrusted.
	private static float Trusted( float size, float distrust ) =>
		1f - distrust * Smoothstep( (size - TrustedCorrection) / (IgnoredCorrection - TrustedCorrection) );

	private void OutOfDeck( int index, Transform model, Transform board, float delta, bool onRail = false, bool airborne = false,
		bool rising = false )
	{
		var foot = _feet[ index ];
		var needed = RequiredPush( foot, model, board, onRail, airborne );
		if ( !float.IsFinite( needed ) )
			needed = 0f;
		// On a rail a foot the clip stands on its board is pressed onto the shown deck, wherever the pull left it: over a
		// kicked end shaped unlike the clips' board, landing from the air, the clip easing the foot down after the board,
		// or sliding it off a kick (the clip's target keeps the height over its own deck's middle, not over the deck's
		// surface where the foot stands). A foot the clip lifts (CLIP_LIFT in the lab's check) is left where it is.
		var side = HipAbove( foot, model, board ) ? 1f : -1f;
		var pressing = false;
		var standing = 0f;
		// On the ground too, a foot the clip stands on its board is laid on the deck and pressed onto it: the clip's target
		// keeps its height over its own deck's middle and its turn against its own deck, so over a tail kicked unlike the
		// clips' board the back foot stood on its heel with its toes 0.6 in up. Not while it steps (see Planted).
		if ( !onRail && !airborne && _skateboard is not null && FootIndex( foot ) is var grounded and >= 0
			&& _stepFrom[ grounded ] is null && side > 0f )
		{
			var share = _groundStand[ Math.Min( grounded, _groundStand.Length - 1 ) ] * _footWeight[ grounded ];
			if ( share > 0f )
			{
				LayFlat( index, model, board, share );
				needed = RequiredPush( foot, model, board, onRail, airborne );
				if ( !float.IsFinite( needed ) )
					needed = 0f;
				var pressed = RequiredPush( foot, model, board, onRail, airborne, press: true );
				if ( float.IsFinite( pressed ) && pressed < 0f && needed <= 0f )
				{
					needed = -MathF.Min( -pressed, PressMost ) * share;
					pressing = true;
				}
			}
		}
		if ( onRail && _skateboard is not null && FootIndex( foot ) is var stood and >= 0 )
		{
			standing = 1f - Smoothstep( (_clipLift[ Math.Min( stood, _clipLift.Length - 1 ) ] - PressLift) / PressLiftFade );
			var pressed = RequiredPush( foot, model, board, onRail, airborne, press: true );
			if ( standing > 0f && float.IsFinite( pressed ) && side * pressed < 0f && side * needed <= 0f )
			{
				needed = -side * MathF.Min( -side * pressed, PressMost ) * standing;
				pressing = true;
			}
		}
		// An air clip over the physical board shown leaving a rail can hold its board far from how the physical board lies:
		// a foot it puts deep in the physical deck is not pushed out through it, which would stretch the leg and fold the
		// ankle; the leg keeps the clip's pose. On a rail a foot in the shown deck is pushed out of it in full, the shown
		// board standing where the slide's clip holds it.
		var pushSlot = Math.Min( index, _pushDistrust.Length - 1 );
		_pushDistrust[ pushSlot ] = Approach( _pushDistrust[ pushSlot ],
			MathX.Lerp( airborne && _boardShown <= 0f ? 1f : 0f, 0f, _railWeight ), delta / SwitchEase );
		if ( _pushDistrust[ pushSlot ] > 0f )
			needed *= Trusted( MathF.Abs( needed ), _pushDistrust[ pushSlot ] );
		// Pushing further out applies at once so the foot never shows inside the deck; letting go (a foot stepping
		// off the deck's edge to push) eases so the leg does not drop in one frame.
		var current = float.IsFinite( _deckPush[ index ] ) ? _deckPush[ index ] : 0f;
		var over = _skateboard is null ? 0f : SurfaceNear( board.PointToLocal( model.PointToWorld(
			SkateUnits.ToEngine( _positions[ foot.Ball ] ) ) ), _skateboard.DeckBounds )?.Reach ?? 0f;
		var release = MathX.Lerp( DeckPushRelease, DeckPushFollow, over );
		// Over the deck the faster release is held to a speed (never slower than the slow release), so a large push never
		// drops the leg in a frame.
		var eased = MathX.Lerp( current, needed, 1f - MathF.Exp( -release * delta ) );
		var slow = MathX.Lerp( current, needed, 1f - MathF.Exp( -DeckPushRelease * delta ) );
		var cap = MathF.Max( DeckPushFollowSpeed * delta, MathF.Abs( slow - current ) );
		// A press comes in eased like a push letting go, never at once.
		var push = !pressing && MathF.Abs( needed ) >= MathF.Abs( current ) && (current == 0f || MathF.Sign( needed ) == MathF.Sign( current ))
			? needed : current + Math.Clamp( eased - current, -cap, cap );
		if ( !pressing && needed != 0f && MathF.Sign( needed ) == MathF.Sign( push ) && MathF.Abs( push ) < MathF.Abs( needed ) )
			push = needed;
		// Getting on, a foot rises onto a deck standing higher under it than the clips' board (a longer board's kicked
		// tail, met earlier than the clips' own) at MountRise rather than in one frame.
		var limited = rising || _rising[ index ];
		if ( limited && push > current + MountRise * delta )
			push = current + MountRise * delta;
		_rising[ index ] = limited && push < needed - 0.01f;
		_deckPush[ index ] = push;
		// A stepping foot is lifted over where the deck holds it (see Planted).
		var lift = 0f;
		if ( _stepLift is not null && index < _stepLift.Length && !onRail )
		{
			lift = float.IsFinite( _stepLift[ index ] ) ? _stepLift[ index ] : 0f;
			_stepLift[ index ] = 0f;
		}
		if ( MathF.Abs( push + lift ) < 0.01f )
			return;
		var normal = SkateUnits.ToCore( model.Rotation.Inverse * board.Rotation.Up );
		var pushed = _positions[ foot.Ankle ] + normal * (push + lift);
		var pushReach = MathF.Max( _railWeight, _airWeight );
		if ( pushReach > 0f )
			pushed = System.Numerics.Vector3.Lerp( pushed, KeepReach( foot, _positions[ foot.Ankle ], pushed, onRail ? DeckUp( model, board ) : default ), pushReach );
		// On a rail the foot turns with the shin as it is pushed out: kept as the clip turned it, a foot the clip sank into
		// the deck folds at the ankle as the shin swings up.
		foot.Leg.Solve( _positions, _rotations, pushed, _kneeHint[ index ], _railWeight );
	}

	// Off the board a push out of the deck left from riding eases out along the deck's up axis, as letting go of it does.
	private void ReleaseDeckPush( int index, Transform model, Transform board, float delta )
	{
		var current = float.IsFinite( _deckPush[ index ] ) ? _deckPush[ index ] : 0f;
		var push = MathF.Abs( current ) < 0.01f ? 0f : MathX.Lerp( current, 0f, 1f - MathF.Exp( -DeckPushRelease * delta ) );
		_deckPush[ index ] = push;
		if ( push == 0f )
			return;
		var foot = _feet[ index ];
		var normal = SkateUnits.ToCore( model.Rotation.Inverse * board.Rotation.Up );
		foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + normal * push, _kneeHint[ index ] );
	}

	// Points along a shin, ankle to knee, as (position, radius).
	private IEnumerable<(System.Numerics.Vector3 Position, float Radius)> ShinPoints( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
	{
		var ankle = _positions[ foot.Ankle ];
		var knee = _positions[ foot.Leg.Lower ];
		for ( var i = 1; i <= ShinSamples; i++ )
			yield return (System.Numerics.Vector3.Lerp( ankle, knee, i / (float)ShinSamples ), foot.ShinRadius);
	}

	// How far a shin must move along the deck's up axis to clear the deck, on its foot's side.
	private float ShinPush( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot, Transform model, Transform board )
	{
		if ( _skateboard is null )
			return 0f;
		var onTop = OnTop( foot, model, board );
		var push = 0f;
		foreach ( var (position, radius) in ShinPoints( foot ) )
		{
			var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
				continue;
			push = onTop ? MathF.Max( push, surface.Top + radius - local.z ) : MathF.Min( push, surface.Bottom - radius - local.z );
		}
		return push;
	}

	// Which way the animation bends a knee: the knee's offset from the hip-to-ankle line. The straighter the leg the
	// less that offset says, so the direction kept from earlier frames takes over and a nearly straight leg never
	// flips its knee. Every leg correction passes it on.
	private static System.Numerics.Vector3 KneeDirection( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		System.Numerics.Vector3 previous, System.Numerics.Vector3[] positions )
	{
		if ( previous.LengthSquared() < 0.5f )
			previous = System.Numerics.Vector3.UnitX;
		var hip = positions[ foot.Leg.Upper ];
		var knee = positions[ foot.Leg.Lower ];
		var line = positions[ foot.Ankle ] - hip;
		if ( line.LengthSquared() < 1e-6f )
			return previous;
		line = System.Numerics.Vector3.Normalize( line );
		var pole = (knee - hip) - line * System.Numerics.Vector3.Dot( knee - hip, line );
		var bend = pole.Length();
		if ( bend < 1e-4f )
			return previous;
		var trust = Math.Clamp( bend / KneeBendTrusted, 0f, 1f );
		var blended = System.Numerics.Vector3.Lerp( previous, pole / bend, trust );
		return blended.LengthSquared() > 1e-6f ? System.Numerics.Vector3.Normalize( blended ) : previous;
	}

	// Knee offset from the hip-to-ankle line (metres) at which the animation's bend direction is fully trusted.
	private const float KneeBendTrusted = 0.05f;

	private static bool Finite( Transform value ) => float.IsFinite( value.Position.x + value.Position.y + value.Position.z
		+ value.Rotation.x + value.Rotation.y + value.Rotation.z + value.Rotation.w );

	private static bool Finite( System.Numerics.Vector3 value ) => float.IsFinite( value.X + value.Y + value.Z );

	// Pulls a foot target in along the line from the hip so it approaches the leg's full length smoothly.
	private Vector3 SoftenReach( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Vector3 target )
	{
		var hip = model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Leg.Upper ] ) );
		var length = SkateUnits.ToEngine( _positions[ foot.Leg.Lower ] - _positions[ foot.Leg.Upper ] ).Length
			+ SkateUnits.ToEngine( _positions[ foot.Ankle ] - _positions[ foot.Leg.Lower ] ).Length;
		var soft = length * SoftReach;
		var span = length - soft;
		var offset = target - hip;
		var distance = offset.Length;
		if ( distance <= soft || span <= 0f )
			return target;
		return hip + offset / distance * (soft + span * (1f - MathF.Exp( -(distance - soft) / span )));
	}

	// Velocity matching: while a foot is planted on the ground pushing, the clip plays at the rate that sweeps that
	// foot back exactly as fast as the board moves, so the foot stays put on the ground instead of sliding. The rate
	// eases back to the authored one when no foot is planted.
	// The clip board's offset from the clip root as the rider last rode.
	private System.Numerics.Vector3 _heldDeck;

	// On foot in the air a state made for one jump's flight plays its clip over the flight the rider has, so a long fall
	// does not play the clip out early and start it over in the air.
	private void MatchAirTime( in RiderAnimationInput input, float delta )
	{
		if ( _states?.Clip is not { } clip || input.Activity == RiderActivity.Riding || !input.Airborne
			|| !_states.Graph.HasAction( AirTimeMatch.Action ) )
			return;
		var speed = MathF.Max( _states.Graph.Play?.Speed ?? 1f, 0.01f );
		var remaining = (clip.Length( _parameters ) - _states.Time) / speed;
		_states.RateScale = AirTimeMatch.Rate( remaining, input.TimeToLand, delta );
	}

	// Riding, a grab's reach (the clip taking the hand to the board) is played fast enough for the hand to be on the board
	// GrabHold seconds before the grab is let go for the landing (see RiderAnimator.GrabRelease), never slower than
	// authored and no more than GrabReachFastest times as fast. Played as authored, an ollie on flat ground let go of the
	// grab as the hand arrived: the hand touched the board and left it at once.
	private const float GrabHold = 0.12f;
	private const float GrabReachFastest = 1.6f;

	private void MatchGrabReach( in RiderAnimationInput input, float delta )
	{
		if ( _states?.Clip is not { } clip || input.Activity != RiderActivity.Riding || !input.Airborne
			|| State is not { } state || !state.Contains( ".GrabsTweaks." ) || !state.EndsWith( ".Into" ) )
			return;
		var speed = MathF.Max( _states.Graph.Play?.Speed ?? 1f, 0.01f );
		var remaining = (clip.Length( _parameters ) - _states.Time) / speed;
		if ( !(remaining > 0f) || !(input.TimeToLand > 0f) )
			return;
		var left = input.TimeToLand - RiderAnimator.GrabRelease - GrabHold;
		var rate = left > 0.02f ? remaining / left : GrabReachFastest;
		_states.RateScale = MathF.Max( _states.RateScale, Math.Clamp( rate, 1f, GrabReachFastest ) );
	}

	private void MatchPushToGround( bool riding, float boardSpeed, float delta )
	{
		if ( _states is null )
			return;
		var target = 1f;
		for ( var i = 0; i < _feet.Length; i++ )
		{
			var ball = _positions[ _feet[ i ].Ball ];
			if ( riding && _locked[ i ] && _ballSeen[ i ] && delta > 0f )
			{
				// Backward speed of the clip's foot relative to the body at the authored rate, inches per second.
				var authored = -(ball.X - _ball[ i ].X) / delta / MathF.Max( _states.RateScale, 0.01f );
				if ( authored > MinimumSweep )
					target = Math.Clamp( MathF.Abs( boardSpeed ) * SkateUnits.InchesPerMetre / authored, MinimumRate, MaximumRate );
			}
			_ball[ i ] = ball;
			_ballSeen[ i ] = true;
		}
		_states.RateScale = MathX.Lerp( _states.RateScale, riding ? target : 1f, 1f - MathF.Exp( -RateSmoothing * delta ) );
	}

	// While the animated board is shown (in the air) it gives way to the feet along its own up axis instead of the
	// feet bending away from it: the animation was made for its own board and skater, and moving the legs there
	// kinks the knees. The board follows the move within a few frames (the legs keep the feet out meanwhile) and
	// eases back once the feet allow; when the feet ask for opposite directions (one above, one beneath) they yield
	// themselves. A board shown on a rail (a darkslide's) rests on it and gives way to nothing: the feet yield.
	private Transform SettleBoard( Transform model, Transform board, float delta, bool onRail = false )
	{
		if ( _skateboard is null || _boardShown <= 0f )
		{
			_settle = 0f;
			return board;
		}
		var wanted = 0f;
		var agreed = true;
		foreach ( var foot in _feet )
		{
			var push = RequiredPush( foot, model, board );
			var shin = ShinPush( foot, model, board );
			var move = -(MathF.Abs( shin ) > MathF.Abs( push ) && (push == 0f || MathF.Sign( shin ) == MathF.Sign( push )) ? shin : push);
			if ( MathF.Abs( move ) < 0.01f )
				continue;
			if ( wanted != 0f && MathF.Sign( move ) != MathF.Sign( wanted ) )
				agreed = false;
			if ( MathF.Abs( move ) > MathF.Abs( wanted ) )
				wanted = move;
		}
		if ( !agreed || onRail )
			wanted = 0f;
		wanted = Math.Clamp( wanted, -SettleLimit, SettleLimit );
		// Giving way follows the feet quickly but never in a single frame (a foot sweeping into a spinning board asks for
		// inches at once); easing back is slower. A foot changing sides in the air (the shorter way out of the deck now the
		// other) turns the give-way round at the release rate: followed at once, the deck crossed an inch in a frame and a
		// leg folded 20 degrees with it (an ollie off a rail).
		var growing = MathF.Abs( wanted ) > MathF.Abs( _settle ) && (MathF.Sign( wanted ) == MathF.Sign( _settle ) || MathF.Abs( _settle ) < 0.01f);
		_settle = MathX.Lerp( _settle, wanted, 1f - MathF.Exp( -(growing ? SettleFollow : SettleRelease) * delta ) );
		if ( MathF.Abs( _settle ) < 0.01f )
			return board;
		// The shown pose blends physics and animation by the shown weight, so the animated pose carries the move.
		var shift = board.Rotation.Up * (_settle / MathF.Max( _boardShown, 0.25f ));
		_skateboard.SetDisplayPose( _animatedBoard.WithPosition( _animatedBoard.Position + shift ), _boardShown );
		return _skateboard.DisplayedTransform;
	}

	// The deck surface under a point, and how fully it applies: fully over the deck, fading to nothing a short way
	// past its edge, so a foot swinging back onto the deck starts rising before it arrives instead of in one frame.
	private ((float Top, float Bottom) Surface, float Reach)? SurfaceNear( Vector3 local, BBox? bounds )
	{
		if ( _skateboard.DeckSurface( local.x, local.y ) is { } surface )
			return (surface, 1f);
		if ( bounds is not { } box || local.x < box.Mins.x - EdgeFade || local.x > box.Maxs.x + EdgeFade
			|| local.y < box.Mins.y - EdgeFade || local.y > box.Maxs.y + EdgeFade )
			return null;
		// Walk toward the deck's middle line to find the nearest point on its outline.
		var inside = new Vector2( Math.Clamp( local.x, box.Mins.x * 0.9f, box.Maxs.x * 0.9f ), 0f );
		if ( _skateboard.DeckSurface( inside.x, inside.y ) is null )
			return null;
		var outside = new Vector2( local.x, local.y );
		for ( var i = 0; i < 10; i++ )
		{
			var middle = (inside + outside) * 0.5f;
			if ( _skateboard.DeckSurface( middle.x, middle.y ) is null )
				outside = middle;
			else
				inside = middle;
		}
		var distance = (new Vector2( local.x, local.y ) - inside).Length;
		if ( distance >= EdgeFade || _skateboard.DeckSurface( inside.x, inside.y ) is not { } edge )
			return null;
		return (edge, 1f - Smoothstep( distance / EdgeFade ));
	}

	/// <summary>Which way the rider's toes point across this board in the pose shown: +1 its right, -1 its left, 0
	/// before a pose.</summary>
	internal float ToeSide( Transform board )
	{
		if ( _feet.Length == 0 || !Finite( _model ) )
			return 0f;
		var toes = Vector3.Zero;
		foreach ( var foot in _feet )
			toes += _model.Rotation * SkateUnits.ToEngine( _positions[ foot.Ball ] - _positions[ foot.Ankle ] );
		return MathF.Sign( Vector3.Dot( toes, board.Rotation.Right ) );
	}

	// The side of the deck a foot is cleared to. On the ground, where its ankle is against the deck's middle. In the
	// air, the way out that moves it least, kept until the other way is clearly shorter: a foot sunk a little into
	// the deck near its middle (a kickflip's flicking foot) would otherwise flick between being pushed up out of the
	// deck and down through it, and the board giving way to it would jump inches in a frame.
	private bool Side( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board, float top, float under )
	{
		var index = FootIndex( foot );
		if ( index < 0 )
			return Above( foot, model, board );
		var kept = _footOnTop[ index ] is { } previous
			? previous ? top <= -under + SideHysteresis : top + SideHysteresis < -under
			: top < -under || top == -under && Above( foot, model, board );
		_footOnTop[ index ] = kept;
		return kept;
	}

	// Inches the other way out of the deck must be shorter by before a foot in the air changes side.
	private const float SideHysteresis = 0.5f;

	// Which side of the deck each foot in the air is cleared to (true on top), as last chosen.
	private readonly bool?[] _footOnTop;

	// The side a foot is on: the side chosen for it in the air, else where its ankle is against the deck's middle.
	private bool OnTop( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board )
	{
		var index = FootIndex( foot );
		return _boardShown > 0f && index >= 0 && _footOnTop[ index ] is { } kept ? kept : Above( foot, model, board );
	}

	private int FootIndex( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
	{
		for ( var i = 0; i < _feet.Length; i++ )
		{
			if ( _feet[ i ].Ankle == foot.Ankle )
				return i;
		}
		return -1;
	}

	// Whether a foot is above the deck rather than beneath it, from its ankle against the deck's middle.
	private bool Above( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board )
	{
		var ankle = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Ankle ] ) ) );
		var surface = _skateboard.DeckSurface( ankle.x, ankle.y ) ?? _skateboard.DeckSurface( 0f, 0f ) ?? (0f, 0f);
		return ankle.z >= (surface.Top + surface.Bottom) * 0.5f;
	}

	// A foot whose whole sole is off the deck and close to the ground (pushing) stands on the real ground under it.
	// The clip places it for its own board's height, so another rider, board or slope would leave the sole
	// hovering or sunk; within a short gap the sole is set on the ground, and the hold fades as the clip lifts it.
	private void PlantOffDeck( int index, Transform model, Transform board, bool grounded, float delta )
	{
		var foot = _feet[ index ];
		var gap = float.MaxValue;
		var wanted = grounded ? PlantShift( foot, model, board, out gap ) : 0f;
		// Lowering onto the ground eases in and out so the leg never snaps or flickers as the foot crosses the deck's
		// edge or the edge of the plant's range; lifting a sole out of the ground applies at once.
		_plant[ index ] = wanted > 0f && wanted >= _plant[ index ] ? wanted
			: MathX.Lerp( _plant[ index ], wanted, 1f - MathF.Exp( -PlantRate * delta ) );
		var ankle = model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Ankle ] ) );
		var target = ankle + Vector3.Up * _plant[ index ];
		// A foot standing on the ground (pushing) stays where it touched down while the body rides on: the clip
		// moves it relative to the body only, so left alone it would slide and shake over the ground.
		// It locks on touching down and lets go only once clearly lifted, so a gap hovering at the edge does not keep
		// re-locking it. It takes full hold at once, exactly where the foot is shown at that moment (which a lock still
		// letting go may hold partly), so taking hold stops the foot without moving it.
		// A braking foot drags over the ground with the board rather than standing where it touched down: locked there like a
		// pushing foot, it was held behind as the board rolled on, the leg ran past its reach, and the foot hung 3 in over
		// the ground through the whole brake.
		var dragging = State?.Contains( ".Brake." ) == true;
		if ( dragging )
			_locked[ index ] = false;
		if ( grounded && gap <= LockGap && !_locked[ index ] && !dragging )
		{
			_locked[ index ] = true;
			_lockPoint[ index ] = Vector3.Lerp( target, _lockPoint[ index ].WithZ( target.z ), _lock[ index ] );
			_lock[ index ] = 1f;
		}
		if ( _locked[ index ] && (!grounded || gap > PlantSnap + PlantFade) )
			_locked[ index ] = false;
		// When the body carries the leg past its reach the held spot is dragged along at that reach, so the foot
		// slides only as much as it must and never jumps.
		var offset = (_lockPoint[ index ] - target).WithZ( 0f );
		if ( offset.Length > LockReach )
			_lockPoint[ index ] = target + offset.Normal * LockReach;
		var lockedAt = _lockPoint[ index ].WithZ( target.z );
		// Letting go eases, so what is left of the offset blends into the lifting foot's swing.
		if ( !_locked[ index ] )
			_lock[ index ] = MathX.Lerp( _lock[ index ], 0f, 1f - MathF.Exp( -LockRelease * delta ) );
		if ( _lock[ index ] > 0.001f )
			target = Vector3.Lerp( target, lockedAt, _lock[ index ] );
		// How far past the leg's soft reach the ground is under a foot being put down: the rider is lowered by as much.
		if ( grounded && _plant[ index ] < -0.01f )
		{
			var hip = model.PointToWorld( SkateUnits.ToEngine( _positions[ foot.Leg.Upper ] ) );
			var length = SkateUnits.ToEngine( _positions[ foot.Leg.Lower ] - _positions[ foot.Leg.Upper ] ).Length
				+ SkateUnits.ToEngine( _positions[ foot.Ankle ] - _positions[ foot.Leg.Lower ] ).Length;
			_groundShort = MathF.Max( _groundShort, hip.Distance( target ) - length * SoftReach );
		}
		target = SoftenReach( foot, model, target );
		if ( (target - ankle).Length < 0.01f )
			return;
		foot.Leg.Solve( _positions, _rotations, SkateUnits.ToCore( model.PointToLocal( target ) ), _kneeHint[ index ] );
	}

	// How far (inches, up positive) a foot beside the deck should move to stand on the ground under it: zero while
	// any of it is over the deck or it is well above the ground.
	private float PlantShift( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot,
		Transform model, Transform board, out float gap )
	{
		gap = float.MaxValue;
		if ( _skateboard is null )
			return 0f;
		var lowest = float.MaxValue;
		var under = Vector3.Zero;
		foreach ( var (position, padding) in SolePoints( foot ) )
		{
			var world = model.PointToWorld( SkateUnits.ToEngine( position ) );
			var local = board.PointToLocal( world );
			if ( _skateboard.DeckSurface( local.x, local.y ) is not null )
				return 0f;
			if ( world.z - padding < lowest )
			{
				lowest = world.z - padding;
				under = world;
			}
		}
		if ( lowest == float.MaxValue )
			return 0f;
		var ground = _rider.Scene.Trace.Ray( under + Vector3.Up * PlantReach, under - Vector3.Up * PlantReach )
			.IgnoreGameObjectHierarchy( _rider ).IgnoreGameObjectHierarchy( _board )
			.IgnoreGameObjectHierarchy( _renderer.GameObject ).Run();
		if ( !ground.Hit || ground.Normal.z < 0.5f )
			return 0f;
		gap = lowest - ground.HitPosition.z;
		// Whether the foot is put down is judged by the gap the clip meant: on a deck standing higher than the clips'
		// board the ground is further below the clip's foot by the difference, and the foot reaches down the whole gap.
		var meant = gap - _groundRise;
		var shift = -gap * (1f - Smoothstep( (meant - PlantSnap) / PlantFade ));
		gap = meant;
		return shift;
	}

	// The measured sole points where the mesh gave them, else the ankle and ball with their rest heights as padding.
	private IEnumerable<(System.Numerics.Vector3 Position, float Padding)> SolePoints(
		(TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
	{
		if ( foot.Sole.Length == 0 )
		{
			yield return (_positions[ foot.Ankle ], foot.AnkleHeight);
			yield return (_positions[ foot.Ball ], foot.BallHeight);
			yield break;
		}
		var rotation = _rotations[ foot.Ankle ];
		foreach ( var point in foot.Sole )
			yield return (_positions[ foot.Ankle ] + System.Numerics.Vector3.Transform( point, rotation ), 0f);
	}

	private float? LowestFootGap( Transform model, Transform board )
	{
		float? lowest = null;
		if ( _skateboard is null )
			return lowest;
		foreach ( var foot in _feet )
		{
			var onTop = OnTop( foot, model, board );
			float? soleLowest = null;
			foreach ( var (position, padding) in SolePoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
					continue;
				var gap = onTop ? local.z - padding - surface.Top : surface.Bottom - (local.z + (padding > 0f ? foot.BallHeight : 0f));
				if ( lowest is null || gap < lowest )
					lowest = gap;
				if ( soleLowest is null || gap < soleLowest )
					soleLowest = gap;
			}
			foreach ( var (position, radius) in ShinPoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				if ( _skateboard.DeckSurface( local.x, local.y ) is not { } surface )
					continue;
				var gap = onTop ? local.z - radius - surface.Top : surface.Bottom - (local.z + radius);
				if ( lowest is null || gap < lowest )
					lowest = gap;
			}
			if ( FootIndex( foot ) is var side and >= 0 && side < SoleDeckGaps.Length )
				SoleDeckGaps[ side ] = soleLowest;
			if ( onTop )
				continue;
			foreach ( var (position, padding) in SolePoints( foot ) )
			{
				var local = board.PointToLocal( model.PointToWorld( SkateUnits.ToEngine( position ) ) );
				foreach ( var box in _skateboard.PartBounds )
				{
					if ( local.x < box.Mins.x || local.x > box.Maxs.x || local.y < box.Mins.y || local.y > box.Maxs.y
						|| local.z > box.Maxs.z )
						continue;
					var gap = box.Mins.z - (local.z + (padding > 0f ? foot.BallHeight : 0f));
					if ( lowest is null || gap < lowest )
						lowest = gap;
				}
			}
		}
		return lowest;
	}

	/// <summary>True while riding switch (see <see cref="AnimationGraphDefinition.StanceSwapClips"/>).</summary>
	internal bool Switch { get; private set; }

	// Shown switch, the clips face the board's tail, so the clip's board is the real one turned half way round.
	private Rotation SwitchTurn => _shownSwitch ? Rotation.FromYaw( 180f ) : Rotation.Identity;

	// Each start of a turn-round clip swaps the stance; the next other clip to start shows it. Returns true when the shown
	// stance changes this frame.
	private bool TrackStance()
	{
		var graph = _treeStates?.Player.Graph ?? _states?.Graph;
		if ( graph is null || _stanceSwapClips.Count == 0 || _director.Requesting || graph.PlayCount == _plays )
			return false;
		_plays = graph.PlayCount;
		if ( graph.Clip is { } clip && _stanceSwapClips.Contains( clip ) )
		{
			Switch = !Switch;
			return false;
		}
		if ( _shownSwitch == Switch )
			return false;
		_shownSwitch = Switch;
		return true;
	}

	// Shows the graph's or the requested motion's pose; on a change between them the difference from the pose shown last
	// is eased out, as a change of clip is.
	private void ShowSource( SkeletonPose source, bool requested, float delta )
	{
		if ( requested != _shownRequest && _sourceShown )
		{
			for ( var bone = 0; bone < _sourceRotations.Length; bone++ )
			{
				_sourcePositions[ bone ] = _pose.Positions[ bone ] - source.Positions[ bone ];
				var offset = System.Numerics.Quaternion.Normalize( _pose.Rotations[ bone ]
					* System.Numerics.Quaternion.Conjugate( System.Numerics.Quaternion.Normalize( source.Rotations[ bone ] ) ) );
				_sourceRotations[ bone ] = offset.W < 0f ? System.Numerics.Quaternion.Negate( offset ) : offset;
			}
			_sourceTime = 0f;
		}
		_shownRequest = requested;
		_sourceShown = true;
		_pose.CopyFrom( source );
		if ( _sourceTime >= SourceBlend )
			return;
		_sourceTime += delta;
		var remaining = 1f - Smoothstep( _sourceTime / SourceBlend );
		for ( var bone = 0; bone < _sourceRotations.Length; bone++ )
		{
			_pose.Positions[ bone ] += _sourcePositions[ bone ] * remaining;
			_pose.Rotations[ bone ] = System.Numerics.Quaternion.Normalize(
				System.Numerics.Quaternion.Slerp( System.Numerics.Quaternion.Identity, _sourceRotations[ bone ], remaining ) * _pose.Rotations[ bone ] );
		}
	}

	// Showing the other stance turns and mirrors the whole pose in one frame; the difference from what was last shown is
	// eased out instead, so the body turns over smoothly.
	private void EaseFlip( Transform model, bool flipped, float delta )
	{
		if ( flipped )
		{
			// What was last shown, carried on with the board since: left where it was, the body stood still for the turn's
			// first frame while the board rolled on under it (1.8 in rolling back down a bank).
			var shownModel = _shownBoard is { } before && _skateboard is not null
				? _skateboard.RenderTransform.ToWorld( before.ToLocal( _shownModel ) ) : _shownModel;
			if ( !Finite( shownModel ) )
				shownModel = _shownModel;
			_reframe = (SkateUnits.ToCore( model.Rotation.Inverse * shownModel.Rotation ),
				SkateUnits.ToCore( model.PointToLocal( shownModel.Position ) ));
			for ( var bone = 0; bone < _positions.Length; bone++ )
			{
				var world = shownModel.PointToWorld( SkateUnits.ToEngine( _shownPositions[ bone ] ) );
				_flipPositions[ bone ] = SkateUnits.ToCore( model.PointToLocal( world ) ) - _positions[ bone ];
				var shown = SkateUnits.ToCore( model.Rotation.Inverse * shownModel.Rotation * SkateUnits.ToEngine( _shownRotations[ bone ] ) );
				var offset = System.Numerics.Quaternion.Normalize( shown * System.Numerics.Quaternion.Conjugate( _rotations[ bone ] ) );
				_flipRotations[ bone ] = offset.W < 0f ? System.Numerics.Quaternion.Negate( offset ) : offset;
			}
			_flipTime = 0f;
		}
		if ( _flipTime >= FlipEase )
			return;
		_flipTime += delta;
		var remaining = 1f - Smoothstep( _flipTime / FlipEase );
		for ( var bone = 0; bone < _positions.Length; bone++ )
		{
			_positions[ bone ] += _flipPositions[ bone ] * remaining;
			_rotations[ bone ] = System.Numerics.Quaternion.Normalize(
				System.Numerics.Quaternion.Slerp( System.Numerics.Quaternion.Identity, _flipRotations[ bone ], remaining ) * _rotations[ bone ] );
		}
	}

	// Off the board in a hippy jump the body flies the jump's own flight above the deck (see RiderAnimator.Root), and its
	// clips rise off their board too, the crouch springing up into a tuck: added, the rider went a body length up, the hips
	// 74 in over the deck and the feet 63, and fell 19 in in the 0.13 s the landing's clip took to bring them back down. The
	// clips' rise counts toward the flight instead: the hips go as high as the higher of the two (the flight's measured from
	// where the hips were as the rider left the deck), the one taking over from the other smoothly, and what is taken off
	// eases out as the rider lands.
	private float _flightHips = float.NaN;
	private float _flightWeight;

	// Inches over which the higher of the clips' and the flight's hips heights takes over from the other, and seconds the
	// height taken off eases out over once the rider is back on the deck.
	private const float FlightJoin = 4f;
	private const float FlightEase = 0.2f;

	private Transform HippyFlight( Transform model, RiderAnimationInput input, bool mirror, float delta )
	{
		var hips = _anchor >= 0 && Rig[ "hips" ] >= 0 ? _retargeter.SourceBone( Rig[ "hips" ], mirror ) : -1;
		_flightWeight = input.OffBoard && hips >= 0 ? 1f : MathF.Max( 0f, _flightWeight - delta / FlightEase );
		if ( hips < 0 || _flightWeight <= 0f )
		{
			_flightHips = float.NaN;
			return model;
		}
		var rise = _retargeter.SourceOffset( hips, mirror ).Z - _retargeter.SourceOffset( _anchor, mirror ).Z;
		if ( !float.IsFinite( _flightHips ) )
			_flightHips = rise;
		var flight = input.OffBoard ? input.OffBoardHeight * SkateUnits.InchesPerMetre : 0f;
		var flown = _flightHips + flight;
		// A smooth maximum, less what it adds where the two meet, so the body leaves the deck without a step.
		var higher = 0.5f * (rise + flown + MathF.Sqrt( (rise - flown) * (rise - flown) + FlightJoin * FlightJoin ) - FlightJoin);
		var drop = (rise + flight - higher) * Smoothstep( _flightWeight );
		return float.IsFinite( drop ) ? model.WithPosition( model.Position + Vector3.Down * drop ) : model;
	}

	private static float Smoothstep( float value )
	{
		value = Math.Clamp( value, 0f, 1f );
		return value * value * (3f - 2f * value);
	}

	// Landing on foot, a foot the clip has planted (its own foot down) is held where it was put down across the ground, and
	// stays held through the stand the landing settles into; it is let go as it lifts, as the rider moves off, or as the pose
	// takes it out of reach (easing back): the walker takes the landing's travel up a frame or two late, and the planted feet
	// slid 39-55 in/s for a tenth of a second after touchdown. Let go only as the landing ended, the hold's offset would ease
	// out across the ground instead. A foot is planted by its height over the clip's root, not over the clip's ground: a
	// landing onto higher or lower ground carries its root (where its feet plant) off that ground, by up to 42 cm.
	// The ball is what is held, not the ankle: a foot rolls off the ground over its ball, the heel rising while the ball stays
	// put, and held by the ankle the ball slid back 33 in/s as the heel rose. It is down while its heel or its ball is (a
	// standing landing puts the heel down a frame or two first). Held, it is let go only once well up (LiftedClearance): let
	// go as it began to rise, the shown foot was still on the ground as the hold eased out.
	private readonly FootLock[] _locks;
	private const float PlantedClearance = 0.025f;
	private const float LiftedClearance = 0.05f;

	private void HoldPlanted( int index, Transform model, bool mirror, bool grounded, string state, float delta )
	{
		var foot = _feet[ index ];
		var hold = _locks[ index ];
		var contact = Contact( foot );
		var keeps = state?.Contains( ".Landing." ) == true || (hold.Holding && state?.Contains( ".Stand." ) == true);
		var planted = grounded && keeps && MathF.Min( RootClearance( foot.Ankle, mirror ), RootClearance( contact, mirror ) )
			<= (hold.Holding ? LiftedClearance : PlantedClearance);
		// The hold works in metres; the model is in inches.
		var free = model.PointToWorld( SkateUnits.ToEngine( _positions[ contact ] ) );
		var held = SkateUnits.Inches( hold.Update( SkateUnits.Metres( free ), planted, delta ) );
		if ( (held - free).LengthSquared < 1e-6f )
			return;
		var target = _positions[ foot.Ankle ] + SkateUnits.ToCore( model.Rotation.Inverse * (held - free) );
		foot.Leg.Solve( _positions, _rotations, target, _kneeHint[ index ] );
	}

	// The bone a foot is held by: its ball, when the clips drive one.
	private int Contact( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot )
		=> foot.Ball >= 0 && _retargeter.SourceBone( foot.Ball, false ) >= 0 ? foot.Ball : foot.Ankle;

	// Metres a clip bone is over its rest height above the clip's root; infinite when the bone is not driven.
	private float RootClearance( int bone, bool mirror )
		=> _retargeter.SourceClearance( bone, mirror ) is { } clearance
			? (clearance + _retargeter.SourceGround * _retargeter.Scale) / SkateUnits.InchesPerMetre : float.PositiveInfinity;

	// While a landing holds a foot, the walker travels as the clip's pose moves that foot back across the ground (model
	// space), so the foot the pose has stays where it is held, rather than at the clip root's travel: in the landings into a
	// run the clip's own planted foot sweeps back 40 in/s faster than its root travels (root 110, foot 150), which the hold
	// took up as 5.7 in of offset in 0.2 s and let go of at 114 in/s as the foot lifted. No faster than FootTravelMost times
	// the root's travel (a pose jump). Null when no foot is held. Each foot's posed contact is kept for the next frame's.
	private const float FootTravelMost = 1.6f;
	private readonly System.Numerics.Vector3[] _contactBefore;
	private readonly bool[] _contactSeen;

	private System.Numerics.Vector3? HeldFootTravel( System.Numerics.Vector3 rootTravel, bool mirror, float delta )
	{
		// The foot the body goes on over: of the held feet the clip has down, the one sweeping back fastest along the root's
		// travel. A held foot already sliding forward into its stride (down by its height, toe dragging) was averaged in at
		// first and slowed the walker from 120 to 13 in/s under the foot still down, which then ran out of reach (ankle 3.4 in
		// in a frame). With the root still, the held feet's travels are averaged.
		var along = new System.Numerics.Vector3( rootTravel.X, rootTravel.Y, 0f );
		var way = along.LengthSquared() > 1f ? System.Numerics.Vector3.Normalize( along ) : System.Numerics.Vector3.Zero;
		var sum = System.Numerics.Vector3.Zero;
		var held = 0;
		var best = float.NegativeInfinity;
		var stance = System.Numerics.Vector3.Zero;
		for ( var i = 0; i < _feet.Length; i++ )
		{
			var contact = Contact( _feet[ i ] );
			var at = _positions[ contact ];
			var down = MathF.Min( RootClearance( _feet[ i ].Ankle, mirror ), RootClearance( contact, mirror ) ) <= PlantedClearance;
			if ( _locks[ i ].Holding && down && _contactSeen[ i ] && delta > 1e-5f )
			{
				var foot = (_contactBefore[ i ] - at) / delta;
				foot.Z = 0f;
				sum += foot;
				held++;
				var ahead = System.Numerics.Vector3.Dot( foot, way );
				if ( ahead > best )
				{
					best = ahead;
					stance = foot;
				}
			}
			_contactBefore[ i ] = at;
			_contactSeen[ i ] = Finite( at );
		}
		if ( held == 0 )
			return null;
		var travel = way == System.Numerics.Vector3.Zero ? sum / held : stance;
		var most = new System.Numerics.Vector3( rootTravel.X, rootTravel.Y, 0f ).Length() * FootTravelMost;
		var speed = travel.Length();
		if ( !float.IsFinite( speed ) )
			return null;
		return speed > most ? (speed > 1e-4f ? travel * (most / speed) : System.Numerics.Vector3.Zero) : travel;
	}

	// On foot a foot is kept out of the ground under it, the lift following what is needed at FootLiftRate; in the air it
	// lets go at that rate.
	private void LiftOnFoot( int index, bool airborne, float delta )
	{
		var foot = _feet[ index ];
		var sole = MathF.Min( _positions[ foot.Ankle ].Z - foot.AnkleHeight, _positions[ foot.Ball ].Z - foot.BallHeight );
		var wanted = airborne ? 0f : Math.Clamp( -sole, 0f, MaximumLift );
		var most = FootLiftRate * delta;
		_footLift[ index ] = Math.Clamp( wanted, _footLift[ index ] - most, _footLift[ index ] + most );
		if ( _footLift[ index ] > 0.01f )
			foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + new System.Numerics.Vector3( 0f, 0f, _footLift[ index ] ) );
	}

	// Lifts a sole that sank below the support surface.
	private void Lift( (TwoBoneIk Leg, int Ankle, int Ball, float AnkleHeight, float BallHeight, System.Numerics.Vector3[] Sole, float ShinRadius) foot, float surface,
		System.Numerics.Vector3 hint = default )
	{
		var sole = MathF.Min( _positions[ foot.Ankle ].Z - foot.AnkleHeight, _positions[ foot.Ball ].Z - foot.BallHeight );
		var lift = Math.Clamp( surface - sole, 0f, MaximumLift );
		if ( lift > 0.01f )
			foot.Leg.Solve( _positions, _rotations, _positions[ foot.Ankle ] + new System.Numerics.Vector3( 0f, 0f, lift ), hint );
	}

	internal void Clear()
	{
		_renderer.SceneModel?.ClearBoneOverrides();
	}
}
