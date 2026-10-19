namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct ContactGeometry(
	int BodyA, int BodyB, Vector3 PointA, Vector3 PointB,
	Vector3 Normal, Vector3 TangentA, Vector3 TangentB,
	float StaticFriction, float SlidingFriction, float Restitution, Vector3 MotionTerm )
{
	internal uint Materials { get; init; }
}
