using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionContactManifoldTests
{
	[Fact]
	public void SphereContactsExpandBothRadiiAndPackShapeMetadata()
	{
		var first = Sphere( 0, 1, 101, 201 );
		var second = Sphere( 1, .5f, 102, 202 );
		var firstBefore = first.AsSpan().ToArray();
		var secondBefore = second.AsSpan().ToArray();
		var output = Output();
		Assert.Equal( 1, CollisionContactManifold.Assemble( first, second, 0, default, output ) );
		Assert.Equal( new uint[] { 101, 102, 201, 202, 1 }, output.Take( 5 ) );
		Near( output[ 8 ], -1 );
		Near( output[ 12 ], 1 );
		Near( output[ 16 ], .5f );
		Assert.Equal( Word( 1 ), output[ 15 ] );
		Assert.Equal( Word( 1 ), output[ 19 ] );
		Assert.All( output.Skip( 5 ).Take( 3 ).Concat( output.Skip( 20 ) ), value => Assert.Equal( Guard, value ) );
		Assert.Equal( firstBefore, first );
		Assert.Equal( secondBefore, second );
	}

	[Theory]
	[InlineData( .49f, 0 )]
	[InlineData( .5f, 1 )]
	public void MarginIncludesTheExactRadiusBoundary( float margin, int expectedCount )
	{
		var output = Output();
		Assert.Equal( expectedCount, CollisionContactManifold.Assemble( Sphere( 0, 1 ), Sphere( 2, .5f ), Word( margin ), default, output ) );
		if ( expectedCount == 0 )
			Assert.All( output, value => Assert.Equal( Guard, value ) );
		else
		{
			Near( output[ 12 ], 1 );
			Near( output[ 16 ], 1.5f );
		}
	}

	[Fact]
	public void SeparationRejectionLeavesEveryOutputWordUnchanged()
	{
		var output = Output();
		Assert.Equal( 0, CollisionContactManifold.Assemble( Sphere( 0, 1 ), Sphere( 3, .5f ), 0, default, output ) );
		Assert.All( output, value => Assert.Equal( Guard, value ) );
	}

	[Theory]
	[InlineData( .25f, 1 )]
	[InlineData( -.25f, 0 )]
	public void OneSidedTriangleFiltersAcceptanceAfterWritingItsContact( float height, int expectedCount )
	{
		var sphere = Sphere( .5f, .5f );
		sphere[ 1 ] = Word( .5f );
		sphere[ 2 ] = Word( height );
		var output = Output();
		Assert.Equal( expectedCount, CollisionContactManifold.Assemble( sphere, Triangle(), 0, default, output ) );
		Assert.Equal( 1u, output[ 4 ] );
		Near( output[ 18 ], 0 );
		Near( output[ 10 ], height > 0 ? 1 : -1 );
	}

	[Fact]
	public void FirstTriangleDoesNotUseTheSecondTriangleFilter()
	{
		var sphere = Sphere( .5f, .5f );
		sphere[ 1 ] = Word( .5f );
		sphere[ 2 ] = Word( -.25f );
		Assert.Equal( 1, CollisionContactManifold.Assemble( Triangle(), sphere, 0, default, Output() ) );
	}

	[Fact]
	public void CoincidentSpheresRetainZeroDirectionWithOutputSignToggle()
	{
		var output = Output();
		Assert.Equal( 1, CollisionContactManifold.Assemble( Sphere( 0, 1 ), Sphere( 0, .5f ), 0, default, output ) );
		Assert.All( output.Skip( 8 ).Take( 4 ), value => Assert.Equal( 0x80000000u, value ) );
		Assert.All( output.Skip( 12 ).Take( 3 ).Concat( output.Skip( 16 ).Take( 3 ) ), value => Assert.Equal( 0u, value ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public void InvalidStorageOrDescriptorFailsBeforeOutputChanges( int scenario )
	{
		var first = Sphere( 0, 1 );
		var second = Sphere( 1, 1 );
		var output = scenario == 0 ? Enumerable.Repeat( Guard, 139 ).ToArray() : Output();
		if ( scenario == 1 )
			first = first.Take( 43 ).ToArray();
		if ( scenario == 2 )
			second[ 36 ] = 6;
		Assert.Throws<ArgumentException>( () => CollisionContactManifold.Assemble( first, second, 0, default, output ) );
		Assert.All( output, value => Assert.Equal( Guard, value ) );
	}

	private const uint Guard = 0x12345678u;
	private static uint[] Output() => Enumerable.Repeat( Guard, 144 ).ToArray();
	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
	private static void Near( uint word, float expected ) => Assert.InRange( Math.Abs( BitConverter.UInt32BitsToSingle( word ) - expected ), 0f, .000002f );

	private static uint[] Sphere( float x, float radius, uint identity = 0, uint material = 0 )
	{
		var shape = new uint[ 44 ];
		shape[ 0 ] = Word( x );
		shape[ 3 ] = Word( 1 );
		shape[ 32 ] = Word( radius );
		shape[ 33 ] = identity;
		shape[ 34 ] = material;
		shape[ 36 ] = 1;
		return shape;
	}

	private static uint[] Triangle()
	{
		var source = new uint[ 24 ];
		source[ 3 ] = source[ 7 ] = source[ 11 ] = Word( 1 );
		source[ 4 ] = Word( 2 );
		source[ 9 ] = Word( 3 );
		source[ 14 ] = Word( 1 );
		source[ 23 ] = 0x10;
		var shape = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( 3, source, shape, ReadOnlySpan<uint>.Empty, 0 ) );
		return shape;
	}
}
