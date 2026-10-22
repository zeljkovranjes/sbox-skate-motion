namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Body flips: grabbing in the air and leaning toward the toes (a front flip) or the heels (a back flip) turns rider and
/// board over together about the board's length, at the rate that brings them round once by the landing (set when the
/// flip starts, then steered so a flip held to the end comes round exactly). The flip keeps turning while it is held,
/// and for at least <see cref="HeldAtLeast"/>; let go after that, it slows to a stop wherever it is. A flip only starts
/// with <see cref="MinimumAir"/> of flight still to come.
/// </summary>
internal sealed class BodyFlip
{
	private float _target;
	private float _time;

	/// <summary>1 for a front flip, -1 for a back flip, 0 when not flipping.</summary>
	internal int Direction { get; private set; }

	internal bool Flipping => Direction != 0;

	/// <summary>How far round (radians) the flip has turned.</summary>
	internal float Angle { get; private set; }

	/// <summary>Least flight still to come (seconds) for a flip to start.</summary>
	internal const float MinimumAir = 0.8f;

	/// <summary>Shortest time (seconds) a flip keeps turning once started, held or not.</summary>
	internal const float HeldAtLeast = 0.3f;

	// Lean (0..1) that asks for a flip, how quickly (seconds) the turn follows its target, the fastest the steering may
	// turn as a share of the starting rate, and the least time left (seconds) it steers over.
	private const float Asked = 0.5f;
	private const float Response = 0.08f;
	private const float MaximumBoost = 2f;
	private const float Settle = 0.05f;
	private const float FullTurn = 2f * MathF.PI;

	/// <summary>
	/// Change of the deck's angular velocity (world) this step. <paramref name="lean"/> is positive toward the rider's
	/// toes; <paramref name="timeToLand"/> is the flight still to come (seconds).
	/// </summary>
	internal Vector3 Step( in BoardBody body, float lean, bool grab, bool goofy, float timeToLand, float step )
	{
		var asked = grab && MathF.Abs( lean ) > Asked ? MathF.Sign( lean ) : 0;
		if ( Direction == 0 )
		{
			if ( asked == 0 || !(timeToLand >= MinimumAir) )
				return Vector3.Zero;
			Direction = asked;
			_target = FullTurn / timeToLand;
			_time = 0f;
			Angle = 0f;
		}
		_time += step;
		// The rider faces the board's right side (left when goofy): turning the deck's up toward that side is forward.
		var axis = body.Forward * ((goofy ? -1f : 1f) * Direction);
		var spin = Vector3.Dot( body.AngularVelocity, axis );
		Angle += spin * step;
		// The rest of the turn over the flight left, so a flip held to the landing comes round upright.
		var wanted = asked == Direction || _time < HeldAtLeast
			? Math.Clamp( (FullTurn - Angle) / MathF.Max( timeToLand, Settle ), 0f, _target * MaximumBoost )
			: 0f;
		return axis * ((wanted - spin) * Math.Min( 1f, step / Response ));
	}

	internal void Reset()
	{
		Direction = 0;
		Angle = 0f;
		_time = 0f;
	}

	/// <summary>Flight still to come (seconds) for a body at <paramref name="height"/> above where it left the ground,
	/// rising at <paramref name="rising"/>, coming back down to that height.</summary>
	internal static float FlightLeft( float height, float rising, float gravity ) =>
		gravity > 0f ? (rising + MathF.Sqrt( MathF.Max( 0f, rising * rising + 2f * gravity * MathF.Max( height, 0f ) ) )) / gravity : 0f;

	/// <summary>
	/// The deck height a flight comes down at: as high over the ground seen below it now as it rode over the ground it
	/// left, so off a roof or past a ramp's lip the flight is counted down to the ground under it, not only back to the
	/// height it left from. With either ground not seen, that height (<paramref name="takeoffHeight"/>).
	/// </summary>
	internal static float LandingHeight( float takeoffHeight, float? takeoffGround, float? groundBelow ) =>
		groundBelow is { } ground && takeoffGround is { } left ? ground + (takeoffHeight - left) : takeoffHeight;
}
