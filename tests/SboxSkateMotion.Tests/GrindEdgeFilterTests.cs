using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeFilterTests
{
	[Theory]
	[InlineData( .009f, -1f, 2, 0 )]
	[InlineData( .01f, -1f, 2, 0 )]
	[InlineData( .011f, -1f, 1, 0 )]
	[InlineData( .011f, 1f, 1, 1 )]
	[InlineData( .02f, -1f, 2, 0 )]
	[InlineData( .021f, -1f, 1, 1 )]
	[InlineData( .099f, -1f, 1, 1 )]
	[InlineData( .1f, -1f, 2, 0 )]
	public void HeightBoundariesRetainOrderedCandidates( float height, float referenceHeight, int count, int first )
	{
		var output = new int[ 2 ];
		var actual = GrindEdgeFilter.Filter( [ Edge( 0f ), Edge( height ) ], new Vector3( 0f, referenceHeight, 0f ), output );
		Assert.Equal( count, actual );
		Assert.Equal( first, output[ 0 ] );
		if ( count == 2 )
			Assert.Equal( 1, output[ 1 ] );
	}

	[Fact]
	public void CollinearDuplicatesAreRetained()
	{
		var output = new int[ 2 ];
		Assert.Equal( 2, GrindEdgeFilter.Filter( [ Edge( 0f ), Edge( 0f ) ], Vector3.Zero, output ) );
		Assert.Equal( new[] { 0, 1 }, output );
	}

	[Fact]
	public void SeparateSegmentsDoNotSuppressEachOther()
	{
		var second = new RailSegment( new Vector3( 2f, .05f, 0f ), new Vector3( 4f, .05f, 0f ) );
		Assert.Equal( 2, GrindEdgeFilter.Filter( [ Edge( 0f ), second ], Vector3.Zero, new int[ 2 ] ) );
	}

	[Fact]
	public void ReversingEdgePreservesHeightSuppression()
	{
		var edge = Edge( 0f );
		var output = new int[ 2 ];
		Assert.Equal( 1, GrindEdgeFilter.Filter( [ new( edge.End, edge.Start ), Edge( .05f ) ], Vector3.Zero, output ) );
		Assert.Equal( 1, output[ 0 ] );
	}

	[Fact]
	public void OutputCapacityIsCheckedBeforeWriting()
	{
		var output = new[] { 91 };
		Assert.Throws<ArgumentException>( () => GrindEdgeFilter.Filter( [ Edge( 0f ), Edge( .05f ) ], Vector3.Zero, output ) );
		Assert.Equal( 91, output[ 0 ] );
		Assert.Throws<ArgumentException>( () => GrindEdgeFilter.Filter( new RailSegment[ 41 ], Vector3.Zero, new int[ 41 ] ) );
	}

	private static RailSegment Edge( float height ) => new( new Vector3( -1f, height, 0f ), new Vector3( 1f, height, 0f ) );
}
