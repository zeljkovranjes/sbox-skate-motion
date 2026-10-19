namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>One solved world constraint, retaining its original body order and packed surfaces.</summary>
internal readonly record struct PhysicalWorldContact(
	PhysicalBodyState FirstBody, PhysicalBodyState SecondBody,
	Vector4 Normal, Vector4 Point, Vector3 Impulse, uint Materials )
{
	internal Vector3 TangentA { get; init; }
	internal Vector3 TangentB { get; init; }
	internal Vector3 NormalImpulse { get; init; }
	internal Vector4 TangentImpulse { get; init; } = new( Impulse, 0f );
}
