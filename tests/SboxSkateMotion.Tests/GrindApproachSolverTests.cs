using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindApproachSolverTests
{
	[Fact]
	public void PredictionUpdatesVelocityBeforePosition()
	{
		var state = new GrindApproachState();
		Create( settings: Settings() with { Gravity = new Vector3( 0f, -2f, 0f ) } ).Update( Input(), state );
		Assert.Equal( new Vector3( 0f, -2.5f, .3f ), state.Poses[ 1 ].Translation );
		Assert.Equal( new Vector3( 0f, -7.5f, .3f ), state.Poses[ 2 ].Translation );
	}

	[Fact]
	public void AngularPredictionRotatesAxesWithoutOrbitingPosition()
	{
		var state = new GrindApproachState();
		var input = Input() with { Board = LimbRigidTransform.Identity with { Translation = new Vector3( 4f, 5f, 6f ) },
			Velocity = Vector3.Zero, AngularVelocity = Vector3.UnitY };
		Create().Update( input, state );
		Assert.Equal( input.Board.Translation, state.Poses[ 1 ].Translation );
		Assert.NotEqual( input.Board.Rotation, state.Poses[ 1 ].Rotation );
		Assert.InRange( state.Poses[ 1 ].Rotation.X.X, .53f, .55f );
	}

	[Fact]
	public void CustomBoardPointsUseEachPredictedTransform()
	{
		var state = new GrindApproachState();
		var points = Points();
		points[ 2 ] = new Vector3( 2f, 3f, 4f );
		Create( points: points ).Update( Input(), state );
		Assert.Equal( new Vector3( 2f, 2.5f, 4.3f ), state.Points[ 7 ] );
	}

	[Fact]
	public void TouchingThePlaneExactlyIsNotAStrictCrossing()
	{
		var state = new GrindApproachState();
		Create( points: new Vector3[ 5 ] ).Update( Input() with {
			Board = LimbRigidTransform.Identity with { Translation = Vector3.UnitY } }, state );
		Assert.All( state.Crossings, value => Assert.Equal( -1f, value ) );
		Assert.False( state.HasCandidate );
	}

	[Fact]
	public void FirstCrossingWinsWhenTheTrajectoryCrossesAgain()
	{
		var state = new GrindApproachState();
		Create( points: new Vector3[ 5 ], settings: Settings() with { Gravity = Vector3.UnitY } ).Update(
			Input() with { Board = LimbRigidTransform.Identity with { Translation = new Vector3( 0f, .5f, .3f ) },
				Velocity = new Vector3( 0f, -3f, 0f ) }, state );
		Assert.Equal( .25f, state.Crossings[ 0 ] );
		Assert.Equal( .25f, state.EarliestCrossing );
	}

	[Theory]
	[InlineData( true, 0 )]
	[InlineData( false, 1 )]
	public void CandidateModeSeparatesZeroFromRemainingDefinitions( bool zero, int expected )
	{
		var state = new GrindApproachState();
		Create().Update( Input() with { CandidateZero = zero }, state );
		Assert.True( state.HasCandidate );
		Assert.Equal( expected, state.SelectedCandidate );
	}

	[Fact]
	public void EqualDistanceLimitRejectsTheCandidate()
	{
		var state = new GrindApproachState();
		var definitions = Candidates();
		definitions[ 0 ] = definitions[ 0 ] with { DistanceLimit = .5f };
		Create( candidates: definitions ).Update( Input() with { CandidateZero = true,
			Board = LimbRigidTransform.Identity with { Translation = new Vector3( 0f, .5f, .5f ) } }, state );
		Assert.Equal( .5f, state.Distances[ 0 ] );
		Assert.False( state.HasCandidate );
	}

	[Fact]
	public void FifthCandidateRejectsWidelySeparatedAxleCrossings()
	{
		var points = Points();
		points[ 2 ] = new Vector3( 0f, 1f, 0f );
		points[ 3 ] = new Vector3( 0f, 4f, 0f );
		var definitions = Candidates();
		for ( var i = 0; i < definitions.Length; i++ )
			definitions[ i ] = definitions[ i ] with { DistanceLimit = i == 4 ? 10f : 0f };
		var state = new GrindApproachState();
		Create( points: points, candidates: definitions ).Update( Input(), state );
		Assert.Equal( 1.5f, state.Crossings[ 2 ] );
		Assert.Equal( 4.5f, state.Crossings[ 3 ] );
		Assert.False( state.HasCandidate );
	}

	[Fact]
	public void LastTwoCandidatesChooseTheCloserBoardPoint()
	{
		var points = Points();
		points[ 2 ] = new Vector3( 0f, 0f, 2f );
		points[ 3 ] = Vector3.Zero;
		var definitions = Candidates();
		for ( var i = 0; i < 5; i++ )
			definitions[ i ] = definitions[ i ] with { DistanceLimit = 0f };
		var state = new GrindApproachState();
		Create( points: points, candidates: definitions ).Update( Input(), state );
		Assert.Equal( 6, state.SelectedCandidate );
		Assert.Equal( 3, state.SelectedPoint );
	}

	[Fact]
	public void CorrectionVelocityAndAccumulatedPositionHaveSeparateLimits()
	{
		var state = new GrindApproachState { CorrectionPosition = new Vector3( 0f, 0f, -.15f ) };
		Create( settings: Settings() with { MaximumCorrectionVelocity = .1f, MaximumCorrectionPosition = .2f } )
			.Update( Input() with { CandidateZero = true }, state );
		Assert.InRange( state.CorrectionVelocity.Length(), .09999f, .10001f );
		Assert.InRange( state.CorrectionPosition.Length(), .19999f, .20001f );
		Assert.True( state.CorrectionPosition.Z < 0f );
	}

	[Fact]
	public void NoCandidatePreservesRetainedSelectionAndCorrection()
	{
		var state = new GrindApproachState { HasCandidate = true, SelectedCandidate = 5, SelectedPoint = 2,
			SelectedAngle = 7f, CorrectionVelocity = Vector3.UnitX, CorrectionPosition = Vector3.UnitZ };
		Create().Update( Input() with { Velocity = Vector3.UnitY }, state );
		Assert.False( state.HasCandidate );
		Assert.Equal( 5, state.PreviousCandidate );
		Assert.Equal( 5, state.SelectedCandidate );
		Assert.Equal( 2, state.SelectedPoint );
		Assert.Equal( 7f, state.SelectedAngle );
		Assert.Equal( Vector3.UnitX, state.CorrectionVelocity );
		Assert.Equal( Vector3.UnitZ, state.CorrectionPosition );
	}

	[Fact]
	public void DegenerateEdgeProducesZeroFramesAndNoCrossing()
	{
		var state = new GrindApproachState();
		Create().Update( Input() with { EdgeEnd = -Vector3.UnitX }, state );
		Assert.Equal( new LimbFrame(), state.EdgeFrame );
		Assert.Equal( new LimbFrame(), state.VelocityFrame );
		Assert.False( state.HasCandidate );
	}

	[Fact]
	public void SolverOwnsBoardGeometryAndDefinitions()
	{
		var points = Points();
		var definitions = Candidates();
		var solver = Create( points: points, candidates: definitions );
		Array.Fill( points, new Vector3( 100f ) );
		Array.Fill( definitions, new GrindApproachCandidate() );
		var state = new GrindApproachState();
		solver.Update( Input() with { CandidateZero = true }, state );
		Assert.True( state.HasCandidate );
		Assert.Equal( Input().Board.Translation, state.Points[ 0 ] );
	}

	[Fact]
	public void InvalidGeometryAndSampleCountsAreRejected()
	{
		Assert.Throws<ArgumentException>( () => Create( settings: Settings() with { SampleCount = 13 } ) );
		Assert.Throws<ArgumentException>( () => Create( settings: Settings() with { SampleCount = 0 } ) );
		Assert.Throws<ArgumentException>( () => Create( points: new Vector3[ 4 ] ) );
		var candidates = Candidates();
		candidates[ 0 ] = candidates[ 0 ] with { PointIndex = 5 };
		Assert.Throws<ArgumentException>( () => Create( candidates: candidates ) );
	}

	private static GrindApproachInput Input() => new(
		LimbRigidTransform.Identity with { Translation = new Vector3( 0f, .5f, .3f ) },
		-Vector3.UnitY, Vector3.Zero, Vector3.UnitY, -Vector3.UnitX, Vector3.UnitX, 1f, false );

	private static GrindApproachSettings Settings() => new( 8, 0f, Vector3.Zero, 1e-6f, 1e-5f, 1f, 100f, 200f, 100f, 100f );

	private static Vector3[] Points() => new[] { Vector3.Zero, Vector3.UnitZ, Vector3.Zero, Vector3.Zero, -Vector3.UnitZ };

	private static GrindApproachCandidate[] Candidates()
	{
		var result = new GrindApproachCandidate[ 7 ];
		var points = new[] { 0, 1, 4, 0, 2, 2, 3 };
		for ( var i = 0; i < result.Length; i++ )
			result[ i ] = new GrindApproachCandidate( 0f, 10f, 10f, 1f, points[ i ] );
		return result;
	}

	private static GrindApproachSolver Create( GrindApproachSettings? settings = null,
		Vector3[] points = null, GrindApproachCandidate[] candidates = null )
	{
		var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
			new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, MathF.PI / 2f },
			2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
		return new GrindApproachSolver( math, settings ?? Settings(), points ?? Points(), candidates ?? Candidates() );
	}
}
