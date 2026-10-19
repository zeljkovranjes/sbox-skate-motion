namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Resets physical skeleton targets, motion and feedback caches from the mutable pose.</summary>
internal static class PhysicalPoseReset
{
	internal static void Update( PhysicalPoseResetState state, PhysicalPosePreparationState preparation,
		EffectorPoseCache cache, PhysicalBodyBinding[] bodies, LimbRigidTransform[] mutablePose, LimbRigidTransform modelToWorld )
	{
		if ( mutablePose.Length == 0 || bodies.Length != mutablePose.Length + 2 || state.Poses.Length != bodies.Length )
			throw new ArgumentException( "Reset requires one body per mutable bone and two trailing targets." );

		state.MotionFlag = 0;
		for ( var i = 0; i < bodies.Length; i++ )
		{
			// Both trailing physical targets use this same authored frame.
			var pose = i < mutablePose.Length ? modelToWorld.Compose( mutablePose[ i ] ) : preparation.SecondFeedbackFrame;
			bodies[ i ].Write( pose );
			if ( bodies[ i ].Body is { } body )
			{
				body.Velocity = Vector3.Zero;
				body.AngularVelocity = Vector3.Zero;
				body.LinearAccumulator = body.DefaultLinearAccumulator;
				body.AngularAccumulator = Vector3.Zero;
			}
		}

		state.LocalReference = mutablePose[ ^1 ].Translation;
		state.WorldReference = modelToWorld.Transform( state.LocalReference );
		state.PreviousWorldReference = state.WorldReference;
		state.ReferenceVelocity = Vector3.Zero;
		for ( var i = 0; i < bodies.Length; i++ )
		{
			state.Poses[ i ] = bodies[ i ].Read();
			state.Positions[ i ] = state.Poses[ i ].Translation;
			state.Deltas[ i ] = Vector3.Zero;
		}

		cache.ResetMotion();
		preparation.CachedPosition = Vector3.Zero;
		preparation.FirstFeedbackPosition = Vector3.Zero;
		preparation.SecondFeedbackPosition = Vector3.Zero;
	}
}
