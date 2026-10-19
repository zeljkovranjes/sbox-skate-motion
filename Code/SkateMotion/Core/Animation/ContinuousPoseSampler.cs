namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;
using static LimbSolverMath;

/// <summary>Samples adjacent unblocked keys using the container's ordered float32 arithmetic.</summary>
internal static class ContinuousPoseSampler
{
	internal static float Weight( float fraction )
	{
		var packed = (uint)( fraction * 65535f );
		return (float)( ( packed << 16 ) | packed ) * ( 1f / 2147483648f ) * .5f;
	}

	internal static AnimationBoneTransform Blend( in AnimationBoneTransform first,
		in AnimationBoneTransform second, float weight )
	{
		var position = BlendVector( first.Position, second.Position, weight );
		position.W = first.Position.W;
		var scale = BlendVector( first.Scale, second.Scale, weight );
		scale.W = 1f;
		return new AnimationBoneTransform( position, BlendRotation( first.Rotation, second.Rotation, weight ), scale );
	}

	private static Vector4 BlendVector( Vector4 first, Vector4 second, float weight )
	{
		var inverse = 1f - weight;
		return new Vector4(
			MultiplyAdd( first.X, inverse, MultiplyAdd( second.X, weight, 0f ) ),
			MultiplyAdd( first.Y, inverse, MultiplyAdd( second.Y, weight, 0f ) ),
			MultiplyAdd( first.Z, inverse, MultiplyAdd( second.Z, weight, 0f ) ),
			1f );
	}

	private static Quaternion BlendRotation( Quaternion first, Quaternion second, float weight )
	{
		var sameSign = Dot( first, second ) > 0f;
		var mixed = sameSign
			? new Quaternion(
				MultiplyAdd( first.X - second.X, -weight, first.X ),
				MultiplyAdd( first.Y - second.Y, -weight, first.Y ),
				MultiplyAdd( first.Z - second.Z, -weight, first.Z ),
				MultiplyAdd( first.W - second.W, -weight, first.W ) )
			: new Quaternion(
				MultiplyAdd( first.X + second.X, -weight, first.X ),
				MultiplyAdd( first.Y + second.Y, -weight, first.Y ),
				MultiplyAdd( first.Z + second.Z, -weight, first.Z ),
				MultiplyAdd( first.W + second.W, -weight, first.W ) );
		var squared = Dot( mixed, mixed );
		var inverse = 1f / MathF.Sqrt( squared );
		var correction = SubtractProduct( squared, MultiplyAdd( inverse, inverse, 0f ), 1f );
		inverse = MultiplyAdd( correction, MultiplyAdd( inverse, .5f, 0f ), inverse );
		return new Quaternion( MultiplyAdd( mixed.X, inverse, 0f ), MultiplyAdd( mixed.Y, inverse, 0f ),
			MultiplyAdd( mixed.Z, inverse, 0f ), MultiplyAdd( mixed.W, inverse, 0f ) );
	}

	private static float Dot( Quaternion first, Quaternion second )
	{
		// The container axis permutation makes the ordered reduction W, X, Z, Y.
		var result = MultiplyAdd( first.W, second.W, 0f );
		result = MultiplyAdd( first.X, second.X, result );
		result = MultiplyAdd( first.Z, second.Z, result );
		return MultiplyAdd( first.Y, second.Y, result );
	}
}
