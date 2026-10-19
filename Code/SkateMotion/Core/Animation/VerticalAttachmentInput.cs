namespace SkateMotion.Core.Animation;

/// <summary>Supplied phase signals and projected distances in the rig's vertical coordinates.</summary>
internal readonly record struct VerticalAttachmentInput(
	int Category, float Trigger, bool PrimaryActive, bool SecondaryActive,
	bool Suppress, bool StopTracking, bool ForceRequest,
	float CurrentHeight, float ProjectedSpeed, float CachedHeight );
