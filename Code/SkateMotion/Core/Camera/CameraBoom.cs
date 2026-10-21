namespace SkateMotion.Core.Camera;

/// <summary>
/// How far the camera actually sits from the focus, and how high it cranes, once the world is in the way. The
/// distance eases in toward a soft limit found with a wider probe before anything is touched, never passes the hard
/// limit (the camera is never inside anything), and eases back out once clear. When the camera's usual place is
/// blocked it also cranes up, by up to <see cref="BoardCrane"/> (or <see cref="FootCrane"/> on foot) as more of the
/// way is blocked, quickly up and slowly back down; the blockage is measured at the usual place, not the craned
/// one, so craning cannot rock back and forth.
/// </summary>
internal sealed class CameraBoom
{
	internal const float BoardCrane = 50f * CameraMath.Degrees;
	internal const float FootCrane = 60f * CameraMath.Degrees;

	// Closest the camera eases in to the focus (m) unless something forces it closer.
	private const float Closest = 0.35f;

	// Distance (m) over which a blockage counts: a camera blocked this far from its place is fully blocked.
	private const float BlockedRange = 2.5f;

	// Pulling in for something in the way is quick; following the framing and easing back out once clear is gentle.
	private static readonly SpringTuning PullIn = SpringTuning.Critical( 12f );
	private static readonly SpringTuning Follow = SpringTuning.Critical( 4f ).Limited( 4f );
	private static readonly SpringTuning CraneUp = SpringTuning.Critical( 6f );
	private static readonly SpringTuning CraneDown = SpringTuning.Critical( 1.2f );

	// Fastest (m/s) a closing soft limit is fed forward to the pull-in.
	private const float FastestClosing = 10f;

	private readonly CameraSpring _distance = new();
	private readonly CameraSpring _crane = new();
	private float _soft = float.PositiveInfinity;

	internal float Distance => _distance.Value;
	internal float Crane => _crane.Value;

	/// <summary>True when the hard limit held the camera in this frame.</summary>
	internal bool Held { get; private set; }

	internal void Reset( float distance )
	{
		_distance.Reset( distance );
		_crane.Reset( 0f );
		_soft = float.PositiveInfinity;
		Held = false;
	}

	/// <summary>
	/// Cranes for how much of the way to the usual place (at the wanted distance, uncraned) is clear: available is
	/// the clear distance there, or the wanted distance when nothing is in the way.
	/// </summary>
	internal float UpdateCrane( float wanted, float available, bool onFoot, float delta )
	{
		var range = MathF.Min( wanted, BlockedRange );
		var blocked = range > 0f ? Math.Clamp( (range - available) / range, 0f, 1f ) : 0f;
		var target = (onFoot ? FootCrane : BoardCrane) * blocked;
		return _crane.Step( target, delta, target > _crane.Value ? CraneUp : CraneDown );
	}

	/// <summary>
	/// Settles the distance for this frame: wanted is the framing's distance, soft where the wider probe meets
	/// something (or infinity), hard where the camera itself would (or infinity).
	/// </summary>
	internal float UpdateDistance( float wanted, float soft, float hard, float delta )
	{
		var target = MathF.Max( MathF.Min( wanted, soft ), MathF.Min( Closest, wanted ) );
		var pulling = soft < wanted && target < _distance.Value;
		// A limit closing in frame after frame is followed without lag, so the hard limit is rarely reached.
		var closing = float.IsFinite( _soft ) && float.IsFinite( soft ) && delta > 0f
			? Math.Clamp( (soft - _soft) / delta, -FastestClosing, 0f )
			: 0f;
		_soft = soft;
		_distance.Step( target, delta, pulling ? PullIn : Follow, pulling ? closing : 0f );
		Held = _distance.Value > hard;
		if ( Held )
			_distance.Reset( MathF.Max( hard, 0f ) );
		return _distance.Value;
	}
}
