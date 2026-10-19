namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>Applies retained posture reflections and ordered bone swaps to a prepared pose.</summary>
internal static class AnimationPosturePoseTransform
{
	internal static void Apply( SkeletonPose pose, AnimationPostureBoneMap map, int mode )
	{
		ArgumentNullException.ThrowIfNull( pose );
		ArgumentNullException.ThrowIfNull( map );
		if ( pose.Positions.Length != map.BoneCount )
			throw new ArgumentException( "The posture map must match the prepared pose." );

		for ( var bone = 0; bone < map.BoneCount; bone++ )
		{
			var paired = map.PairedBone( bone );
			if ( bone > paired )
				continue;
			var transform = pose.Transform( bone );
			var special = mode == 1 && (bone == 0 || map.Kind( bone ) == 0);
			if ( special && map.Kind( bone ) == 0 )
				transform = transform with
				{
					Rotation = AnimationPoseComposer.ComposeRotation( transform.Rotation, new Quaternion( 0f, 1f, 0f, 0f ) )
				};
			transform = Reflect( transform, special );
			if ( bone == paired )
			{
				Write( pose, bone, transform );
				continue;
			}

			// The second operand always uses the ordinary reflection after the complete-record swap.
			var other = pose.Transform( paired );
			Write( pose, paired, transform );
			Write( pose, bone, Reflect( other, false ) );
		}
	}

	private static AnimationBoneTransform Reflect( AnimationBoneTransform transform, bool special )
	{
		var rotation = transform.Rotation;
		var position = transform.Position;
		rotation.Y = Negate( rotation.Y );
		if ( special )
		{
			rotation.Z = Negate( rotation.Z );
			position.X = Negate( position.X );
		}
		else
		{
			rotation.X = Negate( rotation.X );
			position.Z = Negate( position.Z );
		}
		return transform with { Rotation = rotation, Position = position };
	}

	private static float Negate( float value ) => LimbSolverMath.MultiplyAdd( -1f, value, 0f );

	private static void Write( SkeletonPose pose, int bone, AnimationBoneTransform transform )
	{
		pose.Positions[ bone ] = new Vector3( transform.Position.X, transform.Position.Y, transform.Position.Z );
		pose.Rotations[ bone ] = transform.Rotation;
		pose.BoneWeights[ bone ] = transform.Position.W;
		pose.Scales[ bone ] = transform.Scale;
	}
}
