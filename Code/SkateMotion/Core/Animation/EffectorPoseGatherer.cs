namespace SkateMotion.Core.Animation;

/// <summary>Separates untouched animation poses from bind-composed solver poses and helper targets.</summary>
internal static class EffectorPoseGatherer
{
	internal static void GatherBones( int[] solverToClip, LimbRigidTransform[] bindTransforms,
		LimbRigidTransform[] clipBones, LimbRigidTransform[] animatedBones, LimbRigidTransform[] mutableBones )
	{
		var count = solverToClip.Length;
		if ( bindTransforms.Length != count || animatedBones.Length != count || mutableBones.Length != count )
			throw new ArgumentException( "Solver arrays must match the mapped bone count." );
		if ( ReferenceEquals( animatedBones, mutableBones ) || ReferenceEquals( clipBones, animatedBones )
			|| ReferenceEquals( clipBones, mutableBones ) || ReferenceEquals( bindTransforms, animatedBones )
			|| ReferenceEquals( bindTransforms, mutableBones ) )
			throw new ArgumentException( "Gather outputs must be separate from each other and the source arrays." );
		ValidateIndices( solverToClip, clipBones.Length );
		for ( var i = 0; i < count; i++ )
		{
			var animated = clipBones[ solverToClip[ i ] ];
			animatedBones[ i ] = animated;
			mutableBones[ i ] = animated.Compose( bindTransforms[ i ] );
		}
	}

	internal static void GatherAuthored( int[] helperClipBones, int[] endBones,
		LimbRigidTransform[] bindTransforms, LimbRigidTransform[] clipBones, LimbRigidTransform[] authored )
	{
		if ( helperClipBones.Length != 4 || endBones.Length != 4 || authored.Length != 4 )
			throw new ArgumentException( "Authored targets require four helper and endpoint slots." );
		if ( ReferenceEquals( authored, clipBones ) || ReferenceEquals( authored, bindTransforms ) )
			throw new ArgumentException( "Authored targets must be separate from source arrays." );
		ValidateIndices( helperClipBones, clipBones.Length );
		ValidateIndices( endBones, bindTransforms.Length );
		for ( var i = 0; i < 4; i++ )
			authored[ i ] = clipBones[ helperClipBones[ i ] ].Compose( bindTransforms[ endBones[ i ] ] );
	}

	private static void ValidateIndices( int[] indices, int count )
	{
		foreach ( var index in indices )
		{
			if ( index < 0 || index >= count )
				throw new ArgumentException( "Each pose input must reference an existing bone." );
		}
	}
}
