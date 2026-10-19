using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Biases an accepted contact normal by transverse velocity and limits its tilt.</summary>
internal sealed class GrindCandidateNormal
{
	private readonly LimbSolverMath _math;
	private readonly GrindSurfaceNormal _rotation;
	private readonly float _minimumLength;
	private readonly float _minimumAxisSquared;
	private readonly float _radiansPerDegree;
	private readonly float _period;
	private readonly float _inversePeriod;

	internal GrindCandidateNormal( LimbSolverMath math, float minimumLength, float minimumAxisSquared,
		float radiansPerDegree, float period, float inversePeriod )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
		_rotation = new GrindSurfaceNormal( math );
		_minimumLength = minimumLength;
		_minimumAxisSquared = minimumAxisSquared;
		_radiansPerDegree = radiansPerDegree;
		_period = period;
		_inversePeriod = inversePeriod;
	}

	internal Vector3 Evaluate( Vector3 normal, Vector3 direction, Vector3 velocity,
		float velocityGain, float maximumTiltDegrees )
	{
		var transverse = velocity - Scale( direction, Dot( direction, velocity ) );
		var desired = Vector3.UnitY + Scale( -transverse, velocityGain );
		var normalizedNormal = NormalizeOrZero( normal, out _ );
		var normalizedDesired = NormalizeOrZero( desired, out var desiredLength );
		var axis = Cross( normalizedDesired, normalizedNormal );
		var cosine = Dot( normalizedDesired, normalizedNormal );
		// Ordered vector min/max select the second operand for an unordered comparison.
		cosine = -1f > cosine ? -1f : cosine;
		cosine = cosine < 1f ? cosine : 1f;
		var angle = _math.ArcCosine( cosine );
		var turns = Math.Floor( CollisionScalarMath.MultiplyAdd( angle, _inversePeriod, .5d ) );
		var wrapped = Math.Abs( CollisionScalarMath.MultiplyAdd( turns, -_period, angle ) );
		var limit = MultiplyAdd( _radiansPerDegree, maximumTiltDegrees, 0f );
		var axisSquared = Dot( axis, axis );
		if ( wrapped >= limit && !(_minimumAxisSquared > axisSquared) )
		{
			axis = Scale( axis, InverseSquareRoot( axisSquared ) );
			desired = Scale( _rotation.Rotate( normal, axis, -limit ), desiredLength );
		}
		var normalizedDirection = NormalizeOrZero( direction, out _ );
		var along = Dot( desired, normalizedDirection );
		// The projection test is one-sided and its subtraction uses the supplied direction.
		if ( along > 0f )
			desired -= Scale( direction, along );
		return NormalizeOrZero( desired, out _ );
	}

	internal Vector3 EdgeUp( Vector3 direction )
	{
		var up = Cross( direction, Cross( Vector3.UnitY, direction ) );
		up = Scale( up, up.Y < 0f ? -1f : 1f );
		var result = NormalizeOrZero( up, out var length );
		return length > _minimumLength ? result : Vector3.UnitY;
	}

	private Vector3 NormalizeOrZero( Vector3 value, out float length )
	{
		var squared = Dot( value, value );
		var inverse = InverseSquareRoot( squared );
		length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		return length > _minimumLength ? Scale( value, inverse ) : Vector3.Zero;
	}
}
