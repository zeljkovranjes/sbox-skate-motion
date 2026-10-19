namespace SkateMotion.Core.Constraints;

using Quaternion = System.Numerics.Quaternion;

/// <summary>Authored cone and twist limits, with independent angular speed allowances.</summary>
internal readonly record struct RangeJointGeometry(
	Quaternion LocalFrameA, Quaternion LocalFrameB, float SwingCosine, float TwistCosine,
	float MaximumTwistSpeed, float MaximumSwingSpeed, int SwingMode, int TwistMode );
