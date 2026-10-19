using System;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionShapeConstructionTests
{
	[Theory]
	[InlineData( 1, 0x00005678u )]
	[InlineData( 2, 0x00015678u )]
	[InlineData( 3, 0x01035678u )]
	[InlineData( 4, 0x03035678u )]
	[InlineData( 5, 0x01015678u )]
	public void ConstructionRetainsIdentityAndFeatureMetadata( int kind, uint expectedFeatures )
	{
		var source = Source();
		var destination = new uint[ 44 ];
		destination[ 35 ] = 0x12345678u;
		source[ 23 ] = 0x12340000u;
		Assert.True( CollisionShapeConstruction.TryConstruct( kind, source, destination, [], 27u ) );
		Assert.Equal( expectedFeatures, destination[ 35 ] );
		Assert.Equal( 27u, destination[ 33 ] );
		Assert.Equal( 0u, destination[ 34 ] );
		Assert.Equal( (uint)kind, destination[ 36 ] );
		Assert.Equal( source[ 20 ], destination[ 32 ] );
		Assert.Equal( source[ 23 ], destination[ 37 ] );
	}

	[Fact]
	public void SphereTransformsCenterWithoutOverwritingUnusedAxes()
	{
		var source = Source();
		source[ 12 ] = Word( 2f );
		uint[] transform = [0, Word( 1f ), 0, 0, Word( -1f ), 0, 0, 0, 0, 0, Word( 1f ), 0, Word( 3f ), Word( 4f ), Word( 5f ), Word( 1f )];
		var destination = new uint[ 44 ];
		destination[ 4 ] = 0x01234567u;
		Assert.True( CollisionShapeConstruction.TryConstruct( 1, source, destination, transform, 9u ) );
		Assert.Equal( new[] { Word( 3f ), Word( 6f ), Word( 5f ), Word( 1f ) }, destination[ ..4 ] );
		Assert.Equal( 0x01234567u, destination[ 4 ] );
	}

	[Fact]
	public void CylinderUsesAxialThenRadialBasisOrder()
	{
		var destination = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( 5, Source(), destination, [], 0u ) );
		Assert.Equal( new uint[] { 0, 0, Word( 1f ), 0 }, destination[ 4..8 ] );
		Assert.Equal( new uint[] { Word( 1f ), 0, 0, 0 }, destination[ 12..16 ] );
		Assert.Equal( destination[ 4..8 ], destination[ 16..20 ] );
		Assert.Equal( Word( 1.25f ), destination[ 28 ] );
		Assert.Equal( Word( .75f ), destination[ 29 ] );
	}

	[Fact]
	public void DirtyTriangleCachesNormalAndPreservesAdjacencyAcrossRepeatedConstruction()
	{
		var source = Source();
		Array.Clear( source, 0, 16 );
		source[ 4 ] = Word( 2f );
		source[ 9 ] = Word( 3f );
		source[ 23 ] = 0x1002u;
		var first = new uint[ 44 ];
		var second = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( 3, source, first, [], 7u ) );
		Assert.Equal( 0x1000u, source[ 23 ] );
		Assert.InRange( BitConverter.UInt32BitsToSingle( source[ 14 ] ), .99999f, 1.00001f );
		Assert.Equal( source[ 17 ], first[ 38 ] );
		Assert.Equal( source[ 18 ], first[ 39 ] );
		Assert.Equal( source[ 19 ], first[ 40 ] );
		var cached = source.AsSpan().ToArray();
		Assert.True( CollisionShapeConstruction.TryConstruct( 3, source, second, [], 7u ) );
		Assert.Equal( cached, source );
		Assert.Equal( first, second );
	}

	[Fact]
	public void MeshContainerReturnsFalseWithoutInventingPrimitiveGeometry()
	{
		var source = Source();
		var destination = new uint[ 44 ];
		Array.Fill( destination, 0x10203040u );
		var before = destination.AsSpan().ToArray();
		Assert.False( CollisionShapeConstruction.TryConstruct( 6, source, destination, [], 5u ) );
		Assert.Equal( before, destination );
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );

	private static uint[] Source() =>
	[
		Word( 1f ), 0, 0, 0,
		0, Word( 1f ), 0, 0,
		0, 0, Word( 1f ), 0,
		0, 0, 0, Word( 1f ),
		0, Word( 1.25f ), Word( .75f ), Word( .5f ),
		Word( .125f ), 0, 0, 0
	];
}
