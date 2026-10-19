using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateRejectionTests
{
	private static GrindCandidateRejectionSettings Settings => new( .5f, .5f, .25f, 10f, 10f, 100f, .01f, 0f, .5f, 100f );
	private static GrindCandidateRejectionInput Input => new( new( 0, 1, 0, 0 ), new( 1, 0, 0, 0 ), new( .5f ), 2, false, -1, 0f );
	private static GrindTrajectoryState State => new( Vector4.Zero, new( 2, 2, 0, 8 ), Vector4.Zero, new( 4, 5, 6, 7 ) );
	private static MeshWorldQuery Query() => new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f );
	private static GrindCandidateRejection Create( GrindCandidateRejectionSettings? settings = null ) =>
		new( settings ?? Settings, new( [ 0, 1, 2, 3 ], [ .5f, .5f, .5f, .5f ] ), new( .25f ) );

	private static MeshQueryInstance Floor( int filter = -1 )
	{
		var a = new Vector3( -5, 0, -5 );
		var b = new Vector3( -5, 0, 5 );
		var c = new Vector3( 5, 0, 5 );
		var d = new Vector3( 5, 0, -5 );
		var geometry = new MeshQueryGeometry( [ new( a, b, c, 0 ), new( a, c, d, 0 ) ], a, c );
		var identity = Matrix4x4.Identity;
		identity.M44 = 0;
		return new( geometry, identity, identity, a, c, filter, 0, 1 );
	}

	[Fact]
	public void EligibleGroundHitUpdatesVelocityAndRetainedHeight()
	{
		var state = State;
		var height = -10f;
		var pass = Create();
		Assert.Equal( 10f, pass.Evaluate( Input, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( new Vector4( 2, 3, 0, 8 ), state.Velocity );
		Assert.Equal( 1f, height );
		Assert.Equal( State.Position, state.Position );
		Assert.Equal( State.Acceleration, state.Acceleration );
		Assert.Equal( State.Auxiliary, state.Auxiliary );
		Assert.Equal( new Vector3( .25f, 1, 0 ), pass.QueryStart );
		Assert.Equal( new Vector3( .25f, -9, 0 ), pass.QueryEnd );
	}

	[Theory]
	[InlineData( -.5f )]
	[InlineData( .5f )]
	[InlineData( 1f )]
	public void DirectionLimitRejectsEqualityWithoutQueryOrMutation( float y )
	{
		var state = State;
		var height = -10f;
		var pass = Create();
		Assert.Equal( 0f, pass.Evaluate( Input with { Direction = new( 1, y, 0, 0 ) }, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( 0, pass.QueryCount );
		Assert.Equal( State, state );
		Assert.Equal( -10f, height );
	}

	[Fact]
	public void MissingGeometryPreservesStateAndHeight()
	{
		var state = State;
		var height = -10f;
		var pass = Create();
		Assert.Equal( 0f, pass.Evaluate( Input, Query(), [], ref state, ref height ) );
		Assert.Equal( 1, pass.QueryCount );
		Assert.Equal( State, state );
		Assert.Equal( -10f, height );
	}

	[Fact]
	public void InstanceFilterCanRejectOtherwiseEligibleGround()
	{
		var state = State;
		var height = -10f;
		Assert.Equal( 0f, Create().Evaluate( Input with { Filter = 7 }, Query(), [ Floor( 8 ) ], ref state, ref height ) );
		Assert.Equal( State, state );
	}

	[Fact]
	public void NormalDotEqualityRejectsHit()
	{
		var state = State;
		var height = -10f;
		Assert.Equal( 0f, Create( Settings with { MaximumNormalDot = 0f } ).Evaluate( Input, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( State, state );
	}

	[Fact]
	public void PreliminaryScoreSkipsCorrectionAndHeightUpdate()
	{
		var state = State;
		var height = -10f;
		Assert.Equal( -90f, Create().Evaluate( Input with { Preliminary = true }, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( State, state );
		Assert.Equal( -10f, height );
	}

	[Fact]
	public void VerticalRatioRejectsDownwardPrediction()
	{
		var state = State with { Velocity = new( 2, -2, 0, 8 ) };
		var original = state;
		var height = -10f;
		Assert.Equal( -90f, Create().Evaluate( Input, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( original, state );
		Assert.Equal( -10f, height );
	}

	[Theory]
	[InlineData( .25f, -99.75f, false )]
	[InlineData( .5f, 5f, true )]
	public void MinimumDistanceIsInclusiveAndHasDistinctFailureScore( float y, float expected, bool corrected )
	{
		var state = State;
		var height = -10f;
		Assert.Equal( expected, Create().Evaluate( Input with { Point = new( 0, y, 0, 0 ) }, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( corrected ? 3f : 2f, state.Velocity.Y );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void InvalidTimeLaneUsesZeroPointForProbe( int lane )
	{
		var time = new Vector4( .5f );
		time[ lane ] = -1f;
		var state = State;
		var height = -10f;
		var pass = Create();
		pass.Evaluate( Input with { Time = time }, Query(), [], ref state, ref height );
		Assert.Equal( new Vector3( .25f, 0, 0 ), pass.QueryStart );
	}

	[Fact]
	public void NonpositiveSamplePreservesVelocityButCanRaiseRetainedHeight()
	{
		var state = State;
		var height = -10f;
		Assert.Equal( 10f, Create().Evaluate( Input with { SampleIndex = 0 }, Query(), [ Floor() ], ref state, ref height ) );
		Assert.Equal( State, state );
		Assert.Equal( 1f, height );
	}

	[Fact]
	public void HigherRetainedHeightSurvivesCorrection()
	{
		var state = State;
		var height = 10f;
		Create().Evaluate( Input, Query(), [ Floor() ], ref state, ref height );
		Assert.Equal( 10f, height );
	}

	[Fact]
	public void SuppliedCorrectionAuxiliaryLaneReachesVelocity()
	{
		var state = State;
		var height = -10f;
		Create().Evaluate( Input with { CorrectionAuxiliary = 2f }, Query(), [ Floor() ], ref state, ref height );
		Assert.Equal( 12f, state.Velocity.W );
	}

	[Fact]
	public void QueryCountResetsAfterAnEarlyRejection()
	{
		var state = State;
		var height = -10f;
		var pass = Create();
		pass.Evaluate( Input, Query(), [], ref state, ref height );
		pass.Evaluate( Input with { Direction = new( 0, 1, 0, 0 ) }, Query(), [], ref state, ref height );
		Assert.Equal( 0, pass.QueryCount );
	}

	[Theory]
	[InlineData( -1f, 0f )]
	[InlineData( .25f, .5f )]
	[InlineData( .5f, 1f )]
	[InlineData( 1.5f, 3f )]
	[InlineData( 3f, 4f )]
	public void HeightCurveInterpolatesAndClamps( float value, float expected )
	{
		Assert.Equal( expected, new GrindHeightCorrectionCurve( [ 0, .5f, 1, 2 ], [ 0, 1, 2, 4 ] ).Evaluate( value ) );
	}
}
