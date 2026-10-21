using System.Collections.Generic;

namespace SkateMotion.Core.BoardModel;

using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Where a mesh crosses the plane across it at a point along X (each triangle edge passing through the plane gives the
/// (Y, Z) at which it does), and the heights at which a vertical line meets it. Both hold for any tessellation, unlike
/// sampling vertices near the plane or the line.
/// </summary>
internal static class MeshSection
{
	internal static List<Vector2> Across( Vector3[] points, int[] triangles, float x )
	{
		var crossings = new List<Vector2>();
		for ( var t = 0; t + 2 < triangles.Length; t += 3 )
		{
			for ( var edge = 0; edge < 3; edge++ )
			{
				var a = points[ triangles[ t + edge ] ];
				var b = points[ triangles[ t + (edge + 1) % 3 ] ];
				if ( (a.X - x) * (b.X - x) >= 0f )
					continue;
				var share = (x - a.X) / (b.X - a.X);
				crossings.Add( new Vector2( a.Y + (b.Y - a.Y) * share, a.Z + (b.Z - a.Z) * share ) );
			}
		}
		return crossings;
	}

	/// <summary>The heights at which the vertical line through (x, y) passes through the mesh's triangles.</summary>
	internal static List<float> Down( Vector3[] points, int[] triangles, float x, float y )
	{
		var heights = new List<float>();
		for ( var t = 0; t + 2 < triangles.Length; t += 3 )
		{
			var a = points[ triangles[ t ] ];
			var b = points[ triangles[ t + 1 ] ];
			var c = points[ triangles[ t + 2 ] ];
			var area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
			if ( MathF.Abs( area ) < 1e-12f )
				continue;
			var u = ((b.X - x) * (c.Y - y) - (c.X - x) * (b.Y - y)) / area;
			var v = ((c.X - x) * (a.Y - y) - (a.X - x) * (c.Y - y)) / area;
			if ( u < 0f || v < 0f || u + v > 1f )
				continue;
			heights.Add( a.Z * u + b.Z * v + c.Z * (1f - u - v) );
		}
		return heights;
	}
}
