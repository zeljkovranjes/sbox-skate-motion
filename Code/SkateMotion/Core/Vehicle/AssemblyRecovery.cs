using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

// Positional overlap projection. Move the connected assembly together so
// correcting deeply buried wheels cannot stretch joints and create kinetic energy.
internal static class AssemblyRecovery
{
	internal static Vector3 Translation( ReadOnlySpan<CollisionContact> contacts )
	{
		var correction = Vector3.Zero;
		for ( var pass = 0; pass < 16; pass++ )
		{
			foreach ( var contact in contacts )
			{
				var gap = Vector3.Dot( contact.PointA + correction - contact.PointB, contact.Normal );
				if ( gap < 0f )
					correction -= contact.Normal * gap;
			}
		}
		if ( !float.IsFinite( correction.LengthSquared() ) || correction.LengthSquared() > 0.2f * 0.2f )
			return Vector3.Zero;
		foreach ( var contact in contacts )
		{
			if ( Vector3.Dot( contact.PointA + correction - contact.PointB, contact.Normal ) < -0.0001f )
				return Vector3.Zero;
		}
		return correction;
	}
}
