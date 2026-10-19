using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionShapeFeaturesTests
{
	[Fact]
	public void SphereOnlyWritesItsPointCountAndPrimaryFlag()
	{
		var shape = Shape( 1 );
		shape[ 32 ] = Word( 100f );
		var output = Seed();
		CollisionShapeFeatures.Select( shape, Direction( 1, 1, 1 ), true, 0, output );
		for ( var i = 0; i < output.Length; ++i )
		{
			var expected = i == 0 || i == 140 ? 0u : i >= 136 && i < 140 ? shape[ i - 136 ] : 0x3e800000u;
			Assert.Equal( expected, output[ i ] );
		}
	}

	[Fact]
	public void CapsuleSwitchesAtTheAdjacentThresholdWord()
	{
		var shape = Shape( 2 );
		var output = Seed();
		CollisionShapeFeatures.Select( shape, [0, 0, 0x3d4ccccbu, 0], false, 0, output );
		Assert.Equal( 1u, output[ 140 ] );
		Assert.InRange( Value( output[ 16 ] ), 2.49999f, 2.50001f );
		Assert.InRange( Value( output[ 10 ] ), -1.00001f, -.99999f );
		CollisionShapeFeatures.Select( shape, [0, 0, 0x3d4cccccu, 0], false, 0, output );
		Assert.Equal( 0u, output[ 140 ] );
		Assert.Equal( Word( 1.25f ), output[ 138 ] );
	}

	[Fact]
	public void ZeroLengthCapsuleHasFiniteZeroSegmentDirectionAndLength()
	{
		var shape = Shape( 2 );
		shape[ 28 ] = 0;
		var output = Seed();
		CollisionShapeFeatures.Select( shape, Direction( 1, 0, 0 ), false, 0, output );
		Assert.Equal( 1u, output[ 140 ] );
		Assert.Equal( new uint[ 4 ], output[ 8..12 ] );
		Assert.Equal( new uint[ 4 ], output[ 16..20 ] );
	}

	[Fact]
	public void TriangleVertexSelectionRetainsItsPrimaryFlag()
	{
		var output = Seed();
		CollisionShapeFeatures.Select( Shape( 3 ), Direction( 1, 0, 0 ), true, 0, output );
		Assert.Equal( 0u, output[ 140 ] );
		Assert.Equal( 0x3e800000u, output[ 0 ] );
		Assert.Equal( Word( 1f ), output[ 136 ] );
		Assert.Equal( 0u, output[ 137 ] );
		Assert.Equal( 0u, output[ 138 ] );
	}

	[Theory]
	[InlineData( 3, 3 )]
	[InlineData( 4, 4 )]
	[InlineData( 5, 8 )]
	public void BothContactSidesProduceClosedOrderedPolygons( int kind, uint edges )
	{
		var shape = Shape( kind );
		var direction = kind == 3 ? shape[ 4..8 ] : Direction( 0, 0, 1 );
		foreach ( var side in new[] { false, true } )
		{
			var output = Seed();
			CollisionShapeFeatures.Select( shape, direction, side, 0, output );
			Assert.Equal( edges, output[ 140 ] );
			for ( var edge = 0; edge < edges; ++edge )
				for ( var axis = 0; axis < 3; ++axis )
				{
					var start = Value( output[ 4 + edge * 16 + axis ] );
					var delta = Value( output[ 8 + edge * 16 + axis ] ) * Value( output[ 16 + edge * 16 ] );
					var next = Value( output[ 4 + ((edge + 1) % (int)edges) * 16 + axis ] );
					Assert.InRange( Math.Abs( start + delta - next ), 0f, .000002f );
				}
			Assert.Equal( new uint[] { 0x3e800000u, 0x3e800000u, 0x3e800000u, 0x3e800000u }, output[ 144..148 ] );
		}
	}

	[Fact]
	public void BoxThresholdChangesFaceToEdgeWithoutChangingSupportSide()
	{
		var shape = Shape( 4 );
		var output = Seed();
		CollisionShapeFeatures.Select( shape, [0x3e4ccccbu, 0, Word( 1f ), 0], false, 0, output );
		Assert.Equal( 4u, output[ 140 ] );
		CollisionShapeFeatures.Select( shape, [0x3e4cccccu, 0, Word( 1f ), 0], false, 0, output );
		Assert.Equal( 1u, output[ 140 ] );
		Assert.Equal( Word( .5f ), output[ 6 ] );
	}

	[Fact]
	public void BoxZeroDirectionRetainsTheFirstCornerOnTies()
	{
		var output = Seed();
		CollisionShapeFeatures.Select( Shape( 4 ), new uint[ 4 ], true, 0, output );
		Assert.Equal( 0u, output[ 140 ] );
		Assert.Equal( new uint[] { Word( -1.25f ), Word( .75f ), Word( .5f ), Word( 1f ) }, output[ 136..140 ] );
	}

	[Fact]
	public void BoxSegmentRetainsRelocatableDescriptorIdentity()
	{
		var first = Seed();
		var second = Seed();
		var direction = Direction( 1, 0, 1 );
		CollisionShapeFeatures.Select( Shape( 4 ), direction, false, 0x30000u, first );
		CollisionShapeFeatures.Select( Shape( 4 ), direction, false, 0x33000u, second );
		Assert.Equal( 1u, first[ 140 ] );
		Assert.Equal( 0x30010u, first[ 12 ] );
		Assert.Equal( 16u, first[ 13 ] );
		for ( var i = 0; i < first.Length; ++i )
			Assert.Equal( i == 12 ? first[ i ] + 0x3000u : first[ i ], second[ i ] );
	}

	[Fact]
	public void CylinderPointRetainsFlagsAndTransformsItsNormal()
	{
		var output = Seed();
		CollisionShapeFeatures.Select( Shape( 5 ), Direction( .8f, 0, .6f ), true, 0, output );
		Assert.Equal( 0u, output[ 140 ] );
		Assert.Equal( 0x3e800000u, output[ 0 ] );
		Assert.Equal( Word( 1.25f ), output[ 138 ] );
		Assert.InRange( Value( output[ 136 ] ), .74999f, .75001f );
		Assert.Equal( Direction( .8f, 0, .6f ), output[ 132..136 ] );
	}

	[Fact]
	public void InvalidBuffersAndUnsupportedDescriptorsLeaveDestinationUntouched()
	{
		var output = Seed();
		Assert.Throws<ArgumentException>( () => CollisionShapeFeatures.Select( new uint[ 43 ], new uint[ 4 ], false, 0, output ) );
		Assert.Throws<ArgumentException>( () => CollisionShapeFeatures.Select( Shape( 1 ), new uint[ 3 ], false, 0, output ) );
		Assert.Throws<ArgumentException>( () => CollisionShapeFeatures.Select( Shape( 1 ), new uint[ 4 ], false, 0, new uint[ 143 ] ) );
		var shape = Shape( 1 );
		shape[ 36 ] = 6;
		Assert.Throws<ArgumentOutOfRangeException>( () => CollisionShapeFeatures.Select( shape, new uint[ 4 ], false, 0, output ) );
		Assert.Equal( Seed(), output );
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
	private static float Value( uint value ) => BitConverter.UInt32BitsToSingle( value );
	private static uint[] Direction( float x, float y, float z ) => [Word( x ), Word( y ), Word( z ), 0];
	private static uint[] Seed()
	{
		var output = new uint[ 148 ];
		Array.Fill( output, 0x3e800000u );
		return output;
	}

	private static uint[] Shape( int kind )
	{
		uint[] source =
		[
			Word( 1f ), 0, 0, 0, 0, Word( 1f ), 0, 0,
			0, 0, Word( 1f ), 0, 0, 0, 0, Word( 1f ),
			0, Word( 1.25f ), Word( .75f ), Word( .5f ), Word( .125f ), 0, 0, 0
		];
		if ( kind == 3 )
			source[ 23 ] = 2;
		var shape = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( kind, source, shape, [], 0 ) );
		return shape;
	}
}
