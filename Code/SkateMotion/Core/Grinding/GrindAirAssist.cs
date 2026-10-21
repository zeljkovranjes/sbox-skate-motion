using SkateMotion.Core.Vehicle;

namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The end of a jump onto a rail: from the top of the jump (or once the trucks are within <see cref="Window"/> of
/// coming down to a rail's height) a board travelling along a rail it will come down just beside is moved sideways
/// onto its line, never faster than <see cref="PerSecond"/> and at most <see cref="Total"/> in one jump, so an ollie
/// from beside a rail lands on it. While it moves the board it also takes away the board's sideways speed across the
/// rail, so the board comes down on the rail instead of sliding over it. Together with the lock-on's steering
/// (<see cref="GrindAssist"/>), which takes over once this move is used up, it reaches rails up to its own move plus
/// the lock-on's bend away; a rail farther than both can manage in time is left alone. Metres, z up.
/// </summary>
internal sealed class GrindAirAssist
{
	/// <summary>Seconds before the trucks come down to a rail's height that the move starts even while rising.</summary>
	internal const float Window = 16f / 60f;

	/// <summary>Fastest sideways move (m/s): three centimetres a sixtieth of a second.</summary>
	internal const float PerSecond = 1.8f;

	/// <summary>Farthest (m) a board is moved in one jump with the measured handling.</summary>
	internal const float Total = 0.2f;

	// Largest angle (radians) between the board's travel and a rail it is moved onto.
	private const float Heading = 0.524f;

	// How quickly (seconds) the speed across the rail is taken away.
	private const float Settle = 0.03f;

	private float _used;
	private Vector3? _landing;

	/// <summary>
	/// After <see cref="Move"/>: how far (metres) the rail it is moving the board onto lies beyond what is left of the
	/// jump's move, so the move alone will come down short of it (zero when it reaches, or with no rail). The lock-on's
	/// steering (<see cref="GrindAssist"/>) closes that much meanwhile; see <see cref="Covering"/>.
	/// </summary>
	internal float Short { get; private set; }

	/// <summary>After <see cref="Move"/>: how much of the way to the rail (metres) the move still closes, this step's
	/// included: the part the lock-on's steering leaves to it.</summary>
	internal float Covering { get; private set; }

	/// <summary>A new jump: the whole move is available again, and no rail is yet below it.</summary>
	internal void Reset()
	{
		_used = 0f;
		_landing = null;
	}

	/// <summary>
	/// As <see cref="LandingRail"/>, but once a flight has found the rail it comes down on, kept until the jump ends
	/// (<see cref="Reset"/>): close to the rail the move's reach shrinks, but the board still comes down on it.
	/// </summary>
	internal Vector3? Landing( Vector3 position, float lowest, Vector3 velocity, ReadOnlySpan<RailSegment> rails,
		float gravity, float total = Total ) => _landing ??= LandingRail( position, lowest, velocity, rails, gravity, total );

	/// <summary>
	/// The sideways move this step onto the rail the board will come down beside, and the change of velocity taking
	/// away its speed across that rail; both zero with no rail in reach. <paramref name="lowest"/> is the height of the
	/// board's lowest truck; <paramref name="total"/> is the most it may move in one jump. With the deck's
	/// <paramref name="heading"/> turned nearer across the rail than along it (a slide), the board is moved so the rail
	/// comes under the nearest of the deck's middle, nose or tail: a boardslide, noseslide or tailslide.
	/// </summary>
	/// <param name="spin">The deck's turn rate about world up (radians per second), from which the landing alignment
	/// takes the heading it comes down at (<see cref="AirControl.LandingHeading"/>).</param>
	/// <param name="tipReach">How far from the deck's middle (metres) an end slide rests on the rail: the board's own
	/// (<see cref="GrindControl.Reach"/>), where the slide pins it. The default board's fixed reach put a smaller deck's
	/// end past its own end, so it never locked on and came down through the rail.</param>
	internal (Vector3 Move, Vector3 Velocity) Move( Vector3 position, float lowest, Vector3 velocity, ReadOnlySpan<RailSegment> rails,
		float gravity, float step, float total = Total, Vector3? heading = null, float spin = 0f, float tipReach = GrindControl.TipReach )
	{
		var left = total - _used;
		Short = 0f;
		Covering = 0f;
		if ( gravity <= 0f || step <= 0f || left <= 0f )
			return (Vector3.Zero, Vector3.Zero);
		var best = Vector3.Zero;
		var stop = Vector3.Zero;
		var bestOffset = float.MaxValue;
		foreach ( var rail in rails )
		{
			if ( !Arrival( rail, position, lowest, velocity, gravity, heading, spin, tipReach, out var flight ) )
				continue;
			// While rising, only once the trucks are about to come down to the rail's height.
			if ( velocity.Z > 0f && flight.Time > Window )
				continue;
			// Only a rail this move and the lock-on's bend can still reach together before the trucks come down.
			var distance = MathF.Abs( flight.Offset );
			if ( distance > Reachable( flight, left, step ) || distance >= bestOffset )
				continue;
			bestOffset = distance;
			// An even rate that closes the gap by the time the trucks reach the rail's height, within what is left.
			var amount = Math.Min( Math.Min( distance * step / MathF.Max( flight.Time, step ), PerSecond * step ), left );
			best = flight.Side * (-MathF.Sign( flight.Offset ) * amount);
			stop = -flight.Side * (flight.Across * Math.Min( 1f, step / Settle ));
			Covering = MathF.Min( distance, left );
			Short = MathF.Max( distance - left, 0f );
		}
		_used += best.Length();
		return (best, stop);
	}

	/// <summary>Direction of the rail the board's flight comes down on or just beside (within what a jump's move and
	/// the lock-on can reach), or null.</summary>
	internal static Vector3? LandingRail( Vector3 position, float lowest, Vector3 velocity, ReadOnlySpan<RailSegment> rails,
		float gravity, float total = Total )
	{
		if ( gravity <= 0f )
			return null;
		Vector3? found = null;
		var nearest = float.MaxValue;
		foreach ( var rail in rails )
		{
			if ( !Arrival( rail, position, lowest, velocity, gravity, null, 0f, GrindControl.TipReach, out var flight ) )
				continue;
			var distance = MathF.Abs( flight.Offset );
			if ( distance > Reachable( flight, total, 0f ) || distance >= nearest )
				continue;
			nearest = distance;
			found = rail.Direction;
		}
		return found;
	}

	// How the flight comes down by a rail: the time until the trucks reach its height, the rail's horizontal direction
	// and side, the travel along and across it, and how far to its side (the deck's middle, or for a slide its nearest
	// end) arrives.
	private readonly record struct Flight( float Time, Vector3 Side, float Forward, float Across, float Offset );

	private static bool Arrival( in RailSegment rail, Vector3 position, float lowest, Vector3 velocity, float gravity,
		Vector3? heading, float spin, float tipReach, out Flight flight )
	{
		flight = default;
		var travel = new Vector3( velocity.X, velocity.Y, 0f );
		if ( !rail.IsValid || travel.LengthSquared() < 0.01f )
			return false;
		var drop = lowest - 0.5f * (rail.Start.Z + rail.End.Z);
		if ( drop <= 0f )
			return false;
		var time = (velocity.Z + MathF.Sqrt( velocity.Z * velocity.Z + 2f * gravity * drop )) / gravity;
		var along3 = rail.Direction;
		var along = Vector3.Normalize( new Vector3( along3.X, along3.Y, 0f ) );
		var side = new Vector3( -along.Y, along.X, 0f );
		var forward = Vector3.Dot( travel, along );
		var across = Vector3.Dot( travel, side );
		if ( MathF.Abs( across ) > MathF.Abs( forward ) * MathF.Tan( Heading ) )
			return false;
		// Where it would come down, along the rail and to its side.
		var arrival = position + travel * time;
		var reach = Vector3.Dot( new Vector3( arrival.X - rail.Start.X, arrival.Y - rail.Start.Y, 0f ), along );
		if ( reach < 0f || reach > rail.Length )
			return false;
		var offset = Vector3.Dot( new Vector3( arrival.X - rail.Start.X, arrival.Y - rail.Start.Y, 0f ), side );
		if ( heading is { } deck && new Vector3( deck.X, deck.Y, 0f ).LengthSquared() > 0.0001f )
		{
			// The heading it comes down at, as the landing alignment takes it.
			var at = AirControl.LandingHeading( MathF.Atan2( deck.Y, deck.X ), spin );
			offset = SlideOffset( offset, Vector3.Dot( new Vector3( MathF.Cos( at ), MathF.Sin( at ), 0f ), side ), tipReach );
		}
		flight = new( time, side, forward, across, offset );
		return true;
	}

	// For a deck turned off the rail's line by more than the landing alignment's slide turn, the offset of whichever of
	// its middle, nose or tail comes down nearest the rail; otherwise the middle's.
	private static float SlideOffset( float middle, float acrossShare, float tipReach )
	{
		if ( MathF.Abs( acrossShare ) <= MathF.Sin( AirControl.SlideTurn ) )
			return middle;
		var nose = middle + tipReach * acrossShare;
		var tail = middle - tipReach * acrossShare;
		var end = MathF.Abs( nose ) < MathF.Abs( tail ) ? nose : tail;
		return MathF.Abs( end ) < MathF.Abs( middle ) ? end : middle;
	}

	private static float Reachable( in Flight flight, float left, float step ) =>
		MathF.Min( left, PerSecond * MathF.Max( flight.Time, step ) )
			+ MathF.Tan( GrindAssist.MaximumBend ) * MathF.Abs( flight.Forward ) * flight.Time;
}
