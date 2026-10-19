using SkateMotion.Core.Collision;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Converts a predicted edge displacement into a limited velocity correction.</summary>
internal sealed class GrindContactCorrection
{
	private readonly float _sampleInterval;

	internal GrindContactCorrection( float sampleInterval )
	{
		if ( !float.IsFinite( sampleInterval ) || sampleInterval <= 0f )
			throw new ArgumentOutOfRangeException( nameof( sampleInterval ) );
		_sampleInterval = sampleInterval;
	}

	/// <summary>Nonpositive sample indices preserve velocity; XYZ limits the correction applied to all four lanes.</summary>
	internal Vector4 Apply( Vector4 velocity, Vector4 displacement, int sampleIndex, float maximumSpeed )
	{
		if ( sampleIndex <= 0 )
			return velocity;
		var duration = CollisionScalarMath.Narrow( CollisionScalarMath.Product(
			(float)sampleIndex, CollisionScalarMath.Widen( _sampleInterval ) ) );
		var inverse = Reciprocal( duration );
		var correction = new Vector4(
			MultiplyAdd( inverse, displacement.X, 0f ),
			MultiplyAdd( inverse, displacement.Y, 0f ),
			MultiplyAdd( inverse, displacement.Z, 0f ),
			MultiplyAdd( inverse, displacement.W, 0f ) );
		var spatial = new Vector3( correction.X, correction.Y, correction.Z );
		var squared = Dot( spatial, spatial );
		var limit = CollisionScalarMath.Widen( maximumSpeed );
		var scale = 1f;
		// Keep the strict squared comparison and the signed limit; clamping the setting changes behavior.
		if ( CollisionScalarMath.Widen( squared ) > CollisionScalarMath.Product( limit, limit ) )
			scale = CollisionScalarMath.Narrow( CollisionScalarMath.Divide(
				limit, CollisionScalarMath.Widen( SquareRoot( squared ) ) ) );
		return new Vector4(
			MultiplyAdd( correction.X, scale, velocity.X ),
			MultiplyAdd( correction.Y, scale, velocity.Y ),
			MultiplyAdd( correction.Z, scale, velocity.Z ),
			MultiplyAdd( correction.W, scale, velocity.W ) );
	}
}
