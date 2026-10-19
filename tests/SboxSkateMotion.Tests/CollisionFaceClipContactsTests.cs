using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionFaceClipContactsTests
{
	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void ContainedTriangleProducesOrderedPairsOnBothPlanes( bool reverse )
	{
		var quad = Face( [(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)], 0 );
		var triangle = Face( [(-.25f, -.25f), (.25f, -.25f), (0f, .25f)], 1 );
		var quadBefore = quad.AsSpan().ToArray();
		var triangleBefore = triangle.AsSpan().ToArray();
		var output = Output();
		Assert.True( CollisionFaceClipContacts.TryAssemble( reverse ? triangle : quad, reverse ? quad : triangle,
			Vector( 0, 0, 1 ), output ) );
		Assert.Equal( 3u, output[ 132 ] );
		var quadOffset = reverse ? 64 : 0;
		var triangleOffset = reverse ? 0 : 64;
		Near( output, quadOffset, -.25f, -.25f, 0 );
		Near( output, quadOffset + 4, .25f, -.25f, 0 );
		Near( output, quadOffset + 8, 0, .25f, 0 );
		Near( output, triangleOffset, -.25f, -.25f, 1 );
		Near( output, triangleOffset + 4, .25f, -.25f, 1 );
		Near( output, triangleOffset + 8, 0, .25f, 1 );
		Assert.Equal( quadBefore, quad );
		Assert.Equal( triangleBefore, triangle );
		for ( var i = 0; i < output.Length; ++i )
			if ( i is not (>= 0 and < 12) and not (>= 64 and < 76) and not 132 )
				Assert.Equal( 0x12345678u, output[ i ] );
	}

	[Fact]
	public void ContainedQuadrilateralRetainsFourVertexPairs()
	{
		var first = Face( [(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)], 0 );
		var second = Face( [(-.25f, -.25f), (.25f, -.25f), (.25f, .25f), (-.25f, .25f)], 1 );
		var output = Output();
		Assert.True( CollisionFaceClipContacts.TryAssemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Equal( 4u, output[ 132 ] );
		Near( output, 12, -.25f, .25f, 0 );
		Near( output, 76, -.25f, .25f, 1 );
	}

	[Fact]
	public void TouchingQuadrilateralsRetainInclusiveEdgeIntersections()
	{
		var first = Face( [(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)], 0 );
		var second = Face( [(1f, -1f), (3f, -1f), (3f, 1f), (1f, 1f)], 1 );
		var output = Output();
		Assert.True( CollisionFaceClipContacts.TryAssemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Equal( 4u, output[ 132 ] );
		// The third intersection retains a one-word-step overshoot from the reciprocal path.
		uint[] expectedX = [0x3f800000u, 0x3f800000u, 0x3f800001u, 0x3f800000u];
		uint[] expectedY = [0xbf7ffffeu, 0xbf800000u, 0x3f800000u, 0x3f800000u];
		for ( var i = 0; i < 4; ++i )
		{
			Assert.Equal( expectedX[ i ], output[ i * 4 ] );
			Assert.Equal( expectedY[ i ], output[ i * 4 + 1 ] );
			Assert.Equal( expectedX[ i ], output[ 64 + i * 4 ] );
			Assert.Equal( expectedY[ i ], output[ 64 + i * 4 + 1 ] );
			Assert.Equal( 0u, output[ i * 4 + 2 ] );
			Assert.Equal( BitConverter.SingleToUInt32Bits( 1f ), output[ 64 + i * 4 + 2 ] );
		}
	}

	[Fact]
	public void DisjointFacesRequestFallbackAndOnlyResetContactCount()
	{
		var first = Face( [(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)], 0 );
		var second = Face( [(3f, 3f), (5f, 3f), (4f, 5f)], 1 );
		var output = Output();
		Assert.False( CollisionFaceClipContacts.TryAssemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Equal( 0u, output[ 132 ] );
		for ( var i = 0; i < output.Length; ++i )
			if ( i != 132 )
				Assert.Equal( 0x12345678u, output[ i ] );
	}

	[Fact]
	public void UnsupportedFacesFailBeforeChangingOutput()
	{
		var triangle = Face( [(-1f, -1f), (1f, -1f), (0f, 1f)], 0 );
		var output = Output();
		var before = output.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionFaceClipContacts.TryAssemble( triangle, triangle, Vector( 0, 0, 1 ), output ) );
		Assert.Equal( before, output );
	}

	[Fact]
	public void IncompleteOutputFailsBeforeWritingAnyContact()
	{
		var first = Face( [(-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f)], 0 );
		var second = Face( [(-1f, -1f), (1f, -1f), (0f, 1f)], 0 );
		var output = new uint[ 135 ];
		Assert.Throws<ArgumentException>( () => CollisionFaceClipContacts.TryAssemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.All( output, value => Assert.Equal( 0u, value ) );
	}

	private static uint[] Face( (float X, float Y)[] points, float height )
	{
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		feature[ 140 ] = (uint)points.Length;
		Vector( 0, 0, 1 ).CopyTo( feature, 132 );
		for ( var i = 0; i < points.Length; ++i )
		{
			var point = points[ i ];
			var next = points[ (i + 1) % points.Length ];
			var dx = next.X - point.X;
			var dy = next.Y - point.Y;
			var length = MathF.Sqrt( dx * dx + dy * dy );
			var record = 4 + i * 16;
			Vector( point.X, point.Y, height, 1 ).CopyTo( feature, record );
			Vector( dx / length, dy / length, 0 ).CopyTo( feature, record + 4 );
			Vector( dy / length, -dx / length, 0 ).CopyTo( feature, record + 8 );
			feature.AsSpan( record + 12, 4 ).Fill( BitConverter.SingleToUInt32Bits( length ) );
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
