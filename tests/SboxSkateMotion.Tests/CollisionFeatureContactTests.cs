using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionFeatureContactTests
{
	[Fact]
	public void PointPairPreservesBothFeaturesAndUnusedContactStorage()
	{
		var first = Point( 1, 2, 3 );
		var second = Point( 4, 5, 6 );
		var firstBefore = first.AsSpan().ToArray();
		var secondBefore = second.AsSpan().ToArray();
		var output = Output();
		CollisionPointContacts.Assemble( first, second, Vector( 0, 0, 1 ), output );
		Assert.Equal( firstBefore, first );
		Assert.Equal( secondBefore, second );
		Assert.Equal( Vector( 1, 2, 3, 1 ), output[ 0..4 ] );
		Assert.Equal( Vector( 4, 5, 6, 1 ), output[ 64..68 ] );
		for ( var i = 0; i < output.Length; ++i )
			if ( i is not (>= 0 and < 4) and not (>= 64 and < 68) and not 132 )
				Assert.Equal( 0x12345678u, output[ i ] );
		Assert.Equal( 1u, output[ 132 ] );
	}

	[Theory]
	[InlineData( -1f, 0f, 11u )]
	[InlineData( 0f, 0f, 10u )]
	[InlineData( 1f, 1f, 10u )]
	[InlineData( 2f, 2f, 10u )]
	[InlineData( 3f, 2f, 9u )]
	public void PointSegmentClampsStrictlyAndPreservesPairOrder( float x, float expectedX, uint classification )
	{
		foreach ( var reverse in new[] { false, true } )
		{
			var point = Point( x, 1, 0 );
			var segment = Segment( 0, 0, 0, 1, 0, 0, 2 );
			var output = Output();
			CollisionPointContacts.Assemble( reverse ? point : segment, reverse ? segment : point, Vector( 0, 1, 0 ), output );
			Assert.Equal( classification, segment[ 0 ] );
			Assert.Equal( 1u, output[ 132 ] );
			Assert.Equal( Vector( expectedX, 0, 0, 1 ), output.AsSpan( reverse ? 64 : 0, 4 ).ToArray() );
			Assert.Equal( Vector( x, 1, 0, 1 ), output.AsSpan( reverse ? 0 : 64, 4 ).ToArray() );
		}
	}

	[Theory]
	[InlineData( 0f, 0f, 0f, 0f, 10u )]
	[InlineData( 2f, 0f, 1f, 0f, 14u )]
	[InlineData( 2f, 2f, 1f, 1f, 15u )]
	public void PointPolygonProjectsThenClampsToItsFirstMostViolatedEdge( float x, float y, float expectedX, float expectedY, uint classification )
	{
		var polygon = Square();
		var output = Output();
		CollisionPointContacts.Assemble( polygon, Point( x, y, 3 ), Vector( 0, 0, 1 ), output );
		Assert.Equal( Vector( expectedX, expectedY, 0, 1 ), output[ 0..4 ] );
		Assert.Equal( Vector( x, y, 3, 1 ), output[ 64..68 ] );
		Assert.Equal( classification, polygon[ 0 ] );
	}

	[Fact]
	public void CrossingSegmentsRetainTheSeparationBetweenTheirPlanes()
	{
		var first = Segment( 0, 0, 0, 1, 0, 0, 2 );
		var second = Segment( 1, -1, 1, 0, 1, 0, 2 );
		var output = Output();
		CollisionSegmentContacts.Assemble( first, second, Vector( 0, 0, 1 ), output );
		Near( output, 0, 1, 0, 0 );
		Near( output, 64, 1, 0, 1 );
		Assert.Equal( 1u, output[ 132 ] );
		Assert.Equal( 10u, first[ 0 ] );
		Assert.Equal( 10u, second[ 0 ] );
	}

	[Fact]
	public void ParallelOverlapProducesTwoOrderedPairsWithoutChangingClassifications()
	{
		var first = Segment( 0, 0, 0, 1, 0, 0, 2 );
		var second = Segment( 1, 1, 0, 1, 0, 0, 2 );
		var output = Output();
		CollisionSegmentContacts.Assemble( first, second, Vector( 0, 1, 0 ), output );
		Assert.Equal( 2u, output[ 132 ] );
		Near( output, 0, 1, 0, 0 );
		Near( output, 4, 2, 0, 0 );
		Near( output, 64, 1, 1, 0 );
		Near( output, 68, 2, 1, 0 );
		Assert.Equal( 10u, first[ 0 ] );
		Assert.Equal( 10u, second[ 0 ] );
	}

	[Theory]
	[InlineData( 2f )]
	[InlineData( 3f )]
	public void ParallelTouchAndGapUseEndpointsWithDistinctPackedClassifications( float start )
	{
		var first = Segment( 0, 0, 0, 1, 0, 0, 2 );
		var second = Segment( start, 1, 0, 1, 0, 0, 2 );
		var output = Output();
		CollisionSegmentContacts.Assemble( first, second, Vector( 0, 1, 0 ), output );
		Assert.Equal( 1u, output[ 132 ] );
		Assert.Equal( Vector( 2, 0, 0, 1 ), output[ 0..4 ] );
		Assert.Equal( Vector( start, 1, 0, 1 ), output[ 64..68 ] );
		Assert.Equal( 11u, first[ 0 ] );
		Assert.Equal( 9u, second[ 0 ] );
	}

	[Fact]
	public void ZeroLengthSegmentsKeepTheirEndpointsAndOnePair()
	{
		var first = Segment( 1, 2, 3, 0, 0, 0, 0 );
		var second = Segment( 4, 5, 6, 0, 0, 0, 0 );
		var output = Output();
		CollisionSegmentContacts.Assemble( first, second, Vector( 0, 0, 1 ), output );
		Assert.Equal( 1u, output[ 132 ] );
		Assert.Equal( Vector( 1, 2, 3, 1 ), output[ 0..4 ] );
		Assert.Equal( Vector( 4, 5, 6, 1 ), output[ 64..68 ] );
	}

	[Fact]
	public void UnsupportedPairKindsFailBeforeChangingContactStorage()
	{
		var output = Output();
		var first = Square();
		var second = Square();
		Assert.Throws<ArgumentException>( () => CollisionPointContacts.Assemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Throws<ArgumentException>( () => CollisionSegmentContacts.Assemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Throws<ArgumentException>( () => CollisionPointContacts.Assemble( new uint[ 143 ], Point( 0, 0, 0 ), Vector( 0, 0, 1 ), output ) );
		Assert.Equal( Output(), output );
	}

	private static uint[] Point( float x, float y, float z )
	{
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		Vector( x, y, z, 1 ).CopyTo( feature, 136 );
		return feature;
	}

	private static uint[] Segment( float x, float y, float z, float dx, float dy, float dz, float length )
	{
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		feature[ 140 ] = 1;
		Vector( x, y, z, 1 ).CopyTo( feature, 4 );
		Vector( dx, dy, dz ).CopyTo( feature, 8 );
		feature.AsSpan( 16, 4 ).Fill( BitConverter.SingleToUInt32Bits( length ) );
		return feature;
	}

	private static uint[] Square()
	{
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		feature[ 140 ] = 4;
		float[,] points = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
		float[,] directions = { { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 } };
		for ( var i = 0; i < 4; ++i )
		{
			var record = 4 + i * 16;
			Vector( points[ i, 0 ], points[ i, 1 ], 0, 1 ).CopyTo( feature, record );
			Vector( directions[ i, 0 ], directions[ i, 1 ], 0 ).CopyTo( feature, record + 4 );
			Vector( directions[ i, 1 ], -directions[ i, 0 ], 0 ).CopyTo( feature, record + 8 );
			feature.AsSpan( record + 12, 4 ).Fill( BitConverter.SingleToUInt32Bits( 2f ) );
		}
		return feature;
	}

	private static uint[] Output()
	{
		var output = new uint[ 140 ];
		Array.Fill( output, 0x12345678u );
		return output;
	}
	private static uint[] Vector( float x, float y, float z, float w = 0 ) =>
		[BitConverter.SingleToUInt32Bits( x ), BitConverter.SingleToUInt32Bits( y ), BitConverter.SingleToUInt32Bits( z ), BitConverter.SingleToUInt32Bits( w )];
	private static void Near( uint[] output, int offset, float x, float y, float z )
	{
		var expected = new[] { x, y, z };
		for ( var i = 0; i < 3; ++i )
			Assert.InRange( Math.Abs( BitConverter.UInt32BitsToSingle( output[ offset + i ] ) - expected[ i ] ), 0f, .000002f );
	}
}
