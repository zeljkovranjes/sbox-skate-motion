using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

internal static partial class CollisionCylinderAxes
{
	internal static bool TryCircleCircleCandidate( ReadOnlySpan<uint> inputs, Span<uint> output )
	{
		if ( inputs.Length < 28 || output.Length < 4 )
			throw new ArgumentException( "A circle pair requires seven input rows and one output row." );
		var firstCenter = CollisionWordVector.Read( inputs, 0 );
		var firstAxis = CollisionWordVector.Read( inputs, 4 );
		var firstRadius = CollisionWordVector.Read( inputs, 8 );
		var secondCenter = CollisionWordVector.Read( inputs, 12 );
		var secondAxis = CollisionWordVector.Read( inputs, 16 );
		var secondRadius = CollisionWordVector.Read( inputs, 20 );
		var seed = CollisionWordVector.Read( inputs, 24 );
		var tolerance = Multiply( Greater( firstRadius.X, secondRadius.X ) ? secondRadius.X : firstRadius.X, 0x33d6bf94u );
		if ( Greater( 0x007fffffu, tolerance ) )
			return false;
		var secondSeed = MultiplyRowsAndAdd( secondRadius, seed.Cross( secondAxis ), secondCenter );
		var firstSeed = SubtractRowsProduct( firstRadius, seed.Cross( firstAxis ), firstCenter );
		var firstRadial = firstAxis.Cross( secondSeed.Subtract( firstCenter ).Cross( firstAxis ) );
		var secondRadial = secondAxis.Cross( firstSeed.Subtract( secondCenter ).Cross( secondAxis ) );
		if ( Greater( Multiply( firstRadius.X, firstRadius.X ), firstRadial.LengthSquared() )
			|| Greater( Multiply( secondRadius.X, secondRadius.X ), secondRadial.LengthSquared() ) )
			return false;
		var centerDifference = firstCenter.Subtract( secondCenter );
		if ( Greater( tolerance, Dot( centerDifference, seed ) & 0x7fffffffu ) )
		{
			var first = CirclePoint( secondSeed, firstCenter, firstAxis, firstRadius );
			var second = CirclePoint( first, secondCenter, secondAxis, secondRadius );
			var difference = second.Subtract( first );
			var squared = difference.LengthSquared();
			if ( Greater( squared, 0x007fffffu ) )
				difference = difference.Scale( InverseRoot( squared ) );
			else
			{
				var sum = new CollisionWordVector( Add( firstAxis.X, secondAxis.X ), Add( firstAxis.Y, secondAxis.Y ),
					Add( firstAxis.Z, secondAxis.Z ), Add( firstAxis.W, secondAxis.W ) );
				difference = sum.Cross( seed );
				difference = difference.Scale( InverseRoot( difference.LengthSquared() ) );
			}
			difference.Write( output, 0 );
			return true;
		}
		var initialFirst = CirclePairInitialPoint( firstCenter, firstSeed, firstRadius.X, secondRadius.X,
			centerDifference.LengthSquared() );
		return SearchCirclePair( firstCenter, firstAxis, firstRadius, secondCenter, secondAxis, secondRadius,
			initialFirst, secondSeed, tolerance, output );
	}

	private static CollisionWordVector CirclePairInitialPoint( CollisionWordVector center, CollisionWordVector seed,
		uint firstRadius, uint secondRadius, uint squared )
	{
		var sum = Add( firstRadius, secondRadius );
		if ( !Greater( squared, 0x007fffffu ) || !Greater( Multiply( sum, sum ), squared ) )
			return center;
		var distance = Multiply( squared, InverseRoot( squared ) );
		var semiperimeter = Multiply( Add( distance, sum ), 0x3f000000u );
		var areaSquared = Multiply( Multiply( Multiply( semiperimeter, Subtract( semiperimeter, distance ) ),
			Subtract( semiperimeter, firstRadius ) ), Subtract( semiperimeter, secondRadius ) );
		var area = Multiply( areaSquared, InverseRoot( areaSquared ) );
		var scale = Multiply( RefinedReciprocal( firstRadius ),
			Multiply( RefinedReciprocal( distance ), Multiply( area, 0x40000000u ) ) );
		return seed.Subtract( center ).MultiplyAdd( scale, center );
	}

	private static bool SearchCirclePair( CollisionWordVector firstCenter, CollisionWordVector firstAxis,
		CollisionWordVector firstRadius, CollisionWordVector secondCenter, CollisionWordVector secondAxis,
		CollisionWordVector secondRadius, CollisionWordVector initialFirst, CollisionWordVector initialSecond,
		uint tolerance, Span<uint> output )
	{
		var positiveSecond = initialSecond;
		var negativeSecond = CirclePoint( initialFirst, secondCenter, secondAxis, secondRadius );
		var positiveFirst = CirclePoint( positiveSecond, firstCenter, firstAxis, firstRadius );
		var negativeFirst = CirclePoint( negativeSecond, firstCenter, firstAxis, firstRadius );
		var positiveSlope = CirclePairSlope( positiveFirst, positiveSecond, secondCenter, secondAxis );
		var negativeSlope = CirclePairSlope( negativeFirst, negativeSecond, secondCenter, secondAxis );
		if ( Greater( Multiply( positiveSlope, negativeSlope ), 0 ) )
		{
			positiveSecond = new( Subtract( Add( secondCenter.X, secondCenter.X ), positiveSecond.X ),
				Subtract( Add( secondCenter.Y, secondCenter.Y ), positiveSecond.Y ),
				Subtract( Add( secondCenter.Z, secondCenter.Z ), positiveSecond.Z ),
				Subtract( Add( secondCenter.W, secondCenter.W ), positiveSecond.W ) );
			positiveFirst = CirclePoint( positiveSecond, firstCenter, firstAxis, firstRadius );
			positiveSlope = CirclePairSlope( positiveFirst, positiveSecond, secondCenter, secondAxis );
			if ( Greater( Multiply( positiveSlope, negativeSlope ), 0 ) )
				return false;
		}
		if ( !Greater( positiveSlope, negativeSlope ) )
		{
			(positiveFirst, negativeFirst) = (negativeFirst, positiveFirst);
			(positiveSecond, negativeSecond) = (negativeSecond, positiveSecond);
			(positiveSlope, negativeSlope) = (negativeSlope, positiveSlope);
		}
		var weight = 0x3f800000u;
		for ( var iteration = 0; ; ++iteration )
		{
			var chooseNegative = Greater( positiveSlope, negativeSlope ^ 0x80000000u );
			var converged = chooseNegative
				? Greater( negativeSlope, tolerance ^ 0x80000000u ) : Greater( tolerance, positiveSlope );
			if ( converged || iteration == 10 )
			{
				CirclePairDirection( chooseNegative ? negativeFirst : positiveFirst,
					chooseNegative ? negativeSecond : positiveSecond, firstCenter, firstAxis, secondCenter, secondAxis ).Write( output, 0 );
				return true;
			}
			var sum = Add( weight, 0x3f800000u );
			weight = Multiply( weight, 0x3f000000u );
			var blend = Multiply( RefinedReciprocal( sum ),
				MultiplyAdd( RefinedReciprocal( Subtract( positiveSlope, negativeSlope ) ), positiveSlope, weight ) );
			var interpolated = negativeSecond.MultiplyAdd( blend, positiveSecond.Scale( Subtract( 0x3f800000u, blend ) ) );
			var radial = interpolated.Subtract( secondCenter );
			var second = MultiplyRowsAndAdd( radial.Scale( InverseRoot( radial.LengthSquared() ) ), secondRadius, secondCenter );
			var first = CirclePoint( second, firstCenter, firstAxis, firstRadius );
			var slope = CirclePairSlope( first, second, secondCenter, secondAxis );
			if ( Greater( 0, slope ) )
			{
				negativeFirst = first;
				negativeSecond = second;
				negativeSlope = slope;
			}
			else
			{
				positiveFirst = first;
				positiveSecond = second;
				positiveSlope = slope;
			}
		}
	}

	private static uint CirclePairSlope( CollisionWordVector first, CollisionWordVector second,
		CollisionWordVector center, CollisionWordVector axis ) =>
		Dot( second.Subtract( center ), axis.Cross( first.Subtract( center ) ) );

	private static CollisionWordVector CirclePairDirection( CollisionWordVector first, CollisionWordVector second,
		CollisionWordVector firstCenter, CollisionWordVector firstAxis, CollisionWordVector secondCenter, CollisionWordVector secondAxis )
	{
		var difference = second.Subtract( first );
		var alternate = first.Subtract( firstCenter ).Cross( firstAxis ).Cross( second.Subtract( secondCenter ).Cross( secondAxis ) );
		var squared = difference.LengthSquared();
		if ( Greater( squared, 0x007fffffu ) )
			return difference.Scale( SingleInverseRoot( squared ) );
		var alternateSquared = alternate.LengthSquared();
		return Greater( alternateSquared, 0x007fffffu ) ? alternate.Scale( SingleInverseRoot( alternateSquared ) ) : firstAxis;
	}

	private static CollisionWordVector MultiplyRowsAndAdd( CollisionWordVector first, CollisionWordVector second, CollisionWordVector addend ) => new(
		MultiplyAdd( first.X, second.X, addend.X ), MultiplyAdd( first.Y, second.Y, addend.Y ),
		MultiplyAdd( first.Z, second.Z, addend.Z ), MultiplyAdd( first.W, second.W, addend.W ) );

	private static CollisionWordVector SubtractRowsProduct( CollisionWordVector first, CollisionWordVector second, CollisionWordVector addend ) => new(
		NegativeMultiplySubtract( first.X, second.X, addend.X ), NegativeMultiplySubtract( first.Y, second.Y, addend.Y ),
		NegativeMultiplySubtract( first.Z, second.Z, addend.Z ), NegativeMultiplySubtract( first.W, second.W, addend.W ) );
}
