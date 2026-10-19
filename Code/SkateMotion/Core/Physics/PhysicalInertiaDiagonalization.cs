namespace SkateMotion.Core.Physics;

using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Ordered plane sweeps retaining the diagonalizer's unsorted principal axes.</summary>
internal static class PhysicalInertiaDiagonalization
{
	internal static int Solve( Span<float> matrix, Span<float> axes, float tolerance, int passes )
	{
		if ( matrix.Length != 9 || axes.Length != 9 )
			throw new ArgumentException( "Diagonalization requires two nine-element matrices." );
		axes.Clear();
		axes[ 0 ] = axes[ 4 ] = axes[ 8 ] = 1f;
		var squaredTolerance = tolerance * tolerance;
		while ( passes != 0 )
		{
			var offDiagonal = MultiplyAdd( matrix[ 3 ], matrix[ 3 ], matrix[ 6 ] * matrix[ 6 ] );
			offDiagonal = MultiplyAdd( matrix[ 7 ], matrix[ 7 ], offDiagonal );
			var diagonal = MathF.Max( MathF.Max( matrix[ 0 ] * matrix[ 0 ], matrix[ 4 ] * matrix[ 4 ] ), matrix[ 8 ] * matrix[ 8 ] );
			if ( offDiagonal <= diagonal * squaredTolerance )
				break;
			for ( var second = 1; second < 3; ++second )
			{
				for ( var first = 0; first < second; ++first )
				{
					var cosine = 1f;
					var sine = 0f;
					var cross = matrix[ second * 3 + first ];
					if ( cross != 0f )
					{
						var ratio = ((matrix[ first * 3 + first ] - matrix[ second * 3 + second ]) * .5f) * Reciprocal( cross );
						var root = MathF.Sqrt( MultiplyAdd( ratio, ratio, 1f ) );
						var tangent = ratio > 0f ? 1f / (ratio + root) : -1f / (root - ratio);
						cosine = 1f / MathF.Sqrt( MultiplyAdd( tangent, tangent, 1f ) );
						sine = tangent * cosine;
					}
					RotateColumns( axes, first, second, cosine, sine );
					RotateColumns( matrix, first, second, cosine, sine );
					RotateRows( matrix, first, second, cosine, sine );
				}
			}
			--passes;
		}
		return passes;
	}

	private static void RotateColumns( Span<float> matrix, int first, int second, float cosine, float sine )
	{
		for ( var row = 0; row < 3; ++row )
		{
			var a = matrix[ row * 3 + first ];
			var b = matrix[ row * 3 + second ];
			matrix[ row * 3 + second ] = cosine * b - sine * a;
			matrix[ row * 3 + first ] = MultiplyAdd( sine, b, cosine * a );
		}
	}

	private static void RotateRows( Span<float> matrix, int first, int second, float cosine, float sine )
	{
		for ( var column = 0; column < 3; ++column )
		{
			var a = matrix[ first * 3 + column ];
			var b = matrix[ second * 3 + column ];
			matrix[ second * 3 + column ] = cosine * b - sine * a;
			matrix[ first * 3 + column ] = MultiplyAdd( sine, b, cosine * a );
		}
	}
}
