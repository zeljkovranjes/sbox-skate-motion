namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Authored frames and retained feedback positions for physical pose preparation.</summary>
internal sealed class PhysicalPosePreparationState
{
	internal LimbRigidTransform ModelToWorld { get; set; }
	internal LimbRigidTransform SolverFrame { get; set; }
	internal LimbRigidTransform ModelOffset { get; set; }
	internal LimbRigidTransform ContactFrame { get; set; }
	internal LimbRigidTransform SecondaryTarget { get; set; }
	internal LimbRigidTransform FirstFeedbackFrame { get; set; }
	internal LimbRigidTransform SecondFeedbackFrame { get; set; }
	internal byte SuppressionFlag { get; set; }
	internal LimbRigidTransform ComposedContact { get; set; }
	internal Vector3 CachedPosition { get; set; }
	internal Vector3 FirstFeedbackPosition { get; set; }
	internal Vector3 SecondFeedbackPosition { get; set; }
	internal float FeedbackHeight { get; set; }
}
