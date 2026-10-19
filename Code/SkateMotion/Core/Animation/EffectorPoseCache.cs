namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Retains solver poses and weighted position changes independently of raw animation.</summary>
internal sealed class EffectorPoseCache
{
	internal LimbRigidTransform[] Bones { get; }
	internal Vector3 WeightedPosition { get; set; }
	internal Vector3 WeightedPositionDelta { get; private set; }
	internal Vector3 RootOffset { get; set; }
	internal Vector3 RootOffsetDelta { get; private set; }
	internal float DeltaScale { get; set; }

	internal void ResetMotion()
	{
		WeightedPosition = Vector3.Zero;
		WeightedPositionDelta = Vector3.Zero;
		RootOffset = Vector3.Zero;
		RootOffsetDelta = Vector3.Zero;
	}

	internal EffectorPoseCache( int boneCount )
	{
		if ( boneCount <= 0 )
			throw new ArgumentOutOfRangeException( nameof( boneCount ) );
		Bones = new LimbRigidTransform[ boneCount ];
		for ( var i = 0; i < Bones.Length; i++ )
			Bones[ i ] = LimbRigidTransform.Identity;
	}

	internal void Update( LimbRigidTransform[] mutableBones, float[] weights, LimbRigidTransform frame )
	{
		if ( mutableBones.Length != Bones.Length || weights.Length != Bones.Length )
			throw new ArgumentException( "Pose and weight counts must match the cache bone count." );
		Array.Copy( mutableBones, Bones, Bones.Length );
		var weighted = Vector3.Zero;
		for ( var i = 0; i < Bones.Length; i++ )
			weighted = AddScaled( Bones[ i ].Translation, weights[ i ], weighted );
		WeightedPositionDelta = weighted - WeightedPosition;
		WeightedPosition = weighted;
		// Transform each point before subtraction: cancelling translation earlier changes rounding.
		var offset = frame.Transform( weighted ) - frame.Transform( Bones[ 0 ].Translation );
		RootOffsetDelta = Scale( offset - RootOffset, DeltaScale );
		RootOffset = offset;
		DeltaScale = 1f;
	}
}
