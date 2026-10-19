using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandSourceSelectionTests
{
	[Theory]
	[InlineData( -1f )]
	[InlineData( 0f )]
	[InlineData( -1.001f )]
	[InlineData( .001f )]
	public void DepthBoundaryAndOutsideEdgesPreservePreviousPoint( float depth )
	{
		var point = new Vector3( 7f, 8f, 9f );
		Assert.Equal( -1, Select( Create(), [ Edge( -1f, 1f, 1f, depth ) ], ref point ) );
		Assert.Equal( new Vector3( 7f, 8f, 9f ), point );
	}

	[Fact]
	public void CrossingSegmentClipsAgainstBothDepthPlanes()
	{
		var selection = Create();
		var point = Vector3.Zero;
		var edge = new GrindEdgeRecord( new RailSegment( new Vector3( -1f, 1f, -2f ), new Vector3( 1f, 1f, 1f ) ), 0, 0 );
		Assert.Equal( 0, Select( selection, [ edge ], ref point ) );
		Assert.Equal( -1f, selection.Segments[ 0 ].Start.Z );
		Assert.Equal( 0f, selection.Segments[ 0 ].End.Z );
		Assert.InRange( point.X, selection.Segments[ 0 ].Start.X, selection.Segments[ 0 ].End.X );
	}

	[Fact]
	public void HigherDepthCanWinWithinHeightTolerance()
	{
		var selection = Create();
		var point = Vector3.Zero;
		Assert.Equal( 1, Select( selection, [ Edge( -1f, 1f, 1f, -.8f ), Edge( -1f, 1f, .95f, -.2f ) ], ref point ) );
		Assert.Equal( new Vector3( 0f, .95f, -.2f ), point );
		Assert.False( selection.Active[ 0 ] );
	}

	[Fact]
	public void SelectedSourceIndexRemainsHeightWinnerWhenContactMovesToNeighbour()
	{
		var selection = Create();
		var point = Vector3.Zero;
		Assert.Equal( 0, Select( selection,
			[ Edge( 1f, 2f, 2f, -.5f ), Edge( 0f, 1f, 2f, -.5f ), Edge( -1f, 0f, 2f, -.5f ) ], ref point ) );
		Assert.Equal( new Vector3( 0f, 2f, -.5f ), point );
		Assert.True( selection.Active[ 0 ] && selection.Active[ 1 ] && selection.Active[ 2 ] );
	}

	[Fact]
	public void EmptySelectionAfterSuccessPreservesPoint()
	{
		var selection = Create();
		var point = Vector3.Zero;
		Assert.Equal( 0, Select( selection, [ Edge( -1f, 1f, 1f, -.5f ) ], ref point ) );
		var previous = point;
		Assert.Equal( -1, Select( selection, [], ref point ) );
		Assert.Equal( previous, point );
	}

	[Fact]
	public void ReversedReachCornerOrderSelectsSameContact()
	{
		var selection = Create();
		var point = Vector3.Zero;
		Assert.Equal( 0, selection.Select( [ Edge( -1f, 1f, 1f, -.5f ) ], 1,
			new Vector3( 2f, 0f, 0f ), new Vector3( -2f, 0f, 0f ),
			new Vector3( 2f, 3f, 0f ), new Vector3( -2f, 3f, 0f ), 1f, ref point ) );
		Assert.Equal( new Vector3( 0f, 1f, -.5f ), point );
	}

	[Fact]
	public void DisconnectedEdgeDoesNotPullContactAwayFromWinningChain()
	{
		var selection = Create();
		var point = Vector3.Zero;
		Assert.Equal( 0, Select( selection,
			[ Edge( 1f, 2f, 2f, -.5f ), Edge( -1f, .5f, 2f, -.5f ) ], ref point ) );
		Assert.Equal( new Vector3( 1f, 2f, -.5f ), point );
		Assert.False( selection.Active[ 1 ] );
	}

	private static int Select( PhysicalHandSourceSelection selection, GrindEdgeRecord[] edges, ref Vector3 point ) =>
		selection.Select( edges, edges.Length, new Vector3( -2f, 0f, 0f ), new Vector3( 2f, 0f, 0f ),
			new Vector3( -2f, 3f, 0f ), new Vector3( 2f, 3f, 0f ), 1f, ref point );

	[Fact]
	public void CollapsedReachPreservesOutputAndCanonicalizesInvalidScratchCoordinates()
	{
		var selection = Create();
		var point = new Vector3( 7f, 8f, 9f );
		Assert.Equal( -1, selection.Select( [ Edge( -1f, 1f, 1f, -.5f ) ], 1,
			new Vector3( -2f, 1f, 0f ), new Vector3( 2f, 1f, 0f ),
			new Vector3( -2f, 1f, 0f ), new Vector3( 2f, 1f, 0f ), 1f, ref point ) );
		Assert.Equal( new Vector3( 7f, 8f, 9f ), point );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( selection.Segments[ 0 ].End.X ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( selection.Segments[ 0 ].End.Y ) );
		Assert.Equal( 0x7fc00000u, BitConverter.SingleToUInt32Bits( selection.Segments[ 0 ].End.Z ) );
	}

	private static GrindEdgeRecord Edge( float first, float second, float height, float depth ) =>
		new( new RailSegment( new Vector3( first, height, depth ), new Vector3( second, height, depth ) ), 0, 0 );

	private static PhysicalHandSourceSelection Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		return new PhysicalHandSourceSelection( new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) ) );
	}
}
