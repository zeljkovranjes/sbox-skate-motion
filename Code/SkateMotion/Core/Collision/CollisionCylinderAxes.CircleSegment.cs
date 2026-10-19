using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionCylinderAxes
{
	internal static void CircleSegmentCandidate( ReadOnlySpan<uint> inputs, Span<uint> output )
	{
		if ( inputs.Length < 20 || output.Length < 4 )
			throw new ArgumentException( "A circle/segment candidate requires five input rows and one output row." );
		var center = CollisionWordVector.Read( inputs, 0 );
		var axis = CollisionWordVector.Read( inputs, 4 );
		var radius = CollisionWordVector.Read( inputs, 8 );
		var start = CollisionWordVector.Read( inputs, 12 );
		var end = CollisionWordVector.Read( inputs, 16 );
		var direction = end.Subtract( start );
		var descending = Greater( 0, Dot( direction, axis ) );
		var firstPoint = descending ? end : start;
		var secondPoint = descending ? start : end;
		var projectedDirection = axis.Cross( direction ).Cross( axis );
		var denominator = Dot( direction, projectedDirection );
		var parameter = Multiply( Dot( center.Subtract( start ), projectedDirection ),
			Greater( denominator, 0 ) ? RefinedReciprocal( denominator ) : denominator );
		if ( Greater( 0x3f800000u, parameter ) && Greater( parameter, 0 ) )
			secondPoint = direction.MultiplyAdd( parameter, start );
		var firstCircle = CirclePoint( firstPoint, center, axis, radius );
		var secondCircle = CirclePoint( secondPoint, center, axis, radius );
		var firstSlope = Dot( firstCircle.Subtract( firstPoint ), direction );
		var secondSlope = Dot( secondCircle.Subtract( secondPoint ), direction );
		CollisionWordVector candidate;
		if ( Greater( Multiply( firstSlope, secondSlope ), 0 ) )
		{
			var chooseSecond = Greater( firstSlope & 0x7fffffffu, secondSlope & 0x7fffffffu );
			candidate = CircleSegmentDirection( chooseSecond ? secondPoint : firstPoint,
				chooseSecond ? secondCircle : firstCircle, center, axis, direction );
		}
		else
		{
			if ( !Greater( firstSlope, secondSlope ) )
			{
				(firstPoint, secondPoint) = (secondPoint, firstPoint);
				(firstCircle, secondCircle) = (secondCircle, firstCircle);
				(firstSlope, secondSlope) = (secondSlope, firstSlope);
			}
			RefineCircleSegment( center, axis, radius, direction,
				ref firstPoint, ref secondPoint, ref firstCircle, ref secondCircle,
				ref firstSlope, ref secondSlope );
			var chooseSecond = Greater( firstSlope, secondSlope ^ 0x80000000u );
			candidate = CircleSegmentDirection( chooseSecond ? secondPoint : firstPoint,
				chooseSecond ? secondCircle : firstCircle, center, axis, direction );
		}
		candidate.Write( output, 0 );
	}

	private static void RefineCircleSegment( CollisionWordVector center, CollisionWordVector axis,
		CollisionWordVector radius, CollisionWordVector direction,
		ref CollisionWordVector positivePoint, ref CollisionWordVector negativePoint,
		ref CollisionWordVector positiveCircle, ref CollisionWordVector negativeCircle,
		ref uint positiveSlope, ref uint negativeSlope )
	{
		var weight = 0x3f800000u;
		for ( var iteration = 0; iteration < 10; ++iteration )
		{
			var weightSum = Add( weight, 0x3f800000u );
			var difference = Subtract( positiveSlope, negativeSlope );
			weight = Multiply( weight, 0x3f000000u );
			if ( !Greater( positiveSlope, negativeSlope ) )
				break;
			var blend = Multiply( RefinedReciprocal( weightSum ),
				MultiplyAdd( RefinedReciprocal( difference ), positiveSlope, weight ) );
			var point = negativePoint.MultiplyAdd( blend, positivePoint.Scale( Subtract( 0x3f800000u, blend ) ) );
			var circle = CirclePoint( point, center, axis, radius );
			var slope = Dot( circle.Subtract( point ), direction );
			if ( Greater( 0, slope ) )
			{
				negativePoint = point;
				negativeCircle = circle;
				negativeSlope = slope;
			}
			else
			{
				positivePoint = point;
				positiveCircle = circle;
				positiveSlope = slope;
			}
		}
	}

	private static CollisionWordVector CirclePoint( CollisionWordVector point, CollisionWordVector center,
		CollisionWordVector axis, CollisionWordVector radius )
	{
		var difference = point.Subtract( center );
		var along = Dot( difference, axis );
		var radial = new CollisionWordVector(
			NegativeMultiplySubtract( axis.X, along, difference.X ),
			NegativeMultiplySubtract( axis.Y, along, difference.Y ),
			NegativeMultiplySubtract( axis.Z, along, difference.Z ),
			NegativeMultiplySubtract( axis.W, along, difference.W ) );
		var squared = radial.LengthSquared();
		if ( !Greater( 0x007fffffu, squared ) )
			radial = radial.Scale( SingleInverseRoot( squared ) );
		return new( MultiplyAdd( radius.X, radial.X, center.X ), MultiplyAdd( radius.Y, radial.Y, center.Y ),
			MultiplyAdd( radius.Z, radial.Z, center.Z ), MultiplyAdd( radius.W, radial.W, center.W ) );
	}

	private static CollisionWordVector CircleSegmentDirection( CollisionWordVector point,
		CollisionWordVector circle, CollisionWordVector center, CollisionWordVector axis, CollisionWordVector direction )
	{
		var separation = point.Subtract( circle );
		var perpendicular = circle.Subtract( center ).Cross( axis ).Cross( direction );
		var squared = separation.LengthSquared();
		if ( Greater( squared, 0x007fffffu ) )
			return separation.Scale( SingleInverseRoot( squared ) );
		var perpendicularSquared = perpendicular.LengthSquared();
		return Greater( perpendicularSquared, 0x007fffffu )
			? perpendicular.Scale( SingleInverseRoot( perpendicularSquared ) ) : axis;
	}

	private static uint RefinedReciprocal( uint value )
	{
		var estimate = Interpolate( value, ReciprocalEstimate( value ) );
		return MultiplyAdd( estimate, NegativeMultiplySubtract( estimate, value, 0x3f800000u ), estimate );
	}

	private static uint SingleInverseRoot( uint squared )
	{
		var estimate = Interpolate( squared, RootEstimate( squared ) );
		return MultiplyAdd( Multiply( estimate, 0x3f000000u ),
			NegativeMultiplySubtract( squared, Multiply( estimate, estimate ), 0x3f800000u ), estimate );
	}
}
