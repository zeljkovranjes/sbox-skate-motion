using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandSourceLookupTests
{
	[Fact]
	public void NearbyBoundsUseTwoHorizontalAndFourVerticalMetres()
	{
		var lookup = new PhysicalHandSourceLookup();
		var chunk = Chunk( [ Edge( 0, 4f ), Edge( 1, 4.001f ), Edge( 2, 0f, 2f ), Edge( 3, 0f, 2.001f ) ] );
		Assert.True( lookup.TryPrepare( [ chunk ], Vector3.Zero, Vector3.UnitZ ) );
		Assert.Equal( 2, lookup.NearbyCount );
		Assert.Equal( 2, lookup.Count );
		Assert.Equal( 0u, lookup.Geometry.WorldEdges[ 0 ].Metadata );
		Assert.Equal( 2u, lookup.Geometry.WorldEdges[ 1 ].Metadata );
	}

	[Fact]
	public void LookupDoesNotExcludeFlaggedRecordsBeforeDirectionalFiltering()
	{
		var lookup = new PhysicalHandSourceLookup();
		Assert.True( lookup.TryPrepare( [ Chunk( [ Edge( 7, 0 ) with { Flags = 0x80000000 } ] ) ],
			Vector3.Zero, Vector3.UnitZ ) );
		Assert.Equal( 1, lookup.Count );
		Assert.Equal( 0x80000000u, lookup.Geometry.LocalEdges[ 0 ].Flags );
	}

	[Fact]
	public void FreshMissClearsPublicCountsAfterPreviousSuccess()
	{
		var lookup = new PhysicalHandSourceLookup();
		var chunks = new[] { Chunk( [ Edge( 0, 0 ) ] ) };
		Assert.True( lookup.TryPrepare( chunks, Vector3.Zero, Vector3.UnitZ ) );
		Assert.False( lookup.TryPrepare( chunks, new Vector3( 100f ), Vector3.UnitZ ) );
		Assert.Equal( 0, lookup.Count );
		Assert.Equal( 0, lookup.NearbyCount );
	}

	[Fact]
	public void QueryCapacityAppliesBeforeDirectionalAcceptance()
	{
		var edges = new GrindEdgeRecord[ 41 ];
		for ( var i = 0; i < 40; i++ )
			edges[ i ] = new GrindEdgeRecord( new RailSegment( -Vector3.UnitZ, Vector3.UnitZ ), (uint)i, 0 );
		edges[ 40 ] = Edge( 40, 0 );
		var lookup = new PhysicalHandSourceLookup();
		Assert.False( lookup.TryPrepare( [ Chunk( edges ) ], Vector3.Zero, Vector3.UnitZ ) );
		Assert.Equal( 40, lookup.NearbyCount );
		Assert.Equal( 0, lookup.Count );
	}

	private static GrindEdgeRecord Edge( uint id, float height, float depth = 0f ) => new(
		new RailSegment( new Vector3( -1f, height, depth ), new Vector3( 1f, height, depth ) ), id, 0 );

	private static GrindEdgeChunk Chunk( GrindEdgeRecord[] edges )
	{
		var next = new int[ edges.Length ];
		var bounds = new GrindEdgeBounds[ edges.Length ];
		for ( var i = 0; i < edges.Length; i++ )
		{
			next[ i ] = i + 1 < edges.Length ? i + 1 : -1;
			bounds[ i ] = new GrindEdgeBounds( Vector3.Min( edges[ i ].Segment.Start, edges[ i ].Segment.End ),
				Vector3.Max( edges[ i ].Segment.Start, edges[ i ].Segment.End ) );
		}
		return new GrindEdgeChunk( new GrindEdgeBounds( new Vector3( -10f ), new Vector3( 10f ) ),
			[ new GrindEdgeNode( edges.Length == 0 ? -1 : 0, 255, [ -1, -1, -1, -1, -1, -1, -1, -1 ] ) ],
			next, bounds, edges );
	}
}
