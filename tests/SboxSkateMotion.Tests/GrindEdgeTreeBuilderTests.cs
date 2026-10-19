using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeTreeBuilderTests
{
	private static readonly GrindEdgeBounds Full = new( new Vector3( -10f ), new Vector3( 10f ) );

	[Fact]
	public void ThreeSmallEdgesStayInReverseInsertionOrder()
	{
		Assert.Equal( new uint[] { 2, 1, 0 }, Query( Cluster( 3 ) ) );
	}

	[Fact]
	public void FourthSmallEdgeSplitsAndReversesTheExistingChain()
	{
		var chunk = Cluster( 4 );
		Assert.Equal( 2, chunk.NodeCount );
		Assert.Equal( new uint[] { 0, 1, 2, 3 }, Query( chunk ) );
	}

	[Fact]
	public void LaterInsertionSplitsOneMoreLevel()
	{
		var chunk = Cluster( 5 );
		Assert.Equal( 3, chunk.NodeCount );
		Assert.Equal( new uint[] { 3, 2, 1, 0, 4 }, Query( chunk ) );
	}

	[Fact]
	public void ExhaustedNodePoolKeepsNewEdgesInTheExistingLeaf()
	{
		var chunk = Cluster( 8 );
		Assert.Equal( 5, chunk.NodeCount );
		Assert.Equal( new uint[] { 7, 5, 3, 2, 1, 0, 4, 6 }, Query( chunk ) );
	}

	[Fact]
	public void BroadEdgesDoNotCountTowardTheSplitThreshold()
	{
		var builder = new GrindEdgeTreeBuilder( Full, 8 );
		for ( var i = 0; i < 8; i++ )
			Insert( builder, i, new( new Vector3( -9f ), new Vector3( 1f ) ) );
		var chunk = builder.Build();
		Assert.Equal( 1, chunk.NodeCount );
		Assert.Equal( Enumerable.Range( 0, 8 ).Reverse().Select( i => (uint)i ), Query( chunk ) );
	}

	[Fact]
	public void TreeCubeAndOuterChunkBoundsStaySeparate()
	{
		var outer = new GrindEdgeBounds( new Vector3( -10f, -1f, -1f ), new Vector3( 10f, 1f, 1f ) );
		var builder = new GrindEdgeTreeBuilder( outer, 1 );
		Insert( builder, 0, new( new Vector3( -1f, -.8f, -.8f ), new Vector3( 1f, .8f, .8f ) ) );
		var chunk = builder.Build();
		Assert.Equal( outer, chunk.Bounds );
		Assert.Equal( Full, chunk.TreeBounds );
		var query = new GrindEdgeBounds( new Vector3( -.1f, .5f, -.1f ), new Vector3( .1f, .6f, .1f ) );
		Assert.Equal( 1, GrindEdgeQuery.Collect( [ chunk ], query, false, new GrindEdgeRecord[ 40 ] ) );
	}

	[Fact]
	public void OffCenterBoundsProduceAnOffCenterCube()
	{
		var bounds = new GrindEdgeBounds( new Vector3( -10f, -3f, -2f ), new Vector3( 8f, 5f, 2f ) );
		var chunk = new GrindEdgeTreeBuilder( bounds, 0 ).Build();
		Assert.Equal( new GrindEdgeBounds( new Vector3( -10f, -8f, -9f ), new Vector3( 8f, 10f, 9f ) ), chunk.TreeBounds );
		Assert.Empty( Query( chunk ) );
	}

	[Fact]
	public void TouchingBothInnerPlanesKeepsTheEdgeAtTheNode()
	{
		var builder = new GrindEdgeTreeBuilder( Full, 1 );
		Insert( builder, 0, new( new Vector3( -2f ), new Vector3( 2f ) ) );
		Assert.Equal( 0, builder.Build().Node( 0 ).Head );
	}

	[Fact]
	public void InputIdentityDoesNotReplaceInsertionOrder()
	{
		var builder = new GrindEdgeTreeBuilder( Full, 3 );
		foreach ( var index in new[] { 1, 0, 2 } )
			Insert( builder, index, new( new Vector3( -7f ), new Vector3( -6.99f ) ) );
		Assert.Equal( new uint[] { 2, 0, 1 }, Query( builder.Build() ) );
	}

	[Fact]
	public void IncompleteAndDuplicateInputsAreRejected()
	{
		var builder = new GrindEdgeTreeBuilder( Full, 2 );
		Insert( builder, 0, Full );
		Assert.Throws<InvalidOperationException>( () => builder.Build() );
		Assert.Throws<ArgumentException>( () => Insert( builder, 0, Full ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => Insert( builder, 2, Full ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => new GrindEdgeTreeBuilder( Full, 65536 ) );
	}

	private static GrindEdgeChunk Cluster( int count )
	{
		var builder = new GrindEdgeTreeBuilder( Full, count );
		for ( var i = 0; i < count; i++ )
			Insert( builder, i, new( new Vector3( -7f + i * .0001f ), new Vector3( -6.99f + i * .0001f ) ) );
		return builder.Build();
	}

	private static void Insert( GrindEdgeTreeBuilder builder, int index, GrindEdgeBounds bounds ) =>
		builder.Insert( index, bounds, new GrindEdgeRecord( new RailSegment( bounds.Minimum, bounds.Maximum ), (uint)index, 0 ) );

	private static uint[] Query( GrindEdgeChunk chunk )
	{
		var output = new GrindEdgeRecord[ 40 ];
		var count = GrindEdgeQuery.Collect( [ chunk ], Full, false, output );
		return output.Take( count ).Select( record => record.Metadata ).ToArray();
	}
}
