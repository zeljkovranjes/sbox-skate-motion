using System.Collections.Generic;

namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The top and underside of a deck at any point across it, from the deck's own triangles in the board's frame
/// (metres): the highest and lowest of the triangles above or below that point. A coarse grid keeps each query to the
/// few triangles near it.
/// </summary>
internal sealed class DeckShape
{
	private const int CellsAlong = 48;
	private const int CellsAcross = 12;

	private readonly Vector3[] _corners;
	private readonly List<int>[] _cells = new List<int>[ CellsAlong * CellsAcross ];
	private readonly Vector3 _min;
	private readonly Vector3 _max;

	/// <param name="corners">Three corners per triangle, in the board's frame.</param>
	internal DeckShape( Vector3[] corners )
	{
		if ( corners is null || corners.Length < 3 || corners.Length % 3 != 0 )
			throw new ArgumentException( "A deck shape needs whole triangles." );
		_corners = corners;
		_min = new Vector3( float.MaxValue );
		_max = new Vector3( float.MinValue );
		foreach ( var corner in corners )
		{
			_min = Vector3.Min( _min, corner );
			_max = Vector3.Max( _max, corner );
		}
		for ( var i = 0; i < _cells.Length; i++ )
			_cells[ i ] = new List<int>();
		for ( var t = 0; t < corners.Length / 3; t++ )
		{
			var a = corners[ t * 3 ];
			var b = corners[ t * 3 + 1 ];
			var c = corners[ t * 3 + 2 ];
			// Triangles standing on edge (the deck's sides) have no top or underside to give.
			if ( MathF.Abs( (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y) ) < 1e-10f )
				continue;
			var (x0, y0) = Cell( Vector3.Min( a, Vector3.Min( b, c ) ) );
			var (x1, y1) = Cell( Vector3.Max( a, Vector3.Max( b, c ) ) );
			for ( var x = x0; x <= x1; x++ )
			{
				for ( var y = y0; y <= y1; y++ )
					_cells[ x * CellsAcross + y ].Add( t );
			}
		}
	}

	/// <summary>The shape of the deck a layout found in a mesh, or null when it found no deck triangles.</summary>
	internal static DeckShape Of( BoardMesh mesh, BoardLayout layout )
	{
		var corners = new List<Vector3>();
		for ( var t = 0; t < layout.Parts.Length; t++ )
		{
			if ( layout.Parts[ t ] != BoardPart.Deck )
				continue;
			for ( var corner = 0; corner < 3; corner++ )
				corners.Add( layout.ToBoard( mesh.Positions[ mesh.Triangles[ t * 3 + corner ] ] ) );
		}
		return corners.Count >= 3 ? new DeckShape( corners.ToArray() ) : null;
	}

	/// <summary>The deck's box in the board's frame.</summary>
	internal (Vector3 Min, Vector3 Max) Bounds => (_min, _max);

	/// <summary>Top and underside heights at a point across the deck, or null off it.</summary>
	internal (float Top, float Bottom)? Surface( float x, float y )
	{
		if ( !float.IsFinite( x ) || !float.IsFinite( y ) || x < _min.X || x > _max.X || y < _min.Y || y > _max.Y )
			return null;
		var (cx, cy) = Cell( new Vector3( x, y, 0f ) );
		var top = float.MinValue;
		var bottom = float.MaxValue;
		foreach ( var t in _cells[ cx * CellsAcross + cy ] )
		{
			if ( HeightAt( t, x, y ) is not { } z )
				continue;
			top = MathF.Max( top, z );
			bottom = MathF.Min( bottom, z );
		}
		return top >= bottom ? (top, bottom) : null;
	}

	// Places along each end of the deck, and across it (shares of its half width), where its underside is tried.
	private const int StrikeSteps = 40;
	private static readonly float[] StrikeAcross = { 0f, 0.4f, -0.4f, 0.8f, -0.8f };

	/// <summary>
	/// Length of a flat deck, <paramref name="thickness"/> thick and centred on the board, whose nose and tail meet level
	/// ground at the same tip of the board onto a truck as this deck's own do. A kicked-up nose or tail meets the ground
	/// later than its length says (a flat box the deck's whole length strikes it early, at a bank's foot or as the
	/// board tips onto a truck); a flat deck's is its own length. Between the wheelbase and the deck's length.
	/// </summary>
	internal float StrikeLength( BoardLayout layout, float thickness )
	{
		if ( layout?.AxleCentres is not { Length: 2 } axles || !(layout.WheelRadius > 0f) )
			return layout?.Length ?? 0f;
		var length = 0f;
		for ( var end = 0; end < 2; end++ )
		{
			var axle = axles[ end ];
			var outward = end == 0 ? 1f : -1f;
			var reach = MathF.Max( (outward > 0f ? _max.X : -_min.X) - outward * axle.X, 0f );
			var flat = -thickness * 0.5f - axle.Z;
			var tip = StrikeTip( axle, outward, reach, layout.WheelRadius, (_max.Y - _min.Y) * 0.5f );
			length += MathF.Abs( axle.X ) + (tip is { } angle ? FlatReach( flat, angle, layout.WheelRadius, reach ) : reach);
		}
		return Math.Clamp( length, layout.Wheelbase, MathF.Max( layout.Length, layout.Wheelbase ) );
	}

	// The least tip of the board onto a truck (radians) at which its underside beyond that truck's axle meets level
	// ground, the wheels' radius under the axle; null where no underside was found there.
	private float? StrikeTip( Vector3 axle, float outward, float reach, float radius, float halfWidth )
	{
		float? least = null;
		for ( var step = 1; step <= StrikeSteps; step++ )
		{
			var along = reach * step / StrikeSteps;
			if ( along <= radius )
				continue;
			foreach ( var share in StrikeAcross )
			{
				var x = axle.X + outward * along - outward * 1e-4f;
				if ( Surface( x, share * halfWidth * 0.95f ) is not { } surface )
					continue;
				var angle = Tip( along, surface.Bottom - axle.Z, radius );
				if ( angle is { } tip && (least is null || tip < least) )
					least = tip;
			}
		}
		return least;
	}

	// How far beyond the axle a flat underside, <paramref name="height"/> above the axle, ends to meet the ground at the
	// given tip; at most the deck's own reach there.
	private static float FlatReach( float height, float tip, float radius, float reach )
	{
		if ( !(height > 0f) || Tip( reach, height, radius ) is not { } farthest || farthest >= tip )
			return reach;
		// The tip a flat end meets the ground at falls as the end reaches further out.
		var near = radius;
		var far = reach;
		for ( var i = 0; i < 40; i++ )
		{
			var middle = (near + far) * 0.5f;
			if ( Tip( middle, height, radius ) is not { } at || at > tip )
				near = middle;
			else
				far = middle;
		}
		return (near + far) * 0.5f;
	}

	// The tip of the board onto its truck (radians) at which a point <paramref name="along"/> beyond the axle and
	// <paramref name="height"/> above it comes down to the ground under the wheels; null if it never can.
	private static float? Tip( float along, float height, float radius )
	{
		var distance = MathF.Sqrt( along * along + height * height );
		if ( distance <= radius )
			return null;
		return MathF.Atan2( height, along ) + MathF.Asin( radius / distance );
	}

	// Height of a triangle above a point, where the point lies within it seen from above.
	private float? HeightAt( int triangle, float x, float y )
	{
		var a = _corners[ triangle * 3 ];
		var b = _corners[ triangle * 3 + 1 ];
		var c = _corners[ triangle * 3 + 2 ];
		var area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
		var u = ((b.X - x) * (c.Y - y) - (c.X - x) * (b.Y - y)) / area;
		var v = ((c.X - x) * (a.Y - y) - (a.X - x) * (c.Y - y)) / area;
		var w = 1f - u - v;
		const float edge = -1e-5f;
		if ( u < edge || v < edge || w < edge )
			return null;
		return a.Z * u + b.Z * v + c.Z * w;
	}

	private (int X, int Y) Cell( Vector3 point )
	{
		var x = (int)((point.X - _min.X) / MathF.Max( _max.X - _min.X, 1e-6f ) * CellsAlong);
		var y = (int)((point.Y - _min.Y) / MathF.Max( _max.Y - _min.Y, 1e-6f ) * CellsAcross);
		return (Math.Clamp( x, 0, CellsAlong - 1 ), Math.Clamp( y, 0, CellsAcross - 1 ));
	}
}
