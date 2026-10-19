using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionFeatureContactsTests
{
	[Theory]
	[InlineData( 3, false )]
	[InlineData( 8, false )]
	[InlineData( 8, true )]
	public void ContainedPolygonUsesBothPlanesAndPreservesFeatureFlags( int outerCount, bool reverse )
	{
		var outer = Polygon( outerCount, 4f, 0, 0, 0 );
		var inner = Polygon( 3, .25f, 0, 0, 1 );
		var output = Output();
		CollisionFeatureContacts.Assemble( reverse ? inner : outer, reverse ? outer : inner, Vector( 0, 0, 1 ), output );
		Assert.Equal( 3u, output[ 132 ] );
		Assert.Equal( 10u, outer[ 0 ] );
		Assert.Equal( 10u, inner[ 0 ] );
		for ( var i = 0; i < 3; ++i )
		{
			Near( output[ i * 4 + 2 ], reverse ? 1 : 0 );
			Near( output[ 64 + i * 4 + 2 ], reverse ? 0 : 1 );
			for ( var axis = 0; axis < 2; ++axis )
				Near( output[ i * 4 + axis ], BitConverter.UInt32BitsToSingle( output[ 64 + i * 4 + axis ] ) );
		}
		Assert.Equal( 0u, output[ 133 ] );
		Assert.Equal( 0x12345678u, output[ 136 ] );
	}

	[Theory]
	[InlineData( 3 )]
	[InlineData( 4 )]
	[InlineData( 8 )]
	public void DisjointPolygonsProduceClosestPairsAndNormalOverride( int count )
	{
		var first = Polygon( count, 1, 0, 0, 0 );
		var second = Polygon( count, 1, 5, 3, 1 );
		var output = Output();
		CollisionFeatureContacts.Assemble( first, second, Vector( 0, 0, 1 ), output );
		Assert.InRange( output[ 132 ], 1u, 2u );
		Assert.Equal( 1u, output[ 133 ] );
		var lengthSquared = 0f;
		var separation = 0f;
		for ( var axis = 0; axis < 3; ++axis )
		{
			var normal = BitConverter.UInt32BitsToSingle( output[ 128 + axis ] );
			lengthSquared += normal * normal;
			separation += normal * (BitConverter.UInt32BitsToSingle( output[ 64 + axis ] ) - BitConverter.UInt32BitsToSingle( output[ axis ] ));
		}
		Assert.InRange( lengthSquared, .99999f, 1.00001f );
		Assert.True( separation > 0 );
		Assert.InRange( first[ 0 ], 1u, (uint)(count * 2 + 1) );
		Assert.InRange( second[ 0 ], 1u, (uint)(count * 2 + 1) );
	}

	[Fact]
	public void InvalidFeatureKindFailsWithoutChangingCallerBuffers()
	{
		var first = Polygon( 3, 1, 0, 0, 0 );
		var second = first.AsSpan().ToArray();
		second[ 140 ] = 9;
		var output = Output();
		var before = output.AsSpan().ToArray();
		Assert.Throws<ArgumentException>( () => CollisionFeatureContacts.Assemble( first, second, Vector( 0, 0, 1 ), output ) );
		Assert.Equal( before, output );
		Assert.Equal( 10u, first[ 0 ] );
		Assert.Equal( 10u, second[ 0 ] );
	}

	private static uint[] Polygon( int count, float radius, float x, float y, float height )
	{
		var points = new (float X, float Y)[ count ];
		for ( var i = 0; i < count; ++i )
			points[ i ] = (x + radius * MathF.Cos( i * 2 * MathF.PI / count ), y + radius * MathF.Sin( i * 2 * MathF.PI / count ));
		var feature = new uint[ 144 ];
		feature[ 0 ] = 10;
		feature[ 140 ] = (uint)count;
		Vector( 0, 0, 1 ).CopyTo( feature, 132 );
		for ( var i = 0; i < count; ++i )
		{
			var point = points[ i ];
			var next = points[ (i + 1) % count ];
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
		output[ 133 ] = 0;
		return output;
	}

	private static uint[] Vector( float x, float y, float z, float w = 0 ) =>
		[BitConverter.SingleToUInt32Bits( x ), BitConverter.SingleToUInt32Bits( y ), BitConverter.SingleToUInt32Bits( z ), BitConverter.SingleToUInt32Bits( w )];

	private static void Near( uint word, float expected ) => Assert.InRange( Math.Abs( BitConverter.UInt32BitsToSingle( word ) - expected ), 0f, .000002f );
}
