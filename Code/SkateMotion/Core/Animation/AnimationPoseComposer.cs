namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;
using static LimbSolverMath;

/// <summary>Composes local transforms in either order, retaining a selected translation fourth lane.</summary>
internal static class AnimationPoseComposer
{
	internal static AnimationBoneTransform Compose( in AnimationBoneTransform first,
		in AnimationBoneTransform second, bool reverse, bool retainFirstTranslationW )
	{
		var parent = reverse ? second : first;
		var child = reverse ? first : second;
		var rotation = parent.Rotation;
		var axis = new Vector3( rotation.X, rotation.Y, rotation.Z );
		var position = new Vector3( child.Position.X, child.Position.Y, child.Position.Z );
		var inner = AddScaled( position, rotation.W, Cross( axis, position ) );
		var rotated = AddScaled( Cross( axis, inner ), 2f, position );
		var translationW = retainFirstTranslationW ? first.Position.W : second.Position.W;
		return new AnimationBoneTransform(
			new Vector4( rotated.X + parent.Position.X, rotated.Y + parent.Position.Y,
				rotated.Z + parent.Position.Z, translationW ),
			ComposeRotation( rotation, child.Rotation ),
			new Vector4( MultiplyAdd( parent.Scale.X, child.Scale.X, 0f ),
				MultiplyAdd( parent.Scale.Y, child.Scale.Y, 0f ),
				MultiplyAdd( parent.Scale.Z, child.Scale.Z, 0f ),
				MultiplyAdd( parent.Scale.W, child.Scale.W, 0f ) ) );
	}

	internal static void Compose( ReadOnlySpan<AnimationBoneTransform> first,
		ReadOnlySpan<AnimationBoneTransform> second, bool reverse, bool retainFirstTranslationW,
		Span<AnimationBoneTransform> output )
	{
		if ( second.Length != first.Length || output.Length < first.Length )
			throw new ArgumentException( "Pose buffers need matching input lengths and enough output space." );
		for ( var index = 0; index < first.Length; index++ )
			output[ index ] = Compose( first[ index ], second[ index ], reverse, retainFirstTranslationW );
	}

	internal static Quaternion ComposeRotation( Quaternion parent, Quaternion child )
	{
		var a = new Vector3( parent.X, parent.Y, parent.Z );
		var b = new Vector3( child.X, child.Y, child.Z );
		var mixed = AddScaled( b, parent.W, Cross( a, b ) );
		mixed = AddScaled( a, child.W, mixed );
		// Reduction order follows the stored pose axes after conversion to runtime axes.
		var y = MultiplyAdd( a.Y, b.Y, 0f );
		var z = MultiplyAdd( a.Z, b.Z, 0f );
		var x = MultiplyAdd( a.X, b.X, 0f );
		var dot = (y + z) + x;
		return new Quaternion( mixed, MultiplyAdd( parent.W, child.W, 0f ) - dot );
	}
}
