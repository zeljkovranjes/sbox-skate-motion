namespace SkateMotion.Core.Collision;

using System.Collections.Generic;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Spatial lookup over one shape's local triangles: a uniform grid whose cells list the triangles whose bounds reach
/// into them. A query returns, in ascending order, exactly the triangles whose bounds overlap a box, so walking them is
/// the same as walking every triangle and skipping those outside the box, at a cost set by what is near the box rather
/// than by the size of the shape. A level's world mesh is one shape of a great many triangles, and every wheel sweep
/// and contact step walked all of them.
/// </summary>
internal sealed class TriangleGrid
{
	/// <summary>Shapes with fewer triangles are walked whole: the grid would cost more than it saves.</summary>
	internal const int MinimumTriangles = 64;

	// Cells are sized to about twice the average triangle's largest side.
	private const float CellTriangleSides = 2f;

	// Cells in the whole grid at most: past this the cells grow, so a shape spread over a large volume stays small.
	private const long MostCells = 1 << 21;

	// A triangle over more cells than this is kept apart and tested by every query, rather than listed in each cell.
	private const int MostCellsPerTriangle = 64;

	// A query over more cells than this walks every triangle instead.
	private const long MostQueryCells = 4096;

	private readonly Vector3[] _minimum;
	private readonly Vector3[] _maximum;
	private readonly Vector3 _origin;
	private readonly float _inverseCell;
	private readonly int _cellsX;
	private readonly int _cellsY;
	private readonly int _cellsZ;

	// Each cell's triangles are _listed[ _starts[ cell ] .. _starts[ cell + 1 ] ].
	private readonly int[] _starts;
	private readonly int[] _listed;
	private readonly int[] _large;
	private readonly int[] _stamp;
	private int _query;

	private TriangleGrid( MeshTriangle[] triangles )
	{
		var count = triangles.Length;
		_minimum = new Vector3[ count ];
		_maximum = new Vector3[ count ];
		_stamp = new int[ count ];
		var low = new Vector3( float.MaxValue );
		var high = new Vector3( -float.MaxValue );
		var averageSide = 0f;
		for ( var i = 0; i < count; i++ )
		{
			var triangle = triangles[ i ];
			_minimum[ i ] = Vector3.Min( triangle.A, Vector3.Min( triangle.B, triangle.C ) );
			_maximum[ i ] = Vector3.Max( triangle.A, Vector3.Max( triangle.B, triangle.C ) );
			low = Vector3.Min( low, _minimum[ i ] );
			high = Vector3.Max( high, _maximum[ i ] );
			var size = _maximum[ i ] - _minimum[ i ];
			averageSide += MathF.Max( size.X, MathF.Max( size.Y, size.Z ) ) / count;
		}
		_origin = low;
		var span = high - low;
		var cell = averageSide * CellTriangleSides;
		if ( !(cell > 0f) || !float.IsFinite( cell ) )
			cell = MathF.Max( MathF.Max( span.X, MathF.Max( span.Y, span.Z ) ), 1f );
		while ( Cells( span, cell ) > MostCells )
			cell *= 1.25f;
		_inverseCell = 1f / cell;
		_cellsX = Across( span.X, cell );
		_cellsY = Across( span.Y, cell );
		_cellsZ = Across( span.Z, cell );

		// Count each cell's triangles, turn the counts into start offsets, then list the triangles in ascending order.
		_starts = new int[ _cellsX * _cellsY * _cellsZ + 1 ];
		var large = new List<int>();
		var listedCount = 0;
		for ( var i = 0; i < count; i++ )
		{
			var (from, to) = Range( _minimum[ i ], _maximum[ i ] );
			if ( (long)(to.X - from.X + 1) * (to.Y - from.Y + 1) * (to.Z - from.Z + 1) > MostCellsPerTriangle )
			{
				large.Add( i );
				continue;
			}
			for ( var z = from.Z; z <= to.Z; z++ )
			for ( var y = from.Y; y <= to.Y; y++ )
			for ( var x = from.X; x <= to.X; x++ )
			{
				_starts[ Index( x, y, z ) + 1 ]++;
				listedCount++;
			}
		}
		for ( var c = 1; c < _starts.Length; c++ )
			_starts[ c ] += _starts[ c - 1 ];
		_listed = new int[ listedCount ];
		var next = new int[ _starts.Length - 1 ];
		Array.Copy( _starts, next, next.Length );
		var largeAt = 0;
		for ( var i = 0; i < count; i++ )
		{
			if ( largeAt < large.Count && large[ largeAt ] == i )
			{
				largeAt++;
				continue;
			}
			var (from, to) = Range( _minimum[ i ], _maximum[ i ] );
			for ( var z = from.Z; z <= to.Z; z++ )
			for ( var y = from.Y; y <= to.Y; y++ )
			for ( var x = from.X; x <= to.X; x++ )
				_listed[ next[ Index( x, y, z ) ]++ ] = i;
		}
		_large = large.ToArray();
	}

	/// <summary>A grid over the triangles, or null when there are too few to be worth one.</summary>
	internal static TriangleGrid Build( MeshTriangle[] triangles ) =>
		triangles is null || triangles.Length < MinimumTriangles ? null : new TriangleGrid( triangles );

	/// <summary>
	/// Fills <paramref name="result"/> with the indices, ascending, of every triangle whose bounds overlap the box from
	/// <paramref name="minimum"/> to <paramref name="maximum"/> (local space, as the triangles are).
	/// </summary>
	internal void Query( Vector3 minimum, Vector3 maximum, List<int> result )
	{
		result.Clear();
		if ( ++_query == int.MaxValue )
		{
			Array.Clear( _stamp );
			_query = 1;
		}
		var (from, to) = Range( minimum, maximum );
		if ( (long)(to.X - from.X + 1) * (to.Y - from.Y + 1) * (to.Z - from.Z + 1) > MostQueryCells )
		{
			for ( var i = 0; i < _minimum.Length; i++ )
			{
				if ( Overlaps( i, minimum, maximum ) )
					result.Add( i );
			}
			return;
		}
		for ( var z = from.Z; z <= to.Z; z++ )
		for ( var y = from.Y; y <= to.Y; y++ )
		for ( var x = from.X; x <= to.X; x++ )
		{
			var cell = Index( x, y, z );
			for ( var at = _starts[ cell ]; at < _starts[ cell + 1 ]; at++ )
				Collect( _listed[ at ], minimum, maximum, result );
		}
		foreach ( var triangle in _large )
			Collect( triangle, minimum, maximum, result );
		result.Sort();
	}

	private void Collect( int triangle, Vector3 minimum, Vector3 maximum, List<int> result )
	{
		if ( _stamp[ triangle ] == _query )
			return;
		_stamp[ triangle ] = _query;
		if ( Overlaps( triangle, minimum, maximum ) )
			result.Add( triangle );
	}

	// The same overlap test the triangle sweep and contact checks make, so a query keeps exactly the triangles they would.
	private bool Overlaps( int triangle, Vector3 minimum, Vector3 maximum )
	{
		var low = _minimum[ triangle ];
		var high = _maximum[ triangle ];
		return !(low.X > maximum.X || low.Y > maximum.Y || low.Z > maximum.Z
			|| minimum.X > high.X || minimum.Y > high.Y || minimum.Z > high.Z);
	}

	private static long Cells( Vector3 span, float cell ) =>
		(long)Across( span.X, cell ) * Across( span.Y, cell ) * Across( span.Z, cell );

	private static int Across( float span, float cell ) => (int)Math.Min( int.MaxValue / 4, (long)MathF.Floor( span / cell ) + 1 );

	private int Index( int x, int y, int z ) => (z * _cellsY + y) * _cellsX + x;

	// The cells a box covers. Points outside the shape's bounds are clamped onto its edge cells, which hold whatever
	// reaches that far.
	private ((int X, int Y, int Z) From, (int X, int Y, int Z) To) Range( Vector3 minimum, Vector3 maximum ) =>
		((Axis( minimum.X - _origin.X, _cellsX ), Axis( minimum.Y - _origin.Y, _cellsY ), Axis( minimum.Z - _origin.Z, _cellsZ )),
		(Axis( maximum.X - _origin.X, _cellsX ), Axis( maximum.Y - _origin.Y, _cellsY ), Axis( maximum.Z - _origin.Z, _cellsZ )));

	private int Axis( float offset, int cells )
	{
		var cell = offset * _inverseCell;
		if ( !(cell > 0f) )
			return 0;
		return cell >= cells - 1 ? cells - 1 : (int)cell;
	}
}
