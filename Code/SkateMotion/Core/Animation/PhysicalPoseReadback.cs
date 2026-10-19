namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Retains solved body poses, sampled motion and errors relative to animation targets.</summary>
internal static class PhysicalPoseReadback
{
	internal static void Update( PhysicalPoseResetState state, PhysicalBodyBinding[] bodies,
		LimbRigidTransform boardPose, float[] weights )
	{
		if ( state.Poses.Length != 26 || bodies.Length != 26 || weights.Length != 24 )
			throw new ArgumentException( "Readback requires 26 bodies and 24 canonical bone weights." );
		for ( var i = 1; i < bodies.Length; i++ )
			if ( bodies[ i ] is null )
				throw new ArgumentException( "Readback requires every non-board binding." );

		// Motion is sampled at the fixed physical cadence, independently of render delta time.
		state.SampleInterval = .01666666753590107f;
		for ( var i = 0; i < bodies.Length; i++ )
		{
			var pose = i == 0 ? boardPose : bodies[ i ].Read();
			var velocity = Scale( pose.Translation - state.Positions[ i ], 59.999996185302734f );
			state.VelocityChanges[ i ] = velocity - state.Deltas[ i ];
			state.Poses[ i ] = pose;
			state.Positions[ i ] = pose.Translation;
			state.Deltas[ i ] = velocity;
		}

		var weightedPosition = Vector3.Zero;
		var weightedVelocity = Vector3.Zero;
		for ( var i = 0; i < weights.Length; i++ )
		{
			weightedPosition = AddScaled( state.Poses[ i ].Translation, weights[ i ], weightedPosition );
			weightedVelocity = AddScaled( state.Deltas[ i ], weights[ i ], weightedVelocity );
		}
		state.WeightedPhysicalPosition = weightedPosition;
		state.WeightedPhysicalVelocity = weightedVelocity;
	}

	internal static void UpdateErrors( PhysicalPoseResetState state, PhysicalPosePreparationState preparation,
		LimbRigidTransform modelToWorld, LimbRigidTransform[] mutablePose )
	{
		if ( state.Poses.Length != 26 || mutablePose.Length != 24 )
			throw new ArgumentException( "Pose errors require 26 solved bodies and 24 animation targets." );

		state.AnimationErrors[ 0 ] = Vector3.Zero;
		state.FirstFeedbackError = state.Poses[ 24 ].Translation - preparation.FirstFeedbackPosition;
		state.SecondFeedbackError = state.Poses[ 25 ].Translation - preparation.SecondFeedbackPosition;
		for ( var i = 1; i < mutablePose.Length; i++ )
			state.AnimationErrors[ i ] = state.Poses[ i ].Translation - modelToWorld.Transform( mutablePose[ i ].Translation );
	}
}
