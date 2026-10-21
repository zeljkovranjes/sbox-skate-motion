namespace SkateMotion.Core.Camera;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The heading a riding camera settles behind: the way the board travels, not the way it faces, so riding fakie,
/// reverts, powerslides and flips never swing the view. Below walking pace the last heading is held (blended in by
/// speed, so starting off never jumps); after a moment at rest it lines up with the nearer end of the board. In the
/// air it holds the take-off heading and blends back to the travel after landing.
/// </summary>
internal sealed class TravelHeading
{
	// Speeds (m/s) between which the travel direction takes over from the held heading.
	private const float HoldSpeed = 0.3f;
	private const float TravelSpeed = 1f;

	// Seconds at rest before lining up with the board, and how fast (1/s) it lines up then.
	private const float LineupDelay = 1f;
	private const float LineupRate = 2f;

	// Seconds after landing to blend from the take-off heading back to the travel.
	private const float LandingBlend = 0.5f;

	private float _held;
	private float _rest;
	private float _airHeading;
	private float _sinceLanding = float.PositiveInfinity;
	private bool _airborne;

	/// <summary>The current target heading in radians.</summary>
	internal float Value { get; private set; }

	/// <summary>Starts again at a heading (a teleport, or getting on the board).</summary>
	internal void Reset( float heading )
	{
		Value = _held = _airHeading = CameraMath.Wrap( heading );
		_rest = 0f;
		_sinceLanding = float.PositiveInfinity;
		_airborne = false;
	}

	/// <summary>Updates from the board's velocity (m/s), its facing yaw and whether it is in the air.</summary>
	internal float Update( Vector3 velocity, float facing, bool airborne, float delta )
	{
		var live = Live( velocity, facing, delta );
		if ( airborne )
		{
			if ( !_airborne )
				_airHeading = Value;
			_airborne = true;
			Value = _airHeading;
			return Value;
		}
		if ( _airborne )
			_sinceLanding = 0f;
		_airborne = false;
		_sinceLanding += delta;
		Value = _sinceLanding < LandingBlend
			? CameraMath.Blend( _airHeading, live, CameraMath.Ease( _sinceLanding / LandingBlend ) )
			: live;
		return Value;
	}

	private float Live( Vector3 velocity, float facing, float delta )
	{
		var planar = new Vector3( velocity.X, velocity.Y, 0f );
		var speed = planar.Length();
		var weight = CameraMath.Step( HoldSpeed, TravelSpeed, speed );
		if ( weight >= 1f )
		{
			_held = CameraMath.Heading( planar );
			_rest = 0f;
			return _held;
		}
		_rest = weight > 0f ? 0f : _rest + delta;
		if ( _rest >= LineupDelay )
		{
			// Line up behind whichever end of the board is nearer the held heading: a stop while riding fakie keeps
			// the view instead of turning it round.
			_held = CameraMath.Blend( _held, CameraMath.NearerEnd( facing, _held ), 1f - MathF.Exp( -LineupRate * delta ) );
		}
		return weight > 0f ? CameraMath.Blend( _held, CameraMath.Heading( planar ), weight ) : _held;
	}
}
