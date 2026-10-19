namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Applies posture transforms in the runtime pose coordinate system.</summary>
internal static class AnimationRuntimePosturePoseTransform
{
	internal static void Apply( SkeletonPose pose, AnimationPostureBoneMap map, int mode )
	{
		ArgumentNullException.ThrowIfNull( pose );
		ArgumentNullException.ThrowIfNull( map );
		if ( pose.Positions.Length != map.BoneCount )
			throw new ArgumentException( "The posture map must match the prepared pose." );

		// Lane permutations retain the kernel's arithmetic order, including signed zero.
		Permute( pose, false );
		AnimationPosturePoseTransform.Apply( pose, map, mode );
		Permute( pose, true );
	}

	private static void Permute( SkeletonPose pose, bool runtimeAxes )
	{
		for ( var bone = 0; bone < pose.Positions.Length; bone++ )
		{
			var position = pose.Positions[ bone ];
			var rotation = pose.Rotations[ bone ];
			var scale = pose.Scales[ bone ];
			pose.Positions[ bone ] = runtimeAxes
				? new Vector3( position.Z, position.X, position.Y )
				: new Vector3( position.Y, position.Z, position.X );
			pose.Rotations[ bone ] = runtimeAxes
				? new Quaternion( rotation.Z, rotation.X, rotation.Y, rotation.W )
				: new Quaternion( rotation.Y, rotation.Z, rotation.X, rotation.W );
			pose.Scales[ bone ] = runtimeAxes
				? new Vector4( scale.Z, scale.X, scale.Y, scale.W )
				: new Vector4( scale.Y, scale.Z, scale.X, scale.W );
		}
	}
}
