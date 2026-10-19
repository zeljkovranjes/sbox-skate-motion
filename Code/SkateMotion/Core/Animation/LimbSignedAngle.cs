namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Oriented polynomial angles with distinct scalar and fractional-period wrapping.</summary>
internal sealed class LimbSignedAngle
{
	private readonly LimbSolverMath _math;
	private readonly float _minimumSquaredLength;
	private readonly float _period;
	private readonly float _inversePeriod;

	internal LimbSignedAngle( LimbSolverMath math, float minimumSquaredLength, float period, float inversePeriod )
	{
		ArgumentNullException.ThrowIfNull( math );
		if ( !float.IsFinite( minimumSquaredLength ) || minimumSquaredLength < 0f
			|| !float.IsFinite( period ) || period <= 0f || !float.IsFinite( inversePeriod ) || inversePeriod <= 0f )
			throw new ArgumentException( "Angle normalization and period parameters must be finite and valid." );
		_math = math;
		_minimumSquaredLength = minimumSquaredLength;
		_period = period;
		_inversePeriod = inversePeriod;
	}

	internal float Evaluate( Vector3 first, Vector3 second, Vector3 normal )
	{
		var a = Dot( first, first );
		var b = Dot( second, second );
		if ( !(a > _minimumSquaredLength) || !(b > _minimumSquaredLength) )
			return 0f;
		first = NormalizeOnce( first, a );
		second = NormalizeOnce( second, b );
		var angle = _math.ArcCosine( Math.Clamp( Dot( first, second ), -1f, 1f ) );
		return Dot( normal, Cross( first, second ) ) < 0f ? _period - angle : angle;
	}

	internal float Wrapped( Vector3 first, Vector3 second, Vector3 normal )
	{
		var angle = Evaluate( first, second, normal );
		if ( angle >= -_math.Pi && angle < _math.Pi )
			return angle;
		var turns = MathF.Truncate( angle * _inversePeriod );
		angle = MultiplyAdd( turns, -_period, angle );
		if ( angle >= _math.Pi )
			angle -= _period;
		else if ( angle < -_math.Pi )
			angle += _period;
		return angle;
	}

	internal float WrappedFraction( Vector3 first, Vector3 second, Vector3 normal )
	{
		var turns = MultiplyAdd( Evaluate( first, second, normal ), _inversePeriod, 0f );
		var fraction = turns - MathF.Floor( turns );
		if ( fraction > .5f )
			fraction -= 1f;
		return MultiplyAdd( fraction, _period, 0f );
	}

	private static Vector3 NormalizeOnce( Vector3 value, float squared )
	{
		var estimate = 1f / MathF.Sqrt( squared );
		var residual = SubtractProduct( squared, MultiplyAdd( estimate, estimate, 0f ), 1f );
		estimate = MultiplyAdd( MultiplyAdd( estimate, .5f, 0f ), residual, estimate );
		return Scale( value, estimate );
	}
}
