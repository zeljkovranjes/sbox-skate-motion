using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionCylinderPairAxesTests
{
	[Theory]
	[InlineData( 1, 2, 0xbf3fffffu )]
	[InlineData( 2, 4, 0xbf3fffffu )]
	[InlineData( 3, 8, 0xbf3fffffu )]
	[InlineData( 4, 16, 0xbf3fffffu )]
	[InlineData( 5, 7, 0xbfbfffffu )]
	public void CoincidentPrimitivePairsPreserveCandidateCountsAndGap( int kind, int expectedCount, uint expectedGap )
	{
		var cylinder = Shape( 5 );
		var other = Shape( kind );
		var candidates = Enumerable.Repeat( 0x12345678u, 68 ).ToArray();
		Assert.Equal( expectedCount, CollisionPairAxes.BuildCylinderCandidates( cylinder, other, candidates ) );
		Assert.Equal( new uint[] { 0, 0, 0x3f800000u, 0 }, candidates.Take( 4 ) );
		Assert.All( candidates.Skip( expectedCount * 4 ), value => Assert.Equal( 0x12345678u, value ) );
		var output = new uint[ 8 ];
		CollisionPairAxes.Pair( cylinder, other, output );
		Assert.All( output.Take( 4 ), value => Assert.Equal( expectedGap, value ) );
		Assert.Equal( kind == 3 ? 0x3f7fffffu : 0u, output[ 4 ] );
	}

	[Fact]
	public void CapsuleCandidatesKeepNegativeCapOrientationAndZeroCross()
	{
		var candidates = new uint[ 64 ];
		CollisionPairAxes.BuildCylinderCandidates( Shape( 5 ), Shape( 2 ), candidates );
		Assert.Equal( new uint[] { 0x80000000u, 0x80000000u, 0xbf800000u, 0x80000000u }, candidates.Skip( 8 ).Take( 4 ) );
		Assert.All( candidates.Skip( 12 ).Take( 4 ), value => Assert.Equal( 0u, value ) );
	}

	[Fact]
	public void ReversedWrapperTogglesEveryNormalSignBitAndPreservesInputsAndGuards()
	{
		var cylinder = Shape( 5 );
		var triangle = Shape( 3 );
		var beforeCylinder = cylinder.AsSpan().ToArray();
		var beforeTriangle = triangle.AsSpan().ToArray();
		var forward = Enumerable.Repeat( 0x12345678u, 12 ).ToArray();
		var reverse = forward.AsSpan().ToArray();
		CollisionPairAxes.CylinderPair( cylinder, triangle, forward );
		CollisionPairAxes.Pair( triangle, cylinder, reverse );
		Assert.Equal( forward.Take( 4 ), reverse.Take( 4 ) );
		for ( var i = 4; i < 8; ++i )
			Assert.Equal( forward[ i ] ^ 0x80000000u, reverse[ i ] );
		Assert.All( forward.Skip( 8 ).Concat( reverse.Skip( 8 ) ), value => Assert.Equal( 0x12345678u, value ) );
		Assert.Equal( beforeCylinder, cylinder );
		Assert.Equal( beforeTriangle, triangle );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void InvalidCandidateArgumentsLeaveCallerMemoryUntouched( int scenario )
	{
		var cylinder = scenario == 0 ? Shape( 2 ) : Shape( 5 );
		var other = Shape( 4 );
		var candidates = Enumerable.Repeat( 0x12345678u, scenario == 1 ? 63 : 64 ).ToArray();
		if ( scenario == 2 )
			other[ 36 ] = 6;
		if ( scenario == 3 )
			other[ 35 ] = 0x04030000u;
		Assert.Throws<ArgumentException>( () => CollisionPairAxes.BuildCylinderCandidates( cylinder, other, candidates ) );
		Assert.All( candidates, value => Assert.Equal( 0x12345678u, value ) );
	}

	[Fact]
	public void ShortPairOutputFailsBeforeWriting()
	{
		var output = Enumerable.Repeat( 0x12345678u, 7 ).ToArray();
		Assert.Throws<ArgumentException>( () => CollisionPairAxes.Pair( Shape( 5 ), Shape( 4 ), output ) );
		Assert.All( output, value => Assert.Equal( 0x12345678u, value ) );
	}

	private static uint[] Shape( int kind )
	{
		if ( kind == 3 )
		{
			var source = new uint[ 24 ];
			source[ 3 ] = source[ 7 ] = source[ 11 ] = Word( 1 );
			source[ 4 ] = Word( 2 );
			source[ 9 ] = Word( 3 );
			source[ 14 ] = Word( 1 );
			var triangle = new uint[ 44 ];
			Assert.True( CollisionShapeConstruction.TryConstruct( 3, source, triangle, ReadOnlySpan<uint>.Empty, 0 ) );
			return triangle;
		}
		var shape = new uint[ 44 ];
		shape[ 3 ] = Word( 1 );
		shape[ 18 ] = Word( 1 );
		shape[ 28 ] = Word( 1.25f );
		shape[ 29 ] = Word( .75f );
		shape[ 35 ] = kind == 5 ? 0x01010000u : kind == 2 ? 0x00010000u : 0;
		shape[ 36 ] = (uint)kind;
		if ( kind == 5 )
			shape[ 6 ] = Word( 1 );
		if ( kind == 4 )
		{
			shape[ 18 ] = 0;
			shape[ 4 ] = shape[ 9 ] = shape[ 14 ] = Word( 1 );
			shape[ 16 ] = shape[ 21 ] = shape[ 26 ] = Word( 1 );
			shape[ 30 ] = Word( .5f );
			shape[ 35 ] = 0x03030000u;
		}
		return shape;
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
