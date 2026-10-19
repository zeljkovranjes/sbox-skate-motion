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
	private float _sincePop = 10f;
	private float _sinceLanding = 10f;
	private float _rebound;
	private float _speedShown;
	private float _verticalShown;

	// How fast (per second) board-derived animation inputs follow the board.
	private const float InputSmoothing = 15f;

	// Shortly after a landing a rocking board can lift every wheel for a moment; that only counts as leaving the
	// ground again once it lasts, unless the rider popped again.
	private const float ReboundWindow = 0.35f;
	private const float ReboundHold = 0.1f;
	private float _landingSpeed;
	private float _windup;
	private bool _highPop;
	private Rotation _rootRotation = Rotation.Identity;
	private SkateInput _lastInput;
	private SkateTrick _landedTrick;
	private string _plant;
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
	internal float? FootDeckGap => _clips?.FootDeckGap;
	internal float? HandDeckGap( Transform board ) => _clips?.HandDeckGap( board );

	/// <summary>Whether a foot is planted on the ground (pushing) in the animation, or null without clips.</summary>
	internal bool? PushContact => _clips?.FootOnGround;
	internal BoardRetrievalProfile RetrievalProfile => _clips?.RetrievalProfile;
	internal bool RetrieveRequested => _clips?.RetrieveRequested ?? false;
	internal GameObject Visual => _visual;

	/// <summary>Smooths the rider's fixed-step movement for the animation root.</summary>
	internal StepSmoother Smoother { get; } = new();

	// The board's physical pose as shown this frame.
	private Transform BoardShown => (_display ??= _board.Components.GetAll<Component>( FindMode.EverythingInSelf )
		.OfType<IBoardDisplay>().FirstOrDefault())?.RenderTransform ?? _board.WorldTransform;

	private IBoardDisplay _display;

	internal RiderAnimator( GameObject rider, GameObject board, Model model, GameObject front, GameObject rear )
	{
		_rider = rider;
		_board = board;
		_visual = new GameObject( rider, true, "Rider Model" );
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

	internal IReadOnlyList<string> Actions( string family, string style ) =>
		_clips?.Actions( family, style ) ?? System.Array.Empty<string>();

	internal void OnTrick( SkateTrick trick )
	{
		_trick = trick;
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
		_landingKind = LandingKind( BoardShown.Rotation, _lastVelocity, _takeoffYaw );
		_trick = SkateTrick.None;
		_lateTrick = null;
		_sinceLanding = 0f;
		_landingSpeed = speed;
	}

	/// <summary>Plays clips for this frame; returns false when the built-in animation should run instead.</summary>
	internal bool UpdateClips( RiderActivity activity, SkateState board, SkateInput input, Vector3 velocity, bool goofy,
		string style, float delta, string objectMotion = null, string objectHeight = null, bool skitching = false,
		float moveAmount = 0f, bool holdingBoard = false, HippyJump hippy = null )
	{
		_hippyJump = hippy;
		_sincePop += delta;
		_sinceLanding += delta;
		_lastInput = input;
		_rebound = board.IsAirborne ? _rebound + delta : 0f;
		var airborne = board.IsAirborne
			&& (_sinceLanding > ReboundWindow || _rebound > ReboundHold || _sincePop < _sinceLanding);
		_windup = input.FlickY < -0.6f && !airborne ? _windup + delta : 0f;
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
		var timeToLand = board.IsAirborne ? TimeToLand( boardShown.Position, velocity.z / SkateUnits.InchesPerMetre ) : 0f;
		var trick = landed ? _landedTrick : _trick;
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
			Windup = input.FlickY < -0.6f && !airborne ? -input.FlickY : 0f,
			Balance = board.Balance == SkateBalanceMode.Manual ? 1 : board.Balance == SkateBalanceMode.NoseManual ? 2 : 0,
			Grind = GrindName( board.Grind ),
			Trick = TrickName( trick ),
			TrickNollie = trick == SkateTrick.Nollie,
			TrickFlips = Flips( trick ),
			HighPop = _highPop,
			// A grab still held as the board comes down lets go in time for its release to bring the board back underfoot.
			Grab = airborne && timeToLand > 0f && timeToLand < GrabRelease ? null : GrabName( input.Grab ),
			TimeToLand = timeToLand,
			Plant = _plant,
			LateTrick = _lateTrick,
			CatchStyle = landed ? _catchStyle : null,
			// Grabbing with brake held plants a hand, unless it is leaning back into a body flip.
			Handplant = airborne && input.Brake > 0.5f && input.Grab != SkateAirGrab.None && board.BodyFlip == 0
				? HandplantName( input.Grab ) : null,
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
			TimeToLandOnBoard = hippy?.TimeToLand ?? 0f
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
		if ( input.FlickY < -0.65f )
			_lateArmed = true;
		else if ( _lateArmed && input.FlickY > 0.35f )
		{
			_lateArmed = false;
			_lateTrick = input.FlickX < -0.3f ? "kickflip" : input.FlickX > 0.3f ? "heelflip" : _highPop ? "shuvit-high" : "shuvit-low";
			_lateTime = 0.6f;
		}
	}

	private static bool Flips( SkateTrick trick ) => trick is not (SkateTrick.None or SkateTrick.Ollie or SkateTrick.Nollie);

	private static string PlantName( SkateAirGrab grab ) => grab switch
	{
		SkateAirGrab.None => null,
		SkateAirGrab.Crail => "nocomply",
		SkateAirGrab.Tail or SkateAirGrab.Stale or SkateAirGrab.Method => "fastplant",
		_ => "boneless"
	};

	private static string HandplantName( SkateAirGrab grab ) => grab switch
	{
		SkateAirGrab.Nose => "nosebone",
		SkateAirGrab.Tail => "tailbone",
		SkateAirGrab.Mute or SkateAirGrab.Method => "method",
		SkateAirGrab.Stale or SkateAirGrab.Stiffy => "stiffy",
		SkateAirGrab.Melon => "judo",
		SkateAirGrab.Japan => "crossbone",
		SkateAirGrab.Seatbelt => "tuck-knee",
		_ => ""
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
			_rootRotation = shown.Rotation;
			return new Transform( shown.Position, _rootRotation );
		}
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
		return new Transform( boardShown.Position + flight, _rootRotation );
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
		_ => null
	};

	private static string GrindName( SkateGrind grind ) => grind switch
	{
		SkateGrind.FiftyFifty => "fiftyfifty",
		SkateGrind.FiveO => "fivezero",
		SkateGrind.Nosegrind => "nosegrind",
		SkateGrind.Boardslide => "boardslide",
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
		_clips?.Clear();
		_animation.IkLeftFoot = null;
		_animation.IkRightFoot = null;
		_animation.IkRightHand = null;
		_renderer.ClearIk( "foot_left" );
		_renderer.ClearIk( "foot_right" );
		_renderer.ClearIk( "hand_right" );
		_animation.Enabled = false;
		_ragdoll = new( _renderer, velocity, angularVelocity, origin, mass, gravity, _clips?.BoneName( "hips" ) ?? "pelvis", _board );
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
