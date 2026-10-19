namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Lock-on for grinds: while the board falls toward a rail it is passing within <see cref="Reach"/> of, its sideways
/// travel is steered so it closes the gap and arrives on the rail with no sideways speed left (it would otherwise hit
/// the rail's side and bounce off). A rail needing more than <see cref="MaximumBend"/> of turn, or crossed too fast,
/// is left alone.
/// </summary>
internal static class GrindAssist
{
	/// <summary>Farthest (metres) a rail can be to the side and still be locked onto.</summary>
	internal const float Reach = 0.9f;

	/// <summary>Largest angle (radians) between the board's travel and the rail that the lock-on steers.</summary>
	internal const float MaximumBend = 0.192f;

	// Fastest approach across a rail (m/s) that still locks on; time to the rail (seconds) under which only the
	// sideways speed is taken off; and how quickly (seconds) the sideways speed follows its aim.
	private const float CrossLimit = 8f;
	private const float Arriving = 0.03f;
	private const float Response = 0.03f;

	/// <summary>
	/// Change of the board's velocity this step: zero while rising, out of reach, or with no rail to lock onto.
	/// <paramref name="lowest"/> is the height of the board's lowest truck; <paramref name="lockReach"/> is how far to the side a
	/// rail may be; positions are metres, z up.
	/// </summary>
	internal static Vector3 VelocityChange( Vector3 position, float lowest, Vector3 velocity, ReadOnlySpan<RailSegment> rails,
		float gravity, float step, float lockReach = Reach )
	{
		if ( velocity.Z >= 0f || gravity <= 0f )
			return Vector3.Zero;
		var travel = new Vector3( velocity.X, velocity.Y, 0f );
		if ( travel.LengthSquared() < 0.01f )
			return Vector3.Zero;
		var best = Vector3.Zero;
		var bestOffset = lockReach;
		foreach ( var rail in rails )
		{
			if ( !rail.IsValid )
				continue;
			var height = 0.5f * (rail.Start.Z + rail.End.Z);
			var drop = lowest - height;
			if ( drop <= 0f )
				continue;
			// Time until the trucks come down to the rail's height.
			var time = (velocity.Z + MathF.Sqrt( velocity.Z * velocity.Z + 2f * gravity * drop )) / gravity;
			var along3 = rail.Direction;
			var along = Vector3.Normalize( new Vector3( along3.X, along3.Y, 0f ) );
			var side = new Vector3( -along.Y, along.X, 0f );
			// The board has to be over the rail's length when it arrives.
			var arrival = position + travel * time;
			var reach = Vector3.Dot( new Vector3( arrival.X - rail.Start.X, arrival.Y - rail.Start.Y, 0f ), along );
			if ( reach < 0f || reach > rail.Length )
				continue;
			var offset = Vector3.Dot( new Vector3( position.X - rail.Start.X, position.Y - rail.Start.Y, 0f ), side );
			if ( MathF.Abs( offset ) >= bestOffset )
				continue;
			var forward = Vector3.Dot( travel, along );
			var across = Vector3.Dot( travel, side );
			if ( MathF.Abs( across ) > CrossLimit || MathF.Abs( forward ) < 0.1f )
				continue;
			// Sideways speed falling steadily to nothing on arrival covers twice its average: aim for that.
			var aim = time > Arriving ? -2f * offset / time : 0f;
			// The bend limit applies to the path as a whole: the offset closed over the time left.
			if ( MathF.Atan( MathF.Abs( offset ) / MathF.Max( time, Arriving ) / MathF.Abs( forward ) ) > MaximumBend )
				continue;
			bestOffset = MathF.Abs( offset );
			best = side * ((aim - across) * Math.Min( 1f, step / Response ));
		}
		return best;
	}
}
