namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Available surface-query values that refresh a retained launch context.</summary>
internal readonly record struct PhysicalJumpSurfaceInput(
	bool Available, bool CorrectionEnabled, Vector3 FallbackDirection,
	Vector3 Correction, Vector3 PrimaryDirection, Vector3 SecondaryDirection, Vector3 Anchor );
