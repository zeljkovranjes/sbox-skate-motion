using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class CollisionWorldTriangleConstructionTests
{
	[Fact]
	public void GeometryKeepsNormalRefinementsAndOrderedEdges()
	{
		var result = Construct();
		Assert.Equal( new uint[]
		{
			0, 0, 0, 0x3f800000, 0, 0, 0x3f7fffff, 0,
			0x40000000, 0, 0, 0x3f800000, 0, 0x40400000, 0, 0x3f800000,
			0, 0x3f7fffff, 0, 0, 0x3f0e00d5, 0xbf55013f, 0, 0,
			0xbf7fffff, 0, 0, 0, 0x40400001, 0x4066c15a, 0x40000000, 0
		}, result.Take( 32 ) );
	}

	[Fact]
	public void MissingAuthoredEdgesSelectsExplicitBoundaryMode()
	{
		var result = Construct();
		Assert.Equal( 0x1e0u, result[ 37 ] );
		Assert.Equal( new uint[] { 0, 0, 0 }, result.Skip( 38 ).Take( 3 ) );
	}

	[Fact]
	public void AuthoredEdgesKeepPackedFlagsAndCosineOrder()
	{
		var result = Construct( [ 0x20, 0x41, 0x62 ], 0x10 );
		Assert.Equal( 0xdb1u, result[ 37 ] );
		Assert.Equal( new uint[] { 0xbe6f4f20, 0x3ec42c38, 0x3f310b0e }, result.Skip( 38 ).Take( 3 ) );
	}

	[Theory]
	[InlineData( 0u, 0x101u )]
	[InlineData( 0x10u, 0x111u )]
	[InlineData( 0xffffffeFu, 0x101u )]
	public void OnlyAuthoredCallerFlagIsMerged( uint callerFlags, uint expected )
	{
		Assert.Equal( expected, Construct( [ 0, 1, 2 ], callerFlags )[ 37 ] );
	}

	[Fact]
	public void MaterialIdentityAndPaddingSurviveConstruction()
	{
		var result = Construct();
		Assert.Equal( 0u, result[ 32 ] );
		Assert.Equal( 0xabcdef12u, result[ 33 ] );
		Assert.Equal( 0x34231255u, result[ 34 ] );
		Assert.Equal( 0x0103beefu, result[ 35 ] );
		Assert.Equal( 3u, result[ 36 ] );
		Assert.Equal( new uint[] { 0xdeadbeef, 0xdeadbeef, 0xdeadbeef }, result.Skip( 41 ) );
	}

	[Fact]
	public void TransformChangesVerticesBeforeNormalConstruction()
	{
		var transform = Frame();
		transform[ 12 ] = Word( 5 );
		transform[ 13 ] = Word( -7 );
		transform[ 14 ] = Word( 11 );
		var result = new uint[ 44 ];
		CollisionWorldTriangleConstruction.Construct( Vertices(), transform, result, 0, 0, 0 );
		Assert.Equal( new uint[] { Word( 5 ), Word( -7 ), Word( 11 ), Word( 1 ) }, result.Take( 4 ) );
		Assert.Equal( new uint[] { Word( 7 ), Word( -7 ), Word( 11 ), Word( 1 ) }, result.Skip( 8 ).Take( 4 ) );
		Assert.Equal( 0x3f7fffffu, result[ 6 ] );
	}

	[Fact]
	public void InputsRemainUnchanged()
	{
		var vertices = Vertices();
		var transform = Frame();
		byte[] edges = [ 0x20, 0x41, 0x62 ];
		CollisionWorldTriangleConstruction.Construct( vertices, transform, new uint[ 44 ], 0, 0, 0, edges );
		Assert.Equal( Vertices(), vertices );
		Assert.Equal( Frame(), transform );
		Assert.Equal( new byte[] { 0x20, 0x41, 0x62 }, edges );
	}

	[Fact]
	public void PartialAuthoredEdgeSetIsRejected()
	{
		Assert.Throws<ArgumentException>( () => Construct( [ 0, 1 ] ) );
	}

	private static uint[] Construct( byte[]? edges = null, uint callerFlags = 0 )
	{
		var result = Enumerable.Repeat( 0xdeadbeefu, 44 ).ToArray();
		CollisionWorldTriangleConstruction.Construct( Vertices(), Frame(), result, 0x1255, 0x3423,
			0xabcdef12, edges, callerFlags );
		return result;
	}

	private static uint[] Vertices() => [ 0, 0, 0, Word( 1 ), Word( 2 ), 0, 0, Word( 1 ), 0, Word( 3 ), 0, Word( 1 ) ];
	private static uint[] Frame() => [ Word( 1 ), 0, 0, 0, 0, Word( 1 ), 0, 0, 0, 0, Word( 1 ), 0, 0, 0, 0, Word( 1 ) ];
	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
