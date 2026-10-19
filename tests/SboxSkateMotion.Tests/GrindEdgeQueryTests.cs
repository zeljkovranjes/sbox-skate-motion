using System;
using System.Linq;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindEdgeQueryTests
{
	private static readonly GrindEdgeBounds Full = new( new Vector3( -10f ), new Vector3( 10f ) );

	[Fact]
	public void ParentChainsPrecedeChildrenAndPendingChildrenAreLastInFirstOut()
	{
		var root = Children();
		root[ 0 ] = 1;
		root[ 2 ] = 1;
		root[ 6 ] = 2;
		root[ 7 ] = 2;
		var child = Children();
		child[ 7 ] = 3;
		var nodes = new[]
		{
			new GrindEdgeNode( 0, 0xbe, root ),
			new GrindEdgeNode( 3, 0x7f, child ),
			new GrindEdgeNode( 4, 0xff, Children() ),
			new GrindEdgeNode( 5, 0xff, Children() )
		};
		var records = Enumerable.Range( 0, 6 ).Select( i => Record( (uint)i ) ).ToArray();
		var chunk = Chunk( nodes, records, Enumerable.Repeat( -1, 6 ).ToArray() );
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 6, GrindEdgeQuery.Collect( [ chunk ], Full, false, output ) );
		Assert.Equal( new uint[] { 2, 1, 0, 4, 3, 5 }, output.Take( 6 ).Select( edge => edge.Metadata ) );
	}

	[Theory]
	[InlineData( false, 2 )]
	[InlineData( true, 1 )]
	public void LooseSplitIncludesTouchingAndPrunesStrictlyBeyondIt( bool beyond, int expected )
	{
		var children = Children();
		children[ 0 ] = 0;
		children[ 1 ] = 1;
		var records = new[] { Record( 0 ), Record( 1 ) };
		var bounds = new GrindEdgeBounds( new Vector3( 2f, 0f, 0f ), new Vector3( 3f, 1f, 1f ) );
		var chunk = new GrindEdgeChunk( Full, [ new( -1, 0xff, children ) ], [ -1, -1 ], [ bounds, bounds ], records );
		var query = bounds with { Minimum = new Vector3( beyond ? MathF.BitIncrement( 2f ) : 2f, 0f, 0f ) };
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( expected, GrindEdgeQuery.Collect( [ chunk ], query, false, output ) );
		Assert.Equal( 1u, output[ 0 ].Metadata );
		if ( !beyond )
			Assert.Equal( 0u, output[ 1 ].Metadata );
	}

	[Fact]
	public void ReusedOutputPreservesLowFlagsAndReplacesOnlyVerticalBit()
	{
		var output = Enumerable.Repeat( Record( 99, 0xf2345678 ), 40 ).ToArray();
		var chunk = Chain( [ Record( 1, 0x76543210 ), Record( 2, 0x87654321 ) ] );
		Assert.Equal( 2, GrindEdgeQuery.Collect( [ chunk ], Full, false, output ) );
		Assert.Equal( 0x72345678u, output[ 0 ].Flags );
		Assert.Equal( 0xf2345678u, output[ 1 ].Flags );
		Assert.Equal( 99u, output[ 2 ].Metadata );
	}

	[Fact]
	public void ExcludingVerticalEdgesDoesNotConsumeResultCapacity()
	{
		var chunk = Chain( [ Record( 1, uint.MaxValue ), Record( 2 ), Record( 3, 0x80000000 ), Record( 4 ) ] );
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 2, GrindEdgeQuery.Collect( [ chunk ], Full, true, output ) );
		Assert.Equal( new uint[] { 2, 4 }, output.Take( 2 ).Select( edge => edge.Metadata ) );
	}

	[Fact]
	public void CapacityAppliesAcrossChunksInRegistrationOrder()
	{
		var first = Chain( Enumerable.Range( 0, 20 ).Select( i => Record( (uint)i ) ).ToArray() );
		var second = Chain( Enumerable.Range( 20, 30 ).Select( i => Record( (uint)i ) ).ToArray() );
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 40, GrindEdgeQuery.Collect( [ first, second ], Full, false, output ) );
		Assert.Equal( Enumerable.Range( 0, 40 ).Select( i => (uint)i ), output.Select( edge => edge.Metadata ) );
	}

	[Fact]
	public void MinimumDiagonalIsStrictAndRejectedQueriesPreserveSlots()
	{
		var chunk = Chain( [ Record( 1 ) ] );
		var output = Enumerable.Repeat( Record( 99 ), 40 ).ToArray();
		var query = new GrindEdgeBounds( Vector3.Zero, new Vector3( 1f / 256f, 0f, 0f ) );
		Assert.Equal( 0, GrindEdgeQuery.Collect( [ chunk ], query, false, output ) );
		Assert.All( output, record => Assert.Equal( 99u, record.Metadata ) );
		query = query with { Maximum = new Vector3( MathF.BitIncrement( 1f / 256f ), 0f, 0f ) };
		Assert.Equal( 1, GrindEdgeQuery.Collect( [ chunk ], query, false, output ) );
	}

	[Fact]
	public void EdgeBoundsAreCheckedAfterTreeSelection()
	{
		var bounds = new GrindEdgeBounds( new Vector3( 5f ), new Vector3( 6f ) );
		var chunk = new GrindEdgeChunk( Full, [ new( 0, 0xff, Children() ) ], [ -1 ], [ bounds ], [ Record( 1 ) ] );
		Assert.Equal( 0, GrindEdgeQuery.Collect( [ chunk ], new( Vector3.Zero, Vector3.One ), false, new GrindEdgeRecord[ 40 ] ) );
		Assert.Equal( 1, GrindEdgeQuery.Collect( [ chunk ], new( new Vector3( 4f ), new Vector3( 5f ) ), false, new GrindEdgeRecord[ 40 ] ) );
	}

	[Fact]
	public void DisjointChunksAreSkipped()
	{
		var chunk = Chain( [ Record( 1 ) ] );
		Assert.Equal( 0, GrindEdgeQuery.Collect( [ chunk ], new( new Vector3( 11f ), new Vector3( 12f ) ), false, new GrindEdgeRecord[ 40 ] ) );
	}

	[Fact]
	public void OutputSizeIsRejectedBeforeWriting()
	{
		var output = Enumerable.Repeat( Record( 99 ), 39 ).ToArray();
		Assert.Throws<ArgumentException>( () => GrindEdgeQuery.Collect( [ Chain( [ Record( 1 ) ] ) ], Full, false, output ) );
		Assert.All( output, record => Assert.Equal( 99u, record.Metadata ) );
	}

	[Fact]
	public void ChunkOwnsItsInputArrays()
	{
		var children = Children();
		var node = new GrindEdgeNode( 0, 0xff, children );
		var records = new[] { Record( 7 ) };
		var links = new[] { -1 };
		var chunk = Chunk( [ node ], records, links );
		children[ 7 ] = 0;
		records[ 0 ] = Record( 99 );
		links[ 0 ] = 0;
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 1, GrindEdgeQuery.Collect( [ chunk ], Full, false, output ) );
		Assert.Equal( 7u, output[ 0 ].Metadata );
	}

	[Fact]
	public void EmptyRootReturnsNoRecords()
	{
		Assert.Equal( 0, GrindEdgeQuery.Collect( [ Chain( [] ) ], Full, false, new GrindEdgeRecord[ 40 ] ) );
	}

	private static int[] Children() => Enumerable.Repeat( -1, 8 ).ToArray();
	private static GrindEdgeRecord Record( uint id, uint flags = 0 ) => new( new RailSegment( Vector3.Zero, Vector3.UnitX ), id, flags );
	private static GrindEdgeChunk Chunk( GrindEdgeNode[] nodes, GrindEdgeRecord[] records, int[] next ) =>
		new( Full, nodes, next, Enumerable.Repeat( Full, records.Length ).ToArray(), records );

	private static GrindEdgeChunk Chain( GrindEdgeRecord[] records ) => Chunk(
		[ new( records.Length == 0 ? -1 : 0, 0xff, Children() ) ], records,
		Enumerable.Range( 0, records.Length ).Select( i => i + 1 == records.Length ? -1 : i + 1 ).ToArray() );
}
