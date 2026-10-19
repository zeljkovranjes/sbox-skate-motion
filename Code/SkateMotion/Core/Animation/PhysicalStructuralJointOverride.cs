namespace SkateMotion.Core.Animation;

/// <summary>Per-joint locking and range scaling, indexed by retained joint order.</summary>
internal readonly record struct PhysicalStructuralJointOverride(
	bool Locked, float SwingScale, float TwistScale );
