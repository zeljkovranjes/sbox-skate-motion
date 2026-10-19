using System.Numerics;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class TriangleBoundsQueryTests
{
	[Theory]
	[InlineData( -1.00001f, false )]
	[InlineData( -1f, true )]
	[InlineData( 0f, true )]
	[InlineData( 1f, true )]
	[InlineData( 1.00001f, false )]
	public void FaceTouchIsIncludedButSeparatedPlanesAreRejected( float height, bool expected )
	{
		Assert.Equal( expected, TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ),
			new( -2, height, -2 ), new( -2, height, 2 ), new( 2, height, -2 ) ) );
	}

	[Theory]
	[InlineData( -1f, true )]
	[InlineData( -.5f, false )]
	public void FacePlaneRejectsASeparatedCornerDespiteOverlappingCoordinateBounds( float low, bool expected )
	{
		Assert.Equal( expected, TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ),
			new( 2, 2, low ), new( 2, low, 2 ), new( low, 2, 2 ) ) );
	}

	[Fact]
	public void CrossingTriangleNeedsNoVertexInsideTheBox()
	{
		var a = new Vector3( -4, 0, -4 );
		var b = new Vector3( -4, 0, 4 );
		var c = new Vector3( 4, 0, 0 );
		Assert.True( TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ), a, b, c ) );
		Assert.True( TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ), c, b, a ) );
	}

	[Fact]
	public void PointAndSegmentTrianglesRetainClosedBoundsBehavior()
	{
		Assert.True( TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ), new( 1 ), new( 1 ), new( 1 ) ) );
		Assert.False( TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ), new( 2 ), new( 2 ), new( 2 ) ) );
		Assert.True( TriangleBoundsQuery.Intersects( new( -1 ), new( 1 ), new( -2, 0, 0 ), new( 2, 0, 0 ), Vector3.Zero ) );
	}

	[Fact]
	public void TranslationPreservesTheOverlapDecision()
	{
		var offset = new Vector3( 100, -200, 300 );
		Assert.True( TriangleBoundsQuery.Intersects( offset - Vector3.One, offset + Vector3.One,
			offset + new Vector3( -4, 0, -4 ), offset + new Vector3( -4, 0, 4 ), offset + new Vector3( 4, 0, 0 ) ) );
	}
}
