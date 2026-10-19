using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandSourceGeometryTests
{
	[Fact]
	public void AcceptedEdgesKeepPairedWorldAndLocalCoordinates()
	{
		var geometry = new PhysicalHandSourceGeometry();
		var origin = new Vector3( 1, 2, 3 );
		var edge = Edge( new Vector3( 1, 2, 1 ), new Vector3( 1, 2, 5 ) );
		geometry.Prepare( origin, Vector3.UnitX, [ edge ], 1 );
		Assert.Equal( 1, geometry.Count );
		Assert.Equal( edge.Segment, geometry.WorldEdges[ 0 ].Segment );
		Assert.Equal( new Vector3( 2, 0, 0 ), geometry.LocalEdges[ 0 ].Segment.Start );
		Assert.Equal( new Vector3( -2, 0, 0 ), geometry.LocalEdges[ 0 ].Segment.End );
		Assert.Equal( origin, geometry.Frame.Translation );
	}

	[Fact]
	public void RejectsParallelVerticalAndDegenerateDirections()
	{
		var geometry = new PhysicalHandSourceGeometry();
		geometry.Prepare( Vector3.Zero, Vector3.UnitZ,
			[ Edge( Vector3.Zero, Vector3.UnitZ ), Edge( Vector3.Zero, Vector3.UnitY ),
				Edge( Vector3.Zero, Vector3.Zero ), Edge( Vector3.Zero, Vector3.UnitX * .0000005f ) ], 4 );
		Assert.Equal( 0, geometry.Count );
	}

	[Fact]
	public void WorldAndLocalBuffersRetainIndependentLowerFlags()
	{
		var geometry = new PhysicalHandSourceGeometry();
		geometry.WorldEdges[ 0 ] = new GrindEdgeRecord( default, 0, 0x12345678 );
		geometry.LocalEdges[ 0 ] = new GrindEdgeRecord( default, 0, 0x07654321 );
		geometry.Prepare( Vector3.Zero, Vector3.UnitZ,
			[ Edge( -Vector3.UnitX, Vector3.UnitX ) with { Metadata = 73, Flags = 0x8000ffff } ], 1 );
		Assert.Equal( 0x92345678u, geometry.WorldEdges[ 0 ].Flags );
		Assert.Equal( 0x87654321u, geometry.LocalEdges[ 0 ].Flags );
		Assert.Equal( 73u, geometry.WorldEdges[ 0 ].Metadata );
		Assert.Equal( 73u, geometry.LocalEdges[ 0 ].Metadata );
	}

	[Fact]
	public void CapacityKeepsTheFirstFortyAcceptedEdgesAndNextPreparationResetsCount()
	{
		var geometry = new PhysicalHandSourceGeometry();
		var edges = new GrindEdgeRecord[ 41 ];
		for ( var i = 0; i < edges.Length; i++ )
			edges[ i ] = Edge( -Vector3.UnitX, Vector3.UnitX ) with { Metadata = (uint)i };
		geometry.Prepare( Vector3.Zero, Vector3.UnitZ, edges, edges.Length );
		Assert.Equal( 40, geometry.Count );
		Assert.Equal( 39u, geometry.LocalEdges[ 39 ].Metadata );
		geometry.Prepare( Vector3.One, Vector3.UnitZ, [], 0 );
		Assert.Equal( 0, geometry.Count );
		Assert.Equal( Vector3.One, geometry.Frame.Translation );
	}

	[Theory]
	[InlineData( 1f )]
	[InlineData( -1f )]
	public void VerticalNormalUsesIdentityAxesAtTheSuppliedOrigin( float normalY )
	{
		var geometry = new PhysicalHandSourceGeometry();
		geometry.Prepare( Vector3.One, new Vector3( 0, normalY, 0 ), [], 0 );
		Assert.Equal( LimbRigidTransform.Identity.Rotation, geometry.Frame.Rotation );
		Assert.Equal( -Vector3.One, geometry.Inverse.Translation );
	}

	private static GrindEdgeRecord Edge( Vector3 first, Vector3 second ) => new( new RailSegment( first, second ), 0, 0 );
}
