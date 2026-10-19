namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

// A narrow-phase point pair before its friction frame has been constructed.
internal readonly record struct ContactManifoldPoint(
	int BodyA, int BodyB, Vector3 PointA, Vector3 PointB, Vector3 Normal,
	float StaticFriction, float SlidingFriction, float Restitution )
{
	internal uint Materials { get; init; }
}
