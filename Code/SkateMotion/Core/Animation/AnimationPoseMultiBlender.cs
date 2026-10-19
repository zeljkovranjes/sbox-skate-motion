namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;
using static LimbSolverMath;

/// <summary>Accumulates weighted transforms in order and normalizes the resulting rotation once.</summary>
internal static class AnimationPoseMultiBlender
{
	internal const int MaximumInputs = 5;

	internal static AnimationBoneTransform Blend( ReadOnlySpan<AnimationBoneTransform> inputs,
		ReadOnlySpan<float> weights )
	{
		if ( inputs.Length < 1 || inputs.Length > MaximumInputs || weights.Length != inputs.Length )
			throw new ArgumentException( "Blending needs one through five transforms with matching weights." );

		var position = Vector4.Zero;
		var rotation = Vector4.Zero;
		var scale = Vector4.Zero;
		for ( var index = 0; index < inputs.Length; index++ )
		{
			var input = inputs[ index ];
			var weight = weights[ index ];
			position = Accumulate( position, input.Position, weight );
			rotation = Accumulate( rotation, new Vector4( input.Rotation.X, input.Rotation.Y,
				input.Rotation.Z, input.Rotation.W ), weight );
			scale = Accumulate( scale, input.Scale, weight );
		}

		var squares = new Vector4( MultiplyAdd( rotation.X, rotation.X, 0f ),
			MultiplyAdd( rotation.Y, rotation.Y, 0f ), MultiplyAdd( rotation.Z, rotation.Z, 0f ),
			MultiplyAdd( rotation.W, rotation.W, 0f ) );
		// Preserve stored-lane reduction after conversion to runtime ZXY axes.
		var inverse = InverseSquareRoot( ( squares.Y + squares.X ) + ( squares.Z + squares.W ) );
		rotation = Accumulate( Vector4.Zero, rotation, inverse );
		return new AnimationBoneTransform( position,
			new Quaternion( rotation.X, rotation.Y, rotation.Z, rotation.W ), scale );
	}

	private static Vector4 Accumulate( Vector4 sum, Vector4 value, float weight ) => new(
		MultiplyAdd( weight, value.X, sum.X ), MultiplyAdd( weight, value.Y, sum.Y ),
		MultiplyAdd( weight, value.Z, sum.Z ), MultiplyAdd( weight, value.W, sum.W ) );
}
