namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Prepares mapped poses, then publishes motion after the attachment stage.</summary>
internal static class PhysicalAnimationPoseProcessor
{
	internal static void Prepare( PhysicalAnimationPoseState state, PhysicalAnimationPoseInput input )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( input.Clips );
		ArgumentNullException.ThrowIfNull( input.SolverToClip );
		if ( input.SolverToClip.Length != 24 )
			throw new ArgumentException( "The solver mapping requires twenty-four bones." );
		if ( input.MotionBone < 0 || input.MotionBone >= input.Clips.Length )
			throw new ArgumentOutOfRangeException( nameof( input.MotionBone ) );

		state.DeltaTime = input.DeltaTime;
		state.BoardVelocity = input.BoardVelocity;
		var motion = input.Clips[ input.MotionBone ];
		PhysicalAnimationAttributeProcessor.Update( state.Attributes, input.Attributes with
		{
			BoardDisplacement = motion.Translation,
			DeltaTime = input.DeltaTime,
			RightToeBone = input.SolverToClip[ 19 ]
		} );

		// Motion extraction and the mapped board pose have independent clip indices.
		state.ReferenceFrame = motion.Compose( state.RelativeBodyFrame );
		state.RelativeBodyFrame = state.ReferenceFrame;
		state.MotionVelocity = input.ModelToWorld.Rotation.Transform(
			Scale( state.ReferenceFrame.Translation, Reciprocal( input.OwnerDeltaTime ) ), Vector3.Zero );
		state.InverseReferenceFrame = state.ReferenceFrame.RigidInverse();

		var skeleton = state.Skeleton;
		EffectorPoseGatherer.GatherBones( input.SolverToClip, skeleton.BindTransforms,
			input.Clips, skeleton.AnimatedBones, skeleton.MutableBones );
		state.GatheredBoard = skeleton.MutableBones[ 0 ];
		if ( MathF.Abs( state.GatheredBoard.Rotation.X.Y ) > .71f )
			state.Attributes.ActionFlags |= 1UL << 47;
		EffectorPoseGatherer.GatherAuthored( input.HelperClipBones, input.EndBones,
			skeleton.BindTransforms, input.Clips, state.Authored );
	}

	/// <summary>Call after attachment has updated mutable poses and before the physical reset stage.</summary>
	internal static void Publish( PhysicalAnimationPoseState state, float[] weights,
		LimbRigidTransform cacheFrame, bool contactActive, bool publishExternalMotion, Vector4 externalMotion )
	{
		ArgumentNullException.ThrowIfNull( state );
		state.Cache.Update( state.Skeleton.MutableBones, weights, cacheFrame );
		state.CachedRoot = state.Cache.Bones[ 23 ];
		state.CachedBoard = state.Cache.Bones[ 0 ];
		var axisY = state.CachedBoard.Rotation.Z.Y;
		state.BoardAxisDelta = axisY - state.PreviousBoardAxisY;
		state.PreviousBoardAxisY = axisY;
		var attributes = state.Attributes;
		attributes.MotionFlags = (attributes.MotionFlags & ~((1UL << 11) | (1UL << 12)))
			| (axisY > 0f ? 1UL << 12 : 1UL << 11);
		attributes.ActionFlags = (attributes.ActionFlags & ~(1UL << 49))
			| (contactActive ? 1UL << 49 : 0UL);
		if ( publishExternalMotion )
			state.PublishedExternalMotion = externalMotion;
		state.PublishedRootOffset = state.Cache.RootOffset;
		state.PublishedRootOffsetDelta = state.Cache.RootOffsetDelta;
	}
}
