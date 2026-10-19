namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using Scalar = SkateMotion.Core.Collision.CollisionScalarMath;

/// <summary>Finds rounded vertex and infinite-edge contact roots without dividing the retained pair.</summary>
internal static class TriangleSweepRoots
{
	internal static SweepRootStatus Point( Vector3 start, Vector3 delta, Vector3 vertex,
		float radius, ref SweepRoot root )
	{
		var radiusSquared = Scalar.Product( Scalar.Widen( radius ), Scalar.Widen( radius ) );
		var offset = vertex - start;
		if ( Dot( offset, offset ) < radiusSquared )
		{
			root = new( 0f, 1f );
			return SweepRootStatus.Hit;
		}
		var projection = Scalar.Widen( Dot( offset, delta ) );
		if ( projection <= 0f )
			return SweepRootStatus.NotApproaching;

		var denominator = Scalar.Widen( Dot( delta, delta ) );
		var gap = Scalar.Difference( projection, denominator );
		var cross = Cross( offset, delta );
		var discriminant = Scalar.MultiplyAdd( radiusSquared, denominator, -Dot( cross, cross ) );
		if ( discriminant < 0f || (gap > 0f && discriminant < Scalar.Product( gap, gap )) )
			return SweepRootStatus.Miss;
		root = new( Scalar.Narrow( Scalar.Difference( projection, Scalar.SquareRoot( discriminant ) ) ), Scalar.Narrow( denominator ) );
		return SweepRootStatus.Hit;
	}

	internal static SweepRootStatus Edge( Vector3 start, Vector3 delta, Vector3 anchor,
		Vector3 edge, float edgeLengthSquared, float radius, bool exiting, bool requireRoot, ref SweepRoot root )
	{
		var scalarRadius = Scalar.Widen( radius );
		var threshold = Scalar.Product( Scalar.Product( Scalar.Widen( edgeLengthSquared ), scalarRadius ), scalarRadius );
		var offsetCross = Cross( anchor - start, edge );
		var distanceSquared = Dot( offsetCross, offsetCross );
		if ( distanceSquared < threshold && !requireRoot )
		{
			root = new( 0f, 1f );
			return SweepRootStatus.Hit;
		}
		var deltaCross = Cross( delta, edge );
		var projection = Scalar.Widen( Dot( offsetCross, deltaCross ) );
		if ( !exiting && !(projection > 0f) )
			return SweepRootStatus.NotApproaching;

		var denominator = Scalar.Widen( Dot( deltaCross, deltaCross ) );
		var discriminant = Scalar.MultiplyAdd( projection, projection, -Scalar.Product( distanceSquared, denominator ) );
		discriminant = Scalar.MultiplyAdd( threshold, denominator, discriminant );
		if ( discriminant < 0f )
			return SweepRootStatus.Miss;
		var gap = Scalar.Difference( projection, denominator );
		if ( exiting )
		{
			if ( gap >= 0f || !(discriminant < Scalar.Product( gap, gap )) )
				return SweepRootStatus.Miss;
		}
		else if ( gap >= 0f && discriminant <= Scalar.Product( gap, gap ) )
			return SweepRootStatus.Miss;

		var sign = exiting ? -1f : 1f;
		root = new( Scalar.Narrow( -Scalar.MultiplyAdd( sign, Scalar.SquareRoot( discriminant ), -projection ) ), Scalar.Narrow( denominator ) );
		return SweepRootStatus.Hit;
	}
}
