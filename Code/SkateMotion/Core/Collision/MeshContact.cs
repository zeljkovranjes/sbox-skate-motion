namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Nearest accepted mesh contact and the count of all accepted triangles.</summary>
internal readonly record struct MeshContact(
	Vector3 Position,
	Vector3 Normal,
	Matrix4x4 Transform,
	float Fraction,
	ushort Material,
	uint Identity,
	uint HitCount );
