using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class RailEdgeSourceTests
{
	[Fact]
	public void WorldUpDrivesSuppressionAndEndpointsReturnInWorldSpace()
	{
		var source = new RailEdgeSource();
		var lower = Edge( 0f );
		var upper = Edge( .05f );
		source.Update( [ lower, upper ] );
		var output = new GrindEdgeRecord[ 40 ];
		var count = new GrindEdgeLookup().Collect( [ source.Chunk ], Bounds(), RailEdgeSpace.ToLookup( Vector3.UnitZ ), output );
		Assert.Equal( 1, count );
		Assert.Equal( upper, RailEdgeSpace.ToWorld( output[ 0 ].Segment ) );
		Assert.Equal( 1u, output[ 0 ].Metadata );
	}

	[Fact]
	public void CoordinateChangePreservesHandednessAndUnits()
	{
		var value = new Vector3( -7f, 13f, 5f );
		Assert.Equal( new Vector3( 13f, 5f, -7f ), RailEdgeSpace.ToLookup( value ) );
		Assert.Equal( value, RailEdgeSpace.ToWorld( RailEdgeSpace.ToLookup( value ) ) );
		Assert.Equal( RailEdgeSpace.ToLookup( Vector3.UnitZ ),
			Vector3.Cross( RailEdgeSpace.ToLookup( Vector3.UnitX ), RailEdgeSpace.ToLookup( Vector3.UnitY ) ) );
	}

	[Fact]
	public void UnchangedSegmentsReuseTreeAndMovedGeometryReplacesIt()
	{
		var source = new RailEdgeSource();
		var input = new[] { Edge( 0f ) };
		source.Update( input );
		var before = source.Chunk;
		source.Update( input );
		Assert.Same( before, source.Chunk );
		input[ 0 ] = Edge( 50f );
		source.Update( input );
		Assert.NotSame( before, source.Chunk );
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 0, new GrindEdgeLookup().Collect( [ source.Chunk ], Bounds(), Vector3.Zero, output ) );
		Assert.Equal( Edge( 0f ), RailEdgeSpace.ToWorld( before.Record( 0 ).Segment ) );
	}

	[Fact]
	public void FortyCandidatesSurviveWithoutPerSourceEightSegmentLimit()
	{
		var input = new RailSegment[ 48 ];
		Array.Fill( input, Edge( 0f ) );
		var source = new RailEdgeSource();
		source.Update( input );
		Assert.Equal( 48, source.Chunk.EdgeCount );
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 40, new GrindEdgeLookup().Collect( [ source.Chunk ], Bounds(), Vector3.Zero, output ) );
		foreach ( var record in output )
			Assert.Equal( input[ 0 ], RailEdgeSpace.ToWorld( record.Segment ) );
	}

	[Fact]
	public void EmptyUpdateAndSmallQueryDoNotReuseStaleCandidates()
	{
		var source = new RailEdgeSource();
		var lookup = new GrindEdgeLookup();
		var output = new GrindEdgeRecord[ 40 ];
		source.Update( [ Edge( 0f ) ] );
		Assert.Equal( 1, lookup.Collect( [ source.Chunk ], Bounds(), Vector3.Zero, output ) );
		Assert.Equal( 0, lookup.Collect( [ source.Chunk ], default, Vector3.Zero, output ) );
		source.Update( [] );
		Assert.Equal( 0, lookup.Collect( [ source.Chunk ], Bounds(), Vector3.Zero, output ) );
	}

	[Fact]
	public void LookupDoesNotExcludeVerticalEdges()
	{
		var source = new RailEdgeSource();
		var edge = new RailSegment( -Vector3.UnitZ, Vector3.UnitZ );
		source.Update( [ edge ] );
		var output = new GrindEdgeRecord[ 40 ];
		Assert.Equal( 1, new GrindEdgeLookup().Collect( [ source.Chunk ], Bounds(), Vector3.Zero, output ) );
		Assert.True( output[ 0 ].IsVertical );
		Assert.Equal( edge, RailEdgeSpace.ToWorld( output[ 0 ].Segment ) );
	}

	[Fact]
	public void FilteringCompactsMetadataAndSlotFlagsTogether()
	{
		var source = new RailEdgeSource();
		source.Update( [ Edge( 0f ), Edge( .05f ) ] );
		var output = new GrindEdgeRecord[ 40 ];
		output[ 0 ] = new( default, 0, 123u );
		output[ 1 ] = new( default, 0, 456u );
		GrindEdgeQuery.Collect( [ source.Chunk ], Bounds(), false, output );
		var selected = output[ 0 ].Metadata == 1u ? output[ 0 ] : output[ 1 ];
		Assert.Equal( 1, new GrindEdgeLookup().Collect( [ source.Chunk ], Bounds(), Vector3.Zero, output ) );
		Assert.Equal( selected, output[ 0 ] );
	}

	private static RailSegment Edge( float height ) => new( new Vector3( -1f, 0f, height ), new Vector3( 1f, 0f, height ) );

	private static GrindEdgeBounds Bounds() => new( new Vector3( -2f ), new Vector3( 2f ) );
}
