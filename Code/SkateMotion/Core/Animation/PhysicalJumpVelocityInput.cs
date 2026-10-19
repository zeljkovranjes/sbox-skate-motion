namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained surface directions and motion sources for an explicit launch.</summary>
internal readonly record struct PhysicalJumpVelocityInput(
	Vector3 Velocity, Vector3 Position, Vector3 ReferencePosition,
	Vector3 PrimaryDirection, Vector3 SecondaryDirection, Vector3 FallbackDirection,
	Vector3 Anchor, Vector3 Correction, int Mode, bool CorrectionEnabled );
