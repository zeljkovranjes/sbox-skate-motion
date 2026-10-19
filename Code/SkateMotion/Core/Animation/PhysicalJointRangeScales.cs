namespace SkateMotion.Core.Animation;

/// <summary>Primary and alternate range scales in retained structural joint order.</summary>
internal readonly record struct PhysicalJointRangeScales(
	float PrimarySwing, float PrimaryTwist, float AlternateSwing, float AlternateTwist );
