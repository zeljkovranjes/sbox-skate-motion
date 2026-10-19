using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindQueryRequestBuilderTests
{
	private static GrindQueryRequestInput Input => new( new( 3, 0, 0, 0 ), Vector4.Zero, 1, false, 0, 1, 0,
		Vector4.UnitY, Vector4.UnitY, Vector4.UnitY, Matrix4x4.Identity, Vector4.Zero, Vector4.UnitY,
		Vector4.Zero, false, Vector4.Zero, new( 5, 6, 7, 0 ), false,
		new( Vector4.Zero, new( 3, 0, 0, 0 ), new( 0, -10, 0, 0 ), Vector4.Zero ), Vector4.UnitZ,
		new( 0, -9, 0, 0 ), 1, -1 );
	private static GrindQueryRequestSettings Settings => new( .2f, .8f, 40, 4, .25f, 1.2f,
		.1f, 20, -.1f, 4, .0001f, .333f, 3 );

	private static GrindQueryRequestBuilder Create( GrindQueryRequestSettings? settings = null )
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var curve = new GrindEdgeSelectionCurve( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ 1, 1, 1, 1, 1, 1, 1, 1 ] );
		var fan = new GrindQueryFan( math, curve, new( 1e-6f, .1f, MathF.PI / 180, 2, 4, MathF.PI * 2, -100 ) );
		return new( fan, curve, curve, settings ?? Settings );
	}

	[Fact]
	public void EmptyFanStillPreparesOriginAndMarksRequestsReady()
	{
		var state = new GrindQueryRequestState { SelectionFlag = 3, SecondarySelectionFlag = 5 };
		var service = new TrajectoryQueryService( 7, 7 );
		Create().Build( Input with { Count = 0 }, state, service );
		Assert.True( state.Ready );
		Assert.Equal( 0, state.Count );
		Assert.Equal( 0, service.FirstCount );
		Assert.Equal( Input.Velocity, state.Directions[ 0 ] );
		Assert.Equal( 0, state.SelectionFlag );
		Assert.Equal( 0, state.SecondarySelectionFlag );
		Assert.Equal( -100, state.Height );
	}

	[Fact]
	public void OverrideKeepsOriginalPointsForLocalOffsets()
	{
		var state = new GrindQueryRequestState();
		var frame = Matrix4x4.Identity;
		frame.Translation = new( 10, 20, 30 );
		Create().Build( Input with { Override = true, OverridePosition = new( 2, 3, 4, 0 ), LocalFrame = frame }, state, new( 7, 7 ) );
		Assert.Equal( new Vector4( 2, 3, 4, 0 ), state.Origin );
		Assert.Equal( Input.OverrideReference, state.Reference );
		Assert.Equal( new Vector4( 8, 18, 26, 1 ), state.LocalReference );
		Assert.Equal( new Vector4( -2, -2, -4, 0 ), state.BoardOffset );
	}

	[Fact]
	public void LeadTimeDisplacesEverySubmittedPositionAlongItsOwnDirection()
	{
		var state = new GrindQueryRequestState();
		var input = Input with { LeadTime = .5f, Count = 7, Spread = 25 };
		Create().Build( input, state, new( 7, 7 ) );
		for ( var i = 0; i < state.Count; i++ )
		{
			var expected = state.Origin + state.Directions[ i ] * .5f;
			Assert.Equal( new Vector3( expected.X, expected.Y, expected.Z ), state.Targets[ i ].Request.Position );
			Assert.Equal( new Vector3( 0, -9, 0 ), state.Targets[ i ].Request.Acceleration );
		}
	}

	[Fact]
	public void ReuseCanReplaceAnEmptyFanWithOneRetainedTrajectory()
	{
		var state = new GrindQueryRequestState();
		var service = new TrajectoryQueryService( 7, 7 );
		Create().Build( Input with { Count = 0, Override = true, AllowReuse = true }, state, service );
		Assert.True( state.Special );
		Assert.Equal( 1, state.Count );
		Assert.Equal( 1, service.FirstCount );
		Assert.Equal( Vector3.Zero, state.Targets[ 0 ].Request.Position );
		Assert.Equal( new Vector3( 0, -10, 0 ), state.Targets[ 0 ].Request.Acceleration );
		Assert.Equal( 1, state.Targets[ 0 ].Request.FirstStep );
		Assert.Equal( 1, state.Targets[ 0 ].Request.LaterStep );
		Assert.Equal( Input.RetainedAxis, state.RetainedAxis );
	}

	[Theory]
	[InlineData( 1.999f, true )]
	[InlineData( 2f, false )]
	public void RetainedDistanceGateIsStrict( float distance, bool reuse )
	{
		var state = new GrindQueryRequestState();
		Create().Build( Input with { Override = true, AllowReuse = true,
			RetainedTrajectory = Input.RetainedTrajectory with { Position = new( distance, 0, 0, 0 ) } }, state, new( 7, 7 ) );
		Assert.Equal( reuse, state.Special );
	}

	[Fact]
	public void AnOpposingRetainedVelocityRejectsReuse()
	{
		var state = new GrindQueryRequestState();
		Create().Build( Input with { Override = true, AllowReuse = true,
			RetainedTrajectory = Input.RetainedTrajectory with { Velocity = new( -3, 0, 0, 0 ) } }, state, new( 7, 7 ) );
		Assert.False( state.Special );
		Assert.Equal( Settings.FirstStep, state.Targets[ 0 ].Request.FirstStep );
	}

	[Fact]
	public void StationaryCurrentMotionBypassesTheSpeedRatioGate()
	{
		var state = new GrindQueryRequestState();
		Create().Build( Input with { Override = true, AllowReuse = true, Velocity = Vector4.Zero }, state, new( 7, 7 ) );
		Assert.True( state.Special );
	}

	[Fact]
	public void NormalSubmissionRetainsSpecialHistoryAndUnusedOutputs()
	{
		var sentinel = new Vector4( 7, 8, 9, 10 );
		var state = new GrindQueryRequestState { Special = true, RetainedAxis = sentinel, RetainedOffset = sentinel };
		state.Directions[ 6 ] = sentinel;
		Create().Build( Input, state, new( 7, 7 ) );
		Assert.True( state.Special );
		Assert.Equal( sentinel, state.RetainedAxis );
		Assert.Equal( sentinel, state.RetainedOffset );
		Assert.Equal( sentinel, state.Directions[ 6 ] );
	}

	[Fact]
	public void DurationUsesVerticalVelocityAndTheConfiguredUpperLimit()
	{
		var rising = new GrindQueryRequestState();
		var falling = new GrindQueryRequestState();
		var builder = Create();
		builder.Build( Input with { Velocity = new( 3, 5, 0, 0 ) }, rising, new( 7, 7 ) );
		builder.Build( Input with { Velocity = new( 3, -5, 0, 0 ) }, falling, new( 7, 7 ) );
		Assert.True( rising.Duration > falling.Duration );
		var capped = new GrindQueryRequestState();
		Create( Settings with { MaximumDuration = .1f } ).Build( Input, capped, new( 7, 7 ) );
		Assert.Equal( .1f, capped.Duration );
	}
}
