namespace SkateMotion.Core.Animation;

/// <summary>Supplied request gates and rig poses for one attachment update.</summary>
internal readonly record struct BoardAttachmentInput(
	bool AttachFromHand, LimbRigidTransform Hand, LimbRigidTransform Helper,
	bool DescendingMode, bool SuppressDescending,
	GrindAttachmentInput Grind, DescendingAttachmentInput Descending, VerticalAttachmentInput Vertical );
