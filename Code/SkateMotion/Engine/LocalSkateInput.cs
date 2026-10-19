using Sandbox;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>
/// Reads local keyboard, mouse or gamepad into board input. A gamepad drives the sticks directly. On keyboard the
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
	internal static bool Flicking => !Input.UsingController && Input.Down( "SkateFlick" );

	internal SkateInput Read( bool goofy, float delta )
	{
		if ( Input.UsingController )
		{
			var stick = -Input.GetAnalog( InputAnalog.LeftStickX );
			return Board( stick, Input.GetAnalog( InputAnalog.RightStickX ), -Input.GetAnalog( InputAnalog.RightStickY ), goofy );
		}
		var steer = _steer.Step( Input.AnalogMove.y, delta );
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
		return Board( steer, flickX, flickY, goofy );
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
		var flickX = _release > 0f ? _releaseX : 0f;
		var flickY = charging ? -1f : _release > 0f ? 1f : 0f;
		if ( !charging && _release <= 0f )
			flickY = (Input.Down( "SkateNoseManual" ) ? 0.45f : 0f) - (Input.Down( "SkateManual" ) ? 0.45f : 0f);
		_release = Math.Max( 0f, _release - delta );
		return (flickX, flickY);
	}

	private SkateInput Board( float steer, float flickX, float flickY, bool goofy )
	{
		var push = Input.Down( "SkatePush" );
		var brake = Input.Down( "SkateBrake" );
		// Both held is the hippy jump's crouch, which neither pushes nor brakes. It stays held until both are let go, so
		// letting one go a moment before the other still jumps.
		_hippy = push && brake || _hippy && (push || brake);
		var grab = Grab( steer );
		var lean = grab == SkateAirGrab.None ? 0f : (push ? 1f : 0f) - (brake ? 1f : 0f);
		return new( steer, push && !_hippy ? 1f : 0f, brake && !_hippy ? 1f : 0f, flickX, flickY, lean,
			Slide: Input.Down( "SkateSlide" ), Goofy: goofy, Pump: Input.Down( "SkatePump" ), Grab: grab,
			Gesture: Input.Down( "SkateGesture" ) )
		{
			HippyJump = _hippy
		};
	}

	private bool _hippy;

	// Front-hand and back-hand grab actions; holding steering picks the nose or tail variant.
	private static SkateAirGrab Grab( float steer )
	{
		var front = Input.Down( "SkateGrabFront" );
		var back = Input.Down( "SkateGrabBack" );
		if ( front && back ) return SkateAirGrab.Crail;
		if ( front ) return steer > 0.5f ? SkateAirGrab.Nose : steer < -0.5f ? SkateAirGrab.Melon : SkateAirGrab.Mute;
		if ( back ) return steer > 0.5f ? SkateAirGrab.Method : steer < -0.5f ? SkateAirGrab.Tail : SkateAirGrab.Stale;
		return SkateAirGrab.None;
	}
}
