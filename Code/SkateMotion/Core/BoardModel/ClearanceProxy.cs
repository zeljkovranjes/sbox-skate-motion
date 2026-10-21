using System.Collections.Generic;

namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A cheaper stand-in for a densely modelled part, for keeping it out of rails and the ground: the part's vertices
/// gathered into cells of a grid (one cell per facing, so the two sides of a thin deck never merge), each cell's
/// vertices replaced by one point on the outermost of them along the cell's facing, and the triangles kept between
/// those points. The stand-in lies on or just outside the part's surface (never inside it by more than the curvature
/// within one cell), and has a few thousand triangles where the part had tens of thousands.
/// </summary>
internal static class ClearanceProxy
{
	/// <summary>The stand-in's points and triangles (three point indices each), from a part's points, their normals
	/// and its triangles; cells are <paramref name="cell"/> across (in the points' units).</summary>
	internal static (Vector3[] Points, int[] Triangles) Build( IReadOnlyList<Vector3> points, IReadOnlyList<Vector3> normals,
		IReadOnlyList<int> triangles, float cell )
	{
		if ( points is null || triangles is null || points.Count == 0 || !(cell > 0f) )
			return (System.Array.Empty<Vector3>(), System.Array.Empty<int>());
		var clusterOf = new int[ points.Count ];
		var keys = new Dictionary<(long, long, long, int), int>();
		var members = new List<List<int>>();
		for ( var i = 0; i < points.Count; i++ )
		{
			var p = points[ i ];
			var n = normals is not null && i < normals.Count ? normals[ i ] : Vector3.Zero;
			var facing = (n.X >= 0f ? 1 : 0) | (n.Y >= 0f ? 2 : 0) | (n.Z >= 0f ? 4 : 0);
			var key = ((long)MathF.Floor( p.X / cell ), (long)MathF.Floor( p.Y / cell ), (long)MathF.Floor( p.Z / cell ), facing);
			if ( !keys.TryGetValue( key, out var cluster ) )
			{
				cluster = members.Count;
				keys[ key ] = cluster;
				members.Add( new List<int>() );
			}
			members[ cluster ].Add( i );
			clusterOf[ i ] = cluster;
		}
		// Each cell's facing from the triangles around its points (weighted by their area).
		var facings = new Vector3[ members.Count ];
		for ( var t = 0; t + 2 < triangles.Count; t += 3 )
		{
			var a = points[ triangles[ t ] ];
			var b = points[ triangles[ t + 1 ] ];
			var c = points[ triangles[ t + 2 ] ];
			var area = Vector3.Cross( b - a, c - a );
			for ( var corner = 0; corner < 3; corner++ )
				facings[ clusterOf[ triangles[ t + corner ] ] ] += area;
		}
		var result = new Vector3[ members.Count ];
		for ( var cluster = 0; cluster < members.Count; cluster++ )
		{
			var centre = Vector3.Zero;
			foreach ( var i in members[ cluster ] )
				centre += points[ i ];
			centre /= members[ cluster ].Count;
			var facing = facings[ cluster ];
			if ( facing.LengthSquared() < 1e-20f )
			{
				result[ cluster ] = centre;
				continue;
			}
			facing = Vector3.Normalize( facing );
			var outmost = float.MinValue;
			foreach ( var i in members[ cluster ] )
				outmost = MathF.Max( outmost, Vector3.Dot( points[ i ] - centre, facing ) );
			result[ cluster ] = centre + facing * outmost;
		}
		var kept = new List<int>( triangles.Count / 4 );
		var seen = new HashSet<(int, int, int)>();
		for ( var t = 0; t + 2 < triangles.Count; t += 3 )
		{
			var a = clusterOf[ triangles[ t ] ];
			var b = clusterOf[ triangles[ t + 1 ] ];
			var c = clusterOf[ triangles[ t + 2 ] ];
			if ( a == b || b == c || a == c )
				continue;
			// The same triangle, whichever corner it starts from, is kept once.
			var key = a < b && a < c ? (a, b, c) : b < a && b < c ? (b, c, a) : (c, a, b);
			if ( !seen.Add( key ) )
				continue;
			kept.Add( a );
			kept.Add( b );
			kept.Add( c );
		}
		return (result, kept.ToArray());
	}
}
