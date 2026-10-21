using Sandbox;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>Connects retained controller requests to the current playable motor.</summary>
internal sealed class RiderControllerInput
{
	private readonly ControllerActionRouter _router;
	private readonly ControllerFrameProcessor _frame;
	private readonly ControllerInputProfile _profile;
	private readonly GestureInputPipeline _gestures;
	private readonly QueuedInputCommand[] _commands;
	private static readonly InputCommandKey _push = InputCommandKey.Encode( "PUSHING"u8 );
	private static readonly InputCommandKey _leftPush = InputCommandKey.Encode( "LEFTPUSH"u8 );
	private static readonly InputCommandKey _rightPush = InputCommandKey.Encode( "RIGHTPUSH"u8 );
	private static readonly InputCommandKey _newPush = InputCommandKey.Encode( "NEWPUSH"u8 );
	private static readonly InputCommandKey _sprint = InputCommandKey.Encode( "OB_SPRINT"u8 );
	private static readonly InputCommandKey _brake = InputCommandKey.Encode( "BRAKE"u8 );
	private static readonly InputCommandKey _crouch = InputCommandKey.Encode( "CROUCH"u8 );
	private static readonly InputCommandKey _turn = InputCommandKey.Encode( "TURN"u8 );
	private static readonly InputCommandKey _leftGrab = InputCommandKey.Encode( "LEFTAIRGRAB"u8 );
	private static readonly InputCommandKey _rightGrab = InputCommandKey.Encode( "RIGHTAIRGRAB"u8 );
	private static readonly InputCommandKey _leftSlide = InputCommandKey.Encode( "LEFTSLIDESTART"u8 );
	private static readonly InputCommandKey _rightSlide = InputCommandKey.Encode( "RIGHTSLIDESTART"u8 );
	private static readonly InputCommandKey _toggle = InputCommandKey.Encode( "NEWTOGGLEOFFBOARDSTATE"u8 );
	private static readonly InputCommandKey _toggleHeld = InputCommandKey.Encode( "TOGGLEOFFBOARDSTATE"u8 );
	private static readonly InputCommandKey _jump = InputCommandKey.Encode( "OB_JUMP"u8 );
	private static readonly InputCommandKey _drop = InputCommandKey.Encode( "OB_DROPBOARD"u8 );
	private static readonly InputCommandKey _throw = InputCommandKey.Encode( "OB_THROWBOARD"u8 );
	private static readonly InputCommandKey _retrieve = InputCommandKey.Encode( "OB_RETRIEVEBOARD"u8 );
	private static readonly InputCommandKey _bail = InputCommandKey.Encode( "WIPEOUTREQUEST"u8 );
	private static readonly InputCommandKey _recover = InputCommandKey.Encode( "WIPEOUTRECOVER"u8 );
	private static readonly InputCommandKey _darkCatch = InputCommandKey.Encode( "DARKCATCH"u8 );
	private static readonly InputCommandKey _grabWorld = InputCommandKey.Encode( "GRABWORLD"u8 );
	private readonly GrabSelection _grab = new();

	// Seconds since the last update, for the grab pick's settle time.
	private float _delta;
	private static readonly InputCommandKey _walkX = InputCommandKey.Encode( "OB_BIPEDWORLDX"u8 );
	private static readonly InputCommandKey _walkY = InputCommandKey.Encode( "OB_BIPEDWORLDZ"u8 );

	internal InputRequestBuffer Requests => _frame.Requests;
	internal bool Toggle => Has( _toggle );

	/// <summary>True while the mount button is held (waiting at the lip of a drop-in).</summary>
	internal bool ToggleHeld => Has( _toggleHeld );
	internal bool Jump => Has( _jump );

	/// <summary>True while the sprint button is held on foot.</summary>
	internal bool Sprint => Has( _sprint );
	internal bool Drop => Has( _drop );

	/// <summary>True on the frame the right trigger is pulled all the way: throws the board held in the hand.</summary>
	internal bool Throw => Has( _throw );
	internal bool Retrieve => Has( _retrieve );
	internal bool Bail => Has( _bail );
	internal bool Recover => Has( _recover );
	internal Vector3 Walking => new( Value( _walkY ), -Value( _walkX ), 0f );

	internal RiderControllerInput( ControllerInputProfile profile )
	{
		_profile = profile;
		_router = profile.CreateRouter();
		_frame = new( _router );
		_gestures = profile.Gestures?.CreatePipeline( 0 );
		_commands = new QueuedInputCommand[ _gestures?.SetCount ?? 0 ];
	}

	/// <param name="reading">This frame's controller reading with drift taken out (DeviceInputMerge.Settle).</param>
	internal void Update( SkateRiderMode mode, SkateState board, bool interacting, bool goofy,
		Vector3 facing, float delta, ControllerReading reading )
	{
		_delta = delta;
		var riding = mode == SkateRiderMode.Riding;
		var bailed = mode == SkateRiderMode.Bailed;
		// These are current motor gates; complete movement-graph context binding remains separate.
		var context = new ControllerActionContext
		{
			SuppressBail = !riding || interacting,
			BailControlEnabled = bailed,
			BailGestureEnabled = bailed,
			Buttons = new( !riding || board.IsAirborne || interacting,
				false, !riding || interacting, false, interacting, false, false, goofy ),
			Analog = new( interacting, !riding || interacting, !riding || interacting, false ),
			Walking = new( new System.Numerics.Vector3( -facing.y, 0f, facing.x ), false, default,
				_profile.Walking[ 0 ], _profile.Walking[ 1 ] )
		};
		var count = 0;
		if ( _gestures is not null )
		{
			// The engine supplies one notification per rider input update.
			_gestures.Notify( reading, 0, _profile.Gestures.QualityMode, _commands, ref count );
			_router.HoldPatternPending |= _gestures.Receiver.ConsumeHold();
		}
		_frame.Update( reading, delta, context, default, _commands, ref count, null );
	}

	internal SkateInput BoardInput( bool goofy )
	{
		var reading = _router.State.Current;
		var left = Has( _leftGrab );
		var right = Has( _rightGrab );
		// The triggers are the hands: the right trigger is the front hand for a regular stance, the left one for goofy.
		// Where the right stick points as a hand reaches picks the grab.
		var grab = AirGrabs.From( _grab.Step( goofy ? left : right, goofy ? right : left, reading.RightX, reading.RightY, goofy, _delta ) );
		return new( -Value( _turn ), Has( _push ) ? 1f : 0f, Has( _brake ) ? 1f : 0f,
			// The left stick, pushed forward or pulled back while grabbing, leans into a front or back flip.
			reading.RightX, reading.RightY, grab == SkateAirGrab.None ? 0f : reading.LeftY,
			Slide: Has( _leftSlide ) || Has( _rightSlide ), Goofy: goofy, Grab: grab,
			// Crouching (left stick back or a trigger) pumps through transitions, as the pump key does.
			Pump: Has( _crouch ) )
		{
			PushFeet = (Has( _leftPush ) ? SkatePushFeet.Left : SkatePushFeet.None)
				| (Has( _rightPush ) ? SkatePushFeet.Right : SkatePushFeet.None),
			NewPush = Has( _newPush ),
			// Both push buttons together crouch for a hippy jump.
			// Both push buttons with both triggers is a coffin, not a hippy jump.
			HippyJump = Has( _leftPush ) && Has( _rightPush ) && !(left && right),
			// B (or the right bumper) is the catch; the push buttons in the air take a foot off.
			Catch = Has( _darkCatch ),
			FeetOff = (reading.Down( ControllerButtons.West ) ? SkatePushFeet.Left : SkatePushFeet.None)
				| (reading.Down( ControllerButtons.South ) ? SkatePushFeet.Right : SkatePushFeet.None),
			// The right bumper grabs the world: off a ramp's lip, a hand plant.
			HandPlant = Has( _grabWorld )
		};
	}

	private bool Has( InputCommandKey key )
	{
		for ( var index = 0; index < Requests.Count; index++ )
		{
			if ( Requests[ index ].Key == key )
				return true;
		}
		return false;
	}

	private float Value( InputCommandKey key )
	{
		for ( var index = 0; index < Requests.Count; index++ )
		{
			if ( Requests[ index ].Key == key )
				return Requests[ index ].Value;
		}
		return 0f;
	}
}
