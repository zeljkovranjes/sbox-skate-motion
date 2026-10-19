using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionPolygonSegmentContactTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void CrossingFaceClipsBothEndsAndRetainsPlaneSeparation( bool reverse )
	{
		var polygon = Square();
		var segment = Segment( -2, 0, 1, 4 );
		var output = Output();
		CollisionPolygonSegmentContacts.Assemble( reverse ? segment : polygon, reverse ? polygon : segment,
			Vector( 0, 0, 1 ), output );
		var polygonOffset = reverse ? 64 : 0;
		var segmentOffset = reverse ? 0 : 64;
		Near( output, polygonOffset, -1, 0, 0 );
		Near( output, polygonOffset + 4, 1, 0, 0 );
		Near( output, segmentOffset, -1, 0, 1 );
		Near( output, segmentOffset + 4, 1, 0, 1 );
		Assert.Equal( 2u, output[ 132 ] );
		Assert.Equal( 10u, polygon[ 0 ] );
		Assert.Equal( 10u, segment[ 0 ] );
		for ( var i = 0; i < output.Length; ++i )
			if ( i is not (>= 0 and < 8) and not (>= 64 and < 72) and not 132 )
				Assert.Equal( 0x12345678u, output[ i ] );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void SeparatedParallelEdgeReplacesNormalWithoutCollapsingTwoContacts( bool reverse )
	{
		var polygon = Square();
		var segment = Segment( -2, 2, 0, 4 );
		var output = Output();
		CollisionPolygonSegmentContacts.Assemble( reverse ? segment : polygon, reverse ? polygon : segment,
			Vector( 0, 0, 1 ), output );
		var polygonOffset = reverse ? 64 : 0;
		var segmentOffset = reverse ? 0 : 64;
		Near( output, polygonOffset, -1, 1, 0 );
		Near( output, polygonOffset + 4, 1, 1, 0 );
		Near( output, segmentOffset, -1, 2, 0 );
		Near( output, segmentOffset + 4, 1, 2, 0 );
		Near( output, 128, 0, -1, 0 );
		Assert.Equal( 2u, output[ 132 ] );
		Assert.Equal( 1u, output[ 133 ] );
		// Retained word arithmetic leaves an edge correction even at these symmetric bounds.
		Assert.Equal( 16u, polygon[ 0 ] );
		Assert.Equal( 10u, segment[ 0 ] );
	}

	[Fact]
	public void CornerSeparationProducesOnePairAndClassifiesBothEndpoints()
	{
		var polygon = Square();
		var segment = Segment( 2, 2, 0, 1 );
		var output = Output();
		CollisionPolygonSegmentContacts.Assemble( polygon, segment, Vector( 0, 0, 1 ), output );
		Near( output, 0, 1, 1, 0 );
		Near( output, 64, 2, 2, 0 );
		Assert.Equal( 1u, output[ 132 ] );
		Assert.Equal( 15u, polygon[ 0 ] );
		Assert.Equal( 11u, segment[ 0 ] );
		Assert.Equal( 0x12345678u, output[ 133 ] );
	}

	[Fact]
	public void ZeroLengthSegmentInsideFaceRetainsTwoCoincidentPairs()
	{
		var output = Output();
		CollisionPolygonSegmentContacts.Assemble( Square(), Segment( 0, 0, 1, 0 ), Vector( 0, 0, 1 ), output );
		Near( output, 0, 0, 0, 0 );
		Near( output, 4, 0, 0, 0 );
		Near( output, 64, 0, 0, 1 );
		Near( output, 68, 0, 0, 1 );
		Assert.Equal( 2u, output[ 132 ] );
	}

	[Fact]
	public void InvalidFeaturePairIsRejectedBeforeMutatingStorage()
	{
		var first = Square();
		var second = Square();
		var firstBefore = first.AsSpan().ToArray();
		var secondBefore = second.AsSpan().ToArray();
		var output = Output();
		var outputBefore = output.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionPolygonSegmentContacts.Assemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Equal( firstBefore, first );
		Assert.Equal( secondBefore, second );
		Assert.Equal( outputBefore, output );
	}

	private static uint[] Square()
	{
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		feature[ 140 ] = 4;
		Vector( 0, 0, 1 ).CopyTo( feature, 132 );
		float[,] points = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
		float[,] directions = { { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 } };
		for ( var i = 0; i < 4; ++i )
		{
			var record = 4 + i * 16;
			Vector( points[ i, 0 ], points[ i, 1 ], 0, 1 ).CopyTo( feature, record );
			Vector( directions[ i, 0 ], directions[ i, 1 ], 0 ).CopyTo( feature, record + 4 );
			feature.AsSpan( record + 12, 4 ).Fill( BitConverter.SingleToUInt32Bits( 2f ) );
		}
		return feature;
	}

	private static uint[] Segment( float x, float y, float z, float length )
	{
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		feature[ 140 ] = 1;
		Vector( x, y, z, 1 ).CopyTo( feature, 4 );
		Vector( 1, 0, 0 ).CopyTo( feature, 8 );
		feature.AsSpan( 16, 4 ).Fill( BitConverter.SingleToUInt32Bits( length ) );
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
