namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Contact frame and permitted approach directions after a surface investigation.</summary>
internal readonly record struct GrindSurfaceContact(
	Vector3 Normal,
	GrindSurfaceKind Kind,
	bool IsFallback,
	Vector3 Side,
	Vector3 FirstDirection,
	Vector3 SecondDirection,
	Vector3 HighSide );
