using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

// Estimated crash policy and thresholds.
internal static class LandingSafety
{
	internal static bool RequiresBail( ArticulatedBoard board, ReadOnlySpan<CollisionContact> contacts, float impactLimit, float step )
		=> RequiresBail( board, contacts, impactLimit, step, out _, out _ );

	/// <summary>As above, also giving the speed into the surface and the surface normal of the contact that bails.</summary>
	internal static bool RequiresBail( ArticulatedBoard board, ReadOnlySpan<CollisionContact> contacts, float impactLimit, float step,
		out float closing, out Vector3 normal )
	{
		closing = 0f;
		normal = Vector3.Zero;
		foreach ( var contact in contacts )
		{
			ref var part = ref board.Body( contact.BodyA );
			ref var surface = ref board.Body( contact.BodyB );
			var velocity = part.Velocity + Vector3.Cross( part.AngularVelocity, contact.PointA - part.Position );
			var surfaceVelocity = surface.Velocity + Vector3.Cross( surface.AngularVelocity, contact.PointB - surface.Position );
			var approach = -Vector3.Dot( velocity - surfaceVelocity, contact.Normal );
			// Swept contacts can stop the board before the current contact point reaches the surface.
			var gap = Vector3.Dot( contact.PointA - contact.PointB, contact.Normal );
			if ( gap > Math.Max( 0.004f, approach * step ) ) continue;
			closing = approach;
			normal = contact.Normal;
			if ( approach > impactLimit )
				return true;
			// Trucks only touch rails; a hanger meeting the rail's side is a normal grind lock-in.
			if ( approach > 0.5f && !IsTruck( contact.BodyA ) && Vector3.Dot( board.Deck.Up, contact.Normal ) < 0.35f )
				return true;
		}
		return false;
	}

	private static bool IsTruck( int body ) => body is 1 or 2;
}
