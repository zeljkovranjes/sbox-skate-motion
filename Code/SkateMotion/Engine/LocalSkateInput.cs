using Sandbox;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>
/// Reads local keyboard and mouse into board input, merged with a gamepad's sticks and buttons for the board-only
/// controls (DeviceInputMerge: neither device locks the other out). On keyboard the
/// steering keys ramp the trucks in and out instead of jumping to full lock, and holding the flick button turns the
/// mouse into the trick stick: pull down to crouch, flick up (or up and to a side) to pop. Holding Jump and releasing
/// it pops an ollie, with the flip keys held for a kickflip or heelflip. Holding push and brake together crouches for a
/// hippy jump, and letting both go jumps. In the air, push or brake while grabbing leans into a front or back flip.
/// </summary>
internal sealed class LocalSkateInput
{
	private bool _charging;
	private float _release;
	private float _releaseX;
	private readonly KeyRamp _steer = new( SteerIn, SteerOut );
	private Vector2 _flick;

	// Seconds for a steering key to reach full lock, and for released steering to centre.
	private const float SteerIn = 0.35f;
	private const float SteerOut = 0.15f;

	// Mouse travel, in pixels, that moves the trick stick from centre to its edge.
	private const float FlickTravel = 90f;

	/// <summary>True while the mouse drives the trick stick (and so not the camera).</summary>
	internal static bool Flicking
	{
		get
		{
			using var scope = Input.PlayerScope( Input.PlayerIndex.KeyboardAndMouse );
			return Input.Down( "SkateFlick" );
		}
	}

	/// <summary>The keyboard and mouse together with the first controller (the board-only controls).</summary>
	internal SkateInput Read( bool goofy, float delta )
	{
		var raw = LocalControllerInput.Read();
		var pad = DeviceInputMerge.Settle( raw );
		var move = DeviceMerge.KeyboardMove( raw );
		return DeviceMerge.Board( ReadKeys( goofy, delta, move ), DeviceMerge.SteeringKey( move ), Pad( pad, goofy ),
			DeviceInputMerge.Activity( pad ) );
	}

	/// <summary>
	/// A controller read without a controller profile: the sticks steer and flick, and its own presses of the skate
	/// actions push, brake, slide and pump. pad is a settled reading.
	/// </summary>
	internal static SkateInput Pad( ControllerReading pad, bool goofy )
	{
		using var scope = Input.PlayerScope( Input.PlayerIndex.Controller1 );
		return new SkateInput( -pad.LeftX, Input.Down( "SkatePush" ) ? 1f : 0f, Input.Down( "SkateBrake" ) ? 1f : 0f,
			pad.RightX, pad.RightY, Slide: Input.Down( "SkateSlide" ), Goofy: goofy, Pump: Input.Down( "SkatePump" ),
			Gesture: Input.Down( "SkateGesture" ) );
	}

	/// <summary>
	/// The keyboard and mouse alone, whatever device the engine counts as last. move is the movement keys' share of
	/// the movement input (DeviceMerge.KeyboardMove).
	/// </summary>
	internal SkateInput ReadKeys( bool goofy, float delta, Vector3 move )
	{
		using var scope = Input.PlayerScope( Input.PlayerIndex.KeyboardAndMouse );
		var steer = _steer.Step( move.y, delta );
		_steerKey = move.y;
		var (flickX, flickY) = Keys( delta );
		if ( Flicking )
		{
			_flick += new Vector2( Input.MouseDelta.x, -Input.MouseDelta.y ) / FlickTravel;
			if ( _flick.Length > 1f )
				_flick = _flick.Normal;
			flickX = _flick.x;
			flickY = _flick.y;
		}
		else
		{
			_flick = Vector2.Zero;
		}
		return Board( steer, flickX, flickY, goofy, delta );
	}

	// Jump held crouches; its release pops for a moment, sideways while a flip key is held. Manual keys tilt.
	private (float X, float Y) Keys( float delta )
	{
		var charging = Input.Down( "Jump" );
		if ( _charging && !charging )
		{
			_release = 0.06f;
			_releaseX = (Input.Down( "SkateFlipRight" ) ? 1f : 0f) - (Input.Down( "SkateFlipLeft" ) ? 1f : 0f);
		}
		_charging = charging;
		// A flip key still held after the pop keeps the flip turning, as a stick held at the end of its flick does.
		var held = (Input.Down( "SkateFlipRight" ) ? 1f : 0f) - (Input.Down( "SkateFlipLeft" ) ? 1f : 0f);
		var flickX = _release > 0f ? _releaseX : charging ? 0f : held;
		var flickY = charging ? -1f : _release > 0f ? 1f : 0f;
		if ( !charging && _release <= 0f )
			flickY = (Input.Down( "SkateNoseManual" ) ? 0.45f : 0f) - (Input.Down( "SkateManual" ) ? 0.45f : 0f);
		_release = Math.Max( 0f, _release - delta );
		return (flickX, flickY);
	}

	private SkateInput Board( float steer, float flickX, float flickY, bool goofy, float delta )
	{
		var push = Input.Down( "SkatePush" );
		var brake = Input.Down( "SkateBrake" );
		// Both held is the hippy jump's crouch, which neither pushes nor brakes. It stays held until both are let go, so
		// letting one go a moment before the other still jumps.
		_hippy = push && brake || _hippy && (push || brake);
		var front = Input.Down( "SkateGrabFront" );
		var back = Input.Down( "SkateGrabBack" );
		// The grab is picked from the hands and the trick stick: the mouse stick while flicking, otherwise the steering
		// keys for the toe or heel side and the manual keys for the tail (pulled back) or nose (pushed forward).
		var grab = AirGrabs.From( _grab.Step( front, back, Flicking ? flickX : -_steerKey, flickY, goofy, delta ) );
		var lean = grab == SkateAirGrab.None ? 0f : (push ? 1f : 0f) - (brake ? 1f : 0f);
		return new( steer, push && !_hippy ? 1f : 0f, brake && !_hippy ? 1f : 0f, flickX, flickY, lean,
			Slide: Input.Down( "SkateSlide" ), Goofy: goofy, Pump: Input.Down( "SkatePump" ), Grab: grab,
			Gesture: Input.Down( "SkateGesture" ) )
		{
			// Both hands on the board with both feet off is a coffin, not a hippy jump.
			HippyJump = _hippy && !(front && back),
			Catch = Input.Down( "SkateCatch" ),
			// The slide key in the air takes the back foot off the board.
			FeetOff = Input.Down( "SkateSlide" ) ? (goofy ? SkatePushFeet.Left : SkatePushFeet.Right) : SkatePushFeet.None,
			HandPlant = Input.Down( "SkateHandPlant" )
		};
	}

	private bool _hippy;
	private readonly GrabSelection _grab = new();

	// The steering keys as pressed, unramped: picking a grab's side reads them at once.
	private float _steerKey;
}
