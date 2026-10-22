using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Vehicle;

using Vector3 = System.Numerics.Vector3;

// Estimated crash policy and thresholds.
internal static class LandingSafety
{
	internal static bool RequiresBail( ArticulatedBoard board, ReadOnlySpan<CollisionContact> contacts, float impactLimit, float step )
		=> RequiresBail( board, contacts, impactLimit, step, out _, out _ );

	/// <summary>As above, also giving the speed into the surface and the surface normal of the contact that bails.</summary>
	/// <param name="onRail">The board grinds a rail or comes down onto one: its wheels brushing the rail's side are part
	/// of the grind, as its trucks meeting it are.</param>
	/// <param name="wall">The face of the wall the board is turning to or riding (WallRide.Wall), or null: the board meeting
	/// that wall side-on is the wall ride, not a crash.</param>
	internal static bool RequiresBail( ArticulatedBoard board, ReadOnlySpan<CollisionContact> contacts, float impactLimit, float step,
		out float closing, out Vector3 normal, bool onRail = false, Vector3? wall = null )
	{
		closing = 0f;
		normal = Vector3.Zero;
		var centre = CentreVelocity( board );
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
			// How hard it comes down is the speed of the board as a whole into the surface. A board still turning as it lands
			// (a body flip coming round) meets it on one side faster by the turn times half its track, which bailed on a
			// wider board a landing the rider rides away from on a narrower one.
			var landing = -Vector3.Dot( centre - surfaceVelocity, contact.Normal );
			if ( landing > impactLimit )
				return true;
			// Trucks only touch rails; a hanger meeting the rail's side is a normal grind lock-in.
			// On a rail a wheel brushing its side is the grind's too: a narrow board's wheel probes (spheres the wheel's
			// radius) reach a 2 in rail's side while its real wheels keep clear of it.
			// Turning onto a wall for a wall ride, the board meets it side-on as its wheels come round to it: counted as a crash,
			// the rider stepped off and ran it out before the wheels were on the wall.
			if ( approach > 0.5f && !IsTruck( contact.BodyA ) && !(onRail && IsWheel( contact.BodyA ))
				&& !(wall is { } face && Vector3.Dot( contact.Normal, face ) > SameWall)
				&& Vector3.Dot( board.Deck.Up, contact.Normal ) < 0.35f )
				return true;
		}
		return false;
	}

	// How near a contact's normal must lie to the wall's face to be that wall (about 25 degrees).
	private const float SameWall = 0.9f;

	private static bool IsTruck( int body ) => body is 1 or 2;

	private static bool IsWheel( int body ) => body is >= 3 and <= 6;

	// Velocity of the board's centre of mass (the rider's weight is on the deck).
	private static Vector3 CentreVelocity( ArticulatedBoard board )
	{
		var momentum = Vector3.Zero;
		var mass = 0f;
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; i++ )
		{
			ref var body = ref board.Body( i );
			if ( !(body.InverseMass > 0f) )
				continue;
			momentum += body.Velocity / body.InverseMass;
			mass += 1f / body.InverseMass;
		}
		return mass > 0f ? momentum / mass : board.Body( 0 ).Velocity;
	}
}
