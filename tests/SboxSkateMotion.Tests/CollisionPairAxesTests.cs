using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionPairAxesTests
{
	[Fact]
	public void SphereSeparationUsesCentersBeforeRadiusExpansion()
	{
		var first = Shape( 1, 0, 0, 0 );
		var second = Shape( 1, 3, 4, 0 );
		first[ 32 ] = Word( 20 );
		second[ 32 ] = Word( 40 );
		var output = Output();
		CollisionPairAxes.SpherePair( first, second, output );
		Near( output[ 0 ], 5 );
		Near( output[ 4 ], .6f );
		Near( output[ 5 ], .8f );
		Near( output[ 6 ], 0 );
		for ( var i = 1; i < 4; ++i )
			Assert.Equal( output[ 0 ], output[ i ] );
		Assert.Equal( 0x12345678u, output[ 8 ] );
		Assert.Equal( Word( 20 ), first[ 32 ] );
		Assert.Equal( Word( 40 ), second[ 32 ] );
	}

	[Fact]
	public void CoincidentSpheresRetainZeroDirection()
	{
		var shape = Shape( 1, 0, 0, 0 );
		var output = Output();
		CollisionPairAxes.SpherePair( shape, shape, output );
		Assert.All( output.Take( 8 ), value => Assert.Equal( 0u, value ) );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void CollinearSegmentsKeepAxialSeparationInEitherOrder( bool reverse )
	{
		var first = Shape( 2, 0, 0, 0 );
		var second = Shape( 2, 3, 0, 0 );
		var output = Output();
		CollisionPairAxes.GeneralPair( reverse ? second : first, reverse ? first : second, output );
		Near( output[ 0 ], 1 );
		Near( output[ 4 ], reverse ? -1 : 1 );
		Near( output[ 5 ], 0 );
		Near( output[ 6 ], 0 );
	}

	[Fact]
	public void CoincidentParallelSegmentsUseVerticalFallback()
	{
		var shape = Shape( 2, 0, 0, 0 );
		var output = Output();
		CollisionPairAxes.GeneralPair( shape, shape, output );
		Near( output[ 0 ], 0 );
		Near( output[ 4 ], 0 );
		Near( output[ 5 ], 0 );
		Assert.Equal( Word( 1 ), output[ 6 ] );
	}

	[Fact]
	public void PointBesideSegmentUsesPerpendicularDirection()
	{
		var first = Shape( 1, 0, 0, 0 );
		var second = Shape( 2, 0, 2, 0 );
		var output = Output();
		CollisionPairAxes.GeneralPair( first, second, output );
		Near( output[ 0 ], 2 );
		Near( output[ 4 ], 0 );
		Near( output[ 5 ], 1 );
		Near( output[ 6 ], 0 );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void SpecializedBoxesKeepAxialSeparationRegardlessOfFaceMetadata( bool omitMetadata )
	{
		var first = Shape( 4, 0, 0, 0 );
		var second = Shape( 4, 3, 0, 0 );
		if ( omitMetadata )
		{
			first[ 35 ] = 0;
			second[ 35 ] = 0;
		}
		var output = Output();
		CollisionPairAxes.BoxPair( first, second, output );
		Near( output[ 0 ], 1 );
		Near( output[ 4 ], 1 );
		Near( output[ 5 ], 0 );
		Near( output[ 6 ], 0 );
	}

	[Fact]
	public void CoincidentBoxesRetainZeroCrossAxisCandidate()
	{
		var shape = Shape( 4, 0, 0, 0 );
		var output = Output();
		CollisionPairAxes.BoxPair( shape, shape, output );
		// The specialized candidate list retains zero crosses instead of filtering them.
		Assert.All( output.Take( 8 ), value => Assert.Equal( 0u, value ) );
	}

	[Fact]
	public void IncompleteOutputFailsBeforeChangingCallerMemory()
	{
		var shape = Shape( 1, 0, 0, 0 );
		var output = Enumerable.Repeat( 0x12345678u, 7 ).ToArray();
		Assert.Throws<ArgumentException>( () => CollisionPairAxes.GeneralPair( shape, shape, output ) );
		Assert.All( output, value => Assert.Equal( 0x12345678u, value ) );
	}

	private static uint[] Shape( int kind, float x, float y, float z )
	{
		var shape = new uint[ 44 ];
		shape[ 0 ] = Word( x );
		shape[ 1 ] = Word( y );
		shape[ 2 ] = Word( z );
		shape[ 3 ] = Word( 1 );
		shape[ 4 ] = shape[ 9 ] = shape[ 14 ] = Word( 1 );
		shape[ 16 ] = shape[ 21 ] = shape[ 26 ] = Word( 1 );
		shape[ 28 ] = shape[ 29 ] = shape[ 30 ] = Word( 1 );
		shape[ 35 ] = kind == 4 ? 0x03030000u : kind == 2 ? 0x00010000u : 0;
		shape[ 36 ] = (uint)kind;
		return shape;
	}

	private static uint[] Output() => Enumerable.Repeat( 0x12345678u, 12 ).ToArray();
	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
	private static void Near( uint word, float expected ) => Assert.InRange( Math.Abs( BitConverter.UInt32BitsToSingle( word ) - expected ), 0f, .000002f );
}
