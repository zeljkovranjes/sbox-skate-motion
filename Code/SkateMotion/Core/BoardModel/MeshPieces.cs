using System.Collections.Generic;

namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The connected pieces of a triangle mesh: triangles sharing a vertex, or a vertex position (a seam splits vertices
/// that sit in one place), belong to one piece. Each piece's box and triangle count are kept.
/// </summary>
internal sealed class MeshPieces
{
	internal MeshPieces( Vector3[] points, int[] triangles, float weld )
	{
		var parent = new int[ points.Length ];
		for ( var i = 0; i < parent.Length; i++ )
			parent[ i ] = i;
		var cells = new Dictionary<(long, long, long), int>();
		var scale = 1f / MathF.Max( weld, 1e-9f );
		for ( var i = 0; i < points.Length; i++ )
		{
			var key = ((long)MathF.Round( points[ i ].X * scale ), (long)MathF.Round( points[ i ].Y * scale ),
				(long)MathF.Round( points[ i ].Z * scale ));
			if ( cells.TryGetValue( key, out var first ) )
				Join( parent, first, i );
			else
				cells[ key ] = i;
		}
		for ( var t = 0; t + 2 < triangles.Length; t += 3 )
		{
			Join( parent, triangles[ t ], triangles[ t + 1 ] );
			Join( parent, triangles[ t ], triangles[ t + 2 ] );
		}
		var number = new Dictionary<int, int>();
		OfTriangle = new int[ triangles.Length / 3 ];
		var mins = new List<Vector3>();
		var maxs = new List<Vector3>();
		var counts = new List<int>();
		for ( var t = 0; t < OfTriangle.Length; t++ )
		{
			var root = Root( parent, triangles[ t * 3 ] );
			if ( !number.TryGetValue( root, out var piece ) )
			{
				piece = number.Count;
				number[ root ] = piece;
				mins.Add( new Vector3( float.MaxValue ) );
				maxs.Add( new Vector3( float.MinValue ) );
				counts.Add( 0 );
			}
			OfTriangle[ t ] = piece;
			counts[ piece ]++;
			for ( var corner = 0; corner < 3; corner++ )
			{
				var point = points[ triangles[ t * 3 + corner ] ];
				mins[ piece ] = Vector3.Min( mins[ piece ], point );
				maxs[ piece ] = Vector3.Max( maxs[ piece ], point );
			}
		}
		Mins = mins.ToArray();
		Maxs = maxs.ToArray();
		Triangles = counts.ToArray();
	}

	/// <summary>The piece of each triangle.</summary>
	internal int[] OfTriangle { get; }

	internal Vector3[] Mins { get; }

	internal Vector3[] Maxs { get; }

	/// <summary>Triangles in each piece.</summary>
	internal int[] Triangles { get; }

	internal int Count => Triangles.Length;

	internal Vector3 Centre( int piece ) => (Mins[ piece ] + Maxs[ piece ]) * 0.5f;

	internal Vector3 Size( int piece ) => Maxs[ piece ] - Mins[ piece ];

	private static int Root( int[] parent, int index )
	{
		while ( parent[ index ] != index )
		{
			parent[ index ] = parent[ parent[ index ] ];
			index = parent[ index ];
		}
		return index;
	}

	private static void Join( int[] parent, int a, int b )
	{
		a = Root( parent, a );
		b = Root( parent, b );
		if ( a != b )
			parent[ b ] = a;
	}
}
