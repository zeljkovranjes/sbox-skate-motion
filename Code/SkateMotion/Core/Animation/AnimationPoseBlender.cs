namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;
using static LimbSolverMath;

/// <summary>Ordered pose interpolation with sign-corrected, normalized linear rotations.</summary>
internal static class AnimationPoseBlender
{
	internal static AnimationBoneTransform Blend( in AnimationBoneTransform first,
		in AnimationBoneTransform second, float weight ) => new(
		BlendVector( first.Position, second.Position, weight ),
		BlendRotation( first.Rotation, second.Rotation, weight ),
		BlendVector( first.Scale, second.Scale, weight ) );

	internal static void Blend( ReadOnlySpan<AnimationBoneTransform> first,
		ReadOnlySpan<AnimationBoneTransform> second, float weight, Span<AnimationBoneTransform> output )
	{
		if ( second.Length != first.Length || output.Length < first.Length )
			throw new ArgumentException( "Pose buffers need matching input lengths and enough output space." );
		for ( var index = 0; index < first.Length; index++ )
			output[ index ] = Blend( first[ index ], second[ index ], weight );
	}

	internal static AnimationBoneTransform BlendWeighted( in AnimationBoneTransform first,
		in AnimationBoneTransform second, float weight, bool maskFromFirst ) =>
		BlendWithMask( first, second, weight, maskFromFirst ? first.Scale.W : second.Scale.W );

	/// <summary>Blends using explicit influence metadata without occupying a transform lane.</summary>
	internal static AnimationBoneTransform BlendWithMask( in AnimationBoneTransform first,
		in AnimationBoneTransform second, float weight, float mask )
	{
		var scaled = MultiplyAdd( weight, mask, 0f );
		var upper = scaled > 1f ? 1f : scaled;
		var bounded = 0f > upper ? 0f : upper;
		return Blend( first, second, bounded );
	}

	internal static void BlendWeighted( ReadOnlySpan<AnimationBoneTransform> first,
		ReadOnlySpan<AnimationBoneTransform> second, float weight, bool maskFromFirst,
		Span<AnimationBoneTransform> output )
	{
		if ( second.Length != first.Length || output.Length < first.Length )
			throw new ArgumentException( "Pose buffers need matching input lengths and enough output space." );
		for ( var index = 0; index < first.Length; index++ )
			output[ index ] = BlendWeighted( first[ index ], second[ index ], weight, maskFromFirst );
	}

	internal static Vector4 BlendVector( Vector4 first, Vector4 second, float weight ) => new(
		MultiplyAdd( second.X - first.X, weight, first.X ),
		MultiplyAdd( second.Y - first.Y, weight, first.Y ),
		MultiplyAdd( second.Z - first.Z, weight, first.Z ),
		MultiplyAdd( second.W - first.W, weight, first.W ) );

	internal static Quaternion BlendRotation( Quaternion first, Quaternion second, float weight )
	{
		var sameSign = Dot( first, second ) > 0f;
		var mixed = sameSign
			? new Quaternion(
				MultiplyAdd( second.X - first.X, weight, first.X ),
				MultiplyAdd( second.Y - first.Y, weight, first.Y ),
				MultiplyAdd( second.Z - first.Z, weight, first.Z ),
				MultiplyAdd( second.W - first.W, weight, first.W ) )
			: new Quaternion(
				SubtractProduct( second.X + first.X, weight, first.X ),
				SubtractProduct( second.Y + first.Y, weight, first.Y ),
				SubtractProduct( second.Z + first.Z, weight, first.Z ),
				SubtractProduct( second.W + first.W, weight, first.W ) );
		var inverse = InverseSquareRoot( Dot( mixed, mixed ) );
		return new Quaternion( MultiplyAdd( mixed.X, inverse, 0f ), MultiplyAdd( mixed.Y, inverse, 0f ),
			MultiplyAdd( mixed.Z, inverse, 0f ), MultiplyAdd( mixed.W, inverse, 0f ) );
	}

	private static float Dot( Quaternion first, Quaternion second )
	{
		var x = MultiplyAdd( first.X, second.X, 0f );
		var y = MultiplyAdd( first.Y, second.Y, 0f );
		var z = MultiplyAdd( first.Z, second.Z, 0f );
		var w = MultiplyAdd( first.W, second.W, 0f );
		// Preserve stored-lane reduction after conversion to runtime ZXY axes.
		return ( y + x ) + ( z + w );
	}
}
