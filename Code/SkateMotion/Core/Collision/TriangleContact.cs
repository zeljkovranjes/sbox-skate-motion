namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Triangle contact position, normal, two barycentric coordinates and segment fraction.</summary>
internal readonly record struct TriangleContact(
	Vector3 Position,
	Vector3 Normal,
	Vector3 Barycentric,
	float Fraction );
