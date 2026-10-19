namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Chooses a contact normal from ordered angular limits without normalizing the supplied axis.</summary>
internal sealed class GrindSurfaceNormal
{
	private readonly LimbSolverMath _math;

	internal GrindSurfaceNormal( LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
	}

	internal Vector3 Evaluate( Vector3 up, Vector3 direction, float firstLimit, float secondLimit )
	{
		float angle;
		if ( firstLimit >= secondLimit )
			angle = (firstLimit + secondLimit) * .5f;
		else
		{
			if ( firstLimit < _math.Pi && secondLimit > _math.Pi )
				return up;
			var firstDistance = MathF.Abs( _math.Pi - firstLimit );
			var secondDistance = MathF.Abs( _math.Pi - secondLimit );
			angle = firstDistance - secondDistance >= 0f ? secondLimit : firstLimit;
		}
		return Rotate( -up, direction, angle );
	}

	internal Vector3 Rotate( Vector3 value, Vector3 axis, float angle )
	{
		var (sine, cosine) = _math.SinCos( MultiplyAdd( angle, .5f, 0f ) );
		var quaternionVector = Scale( axis, sine );
		var mixed = AddScaled( value, cosine, Cross( quaternionVector, value ) );
		return AddScaled( Cross( quaternionVector, mixed ), 2f, value );
	}
}
