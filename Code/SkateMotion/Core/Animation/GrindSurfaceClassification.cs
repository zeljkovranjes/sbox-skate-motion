namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Classified geometry, contact-angle limits, materials and retained flags.</summary>
internal readonly record struct GrindSurfaceClassification(
	Vector3 Center,
	Vector3 FirstFarPoint,
	Vector3 SecondFarPoint,
	Vector3 Up,
	Vector3 Direction,
	Vector3 HighSide,
	float FirstAngleLimit,
	float SecondAngleLimit,
	GrindSurfaceKind Kind,
	uint AudioSurface,
	uint PhysicalSurface,
	uint Flags );
