namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>First trajectory contact, its sampled time and the surrounding surface direction.</summary>
internal readonly record struct TrajectoryContact(
	Vector3 Position, Vector3 Normal, Vector3 SurfaceNormal, float Time,
	Matrix4x4 Transform, int Sample, ushort Material, uint Identity )
{
	internal static TrajectoryContact Empty
	{
		get
		{
			var identity = Matrix4x4.Identity;
			identity.M44 = 0f;
			return new( Vector3.Zero, Vector3.UnitY, Vector3.UnitY, -1f, identity, -1, 0, 0 );
		}
	}
}
