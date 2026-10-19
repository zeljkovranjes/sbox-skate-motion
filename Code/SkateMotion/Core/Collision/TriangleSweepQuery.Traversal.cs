namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using Scalar = SkateMotion.Core.Collision.CollisionScalarMath;

internal sealed partial class TriangleSweepQuery
{
	private bool TraverseFeatures( Vector3 point, Vector3 delta, Vector3 a, Vector3 b, Vector3 c,
		Vector3 anchor, int feature, float radius, ref TriangleContact contact )
	{
		var remaining = 4;
		var root = new SweepRoot();
		while ( true )
		{
			if ( feature > 2 )
			{
				anchor = feature == 5 ? b : a;
				var edge = feature == 3 ? b - a : feature == 4 ? c - a : c - b;
				var status = TriangleSweepRoots.Edge( point, delta, anchor, edge, Dot( edge, edge ),
					radius, false, false, ref root );
				if ( status == SweepRootStatus.NotApproaching )
					return false;
				var projection = Dot( delta, edge );
				var selected = -1;
				if ( projection > _featureEpsilon )
				{
					var far = anchor + edge;
					var numerator = Dot( far - point, edge );
					if ( SelectBoundary( numerator, projection, status == SweepRootStatus.Miss, ref root ) )
					{
						selected = feature / 2;
						anchor = far;
					}
				}
				if ( selected < 0 && !(-projection <= _featureEpsilon) )
				{
					var numerator = Dot( point - anchor, edge );
					if ( SelectBoundary( numerator, -projection, status == SweepRootStatus.Miss, ref root ) )
						selected = (feature - 3) / 2;
				}
				if ( selected < 0 )
				{
					if ( status == SweepRootStatus.Miss )
						return false;
					FinishEdge( point, delta, anchor, edge, feature, radius, root, ref contact );
					return true;
				}
				feature = selected;
			}
			else
			{
				var status = TriangleSweepRoots.Point( point, delta, anchor, radius, ref root );
				if ( status == SweepRootStatus.NotApproaching )
					return false;
				var selected = (int)status;
				var first = feature == 0 ? b - a : feature == 1 ? a - b : a - c;
				var second = feature == 0 ? c - a : feature == 1 ? c - b : b - c;
				if ( SelectVertexExit( anchor - point, delta, first, selected == 0, ref root ) )
					selected = (feature + 6) / 2;
				if ( SelectVertexExit( anchor - point, delta, second, selected == 0, ref root ) )
					selected = (feature + 9) / 2;
				if ( selected == 0 )
					return false;
				if ( selected <= 2 )
				{
					var time = RootTime( root );
					var position = AddScaled( delta, time, point );
					var fraction = AdvanceFraction( time, contact.Fraction );
					var normal = Scale( position - anchor, Reciprocal( radius ) );
					contact = new( position, normal, new( feature == 1 ? 1f : 0f, feature == 2 ? 1f : 0f, 0f ), fraction );
					return true;
				}
				feature = selected;
			}

			var advance = RootTime( root );
			point = AddScaled( delta, advance, point );
			contact = contact with { Fraction = AdvanceFraction( advance, contact.Fraction ) };
			delta = Scale( delta, Scalar.Narrow( Scalar.Difference( 1d, Scalar.Widen( advance ) ) ) );
			if ( advance > 1f )
				return false;
			// The iteration limit preserves the partially written output.
			if ( remaining == 0 )
				return true;
			remaining--;
		}
	}

	private bool SelectVertexExit( Vector3 offset, Vector3 delta, Vector3 edge, bool noRoot, ref SweepRoot root )
	{
		var projection = Dot( delta, edge );
		return projection > _featureEpsilon && SelectBoundary( Dot( offset, edge ), projection, noRoot, ref root );
	}

	private static bool SelectBoundary( float numerator, float denominator, bool noRoot, ref SweepRoot root )
	{
		if ( numerator <= 0f || (!noRoot && Scalar.Product( denominator, Scalar.Widen( root.Numerator ) )
			< Scalar.Product( numerator, Scalar.Widen( root.Denominator ) )) )
			return false;
		root = new( numerator, denominator );
		return true;
	}

	private static void FinishEdge( Vector3 point, Vector3 delta, Vector3 anchor, Vector3 edge,
		int feature, float radius, SweepRoot root, ref TriangleContact contact )
	{
		var time = RootTime( root );
		point = AddScaled( delta, time, point );
		var fraction = AdvanceFraction( time, contact.Fraction );
		var weight = MultiplyAdd( Scalar.Narrow( Scalar.Divide( 1d, Dot( edge, edge ) ) ), Dot( point - anchor, edge ), 0f );
		var normal = Scale( point - AddScaled( edge, weight, anchor ), Reciprocal( radius ) );
		var barycentric = feature == 3 ? new Vector3( weight, 0f, 0f )
			: feature == 4 ? new Vector3( 0f, weight, 0f ) : new Vector3( Scalar.Narrow( Scalar.Difference( 1d, Scalar.Widen( weight ) ) ), weight, 0f );
		contact = new( point, normal, barycentric, fraction );
	}

	private static float RootTime( SweepRoot root ) =>
		Scalar.Narrow( Scalar.Divide( Scalar.Widen( root.Numerator ), Scalar.Widen( root.Denominator ) ) );

	private static float AdvanceFraction( float time, float fraction ) => Scalar.Narrow(
		Scalar.MultiplyAdd( Scalar.Widen( time ), Scalar.Difference( 1d, Scalar.Widen( fraction ) ), Scalar.Widen( fraction ) ) );
}
