namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained physical poses and root-reference motion rebuilt during a reset.</summary>
internal sealed class PhysicalPoseResetState
{
	internal LimbRigidTransform[] Poses { get; }
	internal Vector3[] Positions { get; }
	internal Vector3[] Deltas { get; }
	internal Vector3[] VelocityChanges { get; }
	internal Vector3[] AnimationErrors { get; }
	internal Vector3 FirstFeedbackError { get; set; }
	internal Vector3 SecondFeedbackError { get; set; }
	internal Vector3 WeightedPhysicalPosition { get; set; }
	internal Vector3 WeightedPhysicalVelocity { get; set; }
	internal float SampleInterval { get; set; }
	internal byte MotionFlag { get; set; }
	internal Vector3 LocalReference { get; set; }
	internal Vector3 WorldReference { get; set; }
	internal Vector3 PreviousWorldReference { get; set; }
	internal Vector3 ReferenceVelocity { get; set; }

	internal PhysicalPoseResetState( int bodyCount )
	{
		if ( bodyCount <= 2 )
			throw new ArgumentOutOfRangeException( nameof( bodyCount ) );
		Poses = new LimbRigidTransform[ bodyCount ];
		Positions = new Vector3[ bodyCount ];
		Deltas = new Vector3[ bodyCount ];
		VelocityChanges = new Vector3[ bodyCount ];
		AnimationErrors = new Vector3[ bodyCount - 2 ];
	}
}
