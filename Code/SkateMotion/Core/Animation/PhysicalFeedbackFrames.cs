namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Builds paired feedback targets with a retained, bounded height change.</summary>
internal static class PhysicalFeedbackFrames
{
	private const float MaximumHeightChange = .04f;
	private const float TargetSeparation = .3f;

	internal static void Update( PhysicalPosePreparationState state, Vector3 referencePosition, float requestedHeight )
	{
		var minimum = state.FeedbackHeight - MaximumHeightChange;
		var maximum = state.FeedbackHeight + MaximumHeightChange;
		var height = minimum - requestedHeight >= 0f ? minimum : requestedHeight;
		state.FeedbackHeight = maximum - height >= 0f ? height : maximum;
		var rotation = state.ModelToWorld.Rotation.Orthonormalized();
		state.SecondFeedbackFrame = new LimbRigidTransform( rotation, referencePosition );
		state.FirstFeedbackFrame = new LimbRigidTransform( rotation,
			AddScaled( rotation.Y, state.FeedbackHeight + TargetSeparation, referencePosition ) );
	}
}
