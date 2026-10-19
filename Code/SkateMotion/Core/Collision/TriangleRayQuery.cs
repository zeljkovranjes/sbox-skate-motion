namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;

/// <summary>Tests directed segments against triangles with supplied determinant and boundary tolerances.</summary>
internal sealed class TriangleRayQuery
{
	private readonly float _minimumDeterminant;
	private readonly float _boundaryTolerance;

	internal TriangleRayQuery( float minimumDeterminant, float boundaryTolerance )
	{
		_minimumDeterminant = minimumDeterminant;
		_boundaryTolerance = boundaryTolerance;
	}

	internal bool Evaluate( Vector3 start, Vector3 delta, Vector3 a, Vector3 b, Vector3 c,
		ref TriangleContact contact )
	{
		var ab = b - a;
		var ac = c - a;
		var cross = Cross( delta, ac );
		var determinant = Dot( ab, cross );
		if ( !(determinant > _minimumDeterminant) )
			return false;

		var lower = determinant * _boundaryTolerance;
		var upper = determinant - lower;
		var offset = start - a;
		var u = Dot( offset, cross );
		if ( lower > u || upper < u )
			return false;
		var projected = Cross( offset, ab );
		var v = Dot( delta, projected );
		if ( lower > v || u + v > upper )
			return false;

		var numerator = Dot( ac, projected );
		// A rejected segment interval still exposes its unscaled numerator.
		contact = contact with { Fraction = numerator };
		if ( lower > numerator || upper < numerator )
			return false;

		var inverse = 1f / determinant;
		var fraction = numerator * inverse;
		contact = new( AddScaled( delta, fraction, start ), Normalize( Cross( a - b, a - c ) ),
			new( u * inverse, v * inverse, 0f ), fraction );
		return true;
	}
}
