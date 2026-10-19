namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;
using static SkateMotion.Core.Animation.LimbSolverMath;
using Scalar = SkateMotion.Core.Collision.CollisionScalarMath;

/// <summary>Evaluates directed triangle sweeps with rounded face, edge and vertex contacts.</summary>
internal sealed partial class TriangleSweepQuery
{
	private readonly TriangleRayQuery _ray;
	private readonly double _featureEpsilon;

	internal TriangleSweepQuery( float minimumDeterminant, float boundaryTolerance, float featureEpsilon )
	{
		_ray = new( minimumDeterminant, boundaryTolerance );
		_featureEpsilon = Scalar.Widen( featureEpsilon );
	}

	internal bool Evaluate( Vector3 start, Vector3 delta, Vector3 a, Vector3 b, Vector3 c,
		float radius, float margin, ref TriangleContact contact )
	{
		var combined = Scalar.Sum( Scalar.Widen( radius ), Scalar.Widen( margin ) );
		if ( combined == 0d )
			return _ray.Evaluate( start, delta, a, b, c, ref contact );

		var normal = Normalize( Cross( a - b, a - c ) );
		// Invalid vector normalization produces a positive quiet NaN in each affected lane.
		normal = new( CanonicalInvalid( normal.X ), CanonicalInvalid( normal.Y ), CanonicalInvalid( normal.Z ) );
		contact = contact with { Normal = normal, Fraction = 0f };
		var hit = EvaluateRounded( start, delta, a, b, c, Scalar.Narrow( combined ), ref contact );
		if ( hit )
			contact = contact with { Position = contact.Position - Scale( contact.Normal, radius ) };
		return hit;
	}

	private bool EvaluateRounded( Vector3 start, Vector3 delta, Vector3 a, Vector3 b, Vector3 c,
		float radius, ref TriangleContact contact )
	{
		var scalarRadius = Scalar.Widen( radius );
		var ab = b - a;
		var ac = c - a;
		var offset = start - a;
		var crossB = Cross( delta, ac );
		var determinant = Scalar.Widen( Dot( ab, crossB ) );
		var sign = determinant < 0f ? -1f : 1f;
		determinant = determinant < 0f ? -determinant : determinant;
		if ( Scalar.Product( Dot( contact.Normal, offset ), sign ) < -scalarRadius )
			return false;
		var widthB = Scalar.Product( AbsoluteSum( crossB ), scalarRadius );
		var u = Scalar.Product( Dot( offset, crossB ), sign );
		if ( -widthB > u || Scalar.Sum( determinant, widthB ) < u )
			return false;
		var crossC = Cross( ab, delta );
		var widthC = Scalar.Product( AbsoluteSum( crossC ), scalarRadius );
		var v = Scalar.Product( Dot( offset, crossC ), sign );
		var high = Scalar.Sum( determinant, widthC );
		if ( -widthC > v || v > high || Scalar.Sum( u, v ) > Scalar.Sum( high, widthB ) )
			return false;

		if ( determinant > _featureEpsilon )
		{
			var shifted = offset - Scale( contact.Normal, Scalar.Narrow( Scalar.Product( scalarRadius, sign ) ) );
			var numerator = Scalar.Product( -sign, Dot( shifted, Cross( ac, ab ) ) );
			if ( determinant < numerator )
				return false;
			if ( !(numerator < 0f) )
			{
				var inverse = Scalar.Divide( 1d, determinant );
				var fraction = Scalar.Narrow( Scalar.Product( numerator, inverse ) );
				contact = contact with { Fraction = fraction };
				u = Scalar.Product( Dot( shifted, crossB ), sign );
				if ( !(u < 0f) && !(determinant < u) )
				{
					v = Scalar.Product( Dot( shifted, crossC ), sign );
					if ( !(v < 0f) && !(Scalar.Sum( u, v ) > determinant) )
					{
						contact = new( AddScaled( delta, fraction, start ), Scale( contact.Normal, sign ),
							new( Scalar.Narrow( Scalar.Product( inverse, u ) ), Scalar.Narrow( Scalar.Product( inverse, v ) ), 0f ), fraction );
						return true;
					}
				}
				offset = AddScaled( delta, fraction, offset );
				delta = Scale( delta, Scalar.Narrow( Scalar.Difference( 1d, Scalar.Widen( fraction ) ) ) );
			}
		}

		var point = offset + a;
		var closest = ClosestFeature( point, a, ab, ac );
		var anchor = AddScaled( ac, closest.V, AddScaled( ab, closest.U, a ) );
		var difference = point - anchor;
		var depth = Scalar.MultiplyAdd( scalarRadius, scalarRadius, -Dot( difference, difference ) );
		if ( !(depth <= 0f) )
		{
			var normal = contact.Normal;
			var length = Dot( difference, difference );
			if ( length > 0f )
			{
				// Overlap resolution uses one refinement; face initialization uses two.
				var estimate = 1f / MathF.Sqrt( length );
				var squared = MultiplyAdd( estimate, estimate, 0f );
				var half = MultiplyAdd( estimate, .5f, 0f );
				var refined = MultiplyAdd( half, SubtractProduct( length, squared, 1f ), estimate );
				normal = Scale( difference, refined );
			}
			contact = new( point, normal, new( closest.U, closest.V, Scalar.Narrow( depth ) ), contact.Fraction );
			return true;
		}
		if ( closest.Feature == 6 )
		{
			var normal = determinant < 0f ? Scale( contact.Normal, -1f ) : contact.Normal;
			contact = new( point, normal, new( closest.U, closest.V, 0f ), contact.Fraction );
			return true;
		}
		return TraverseFeatures( point, delta, a, b, c, anchor, closest.Feature, radius, ref contact );
	}

	private static double AbsoluteSum( Vector3 value ) => Scalar.Sum(
		Scalar.Sum( Math.Abs( Scalar.Widen( value.X ) ), Math.Abs( Scalar.Widen( value.Y ) ) ), Math.Abs( Scalar.Widen( value.Z ) ) );

	private static float CanonicalInvalid( float value ) => float.IsNaN( value ) ? BitConverter.UInt32BitsToSingle( 0x7fc00000u ) : value;
}
