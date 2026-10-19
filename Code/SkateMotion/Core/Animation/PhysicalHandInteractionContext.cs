namespace SkateMotion.Core.Animation;

/// <summary>Owner activation, reset flags and model placement for generated hand contacts.</summary>
internal readonly record struct PhysicalHandInteractionContext(
	ulong ActionFlags, ulong RigFlags, LimbRigidTransform ModelToWorld );
