namespace SkateMotion.Core.Animation;

/// <summary>Supplied activation, approach, edge metadata and rig frames for attachment requests.</summary>
internal readonly record struct GrindAttachmentInput(
	bool Enabled, bool Suppressed, GrindApproachInput Approach, LimbFrame EdgeMetadata,
	LimbRigidTransform ModelToWorld, LimbRigidTransform WorldToModel,
	LimbRigidTransform AuthoredBoard, LimbRigidTransform MutableBoard );
