namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Compiled structural limits, retaining independent angular modes and cosines.</summary>
internal readonly record struct PhysicalStructuralJointLimits(
	Vector4 LinearSpeed, float SwingSpeed, float TwistSpeed, float SwingAngle, float TwistAngle,
	float SwingCosine, float TwistCosine, int SwingMode, int TwistMode );
