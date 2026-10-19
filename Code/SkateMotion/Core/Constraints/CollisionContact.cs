namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

// World-space collision data; body state supplies velocity and acceleration separately.
internal readonly record struct CollisionContact(
	int BodyA, int BodyB, Vector3 PointA, Vector3 PointB,
	Vector3 Normal, Vector3 TangentA, Vector3 TangentB,
	float StaticFriction, float SlidingFriction, float Restitution )
{
	internal uint Materials { get; init; }

	// Sweep hits lie at a future time. Preserve their separating plane, but do not
	// feed travel along that plane into the compiler's tangential position bias.
	internal CollisionContact WithProjectedSurfacePoint() => this with
	{
		PointB = PointA + Normal * Vector3.Dot( PointB - PointA, Normal )
	};
}
