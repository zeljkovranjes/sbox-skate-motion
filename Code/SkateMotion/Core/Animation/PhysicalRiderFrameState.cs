namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retains rider placement frames and one-update anchor and heading requests.</summary>
internal sealed class PhysicalRiderFrameState
{
	internal LimbRigidTransform RootFrame { get; set; } = LimbRigidTransform.Identity;
	internal LimbRigidTransform LocalFrame { get; set; } = LimbRigidTransform.Identity;
	internal LimbRigidTransform BoardFrame { get; set; } = LimbRigidTransform.Identity;
	internal LimbRigidTransform WorldToBoard { get; set; } = LimbRigidTransform.Identity;
	internal LimbRigidTransform RelativeFrame { get; set; } = LimbRigidTransform.Identity;
	internal LimbRigidTransform ModelToWorld { get; set; } = LimbRigidTransform.Identity;
	internal LimbRigidTransform WorldToModel { get; set; } = LimbRigidTransform.Identity;
	internal Vector3 PreviousBoardPosition { get; set; }
	internal Vector3 Anchor { get; set; }
	internal bool PreserveAnchor { get; set; }
	internal bool ResetHeading { get; set; }
}
