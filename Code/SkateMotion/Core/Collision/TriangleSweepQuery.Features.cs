namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using Scalar = SkateMotion.Core.Collision.CollisionScalarMath;

internal sealed partial class TriangleSweepQuery
{
	private (float U, float V, int Feature) ClosestFeature( Vector3 point, Vector3 a, Vector3 ab, Vector3 ac )
	{
		var aa = Scalar.Widen( Dot( ab, ab ) );
		var cc = Scalar.Widen( Dot( ac, ac ) );
		var mixed = Scalar.Widen( Dot( ab, ac ) );
		var offset = a - point;
		var alongB = Scalar.Widen( Dot( ab, offset ) );
		var alongC = Scalar.Widen( Dot( ac, offset ) );
		var determinant = -Scalar.MultiplyAdd( mixed, mixed, -Scalar.Product( aa, cc ) );
		var u = Scalar.MultiplyAdd( mixed, alongC, -Scalar.Product( cc, alongB ) );
		var v = Scalar.MultiplyAdd( mixed, alongB, -Scalar.Product( aa, alongC ) );

		if ( !(determinant < _featureEpsilon) )
		{
			if ( !(Scalar.Sum( v, u ) > determinant) )
			{
				if ( u < 0f )
					return alongC < 0f ? ClosestAc( alongC, cc ) : ClosestAb( alongB, aa );
				if ( v < 0f )
					return alongB < 0f ? ClosestAb( alongB, aa ) : ClosestAc( alongC, cc );
				var inverse = Scalar.Divide( 1d, determinant );
				return (Scalar.Narrow( Scalar.Product( inverse, u ) ), Scalar.Narrow( Scalar.Product( v, inverse ) ), 6);
			}
			if ( u < 0f )
			{
				if ( Scalar.Sum( mixed, alongB ) > Scalar.Sum( cc, alongC ) )
					return ClosestAc( alongC, cc );
			}
			else if ( v < 0f && Scalar.Sum( aa, alongB ) < Scalar.Sum( mixed, alongC ) )
				return ClosestAb( alongB, aa );
		}
		else
		{
			var length = Scalar.MultiplyAdd( mixed, -2d, Scalar.Sum( aa, cc ) );
			if ( !(aa > cc) )
			{
				if ( cc > length )
					return ClosestAc( alongC, cc );
			}
			else if ( aa > length )
				return ClosestAb( alongB, aa );
		}
		var numerator = Scalar.Difference( Scalar.Difference( Scalar.Sum( cc, alongC ), mixed ), alongB );
		if ( !(numerator > 0f) )
			return (0f, 1f, 2);
		var edgeLength = Scalar.MultiplyAdd( mixed, -2d, Scalar.Sum( aa, cc ) );
		if ( numerator < edgeLength )
		{
			u = Scalar.Divide( numerator, edgeLength );
			return (Scalar.Narrow( u ), Scalar.Narrow( Scalar.Difference( 1d, u ) ), 5);
		}
		return (1f, 0f, 1);
	}

	private static (float U, float V, int Feature) ClosestAb( double along, double squaredLength )
	{
		if ( along < 0f )
			return squaredLength > -along ? (Scalar.Narrow( Scalar.Divide( -along, squaredLength ) ), 0f, 3) : (1f, 0f, 1);
		return (0f, 0f, 0);
	}

	private static (float U, float V, int Feature) ClosestAc( double along, double squaredLength )
	{
		if ( along < 0f )
			return squaredLength > -along ? (0f, Scalar.Narrow( Scalar.Divide( -along, squaredLength ) ), 4) : (0f, 1f, 2);
		return (0f, 0f, 0);
	}
}
