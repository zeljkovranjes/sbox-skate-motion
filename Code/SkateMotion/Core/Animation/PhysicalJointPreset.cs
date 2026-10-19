namespace SkateMotion.Core.Animation;

/// <summary>Position and velocity coefficients for both constraints of a preset joint drive.</summary>
internal readonly record struct PhysicalJointPreset(
	float FirstPosition, float FirstVelocity, float SecondPosition, float SecondVelocity );
