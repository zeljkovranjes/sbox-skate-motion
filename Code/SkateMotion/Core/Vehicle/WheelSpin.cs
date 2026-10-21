namespace SkateMotion.Core.Vehicle;

/// <summary>
/// How far each wheel has turned about its axle (radians, positive rolling forward). A wheel on the ground rolls with
/// it: its turn rate is its speed along its own rolling direction over its radius, so it rolls backwards with the
/// board, stops as the board slides across its wheels (a powerslide, a revert's swing) and turns with its truck. Off the
/// ground (in the air, on a rail, a manual's raised wheels) it spins on, slowed by the drag of a free wheel.
/// </summary>
internal sealed class WheelSpin
{
	/// <summary>Share of its spin a free wheel loses per second (per sixtieth of a second 0.006, compounded).</summary>
	internal const float FreeDrag = 0.36f;

	private readonly float[] _angle = new float[ 4 ];
	private readonly float[] _rate = new float[ 4 ];

	/// <summary>How far a wheel has turned (radians, within one turn).</summary>
	internal float Angle( int wheel ) => _angle[ wheel ];

	/// <summary>How fast a wheel turns (radians per second, positive rolling forward).</summary>
	internal float Rate( int wheel ) => _rate[ wheel ];

	/// <param name="rolling">The wheel's speed along its rolling direction (m/s, positive forward).</param>
	/// <param name="radius">The wheel's radius, metres.</param>
	/// <param name="touching">The wheel is on the ground.</param>
	internal void Update( int wheel, float rolling, float radius, bool touching, float step )
	{
		if ( !float.IsFinite( step ) || step <= 0f )
			return;
		if ( touching && float.IsFinite( rolling ) && radius > 0f )
			_rate[ wheel ] = rolling / radius;
		else
			_rate[ wheel ] *= MathF.Exp( -FreeDrag * step );
		_angle[ wheel ] = MathF.IEEERemainder( _angle[ wheel ] + _rate[ wheel ] * step, MathF.Tau );
	}

	internal void Reset()
	{
		Array.Clear( _angle );
		Array.Clear( _rate );
	}
}
