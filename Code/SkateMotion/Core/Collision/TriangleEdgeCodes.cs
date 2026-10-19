namespace SkateMotion.Core.Collision;

using System.Collections.Generic;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Authored edge codes for a triangle mesh that has none, derived from which triangles share each edge. Each triangle
/// gets three bytes, for its edges (A,B), (B,C) and (C,A): the low five bits pick the smallest cosine a contact normal
/// may make with the face there (1 - pi^2 / (8 * 2^code)), 0x20 marks a convex edge and 0x40 restricts contacts on
/// the vertex that starts the edge (A, B, C). Without them every internal edge of a smooth or faceted surface reads
/// as an open boundary, so a rolling wheel meets each seam as a small wall.
/// </summary>
internal static class TriangleEdgeCodes
{
	internal const byte Convex = 0x20;
	internal const byte VertexRestricted = 0x40;
	// The descriptor shifts 8 left by the code in 32-bit integers; 27 is the largest that stays positive.
	private const int MaximumCode = 27;

	// Open or shared by more than two faces: contacts there keep their full range, as an untagged edge does.
	private const byte Open = Convex;

	/// <param name="a">First corner of each triangle.</param>
	/// <param name="b">Second corner of each triangle.</param>
	/// <param name="c">Third corner of each triangle.</param>
	/// <param name="weld">Corners closer than this are the same point.</param>
	internal static byte[] Compute( ReadOnlySpan<Vector3> a, ReadOnlySpan<Vector3> b, ReadOnlySpan<Vector3> c, float weld )
	{
		if ( a.Length != b.Length || a.Length != c.Length )
			throw new ArgumentException( "Each triangle needs three corners." );
		if ( !(weld > 0f) )
			throw new ArgumentOutOfRangeException( nameof( weld ) );
		var count = a.Length;
		var corners = new int[ count * 3 ];
		var points = new Dictionary<(long, long, long), int>();
		var positions = new List<Vector3>();
		for ( var i = 0; i < count; ++i )
		{
			corners[ i * 3 ] = Weld( a[ i ], weld, points, positions );
			corners[ i * 3 + 1 ] = Weld( b[ i ], weld, points, positions );
			corners[ i * 3 + 2 ] = Weld( c[ i ], weld, points, positions );
		}
		var normals = new Vector3[ count ];
		for ( var i = 0; i < count; ++i )
		{
			var normal = Vector3.Cross( b[ i ] - a[ i ], c[ i ] - a[ i ] );
			normals[ i ] = normal.LengthSquared() > 0f ? Vector3.Normalize( normal ) : Vector3.Zero;
		}
		var edges = new Dictionary<(int, int), List<int>>();
		for ( var i = 0; i < count * 3; ++i )
		{
			var key = Key( corners[ i ], corners[ Next( i ) ] );
			if ( key.Item1 == key.Item2 )
				continue;
			if ( !edges.TryGetValue( key, out var sides ) )
				edges[ key ] = sides = new List<int>( 2 );
			sides.Add( i );
		}

		var grid = new SurfaceGrid( a, b, c, normals );
		var codes = new byte[ count * 3 ];
		// A corner stays open for vertex contacts while any edge through it is open or a real ridge.
		var openCorner = new bool[ positions.Count ];
		for ( var i = 0; i < count * 3; ++i )
		{
			var face = i / 3;
			var key = Key( corners[ i ], corners[ Next( i ) ] );
			var degenerate = key.Item1 == key.Item2;
			List<int> sides = null;
			if ( !degenerate )
				edges.TryGetValue( key, out sides );
			if ( degenerate || sides is not { Count: 2 } )
			{
				// An open edge resting on another surface (a ramp placed on a floor) joins that surface instead.
				var start = positions[ corners[ i ] ];
				var end = positions[ corners[ Next( i ) ] ];
				var support = degenerate || sides is { Count: > 2 } ? -1
					: grid.Support( start, end, normals[ face ], face, a, b, c, normals );
				if ( support < 0 )
				{
					codes[ i ] = Open;
					openCorner[ corners[ i ] ] = openCorner[ corners[ Next( i ) ] ] = true;
					continue;
				}
				// The supporting surface continues beyond the edge, away from this face.
				var opposite = positions[ corners[ face * 3 + (i % 3 + 2) % 3 ] ];
				var along = Vector3.Normalize( end - start );
				var inward = opposite - start - along * Vector3.Dot( opposite - start, along );
				var outward = -Vector3.Normalize( inward - normals[ support ] * Vector3.Dot( inward, normals[ support ] ) );
				var rested = Math.Clamp( Vector3.Dot( normals[ face ], normals[ support ] ), -1f, 1f );
				var ridge = Vector3.Dot( outward, normals[ face ] ) <= 0f;
				codes[ i ] = (byte)(Code( rested ) | (ridge ? Convex : 0));
				openCorner[ corners[ i ] ] = openCorner[ corners[ Next( i ) ] ] = true;
				continue;
			}
			var other = sides[ 0 ] == i ? sides[ 1 ] : sides[ 0 ];
			var neighbour = other / 3;
			var cosine = Math.Clamp( Vector3.Dot( normals[ face ], normals[ neighbour ] ), -1f, 1f );
			// The neighbour's far corner below this face's plane means the surface folds away: a ridge.
			var far = positions[ corners[ neighbour * 3 + (other % 3 + 2) % 3 ] ];
			var bend = Vector3.Dot( far - positions[ corners[ i ] ], normals[ face ] );
			var convex = bend <= weld;
			codes[ i ] = (byte)(Code( cosine ) | (convex ? Convex : 0));
			if ( convex && cosine < RidgeCosine )
				openCorner[ corners[ i ] ] = openCorner[ corners[ Next( i ) ] ] = true;
		}
		for ( var i = 0; i < count * 3; ++i )
		{
			if ( !openCorner[ corners[ i ] ] )
				codes[ i ] |= VertexRestricted;
		}
		return codes;
	}

	// An open edge closer than this (metres) to another surface rests on it.
	internal const float RestingGap = 0.006f;

	// Edges bending less than about 2.5 degrees count as smooth for their corners.
	private const float RidgeCosine = 0.999f;

	/// <summary>The cosine an edge code stands for.</summary>
	internal static float Cosine( int code ) => 1f - MathF.PI * MathF.PI / (8f * (1 << Math.Clamp( code, 0, MaximumCode )));

	// The largest code whose cosine does not exceed the edge's, so every normal between the two faces is kept.
	private static int Code( float cosine )
	{
		var code = 0;
		while ( code < MaximumCode && Cosine( code + 1 ) <= cosine )
			code++;
		return code;
	}

	private static int Next( int corner ) => corner % 3 == 2 ? corner - 2 : corner + 1;

	private static (int, int) Key( int first, int second ) => first < second ? (first, second) : (second, first);

	private static int Weld( Vector3 point, float weld, Dictionary<(long, long, long), int> points, List<Vector3> positions )
	{
		var key = ((long)MathF.Round( point.X / weld ), (long)MathF.Round( point.Y / weld ), (long)MathF.Round( point.Z / weld ));
		if ( points.TryGetValue( key, out var index ) )
			return index;
		points[ key ] = positions.Count;
		positions.Add( point );
		return positions.Count - 1;
	}

	// Triangles bucketed by the cells their bounds cover, to find what an open edge rests on without a full scan.
	private sealed class SurfaceGrid
	{
		private const float Cell = 1f;
		private readonly Dictionary<(int, int, int), List<int>> _cells = new();

		internal SurfaceGrid( ReadOnlySpan<Vector3> a, ReadOnlySpan<Vector3> b, ReadOnlySpan<Vector3> c, Vector3[] normals )
		{
			for ( var i = 0; i < a.Length; ++i )
			{
				if ( normals[ i ] == Vector3.Zero )
					continue;
				var low = Cells( Vector3.Min( a[ i ], Vector3.Min( b[ i ], c[ i ] ) ) - new Vector3( RestingGap ) );
				var high = Cells( Vector3.Max( a[ i ], Vector3.Max( b[ i ], c[ i ] ) ) + new Vector3( RestingGap ) );
				// Very large faces would fill many cells; they are checked against every edge instead.
				if ( (long)(high.Item1 - low.Item1 + 1) * (high.Item2 - low.Item2 + 1) * (high.Item3 - low.Item3 + 1) > 4096 )
				{
					_large.Add( i );
					continue;
				}
				for ( var x = low.Item1; x <= high.Item1; ++x )
				for ( var y = low.Item2; y <= high.Item2; ++y )
				for ( var z = low.Item3; z <= high.Item3; ++z )
					Add( (x, y, z), i );
			}
		}

		private readonly List<int> _large = new();

		private void Add( (int, int, int) cell, int triangle )
		{
			if ( !_cells.TryGetValue( cell, out var list ) )
				_cells[ cell ] = list = new List<int>( 4 );
			list.Add( triangle );
		}

		private static (int, int, int) Cells( Vector3 point ) =>
			((int)MathF.Floor( point.X / Cell ), (int)MathF.Floor( point.Y / Cell ), (int)MathF.Floor( point.Z / Cell ));

		/// <summary>A triangle facing the same way whose surface holds the whole edge within the resting gap, or -1.</summary>
		internal int Support( Vector3 start, Vector3 end, Vector3 normal, int self, ReadOnlySpan<Vector3> a,
			ReadOnlySpan<Vector3> b, ReadOnlySpan<Vector3> c, Vector3[] normals )
		{
			var middle = (start + end) * 0.5f;
			var best = -1;
			var bestGap = float.MaxValue;
			_cells.TryGetValue( Cells( middle ), out var local );
			for ( var pass = 0; pass < 2; ++pass )
			{
				var candidates = pass == 0 ? local : _large;
				if ( candidates is null )
					continue;
				foreach ( var other in candidates )
				{
					if ( other == self || Vector3.Dot( normals[ other ], normal ) <= 0f )
						continue;
					var n = normals[ other ];
					var gap = MathF.Max( MathF.Abs( Vector3.Dot( start - a[ other ], n ) ),
						MathF.Abs( Vector3.Dot( end - a[ other ], n ) ) );
					if ( gap > RestingGap || gap >= bestGap || !Inside( middle, a[ other ], b[ other ], c[ other ], n ) )
						continue;
					best = other;
					bestGap = gap;
				}
			}
			return best;
		}

		private static bool Inside( Vector3 point, Vector3 a, Vector3 b, Vector3 c, Vector3 n ) =>
			Vector3.Dot( Vector3.Cross( b - a, point - a ), n ) >= 0f
			&& Vector3.Dot( Vector3.Cross( c - b, point - b ), n ) >= 0f
			&& Vector3.Dot( Vector3.Cross( a - c, point - c ), n ) >= 0f;
	}
}
