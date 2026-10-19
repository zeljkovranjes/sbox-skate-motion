using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Suppresses overlapping nearby edges in the rig's Y-up coordinate frame.</summary>
internal static class GrindEdgeFilter
{
	internal const int Capacity = 40;
	private const float ParallelLimit = 0.0001f;
	private const float SeparationLimit = 0.010000000707805157f;

	/// <summary>Writes retained input indices in order; endpoints and metadata stay owned by the caller.</summary>
	internal static int Filter( RailSegment[] candidates, Vector3 reference, int[] retained )
	{
		ArgumentNullException.ThrowIfNull( candidates );
		ArgumentNullException.ThrowIfNull( retained );
		return FilterSpan( candidates, reference, retained );
	}

	internal static int FilterSpan( ReadOnlySpan<RailSegment> candidates, Vector3 reference, Span<int> retained )
	{
		if ( candidates.Length > Capacity || retained.Length < candidates.Length )
			throw new ArgumentException( "At most 40 candidates and output space for every candidate are required." );
		Span<Vector3> directions = stackalloc Vector3[ Capacity ];
		for ( var i = 0; i < candidates.Length; i++ )
			directions[ i ] = Normalize( candidates[ i ].End - candidates[ i ].Start );
		var count = 0;
		for ( var i = 0; i < candidates.Length; i++ )
		{
			var keep = true;
			for ( var j = 0; j < candidates.Length; j++ )
			{
				if ( i == j )
					continue;
				// Preserve the lower-triangle cross-product order for both comparisons.
				var cross = Cross( directions[ Math.Max( i, j ) ], directions[ Math.Min( i, j ) ] );
				if ( Dot( cross, cross ) >= ParallelLimit )
					continue;
				if ( Suppresses( candidates[ i ], directions[ i ], candidates[ j ], directions[ j ], reference ) )
					keep = false;
			}
			if ( keep )
				retained[ count++ ] = i;
		}
		return count;
	}

	private static bool Suppresses( RailSegment current, Vector3 direction, RailSegment other,
		Vector3 otherDirection, Vector3 reference )
	{
		var offset = other.Start - current.Start;
		var perpendicular = offset - Scale( otherDirection, Dot( otherDirection, offset ) );
		var separation = Dot( perpendicular, perpendicular );
		if ( separation <= ParallelLimit || separation >= SeparationLimit )
			return false;
		var midpoint = Scale( current.Start + current.End, .5f );
		var relative = other.Start - midpoint;
		if ( !(Dot( relative, other.End - midpoint ) < 0f) )
			return false;
		var ownDelta = midpoint - reference;
		var projected = relative - Scale( otherDirection, Dot( otherDirection, relative ) );
		var otherDelta = midpoint + projected - reference;
		var up = Cross( Cross( direction, Vector3.UnitY ), direction );
		var squared = Dot( up, up );
		var inverse = InverseSquareRoot( squared );
		var length = squared == 0f ? 0f : MultiplyAdd( squared, inverse, 0f );
		up = length > 0.000001f ? Scale( up, inverse ) : Vector3.Zero;
		var height = Dot( offset, up );
		return (height > -.02f && Dot( ownDelta, ownDelta ) > Dot( otherDelta, otherDelta )) || height > .02f;
	}
}
