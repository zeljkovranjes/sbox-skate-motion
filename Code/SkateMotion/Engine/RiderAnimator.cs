using System.Linq;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Citizen;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Vehicle;

namespace SkateMotion.Engine;

internal sealed class RiderAnimator
{
	private readonly GameObject _visual;
	private readonly SkinnedModelRenderer _renderer;
	private readonly CitizenAnimationHelper _animation;
	private readonly GameObject _front;
	private readonly GameObject _rear;
	private readonly GameObject _carry;
	private readonly GameObject _reach;
	private readonly GameObject _stepLeft;
	private readonly GameObject _stepRight;
	private Transform _handStart;
	private Transform _leftStart;
	private Transform _rightStart;
	private Rotation _visualStart;
	private readonly bool _ownsFront;
	private readonly bool _ownsRear;
	private readonly Transform _leftAnkle;
	private readonly Transform _rightAnkle;
	private RiderRagdoll _ragdoll;
	private readonly GameObject _rider;
	private readonly GameObject _board;
	private ClipRiderAnimation _clips;
	private SkateTrick _trick;

	// Whether the trick under way, and the one just landed, were popped off the nose (their nollie versions).
	private bool _trickNose;

	// The stick has not come back to the middle since the last pop.
	private bool _windupSpent;
	private int _windupSide;
	private bool _landedNose;
	private float _sincePop = 10f;
	private float _sinceLanding = 10f;
	private float _rebound;
	private float _speedShown;
	private float _verticalShown;

	// How fast (per second) board-derived animation inputs follow the board.
	private const float InputSmoothing = 15f;

	// Every wheel can lift for a moment without the rider leaving the ground (a board rocking after a landing, a hop
	// where the floor meets a ramp): air the rider did not pop into only counts once it lasts.
	private const float ReboundHold = 0.1f;
	private float _landingSpeed;
	private float _windup;
	private bool _highPop;
	private Rotation _rootRotation = Rotation.Identity;

	// Stepping off a tilted or turned-over board (along a wall, in a body flip) the body comes upright at this rate (per
	// second) instead of in one frame; the turn still to go, and what the last root was for.
	private const float UprightRate = 8f;
	private Rotation _uprightTurn = Rotation.Identity;
	private RiderActivity _rootActivity;
	private SkateInput _lastInput;
	private SkateTrick _landedTrick;
	private string _plant;

	// A hand plant: how long the hand has been planted (or since it let go), and which way it turns the rider.
	private bool _planting;
	private float _plantPart = 10f;
	private bool _plantFrontside = true;

	// Seconds a hand plant plays its way in, and its way out after it lets go.
	private const float PlantInto = 0.3f;
	private const float PlantOut = 0.5f;

	// The hand plant's part under way (see RiderAnimationInput.Handplant): it turns the way the rider steers as it plants,
	// frontside with no steering.
	private string HandplantPart( bool planted, float steer, float delta )
	{
		if ( planted != _planting )
		{
			_planting = planted;
			_plantPart = 0f;
			if ( planted )
				_plantFrontside = steer >= 0f;
		}
		else
		{
			_plantPart += delta;
		}
		if ( planted )
			return _plantPart < PlantInto ? "into" : "cycle";
		return _plantPart < PlantOut ? "out" : null;
	}
	private string _catchStyle;
	private string _lateTrick;
	private bool _lateArmed;
	private float _lateTime;
	private bool _hippy;
	private float _airTime;
	private float _takeoffYaw;
	private bool _wasAirborne;
	private Vector3 _lastVelocity;
	private string _landingKind;

	// A landing below this speed (inches per second) is always clean; a turn in the air past SpinTurn (degrees) is a
	// spin landing; otherwise a heading this far off the travel (sine of the angle) is a sketchy one.
	private const float CleanBelow = 2f * 39.37f;
	private const float SpinTurn = 5.73f;
	private const float SketchySine = 0.2f;

	// Classifies a landing from the deck as it comes down, the travel and the deck's heading at take-off.
	internal static string LandingKind( Rotation deck, Vector3 velocity, float takeoffYaw )
	{
		var travel = velocity.WithZ( 0f );
		if ( travel.Length < CleanBelow )
			return null;
		if ( MathF.Abs( MathX.DeltaDegrees( takeoffYaw, deck.Yaw() ) ) > SpinTurn )
			return "spin";
		var forward = deck.Forward.WithZ( 0f );
		if ( forward.Length < 0.01f )
			return null;
		// Riding backwards counts as on the line too.
		var sine = MathF.Abs( Vector3.Cross( forward.Normal, travel.Normal ).z );
		return sine >= SketchySine ? "sketchy" : null;
	}
	private float _stationary;
	private float _lastDeckZ;
	private bool _onGround = true;
	private float _fallSpeed;
	internal RiderRagdoll Ragdoll => _ragdoll;

	/// <summary>Graph state of the clip animation, or null when the built-in animation is used.</summary>

	internal string ClipMotion => _clips?.Motion;

	/// <summary>True while the clip rider rides switch.</summary>
	internal bool Switch => _clips?.Switch ?? false;
	internal string ClipState => _clips?.State;
	internal Transform? CarriedBoardPose => _clips?.CarriedBoardPose;

	/// <summary>
	/// Where the rider stands at a drop-in's lip, facing over it, while it drops in (null otherwise): the board the clips set
	/// down there lies where they have it against where their skater stands.
	/// </summary>
	internal Transform? BoardRestFrame
	{
		get => _clips?.BoardRestFrame;
		set
		{
			if ( _clips is not null )
				_clips.BoardRestFrame = value;
		}
	}

	/// <summary>How far through its own travel the playing clip is (0 to 1), or null for a clip that does not travel.</summary>
	internal float? ClipTravel => _clips?.TravelProgress;

	/// <summary>While a landing on foot plays, how its clip's root travels (inches per second, world); null otherwise.</summary>
	internal Vector3? LandingTravel => _clips?.LandingTravel;

	/// <summary>Where the playing clip's root is against where its skater stands by the board (inches), or null.</summary>
	internal Vector3? ClipTravelOffset => _clips?.TravelOffset;

	internal float? FootDeckGap => _clips?.FootDeckGap;
	internal float?[] SoleDeckGaps => _clips?.SoleDeckGaps;
	internal float?[] ClipToeHeights => _clips?.ClipToeHeights;
	internal float? HandDeckGap( Transform board ) => _clips?.HandDeckGap( board );

	/// <summary>Whether a foot is planted on the ground (pushing) in the animation, or null without clips.</summary>
	internal bool? PushContact => _clips?.FootOnGround;

	/// <summary>True while the animation holds the board dark side up.</summary>
	internal bool DarkSideUp => _clips?.Dark ?? false;

	/// <summary>The graph has the board dark side up on the ground (see <see cref="ClipRiderAnimation.DarkOnGround"/>).</summary>
	internal bool DarkOnGround => _clips?.DarkOnGround ?? false;

	/// <summary>The lip of the board's last hand plant, for the planting hand.</summary>
	internal Vector3? PlantLip
	{
		get => _clips?.PlantLip;
		set
		{
			if ( _clips is not null )
				_clips.PlantLip = value;
		}
	}
	internal BoardRetrievalProfile RetrievalProfile => _clips?.RetrievalProfile;
	internal bool RetrieveRequested => _clips?.RetrieveRequested ?? false;

	/// <summary>True once a drop or throw playing has reached the moment its hand lets go of the board.</summary>
	internal bool BoardReleased => _clips?.BoardReleased ?? false;

	/// <summary>How far the rider should turn (degrees, left positive) for a reach to cover the board being picked up.</summary>
	internal float ReachTurn => _clips?.ReachTurn ?? 0f;

	/// <summary>The board-handling motion playing over the body (a reach, drop or throw), or null.</summary>
	internal string HandlingMotion => _clips?.HandlingMotion;

	internal GameObject Visual => _visual;

	/// <summary>Smooths the rider's fixed-step movement for the animation root.</summary>
	internal StepSmoother Smoother { get; } = new();

	// The board's physical pose as shown this frame, leaning into a turn as the deck does.
	private Transform BoardShown => (_display ??= _board.Components.GetAll<Component>( FindMode.EverythingInSelf )
		.OfType<IBoardDisplay>().FirstOrDefault())?.RiddenTransform ?? _board.WorldTransform;

	private IBoardDisplay _display;

	internal RiderAnimator( GameObject rider, GameObject board, Model model, GameObject front, GameObject rear )
	{
		_rider = rider;
		_board = board;
		// Hidden until it is first posed: a model shown before then stands in its bind pose (the T) for a frame or more.
		_visual = new GameObject( rider, false, "Rider Model" );
		_renderer = _visual.Components.Create<SkinnedModelRenderer>();
		_renderer.Model = model ?? Model.Load( "models/citizen/citizen.vmdl" );
		_leftAnkle = _renderer.Model.GetBoneTransform( "ankle_L" );
		_rightAnkle = _renderer.Model.GetBoneTransform( "ankle_R" );
		if ( model is null )
			RiderOutfit.ApplyDefault( _renderer );
		_animation = _visual.Components.Create<CitizenAnimationHelper>();
		_animation.Target = _renderer;
		_ownsFront = !front.IsValid();
		_ownsRear = !rear.IsValid();
		_front = front.IsValid() ? front : Target( board, "Front Foot", new( 8f, 0f, 1f ) );
		_rear = rear.IsValid() ? rear : Target( board, "Rear Foot", new( -8f, 0f, 1f ) );
		_carry = Target( rider, "Carry Hand", new( 1f, -17f, 37f ) );
		var wrist = _renderer.Model.GetBoneTransform( "hand_R" );
		var knuckle = _renderer.Model.GetBoneTransform( "finger_middle_0_R" );
		var fingers = knuckle.Position - wrist.Position;
		if ( fingers.LengthSquared > 0.001f )
			_carry.LocalRotation = Rotation.FromToRotation( fingers.Normal, Vector3.Down ) * wrist.Rotation;
		_reach = Target( rider, "Interaction Hand", Vector3.Zero );
		_stepLeft = Target( rider, "Interaction Left Foot", Vector3.Zero );
		_stepRight = Target( rider, "Interaction Right Foot", Vector3.Zero );
	}

	/// <summary>Switches from the built-in animation to clip playback driven by gameplay.</summary>
	internal void UseClips( RiderAnimationAssets assets )
	{
		_clips?.Clear();
		_clips = null;
		if ( assets is null )
			return;
		try
		{
			_clips = new ClipRiderAnimation( _renderer, assets, _rider, _board );
		}
		catch ( InvalidOperationException error )
		{
			// Not a humanoid the clips can drive: keep the built-in animation.
			Log.Warning( error.Message );
		}
	}

	internal void Request( IReadOnlyDictionary<string, string> tags, float seconds ) => _clips?.Request( tags, seconds );

	/// <summary>Blends the clips' first poses from a body pose (see <see cref="Components.SkateRider.BlendFrom"/>).</summary>
	internal void BlendFrom( Transform[] modelPose, float seconds ) => _clips?.BlendFrom( modelPose, seconds );

	internal IReadOnlyList<string> Actions( string family, string style ) =>
		_clips?.Actions( family, style ) ?? System.Array.Empty<string>();

	internal void OnTrick( SkateTrick trick ) => OnTrick( trick, false );

	/// <summary>A trick popped, <paramref name="nose"/> when off the nose (a nollie version).</summary>
	internal void OnTrick( SkateTrick trick, bool nose )
	{
		_trick = trick;
		_trickNose = nose;
		_windupSpent = true;
		_sincePop = 0f;
		_airTime = 0f;
		// A longer windup charges a higher pop; a grab held at the pop plants a foot; braking hops off the board.
		_highPop = _windup > 0.3f;
		_plant = PlantName( _lastInput.Grab );
		_hippy = _lastInput.Brake > 0.5f;
	}

	internal void OnLanded( float speed )
	{
		var flips = Flips( _trick );
		_catchStyle = !flips ? null : _lastInput.Slide ? "darkcatch" : _lastInput.Gesture ? "underflip"
			: _airTime > 1.3f ? "quadflip" : null;
		_landedTrick = _trick;
		_landedNose = _trickNose;
		_trickNose = false;
		_landingKind = LandingKind( BoardShown.Rotation, _lastVelocity, _takeoffYaw );
		_trick = SkateTrick.None;
		_lateTrick = null;
		_sinceLanding = 0f;
		_landingSpeed = speed;
	}

	/// <summary>Plays clips for this frame; returns false when the built-in animation should run instead.</summary>
	internal bool UpdateClips( RiderActivity activity, SkateState board, SkateInput input, Vector3 velocity, bool goofy,
		string style, float delta, string objectMotion = null, string objectHeight = null, bool skitching = false,
		float moveAmount = 0f, bool holdingBoard = false, HippyJump hippy = null, bool runout = false, string groundSlope = null,
		float edgeDistance = float.PositiveInfinity, bool mountHeld = false, bool droppingIn = false )
	{
		_hippyJump = hippy;
		_sincePop += delta;
		_sinceLanding += delta;
		_lastInput = input;
		_rebound = board.IsAirborne ? _rebound + delta : 0f;
		var airborne = board.IsAirborne && (_rebound > ReboundHold || _sincePop < _sinceLanding);
		// Riding, the rider's footing is the board's: stepping off, it starts from there (on the ground for a runout) rather
		// than from wherever it last walked.
		if ( activity == RiderActivity.Riding )
			_onGround = !airborne;
		// A wind-up is the stick held toward one end from the middle. The flick that lets it go swings the stick to the
		// other end, and a stick left at the end of a flick, is no new wind-up: one starts again once the stick has come
		// back to the middle.
		if ( MathF.Abs( input.FlickY ) < 0.15f )
		{
			_windupSpent = false;
			_windupSide = 0;
		}
		else if ( MathF.Abs( input.FlickY ) > 0.6f )
		{
			var side = MathF.Sign( input.FlickY );
			if ( _windupSide == 0 )
				_windupSide = side;
			else if ( side != _windupSide )
				_windupSpent = true;
		}
		var winding = MathF.Abs( input.FlickY ) > 0.6f && !airborne && !_windupSpent;
		_windup = winding ? _windup + delta : 0f;
		if ( board.IsAirborne && !_wasAirborne )
			_takeoffYaw = BoardShown.Rotation.Yaw();
		_wasAirborne = board.IsAirborne;
		_lastVelocity = velocity;
		if ( board.IsAirborne )
			_airTime += delta;
		else if ( _sinceLanding > 0.5f )
		{
			_plant = null;
			_hippy = false;
		}
		TrackLateTrick( input, board.IsAirborne, delta );
		var horizontal = velocity.WithZ( 0f ).Length / SkateUnits.InchesPerMetre;
		_stationary = horizontal < 0.2f && MathF.Abs( board.Speed ) < 0.2f ? _stationary + delta : 0f;
		// Inputs taken from the board use its pose as shown (smoothed between physics steps) and a smoothed speed:
		// read raw, they change only on frames with a physics step and make blends and playback rates lurch.
		var boardShown = BoardShown;
		var deckZ = boardShown.Position.z;
		var vertical = activity == RiderActivity.Riding
			? (deckZ - _lastDeckZ) / MathF.Max( delta, 0.0001f ) / SkateUnits.InchesPerMetre
			: velocity.z / SkateUnits.InchesPerMetre;
		_lastDeckZ = deckZ;
		_verticalShown = MathX.Lerp( _verticalShown, vertical, 1f - MathF.Exp( -InputSmoothing * delta ) );
		_speedShown = MathX.Lerp( _speedShown, board.Speed, 1f - MathF.Exp( -InputSmoothing * delta ) );
		if ( _clips is null || activity == RiderActivity.Bailed )
			return false;
		ClearHelperIk();
		var deck = boardShown.Rotation;
		var landed = _sinceLanding < 0.35f;
		// On foot the time to land is the rider's own, from where it is and how fast it rises.
		var timeToLand = activity == RiderActivity.Riding
			? board.IsAirborne ? TimeToLand( boardShown.Position, velocity.z / SkateUnits.InchesPerMetre ) : 0f
			: !_onGround ? TimeToLand( _rider.WorldPosition, velocity.z / SkateUnits.InchesPerMetre ) : 0f;
		var trick = landed ? _landedTrick : _trick;
		// The grind as named from its lock-on (a board shown for another machine has only the contacts' kind).
		var named = board.NamedGrind != SkateGrind.None;
		var grind = HeldGrind( named ? board.NamedGrind : board.Grind, delta );
		var snapshot = new RiderAnimationInput
		{
			Activity = activity,
			Airborne = activity == RiderActivity.Riding ? airborne : !_onGround,
			// Rolling backwards is a negative speed to the animation (fakie riding).
			Speed = board.Fakie ? -_speedShown : _speedShown,
			Steer = input.Steer,
			Pushing = input.Push > 0.1f,
			PushLeft = (input.PushFeet & SkatePushFeet.Left) != 0,
			PushRight = (input.PushFeet & SkatePushFeet.Right) != 0,
			NewPush = input.NewPush,
			Brake = input.Brake,
			// A powerslide the board reports, or without one (estimated handling) the slide input itself.
			Sliding = board.PowerSlide != 0 || (input.Slide && !board.Reverting),
			SlideSide = board.PowerSlide,
			Reverting = board.Reverting,
			Pumping = input.Pump,
			Windup = winding ? MathF.Abs( input.FlickY ) : 0f,
			WindupNose = winding && input.FlickY > 0f,
			WindupSpin = winding ? input.FlickX * (goofy ? -1f : 1f) : 0f,
			TweakX = airborne && input.Grab != SkateAirGrab.None ? input.FlickX * (goofy ? -1f : 1f) : 0f,
			TweakY = airborne && input.Grab != SkateAirGrab.None ? input.FlickY : 0f,
			AirStickX = airborne ? input.FlickX * (goofy ? -1f : 1f) : 0f,
			AirStickY = airborne ? input.FlickY : 0f,
			Catch = input.Catch,
			FeetOff = (int)input.FeetOff,
			FlipHeld = airborne && FlipHeld( trick, input.FlickX * (goofy ? -1f : 1f) ),
			Balance = board.Balance == SkateBalanceMode.Manual ? 1 : board.Balance == SkateBalanceMode.NoseManual ? 2 : 0,
			Grind = GrindName( grind ),
			GrindFrontside = GrindFrontside( board, grind ),
			GrindNoseToHeels = GrindNoseToHeels( board, grind ),
			GrindBackward = named ? board.NamedBackward : board.GrindBackward,
			GrindTwisted = named ? board.NamedTwisted : board.GrindTwisted,
			GrindLow = named ? board.NamedLow : board.GrindLow,
			GrindOnLedgeTop = named ? board.NamedOnLedgeTop : board.GrindOnLedgeTop,
			Trick = TrickName( trick ),
			TrickNollie = trick == SkateTrick.Nollie || trick != SkateTrick.None && (landed ? _landedNose : _trickNose),
			TrickFlips = Flips( trick ),
			HighPop = _highPop,
			// A grab still held as the board comes down lets go in time for its release to bring the board back underfoot.
			Grab = LetGoForLanding( airborne, timeToLand, input.Grab != SkateAirGrab.None ) ? null : GrabName( input.Grab ),
			TimeToLand = timeToLand,
			Plant = _plant,
			LateTrick = _lateTrick,
			LateBackFoot = _lateBack,
			LateFrontside = _lateFrontside,
			CatchStyle = landed ? _catchStyle : null,
			// Grabbing with brake held plants a hand, unless it is leaning back into a body flip.
			Handplant = HandplantPart( board.HandPlanting, input.Steer * (goofy ? -1f : 1f), delta ),
			HandplantFrontside = _plantFrontside,
			OneFoot = input.Gesture && airborne,
			Gesturing = input.Gesture,
			Adjust = airborne && MathF.Abs( input.FlickY ) is > 0.2f and < 0.6f ? -input.FlickY : 0f,
			TimeSincePop = _sincePop,
			TimeSinceLanding = _sinceLanding,
			LandingSpeed = _landingSpeed,
			LandingKind = landed ? _landingKind : null,
			VerticalSpeed = _verticalShown,
			DeckUp = deck.Up.z,
			DeckRoll = deck.Left.z,
			DeckPitch = MathF.Asin( Math.Clamp( -deck.Forward.z, -1f, 1f ) ),
			MoveSpeed = horizontal,
			MoveAmount = moveAmount,
			HoldingBoard = holdingBoard,
			Goofy = goofy,
			Style = string.IsNullOrWhiteSpace( style ) ? null : style,
			Stationary = _stationary,
			Skitching = skitching,
			ObjectMotion = objectMotion,
			ObjectHeight = objectHeight,
			HippyJump = _hippy || input.HippyJump || hippy?.Flying == true || (input.Brake > 0.5f && input.FlickY < -0.6f),
			HippyCrouch = hippy?.Crouching == true,
			BodyFlipping = board.BodyFlip != 0,
			WallRiding = board.WallRiding,
			Crouch = board.Crouch,
			OffBoard = hippy?.Flying == true,
			OffBoardHeight = hippy?.Offset.Z ?? 0f,
			OffBoardSpeed = hippy?.VerticalSpeed ?? 0f,
			OverBoard = hippy?.OverBoard ?? true,
			TimeToLandOnBoard = hippy?.TimeToLand ?? 0f,
			Runout = runout,
			GroundSlope = groundSlope,
			EdgeDistance = edgeDistance,
			MountHeld = mountHeld,
			DroppingIn = droppingIn
		};
		_clips.Update( snapshot, delta, Root( activity, board, delta ) );
		return true;
	}

	/// <summary>Tells the clips whether the walking rider stands on the ground and how fast it was falling.</summary>
	internal void SetFooting( bool grounded, float fallSpeed )
	{
		if ( grounded && !_onGround )
		{
			_sinceLanding = 0f;
			_landingSpeed = MathF.Abs( _fallSpeed );
		}
		_onGround = grounded;
		_fallSpeed = fallSpeed;
	}

	// A full flick in the air (pull back, then forward) plays a late trick; the board's physics is unchanged.
	private void TrackLateTrick( SkateInput input, bool airborne, float delta )
	{
		_lateTime = MathF.Max( 0f, _lateTime - delta );
		if ( _lateTime <= 0f )
			_lateTrick = null;
		if ( !airborne || _sincePop < 0.3f )
		{
			_lateArmed = false;
			return;
		}
		// Pulled back and flicked forward the front foot flicks the board; pushed forward and pulled back, the back foot.
		// Flicked to a side it flips (a kickflip or heelflip for the stance); straight, it shoves the board round the
		// way the stick swept on the way.
		var stance = input.Goofy ? -1f : 1f;
		if ( MathF.Abs( input.FlickY ) > 0.65f && !_lateArmed )
		{
			_lateArmed = true;
			_lateBack = input.FlickY > 0f;
			_lateSweep = 0f;
		}
		else if ( _lateArmed && (_lateBack ? input.FlickY < -0.35f : input.FlickY > 0.35f) )
		{
			_lateArmed = false;
			var side = input.FlickX * stance;
			_lateTrick = side < -0.3f ? "kickflip" : side > 0.3f ? "heelflip" : _highPop ? "shuvit-high" : "shuvit-low";
			_lateFrontside = _lateSweep * stance > 0f;
			_lateTime = 0.6f;
		}
		if ( _lateArmed && MathF.Abs( input.FlickX ) > MathF.Abs( _lateSweep ) )
			_lateSweep = input.FlickX;
	}

	// The foot that flicked the late trick (the back one off the nose), and for a shove which way the stick swept.
	private bool _lateBack;
	private bool _lateFrontside;
	private float _lateSweep;

	// A kickflip (or heelflip) still held toward the side it was flicked to keeps turning.
	private static bool FlipHeld( SkateTrick trick, float side ) => trick switch
	{
		SkateTrick.Kickflip => side < -0.3f,
		SkateTrick.Heelflip => side > 0.3f,
		_ => false
	};

	private static bool Flips( SkateTrick trick ) =>
		trick is not (SkateTrick.None or SkateTrick.Ollie or SkateTrick.Nollie or SkateTrick.Plant);

	private static string PlantName( SkateAirGrab grab ) => grab switch
	{
		SkateAirGrab.None => null,
		SkateAirGrab.Crail => "nocomply",
		SkateAirGrab.Tail or SkateAirGrab.Stale or SkateAirGrab.Method => "fastplant",
		_ => "boneless"
	};

	// Clips are authored in the board's travel frame. On the ground that frame follows the deck surface; in the
	// air it keeps only the heading, so flips and spins of the physical board do not turn the rider with it.
	// The animation is placed where the rider or board is shown this frame (smoothed between physics steps), so it
	// never steps at the physics rate under a pose that changes every frame.
	private Transform Root( RiderActivity activity, SkateState board, float delta )
	{
		if ( activity != RiderActivity.Riding )
		{
			var shown = Smoother.At( _rider.WorldTransform, Time.Now );
			if ( activity == RiderActivity.Dismounting && _rootActivity == RiderActivity.Riding )
				_uprightTurn = _rootRotation * shown.Rotation.Inverse;
			_rootActivity = activity;
			_uprightTurn = Rotation.Slerp( _uprightTurn, Rotation.Identity, 1f - MathF.Exp( -UprightRate * delta ) );
			_rootRotation = _uprightTurn * shown.Rotation;
			return new Transform( shown.Position, _rootRotation );
		}
		_rootActivity = activity;
		var boardShown = BoardShown;
		var deck = boardShown.Rotation;
		// Off the board in a hippy jump the body follows its own flight above the board rolling beneath.
		var flight = _hippyJump is { Flying: true } jump ? SkateUnits.Inches( jump.Offset ) : Vector3.Zero;
		// The rider carries the board: in the air too the body follows the physical deck's whole rotation (a ramp
		// launch tips both), and the animation shows tricks as the board's turn relative to the body. With clips the
		// pop gives the deck no trick spin, so once a trick is caught the shown board sits on the physical one.
		var target = board.IsAirborne && _clips is null ? Rotation.FromYaw( deck.Yaw() ) : deck;
		// Turning over in a body flip the rider is carried round with the board, never trailing it.
		_rootRotation = board.BodyFlip != 0 && _clips is not null ? target
			: Rotation.Slerp( _rootRotation, target, 1f - MathF.Exp( -12f * delta ) );
		// Raised with the shown board out of a rail, the rider stays standing on it.
		var lift = _display?.RailLift ?? Vector3.Zero;
		return new Transform( boardShown.Position + flight + lift, _rootRotation );
	}

	/// <summary>The rider came back down onto the deck from a hippy jump at this speed (m/s).</summary>
	internal void OnHippyLanded( float speed )
	{
		_sinceLanding = 0f;
		_landingSpeed = speed;
		_landingKind = null;
	}

	private HippyJump _hippyJump;

	// Seconds before touchdown that a held grab lets go.
	private const float GrabRelease = 0.4f;

	// A grab let go for the landing stays let go through the landing until the grab is let go of, even where the landing
	// can no longer be seen ahead in the last moments (a grab still held on touching down is not a ground grab); clearly
	// more air ahead (a drop beyond a ledge) lets it be held again.
	private bool _grabLetGo;

	private bool LetGoForLanding( bool airborne, float timeToLand, bool grabbing )
	{
		if ( !grabbing )
			_grabLetGo = false;
		else if ( airborne && timeToLand > 0f && timeToLand < GrabRelease )
			_grabLetGo = true;
		else if ( airborne && timeToLand > GrabRelease + 0.2f )
			_grabLetGo = false;
		return _grabLetGo;
	}

	// Seconds until a body here, rising at this speed (m/s), comes down onto whatever is below it; zero with nothing
	// below in reach.
	private float TimeToLand( Vector3 position, float rising )
	{
		var below = _board.Scene.Trace.Ray( position, position + Vector3.Down * GroundReach )
			.IgnoreGameObjectHierarchy( _board ).IgnoreGameObjectHierarchy( _rider ).Run();
		if ( !below.Hit )
			return 0f;
		var drop = (position.z - below.HitPosition.z) / SkateUnits.InchesPerMetre;
		return BodyFlip.FlightLeft( drop, rising, Gravity );
	}

	// Gravity (m/s^2) the landing prediction assumes.
	private const float Gravity = 9.8f;

	// Farthest (inches) below the board the ground is looked for.
	private const float GroundReach = 2000f;

	private void ClearHelperIk()
	{
		_animation.IkLeftFoot = null;
		_animation.IkRightFoot = null;
		_animation.IkRightHand = null;
	}

	private static string TrickName( SkateTrick trick ) => trick switch
	{
		SkateTrick.Ollie => "ollie",
		SkateTrick.Nollie => "nollie",
		SkateTrick.Kickflip => "kickflip",
		SkateTrick.Heelflip => "heelflip",
		SkateTrick.Shuvit => "shuvit",
		SkateTrick.VarialKickflip => "varialkickflip",
		SkateTrick.VarialHeelflip => "varialheelflip",
		SkateTrick.Hardflip => "hardflip",
		SkateTrick.InwardHeelflip => "inwardheelflip",
		SkateTrick.Treflip => "treflip",
		SkateTrick.Laserflip => "laserflip",
		SkateTrick.FrontsideShuvit => "shuvit-frontside",
		SkateTrick.Shuvit360 => "shuvit360",
		SkateTrick.FrontsideShuvit360 => "shuvit360-frontside",
		SkateTrick.Hardflip360 => "hardflip360",
		SkateTrick.InwardHeelflip360 => "inwardheelflip360",
		SkateTrick.Plant => "plant",
		_ => null
	};

	private static string GrabName( SkateAirGrab grab ) => grab switch
	{
		SkateAirGrab.Nose => "nosegrab",
		SkateAirGrab.Tail => "tailgrab",
		SkateAirGrab.Mute => "mutegrab",
		SkateAirGrab.Stale => "stalegrab",
		SkateAirGrab.Seatbelt => "seatbeltgrab",
		SkateAirGrab.Crail => "crailgrab",
		SkateAirGrab.Melon => "melon",
		SkateAirGrab.Method => "method",
		SkateAirGrab.Japan => "japan",
		SkateAirGrab.Stiffy => "stiffy",
		SkateAirGrab.Indy => "indy",
		SkateAirGrab.NoseCrail => "nosecrail",
		SkateAirGrab.RocketAir => "rocketair",
		_ => null
	};

	// Frontside when the rail was on the rider's toe side as the board came onto it, decided as the grind starts.
	private bool GrindFrontside( SkateState board, SkateGrind grind )
	{
		if ( grind == SkateGrind.None )
		{
			_grindSide = null;
			_grindToesRight = null;
			return true;
		}
		if ( _grindSide is null )
		{
			var toes = _clips?.ToeSide( BoardShown ) ?? 0f;
			_grindToesRight = toes == 0f ? null : toes > 0f;
			_grindSide = toes == 0f || board.GrindRailOnRight == toes > 0f;
		}
		return _grindSide.Value;
	}

	private bool? _grindSide;
	private bool? _grindToesRight;

	// A rail lost for a moment (a truck bouncing on it) is still the same grind: its name and sides hold for this long, so
	// the graph does not drop out of the grind for a frame and pick its name afresh.
	private const float RailLossHold = 0.15f;
	private SkateGrind _heldGrind;
	private float _offRail = 1f;

	private SkateGrind HeldGrind( SkateGrind grind, float delta )
	{
		if ( grind != SkateGrind.None )
		{
			_heldGrind = grind;
			_offRail = 0f;
		}
		// Popped off it, the grind is over at once.
		else if ( _sincePop < RailLossHold || (_offRail += delta) >= RailLossHold )
		{
			_heldGrind = SkateGrind.None;
		}
		return _heldGrind;
	}

	// Whether the nose points toward the rider's heels across the rail (the heels are on the deck's right when the toes
	// are not), or null when the toe side is unknown.
	private bool? GrindNoseToHeels( SkateState board, SkateGrind grind ) =>
		grind == SkateGrind.None || _grindToesRight is not { } toesRight ? null : board.GrindNoseRight != toesRight;

	private static string GrindName( SkateGrind grind ) => grind switch
	{
		SkateGrind.FiftyFifty => "fiftyfifty",
		SkateGrind.FiveO => "fivezero",
		SkateGrind.Nosegrind => "nosegrind",
		SkateGrind.Boardslide => "boardslide",
		SkateGrind.Noseslide => "noseslide",
		SkateGrind.Tailslide => "tailslide",
		_ => null
	};

	internal void BeginInteraction()
	{
		_renderer.TryGetBoneTransform( "hand_R", out _handStart );
		_renderer.TryGetBoneTransform( "ankle_L", out _leftStart );
		_renderer.TryGetBoneTransform( "ankle_R", out _rightStart );
		_visualStart = _visual.WorldRotation;
	}

	internal void UpdateInteraction( RiderInteraction interaction, bool goofy )
	{
		var progress = interaction.Progress;
		var mounting = interaction.Action == RiderAction.Mount;
		if ( mounting || interaction.Action == RiderAction.Dismount )
		{
			var frame = new Transform( interaction.StepDestination, interaction.StartRotation );
			var left = mounting ? (goofy ? _rear : _front).WorldTransform : frame.ToWorld( _leftAnkle );
			var right = mounting ? (goofy ? _front : _rear).WorldTransform : frame.ToWorld( _rightAnkle );
			StepFoot( _stepLeft, _leftStart, left, progress / 0.7f );
			StepFoot( _stepRight, _rightStart, right, (progress - 0.25f) / 0.75f );
			_animation.IkLeftFoot = _stepLeft;
			_animation.IkRightFoot = _stepRight;
			var stance = mounting ? Rotation.FromYaw( goofy ? 90f : -90f ) : Rotation.Identity;
			_visual.WorldRotation = Rotation.Slerp( _visualStart, interaction.StartRotation * stance, RiderInteraction.Ease( progress ) );
			_animation.DuckLevel = mounting ? 0.25f * RiderInteraction.Ease( progress ) : 0.25f * (1f - RiderInteraction.Ease( progress ));
			return;
		}
		var pickup = interaction.Action == RiderAction.PickUp;
		var reach = pickup ? RiderInteraction.Ease( progress / 0.4f ) : 1f;
		_reach.WorldPosition = Vector3.Lerp( _handStart.Position, interaction.ReachPosition, reach );
		_reach.WorldRotation = Rotation.Slerp( _handStart.Rotation, _carry.WorldRotation, reach );
		_animation.IkRightHand = _reach;
		_animation.DuckLevel = pickup ? MathF.Sin( MathF.PI * progress ) * 0.95f : MathF.Sin( MathF.PI * progress ) * 0.4f;
		_animation.WithLook( interaction.ReachPosition - _visual.WorldPosition - Vector3.Up * 50f, 0.7f, 0.5f, 0.3f );
	}

	private static void StepFoot( GameObject target, Transform from, Transform to, float progress )
	{
		var blend = RiderInteraction.Ease( progress );
		target.WorldPosition = Vector3.Lerp( from.Position, to.Position, blend ) + Vector3.Up * (MathF.Sin( MathF.PI * blend ) * 4f);
		target.WorldRotation = Rotation.Slerp( from.Rotation, to.Rotation, blend );
	}

	internal void Update( SkateRiderMode mode, SkateState board, Vector3 velocity, bool grounded, bool goofy )
	{
		if ( mode == SkateRiderMode.Bailed ) return;
		_visual.Enabled = true;
		var riding = mode == SkateRiderMode.Riding;
		_visual.LocalRotation = Rotation.FromYaw( riding ? (goofy ? 90f : -90f) : 0f );
		UpdateDefaultFeet( goofy );
		_animation.WithVelocity( riding ? Vector3.Zero : velocity );
		_animation.WithWishVelocity( riding ? Vector3.Zero : velocity );
		_animation.IsGrounded = riding ? !board.IsAirborne : grounded;
		_animation.DuckLevel = riding ? 0.25f : 0f;
		_animation.IkLeftFoot = riding && !board.IsAirborne ? (goofy ? _rear : _front) : null;
		_animation.IkRightFoot = riding && !board.IsAirborne ? (goofy ? _front : _rear) : null;
		_animation.IkRightHand = mode == SkateRiderMode.Carrying ? _carry : null;
	}

	internal void Bail( Vector3 velocity, Vector3 angularVelocity, Vector3 origin, float mass, float gravity )
	{
		_visual.Enabled = true;
		_animation.IkLeftFoot = null;
		_animation.IkRightFoot = null;
		_animation.IkRightHand = null;
		_renderer.ClearIk( "foot_left" );
		_renderer.ClearIk( "foot_right" );
		_renderer.ClearIk( "hand_right" );
		_animation.Enabled = false;
		// The ragdoll takes up the pose the clips last showed; their bone overrides go only once it has, or it would
		// start from the model's bind pose.
		_ragdoll = new( _renderer, velocity, angularVelocity, origin, mass, gravity, _clips?.BoneName( "hips" ) ?? "pelvis", _board );
		_clips?.Clear();
	}

	/// <summary>Where the rider's head is shown this frame, or null when the model has no head bone.</summary>
	internal Vector3? HeadPosition => Bone( "head", out var head ) ? head.Position : null;

	// World transform of a humanoid role on whatever model the rider uses.
	private bool Bone( string role, out Transform transform )
	{
		transform = default;
		var name = _clips?.BoneName( role );
		return name is not null && _renderer.TryGetBoneTransform( name, out transform );
	}

	/// <summary>Get-up motion for how the ragdoll lies: on its back, front or a side.</summary>
	private string GetUpAction()
	{
		if ( !Bone( "hips", out var pelvis ) || !Bone( "head", out var head )
			|| !Bone( "left_upleg", out var left ) || !Bone( "right_upleg", out var right ) )
			return "recovery-onbacksprawl";
		var across = (left.Position - right.Position).Normal;
		var along = (head.Position - pelvis.Position).Normal;
		var chest = Vector3.Cross( across, along ).Normal;
		if ( chest.z > 0.5f ) return "recovery-onbacksprawl";
		if ( chest.z < -0.5f ) return "recovery-onfront";
		return across.z > 0f ? "recovery-onright" : "recovery-onleft";
	}

	internal void Recover( GameObject rider )
	{
		var getUp = _clips is null ? null : GetUpAction();
		_ragdoll?.Destroy();
		_ragdoll = null;
		_visual.SetParent( rider, false );
		_visual.LocalPosition = Vector3.Zero;
		_visual.LocalRotation = Rotation.Identity;
		_animation.Enabled = true;
		if ( getUp is not null )
			_clips.Request( new Dictionary<string, string> { [ "family" ] = "wipeout", [ "action" ] = getUp, [ "phase" ] = "once" }, 1.6f );
	}

	private void UpdateDefaultFeet( bool goofy )
	{
		var stance = Rotation.FromYaw( goofy ? 90f : -90f );
		if ( _ownsFront ) SetDefaultFoot( _front, goofy ? _rightAnkle : _leftAnkle, stance, 8f );
		if ( _ownsRear ) SetDefaultFoot( _rear, goofy ? _leftAnkle : _rightAnkle, stance, -8f );
	}

	private static void SetDefaultFoot( GameObject target, Transform ankle, Rotation stance, float alongDeck )
	{
		// Citizen's IK end effector is the ankle bone, not a sole-aligned ground frame.
		target.LocalPosition = new Vector3( alongDeck, 0f, 1f + ankle.Position.z );
		target.LocalRotation = stance * ankle.Rotation;
	}

	internal void Destroy()
	{
		_ragdoll?.Destroy();
		if ( _ownsFront && _front.IsValid() ) _front.Destroy();
		if ( _ownsRear && _rear.IsValid() ) _rear.Destroy();
		if ( _carry.IsValid() ) _carry.Destroy();
		if ( _reach.IsValid() ) _reach.Destroy();
		if ( _stepLeft.IsValid() ) _stepLeft.Destroy();
		if ( _stepRight.IsValid() ) _stepRight.Destroy();
		if ( _visual.IsValid() ) _visual.Destroy();
	}

	private static GameObject Target( GameObject parent, string name, Vector3 position )
	{
		var target = new GameObject( parent, true, name );
		target.LocalPosition = position;
		target.LocalRotation = Rotation.FromYaw( -90f );
		return target;
	}
}
