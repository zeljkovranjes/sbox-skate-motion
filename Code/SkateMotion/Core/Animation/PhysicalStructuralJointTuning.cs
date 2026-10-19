namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Authored motion limits and global angular locking overrides.</summary>
internal readonly record struct PhysicalStructuralJointTuning(
	Vector4 LinearSpeed, float SwingSpeed, float TwistSpeed, bool LockSwing, bool LockTwist );
