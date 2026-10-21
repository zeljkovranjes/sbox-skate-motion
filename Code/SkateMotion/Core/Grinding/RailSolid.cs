namespace SkateMotion.Core.Grinding;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A rail's solid as an oriented box in metres, its local X along the rail and local Z the rail's up: what a board
/// rests on top of and what nothing shown may pass into. <see cref="Lift(Vector3, Vector3, Vector3)"/> says how far a
/// drawn shape must rise along the rail's up to sit on its top instead of in it.
/// </summary>
/// <param name="Centre">Centre of the box.</param>
/// <param name="Rotation">The box's turn: its local X runs along the rail, local Z is the rail's up.</param>
/// <param name="Half">Half the box's size along its own axes.</param>
internal readonly record struct RailSolid( Vector3 Centre, Quaternion Rotation, Vector3 Half )
{
	/// <summary>
	/// Extra depth (metres) a shape inside the rail from one of its sides or ends may count beyond how far in from that
	/// side it is: a shape over the rail's top near its edge or end is lifted fully, one coming in from beside the rail
	/// or past its end only gradually.
	/// </summary>
	internal const float SideReach = 0.025f;

	/// <summary>The rail's up: the box's local Z.</summary>
	internal Vector3 Up => Vector3.Transform( Vector3.UnitZ, Rotation );

	internal bool IsValid => float.IsFinite( Centre.X + Centre.Y + Centre.Z + Half.X + Half.Y + Half.Z )
		&& Half.X > 0f && Half.Y > 0f && Half.Z > 0f;

	/// <summary>True when a point lies on the middle line of the box's top, within <paramref name="tolerance"/> metres.</summary>
	internal bool OnTopMiddle( Vector3 point, float tolerance )
	{
		var local = Local( point );
		return MathF.Abs( local.X ) <= Half.X + tolerance && MathF.Abs( local.Y ) <= tolerance
			&& MathF.Abs( local.Z - Half.Z ) <= tolerance;
	}

	/// <summary>
	/// True when a point is on the rail's top or at most <paramref name="reach"/> metres above it, over the rail or no
	/// further out from its sides than <paramref name="aside"/>: where a board's truck is when it rests on the rail or is
	/// about to come down onto it.
	/// </summary>
	internal bool AtTop( Vector3 point, float reach, float aside )
	{
		var local = Local( point );
		return MathF.Abs( local.X ) <= Half.X && MathF.Abs( local.Y ) <= Half.Y + aside
			&& local.Z >= Half.Z - SideReach && local.Z <= Half.Z + reach;
	}

	/// <summary>
	/// The move (metres, world) that takes a ball of <paramref name="radius"/> at <paramref name="point"/> out of the box:
	/// up over its top for a ball belonging to something above the rail (<paramref name="fromAbove"/>: a leg whose hip is
	/// over the top), otherwise out past the nearer side; never down, and zero when the ball is clear of it or past an end.
	/// The way out follows what the ball belongs to, not which way is shorter, so it does not flip between up and sideways
	/// as the ball moves.
	/// </summary>
	internal Vector3 Exit( Vector3 point, float radius, bool fromAbove )
	{
		var local = Local( point );
		if ( MathF.Abs( local.X ) > Half.X || MathF.Abs( local.Y ) >= Half.Y + radius || local.Z >= Half.Z + radius
			|| local.Z <= -Half.Z - radius )
			return Vector3.Zero;
		var side = Half.Y + radius - MathF.Abs( local.Y );
		var move = fromAbove ? new Vector3( 0f, 0f, Half.Z + radius - local.Z ) : new Vector3( 0f, local.Y < 0f ? -side : side, 0f );
		return Vector3.Transform( move, Rotation );
	}

	/// <summary>True when a point is higher than the box's top.</summary>
	internal bool AboveTop( Vector3 point ) => Local( point ).Z >= Half.Z;

	/// <summary>Distance from a point to the box (metres); negative inside.</summary>
	internal float Distance( Vector3 point )
	{
		var local = Local( point );
		var outside = Vector3.Max( Vector3.Abs( local ) - Half, Vector3.Zero );
		if ( outside.LengthSquared() > 0f )
			return outside.Length();
		var inside = Half - Vector3.Abs( local );
		return -MathF.Min( inside.X, MathF.Min( inside.Y, inside.Z ) );
	}

	/// <summary>
	/// How far a triangle must rise along <see cref="Up"/> to clear the box (metres), zero when it is clear of it. A
	/// triangle reaching down through the top rises until its lowest point over the rail is level with the top, however
	/// deep it reaches; one that comes in through a side counts no deeper than it is in from that side plus
	/// <see cref="SideReach"/>. Near an end of the rail a point counts no deeper than it is in from that end plus the
	/// same reach. So the lift grows and shrinks continuously as a shape comes down onto the rail, moves in from beside
	/// it or passes its end, and never by more than the reach at once.
	/// </summary>
	internal float Lift( Vector3 a, Vector3 b, Vector3 c ) => Lift( a, b, c, null );

	/// <summary>
	/// As <see cref="Lift(Vector3, Vector3, Vector3)"/>, for a triangle of a shape that is known to reach down through
	/// the rail's top (<paramref name="fromAbove"/> true: every part of it over the rail rises by its full depth below the
	/// top, even one lying wholly inside the rail) or known not to (false); null decides from the triangle alone.
	/// </summary>
	internal float Lift( Vector3 a, Vector3 b, Vector3 c, bool? fromAbove )
	{
		Span<Vector3> corners = stackalloc Vector3[ 3 ];
		Span<Vector3> polygon = stackalloc Vector3[ 12 ];
		Span<Vector3> clipped = stackalloc Vector3[ 12 ];
		var count = OverRail( a, b, c, corners, polygon, clipped, out var highest, out var lowest );
		if ( count == 0 || lowest >= Half.Z )
			return 0f;
		var u = corners[ 1 ] - corners[ 0 ];
		var v = corners[ 2 ] - corners[ 0 ];
		// Reaching down through the top: the depth below the top, wherever it is over the rail, fading at the ends.
		if ( fromAbove ?? highest >= Half.Z )
			return Best( polygon[ ..count ], corners[ 0 ], u, v, FromAbove, true );
		// Otherwise only the part inside the box counts, no deeper than it is in from a side plus the side reach.
		for ( var face = 4; face < 6 && count > 0; face++ )
		{
			count = Clip( polygon, count, clipped, face );
			clipped[ ..count ].CopyTo( polygon );
		}
		return count == 0 ? 0f : Best( polygon[ ..count ], corners[ 0 ], u, v, FromAnySide, false );
	}

	/// <summary>True when, over the rail, part of a triangle lies above the rail's top and part below it: the shape it
	/// belongs to reaches down through the top.</summary>
	internal bool ReachesThroughTop( Vector3 a, Vector3 b, Vector3 c )
	{
		Span<Vector3> corners = stackalloc Vector3[ 3 ];
		Span<Vector3> polygon = stackalloc Vector3[ 12 ];
		Span<Vector3> clipped = stackalloc Vector3[ 12 ];
		return OverRail( a, b, c, corners, polygon, clipped, out var highest, out var lowest ) > 0
			&& highest >= Half.Z && lowest < Half.Z;
	}

	// The part of a triangle over the rail (inside its sides and ends, at any height), in the box's frame: the polygon's
	// corner count, with the triangle's own corners and the polygon's highest and lowest points.
	private int OverRail( Vector3 a, Vector3 b, Vector3 c, Span<Vector3> corners, Span<Vector3> polygon,
		Span<Vector3> clipped, out float highest, out float lowest )
	{
		corners[ 0 ] = Local( a );
		corners[ 1 ] = Local( b );
		corners[ 2 ] = Local( c );
		highest = float.MinValue;
		lowest = float.MaxValue;
		var least = Vector3.Min( corners[ 0 ], Vector3.Min( corners[ 1 ], corners[ 2 ] ) );
		var most = Vector3.Max( corners[ 0 ], Vector3.Max( corners[ 1 ], corners[ 2 ] ) );
		if ( least.X > Half.X || least.Y > Half.Y || least.Z > Half.Z || most.X < -Half.X || most.Y < -Half.Y )
			return 0;
		corners.CopyTo( polygon );
		var count = 3;
		for ( var face = 0; face < 4 && count > 0; face++ )
		{
			count = Clip( polygon, count, clipped, face );
			clipped[ ..count ].CopyTo( polygon );
		}
		for ( var i = 0; i < count; i++ )
		{
			highest = MathF.Max( highest, polygon[ i ].Z );
			lowest = MathF.Min( lowest, polygon[ i ].Z );
		}
		return count;
	}

	// The measures a point's lift is the least of: from above the depth below the top and in from each end, from beside
	// the rail also the depth in from each side plus the side reach.
	private static readonly int[] FromAbove = { 0, 3, 4 };
	private static readonly int[] FromAnySide = { 0, 1, 2, 3, 4 };

	// The largest over a convex polygon of the least of some linear measures: at a corner, where an edge crosses the line
	// two measures are equal on, or where three are equal inside it (in the plane of the triangle a + s u + t v).
	private float Best( ReadOnlySpan<Vector3> polygon, Vector3 a, Vector3 u, Vector3 v, int[] measures, bool overRail )
	{
		var best = 0f;
		for ( var i = 0; i < polygon.Length; i++ )
		{
			var p = polygon[ i ];
			var q = polygon[ (i + 1) % polygon.Length ];
			best = MathF.Max( best, Reach( p, measures ) );
			for ( var f = 0; f < measures.Length; f++ )
			for ( var g = f + 1; g < measures.Length; g++ )
			{
				var start = Measure( p, measures[ f ] ) - Measure( p, measures[ g ] );
				var end = Measure( q, measures[ f ] ) - Measure( q, measures[ g ] );
				if ( start * end >= 0f )
					continue;
				best = MathF.Max( best, Reach( Vector3.Lerp( p, q, start / (start - end) ), measures ) );
			}
		}
		for ( var f = 0; f < measures.Length; f++ )
		for ( var g = f + 1; g < measures.Length; g++ )
		for ( var h = g + 1; h < measures.Length; h++ )
		{
			var (mf, mg, mh) = (measures[ f ], measures[ g ], measures[ h ]);
			var a1 = Slope( u, mf ) - Slope( u, mg );
			var b1 = Slope( v, mf ) - Slope( v, mg );
			var c1 = Measure( a, mg ) - Measure( a, mf );
			var a2 = Slope( u, mf ) - Slope( u, mh );
			var b2 = Slope( v, mf ) - Slope( v, mh );
			var c2 = Measure( a, mh ) - Measure( a, mf );
			var determinant = a1 * b2 - a2 * b1;
			if ( MathF.Abs( determinant ) < 1e-12f )
				continue;
			var s = (c1 * b2 - c2 * b1) / determinant;
			var t = (a1 * c2 - a2 * c1) / determinant;
			var point = a + u * s + v * t;
			if ( s < 0f || t < 0f || s + t > 1f || !Inside( point, overRail ) )
				continue;
			best = MathF.Max( best, Reach( point, measures ) );
		}
		return best;
	}

	private Vector3 Local( Vector3 point ) => Vector3.Transform( point - Centre, Quaternion.Conjugate( Rotation ) );

	// Inside the box, or with overRail anywhere over or under it (inside its sides and ends).
	private bool Inside( Vector3 local, bool overRail ) => MathF.Abs( local.X ) <= Half.X && MathF.Abs( local.Y ) <= Half.Y
		&& (overRail || MathF.Abs( local.Z ) <= Half.Z);

	// The lift a point asks for: the least of its measures.
	private float Reach( Vector3 local, int[] measures )
	{
		var reach = float.MaxValue;
		foreach ( var measure in measures )
			reach = MathF.Min( reach, Measure( local, measure ) );
		return MathF.Max( reach, 0f );
	}

	// Measure 0 is the depth below the top; 1 to 4 the depth in from each side and end plus the side reach.
	private float Measure( Vector3 local, int face ) => face switch
	{
		0 => Half.Z - local.Z,
		1 => Half.Y - local.Y + SideReach,
		2 => Half.Y + local.Y + SideReach,
		3 => Half.X - local.X + SideReach,
		_ => Half.X + local.X + SideReach
	};

	// How a measure changes along a direction.
	private static float Slope( Vector3 direction, int face ) => face switch
	{
		0 => -direction.Z,
		1 => -direction.Y,
		2 => direction.Y,
		3 => -direction.X,
		_ => direction.X
	};

	// Keeps the part of a polygon inside one face of the box: faces 0-5 are +X, -X, +Y, -Y, +Z, -Z.
	private int Clip( Span<Vector3> polygon, int count, Span<Vector3> output, int face )
	{
		var written = 0;
		for ( var i = 0; i < count && written + 1 < output.Length; i++ )
		{
			var p = polygon[ i ];
			var q = polygon[ (i + 1) % count ];
			var inP = Inward( p, face );
			var inQ = Inward( q, face );
			if ( inP >= 0f )
				output[ written++ ] = p;
			if ( inP >= 0f != inQ >= 0f )
				output[ written++ ] = Vector3.Lerp( p, q, inP / (inP - inQ) );
		}
		return written;
	}

	// How far inside one face of the box a local point is.
	private float Inward( Vector3 local, int face ) => face switch
	{
		0 => Half.X - local.X,
		1 => Half.X + local.X,
		2 => Half.Y - local.Y,
		3 => Half.Y + local.Y,
		4 => Half.Z - local.Z,
		_ => Half.Z + local.Z
	};
}
