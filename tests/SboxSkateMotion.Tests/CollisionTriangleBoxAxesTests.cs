using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionTriangleBoxAxesTests
{
	[Theory]
	[InlineData( 2f, 1f )]
	[InlineData( -2f, -1f )]
	public void FlatTriangleKeepsVerticalBoxGap( float height, float direction )
	{
		var output = new uint[ 8 ];
		CollisionPairAxes.TriangleBoxPair( Triangle(), Box( height ), output );
		Near( output[ 0 ], 1.75f );
		Near( output[ 4 ], 0 );
		Near( output[ 5 ], 0 );
		Near( output[ 6 ], direction );
	}

	[Fact]
	public void ReverseOrderFlipsAllDirectionSignBitsWithoutChangingGap()
	{
		var triangle = Triangle();
		var box = Box( 2 );
		var forward = new uint[ 8 ];
		var reverse = new uint[ 8 ];
		CollisionPairAxes.TriangleBoxPair( triangle, box, forward );
		CollisionPairAxes.BoxTrianglePair( box, triangle, reverse );
		for ( var i = 0; i < 4; ++i )
		{
			Assert.Equal( forward[ i ], reverse[ i ] );
			Assert.Equal( forward[ 4 + i ] ^ 0x80000000u, reverse[ 4 + i ] );
		}
	}

	[Fact]
	public void OverlappingAlignedGeometryRetainsZeroCrossCandidate()
	{
		var output = new uint[ 8 ];
		CollisionPairAxes.TriangleBoxPair( Triangle(), Box( 0 ), output );
		Assert.All( output, value => Assert.Equal( 0u, value ) );
	}

	[Fact]
	public void SeparationLeavesDescriptorsAndUnusedOutputUnchanged()
	{
		var triangle = Triangle();
		var box = Box( 2 );
		var triangleBefore = triangle.AsSpan().ToArray();
		var boxBefore = box.AsSpan().ToArray();
		var output = Enumerable.Repeat( 0x12345678u, 12 ).ToArray();
		CollisionPairAxes.TriangleBoxPair( triangle, box, output );
		Assert.Equal( triangleBefore, triangle );
		Assert.Equal( boxBefore, box );
		Assert.All( output.Skip( 8 ), value => Assert.Equal( 0x12345678u, value ) );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void WrongOrderedKindsFailBeforeWritingOutput( bool reverse )
	{
		var triangle = Triangle();
		var box = Box( 2 );
		var output = Enumerable.Repeat( 0x12345678u, 8 ).ToArray();
		if ( reverse )
			Assert.Throws<ArgumentException>( () => CollisionPairAxes.BoxTrianglePair( triangle, box, output ) );
		else
			Assert.Throws<ArgumentException>( () => CollisionPairAxes.TriangleBoxPair( box, triangle, output ) );
		Assert.All( output, value => Assert.Equal( 0x12345678u, value ) );
	}

	[Fact]
	public void IncompleteOutputFailsBeforeAnyWrite()
	{
		var output = Enumerable.Repeat( 0x12345678u, 7 ).ToArray();
		Assert.Throws<ArgumentException>( () => CollisionPairAxes.TriangleBoxPair( Triangle(), Box( 2 ), output ) );
		Assert.All( output, value => Assert.Equal( 0x12345678u, value ) );
	}

	private static uint[] Triangle()
	{
		var source = new uint[ 24 ];
		Set( source, 0, -1, -1, 0, 1 );
		Set( source, 4, 1, -1, 0, 1 );
		Set( source, 8, 0, 1, 0, 1 );
		source[ 23 ] = 2;
		var result = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( 3, source, result, ReadOnlySpan<uint>.Empty, 0 ) );
		return result;
	}

	private static uint[] Box( float height )
	{
		var source = new uint[ 24 ];
		Set( source, 0, 1, 0, 0, 0 );
		Set( source, 4, 0, 1, 0, 0 );
		Set( source, 8, 0, 0, 1, 0 );
		Set( source, 12, 0, 0, height, 1 );
		Set( source, 16, 0, .25f, .25f, .25f );
		var result = new uint[ 44 ];
		Assert.True( CollisionShapeConstruction.TryConstruct( 4, source, result, ReadOnlySpan<uint>.Empty, 0 ) );
		return result;
	}

	private static void Set( uint[] target, int offset, float x, float y, float z, float w )
	{
		target[ offset ] = BitConverter.SingleToUInt32Bits( x );
		target[ offset + 1 ] = BitConverter.SingleToUInt32Bits( y );
		target[ offset + 2 ] = BitConverter.SingleToUInt32Bits( z );
		target[ offset + 3 ] = BitConverter.SingleToUInt32Bits( w );
	}

	private static void Near( uint word, float expected ) => Assert.InRange( Math.Abs( BitConverter.UInt32BitsToSingle( word ) - expected ), 0f, .000002f );
}
