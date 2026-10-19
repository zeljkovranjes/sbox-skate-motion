using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Grinding;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalHandSourceControllerTests
{
	private static PhysicalHandSourceInput Input => new( Vector3.Zero, new Vector3( 1f, 4f, 0f ), Vector3.UnitZ, 0 );
	private static GrindEdgeRecord Edge => new( new RailSegment( new Vector3( -2f, .5f, -.25f ), new Vector3( 2f, .5f, -.25f ) ), 7, 0x80000000u );

	[Fact]
	public void AcceptedContactPublishesWorldEdgeWhileRetainingUnrelatedFlags()
	{
		var state = new PhysicalHandSourceState { Edge = Edge with { Metadata = 99, Flags = 0x12345678u } };
		Create().Update( [ Chunk( Edge ) ], Input, state );
		Assert.True( state.Enabled );
		Assert.Equal( 1, state.Side );
		Assert.Equal( Edge.Segment, state.Edge.Segment );
		Assert.Equal( 7u, state.Edge.Metadata );
		Assert.Equal( 0x92345678u, state.Edge.Flags );
		Assert.Equal( .5f, state.Point.Y );
		Assert.Equal( -.25f, state.Point.Z );
	}

	[Fact]
	public void NearbyMissAfterSuccessPreservesAllPublishedOutput()
	{
		var controller = Create();
		var state = new PhysicalHandSourceState();
		controller.Update( [ Chunk( Edge ) ], Input, state );
		var point = state.Point;
		var edge = state.Edge;
		controller.Update( [ Chunk( Edge ) ], Input with { Origin = new Vector3( 100f ) }, state );
		Assert.True( state.Enabled );
		Assert.Equal( point, state.Point );
		Assert.Equal( edge, state.Edge );
		Assert.Equal( 1, state.Side );
		Assert.Equal( 0, controller.Lookup.NearbyCount );
		Assert.Equal( 0, controller.Lookup.Count );
	}

	[Fact]
	public void DirectionFilterMissAlsoPreservesEnabledOutput()
	{
		var state = new PhysicalHandSourceState { Enabled = true, Point = new Vector3( 7f ), Side = 99, Edge = Edge };
		var vertical = Edge with { Segment = new RailSegment( -Vector3.UnitZ, Vector3.UnitZ ) };
		Create().Update( [ Chunk( vertical ) ], Input, state );
		Assert.True( state.Enabled );
		Assert.Equal( new Vector3( 7f ), state.Point );
		Assert.Equal( 99, state.Side );
		Assert.Equal( Edge, state.Edge );
	}

	[Fact]
	public void InvalidReachClearsOnlyActivation()
	{
		var state = new PhysicalHandSourceState { Enabled = true, Point = new Vector3( 7f ), Side = 99, Edge = Edge };
		Create().Update( [ Chunk( Edge ) ], Input with { Velocity = new Vector3( 20f, 4f, 0f ) }, state );
		Assert.False( state.Enabled );
		Assert.Equal( new Vector3( 7f ), state.Point );
		Assert.Equal( 99, state.Side );
		Assert.Equal( Edge, state.Edge );
	}

	[Fact]
	public void FailedSelectionClearsOnlyActivation()
	{
		var state = new PhysicalHandSourceState { Enabled = true, Point = new Vector3( 7f ), Side = 99, Edge = Edge };
		var high = Edge with { Segment = new RailSegment( new Vector3( -2f, 3f, -.25f ), new Vector3( 2f, 3f, -.25f ) ) };
		Create().Update( [ Chunk( high ) ], Input, state );
		Assert.False( state.Enabled );
		Assert.Equal( new Vector3( 7f ), state.Point );
		Assert.Equal( 99, state.Side );
		Assert.Equal( Edge, state.Edge );
	}

	[Theory]
	[InlineData( 0, 1 )]
	[InlineData( 1, 1 )]
	[InlineData( 2, 2 )]
	[InlineData( 3, 2 )]
	public void SideRequestOverridesDirectionWithoutDestroyingReach( int requested, int expected )
	{
		var state = new PhysicalHandSourceState();
		Create().Update( [ Chunk( Edge ) ], Input with { RequestedSide = requested }, state );
		Assert.True( state.Enabled );
		Assert.Equal( expected, state.Side );
		Assert.Equal( expected == 1, state.Point.X > 0f );
	}

	[Fact]
	public void ShapedMotionOverridePreservesSpeedAndVerticalApex()
	{
		var builder = Reach();
		var shaped = PhysicalHandReachBuilder.ShapeMotion( new Vector3( 1f, 4f, 0f ) );
		var before = builder.BuildShaped( Vector3.Zero, shaped );
		var after = builder.BuildShaped( Vector3.Zero, shaped with { X = -.0000152587890625f } );
		Assert.Equal( before.Motion.Y, after.Motion.Y );
		Assert.Equal( before.FirstLower.Y, after.FirstLower.Y );
		Assert.Equal( before.FirstUpper.Y, after.FirstUpper.Y );
		Assert.True( before.FirstUpper.X > 0f && after.FirstUpper.X < 0f );
	}

	[Fact]
	public void SurfaceFrameConvertsMotionAndContactBackToWorld()
	{
		var state = new PhysicalHandSourceState();
		var edge = Edge with { Segment = new RailSegment( new Vector3( -.25f, .5f, 2f ), new Vector3( -.25f, .5f, -2f ) ) };
		Create().Update( [ Chunk( edge ) ], Input with { Normal = Vector3.UnitX, Velocity = new Vector3( 0f, 4f, -1f ) }, state );
		Assert.True( state.Enabled );
		Assert.Equal( -.25f, state.Point.X );
		Assert.Equal( .5f, state.Point.Y );
		Assert.True( state.Point.Z < 0f );
		Assert.Equal( edge.Segment, state.Edge.Segment );
	}

	private static PhysicalHandSourceController Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		return new( Reach(), new PhysicalHandSourceSelection( new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) ) ), 1.5f );
	}

	private static PhysicalHandReachBuilder Reach() => new(
		Flat( .1f ), Flat( 12f ), Flat( 0f ), Flat( 1f ), Flat( 2f ), Flat( .5f ), Flat( 1f ), .25f );

	private static GrindHeightCorrectionCurve Flat( float value ) => new( [ 0f, 2f, 5f, 10f ], [ value, value, value, value ] );

	private static GrindEdgeChunk Chunk( GrindEdgeRecord edge ) => new(
		new GrindEdgeBounds( new Vector3( -10f ), new Vector3( 10f ) ),
		[ new GrindEdgeNode( 0, 255, [ -1, -1, -1, -1, -1, -1, -1, -1 ] ) ], [ -1 ],
		[ new GrindEdgeBounds( Vector3.Min( edge.Segment.Start, edge.Segment.End ), Vector3.Max( edge.Segment.Start, edge.Segment.End ) ) ], [ edge ] );
}
