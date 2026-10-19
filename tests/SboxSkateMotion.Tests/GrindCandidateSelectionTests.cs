using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindCandidateSelectionTests
{
	private static GrindScoringCandidate Candidate => new( Vector4.UnitY, Vector4.UnitX, Vector4.UnitY,
		new( .5f ), new( Vector4.UnitY, Vector4.UnitX, Vector4.Zero, new( 7 ) ), 2, 0, .2f, 0 );
	private static GrindQueryRequestInput Request => new( new( 3, 0, 0, 0 ), Vector4.Zero, 2, false, 0, 1, 0,
		Vector4.UnitY, Vector4.UnitY, Vector4.UnitY, Matrix4x4.Identity, Vector4.Zero, Vector4.UnitY,
		Vector4.Zero, false, Vector4.Zero, Vector4.Zero, false, default, Vector4.Zero, new( 0, -9, 0, 0 ), 1, -1 );
	private static GrindCandidateSelectionInput Input => new( 0,
		new( 0, false, 1ul << 61, 1ul << 58, 1, Vector4.UnitY, Vector4.UnitX, Vector4.UnitY, 0 ),
		new( Vector3.UnitX, Vector3.Zero, Vector3.Zero, .5f, .3f, .6f, .2f, -1 ), Request, Vector4.UnitY );
	private static GrindEdgeSelectionCurve Flat( float value ) => new( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ value, value, value, value, value, value, value, value ] );

	private static GrindCandidateSelectionState State( int count = 2, ushort update = 1 )
	{
		var state = new GrindCandidateSelectionState { UpdateCount = update, Early = true, Capture = true };
		state.Queries.Ready = true;
		state.Queries.Count = count;
		state.Queries.Height = 100;
		state.Queries.Origin = Vector4.UnitY;
		state.Queries.Targets[ 0 ].Deliver( TrajectoryContact.Empty );
		for ( var i = 0; i < 7; i++ )
		{
			state.Candidates[ i ] = Candidate;
			state.Queries.Directions[ i ] = new( i + 2, 0, 0, 0 );
		}
		return state;
	}

	private static bool Run( GrindCandidateSelection selector, GrindCandidateSelectionState state,
		TrajectoryQueryService service, GrindCandidateSelectionInput? input = null ) =>
		selector.Evaluate( input ?? Input, state, [], new( 1e-8f, -.00001f, 2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f ), [], service );

	[Theory]
	[InlineData( false, true )]
	[InlineData( true, false )]
	public void WaitingPreservesSelectionAndOnlyClearsRepeatedFlag( bool ready, bool completed )
	{
		var (selector, scoring, queue) = Create();
		var state = State();
		state.Queries.Ready = ready;
		if ( !completed ) state.Queries.Targets[ 0 ].Reset( default, 0 );
		state.Active = true;
		state.Repeated = true;
		state.SelectedIndex = 6;
		state.Selected = Candidate;
		scoring.Scores[ 0 ] = 73;
		queue.SetSamples( [ default, default ] );
		Assert.True( Run( selector, state, new( 7, 7 ) ) );
		Assert.False( state.Repeated );
		Assert.Equal( 1, state.UpdateCount );
		Assert.Equal( 6, state.SelectedIndex );
		Assert.Equal( Candidate, state.Selected );
		Assert.Equal( 73, scoring.Scores[ 0 ] );
		Assert.Equal( 2, queue.RemainingCount );
	}

	[Theory]
	[InlineData( 2, 3, true )]
	[InlineData( 65535, 0, false )]
	public void UpdateCounterWrapsAndRepeatedFlagUsesItsPreviousValue( ushort before, ushort after, bool repeated )
	{
		var state = State( update: before );
		Assert.True( Run( Create().Selector, state, new( 7, 7 ) ) );
		Assert.Equal( after, state.UpdateCount );
		Assert.Equal( repeated, state.Repeated );
		Assert.True( state.Early );
	}

	[Fact]
	public void EqualScoresKeepFirstCandidateAndDrainPendingSamples()
	{
		var (selector, scoring, queue) = Create();
		var state = State();
		queue.SetSamples( [ default, default, default ] );
		Run( selector, state, new( 7, 7 ) );
		Assert.Equal( scoring.Scores[ 0 ], scoring.Scores[ 1 ] );
		Assert.Equal( 0, state.SelectedIndex );
		Assert.Equal( 0, queue.RemainingCount );
		Assert.Equal( 3, queue.SourceEdgeCount );
	}

	[Fact]
	public void HeightBiasCanSelectAnOtherwiseEqualLaterCandidate()
	{
		var state = State();
		state.Queries.Height = 2;
		state.Candidates[ 1 ] = Candidate with { Trajectory = Candidate.Trajectory with { Position = new( 0, 3, 0, 0 ) } };
		var (selector, scoring, _) = Create();
		Run( selector, state, new( 7, 7 ) );
		Assert.Equal( 1, state.SelectedIndex );
		Assert.Equal( 10, scoring.Biases[ 1 ] );
		Assert.Equal( state.Candidates[ 1 ], state.Selected );
	}

	[Fact]
	public void InvalidTimeRetainsTrajectoryAndKindButClearsActiveAndRepeated()
	{
		var state = State( 1, 2 );
		state.Candidates[ 0 ] = Candidate with { Time = new( -1 ) };
		state.Trajectory = Candidate.Trajectory;
		state.SelectedKind = 31;
		state.Active = true;
		Assert.False( Run( Create().Selector, state, new( 7, 7 ) ) );
		Assert.False( state.Repeated );
		Assert.True( state.HasSelection );
		Assert.Equal( 1, state.Queries.SecondarySelectionFlag );
		Assert.Equal( Candidate.Direction, state.FallbackDirection );
		Assert.Equal( Candidate.Trajectory, state.Trajectory );
		Assert.Equal( 31, state.SelectedKind );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 0 )]
	public void NonpositiveCountStillCopiesSlotZero( int count )
	{
		var state = State( count );
		Run( Create().Selector, state, new( 7, 7 ) );
		Assert.Equal( 0, state.SelectedIndex );
		Assert.Equal( Candidate, state.Selected );
		Assert.True( state.HasSelection );
	}

	[Fact]
	public void RepeatKeepsChosenVelocityAndSnapshotWhileResettingQueryCompletion()
	{
		var state = State( update: 0 );
		state.Candidates[ 0 ] = Candidate with { Kind = 6 };
		var service = new TrajectoryQueryService( 7, 7 );
		Assert.True( Run( Create().Selector, state, service ) );
		Assert.Equal( 1, state.SelectedIndex );
		Assert.Equal( new Vector4( 3, 0, 0, 0 ), state.SelectedVelocity );
		Assert.Equal( state.SelectedVelocity, state.Request.Velocity );
		Assert.Equal( state.SelectedVelocity, state.Queries.Directions[ 0 ] );
		Assert.Equal( 2, state.UpdateCount );
		Assert.Equal( 2, service.FirstCount );
		Assert.True( state.Queries.Ready );
		Assert.False( state.Queries.Targets[ 0 ].Completed );
		Assert.Equal( new Vector4( .5f ), state.Selected.Time );
		Assert.Equal( 0, state.Queries.SelectionFlag );
	}

	[Fact]
	public void SuppressionPreservesCaptureAndPreventsRegeneration()
	{
		var state = State( update: 0 );
		state.Queries.Special = true;
		var service = new TrajectoryQueryService( 7, 7 );
		Run( Create().Selector, state, service );
		Assert.True( state.Capture );
		Assert.Equal( 1, state.UpdateCount );
		Assert.False( state.Queries.Ready );
		Assert.Equal( 0, service.FirstCount );
	}

	[Fact]
	public void ZeroCountRepeatFinishesWithReadyStateAndNoSubmittedRequests()
	{
		var state = State( update: 0 );
		var service = new TrajectoryQueryService( 7, 7 );
		Run( Create().Selector, state, service, Input with { Request = Request with { Count = 0 } } );
		Assert.Equal( 2, state.UpdateCount );
		Assert.True( state.Queries.Ready );
		Assert.Equal( 0, state.Queries.Count );
		Assert.Equal( 0, service.FirstCount );
	}

	private static (GrindCandidateSelection Selector, GrindCandidateScoring Scoring, GrindEdgeSampleQueue Queue) Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var angle = new LimbSignedAngle( math, .0001f, MathF.PI * 2, 1 / (MathF.PI * 2) );
		var sampler = new GrindEdgeContactSampler( angle, new( 1, 1e-6f, .1f, -.2f, 10, MathF.PI ) );
		var queue = new GrindEdgeSampleQueue( Flat( 0 ), new( 1000, -10000, MathF.PI ) );
		var plan = new GrindSurfaceProbePlan( new( 1e-6f, 1e-5f, .09f, .04f, 1.06f, .001f, 6 ) );
		var classifier = new GrindSurfaceClassifier( math, new( .65f, 1e-6f, .9f, .99f,
			-.035f, .035f, -.24f, -.2f, MathF.PI / 2, MathF.PI * 2, .01f ) );
		var contacts = new GrindSurfaceContactQuery( new( plan, classifier ), new( math, .25f, .75f ) );
		var normal = new GrindCandidateNormal( math, 1e-6f, 1.52587890625e-5f,
			MathF.PI / 180, MathF.PI * 2, 1 / (MathF.PI * 2) );
		var correction = new GrindContactCorrection( .1f );
		var acceptance = new GrindCandidateAcceptance( sampler, queue, contacts, correction, normal, angle,
			new( -.4f, .9f, 1e-6f, 7, 36, 64, .5f, .35f, 1, .65f, .7f, 180, 1, .1f, 20, .1f, MathF.PI / 180, .0001f ) );
		var rejection = new GrindCandidateRejection( new( .5f, .5f, 0, 10, 10, 100, .01f, 0, .5f, 100 ),
			new( [ 0, 1, 2, 3 ], [ .5f, .5f, .5f, .5f ] ), correction );
		var scoring = new GrindCandidateScoring( new( new(), acceptance, 100 ), new( 1e-6f, 100, 3, Flat( 0 ) ), rejection,
			new( .1f, 1e-6f, 200, 20, 0, 500, .1f, .5f, -500, -20, -10 ), Flat( 0 ), Flat( 0 ), Flat( 0 ) );
		var fan = new GrindQueryFan( math, Flat( 1 ), new( 1e-6f, .1f, MathF.PI / 180, 2, 4, MathF.PI * 2, -100 ) );
		var builder = new GrindQueryRequestBuilder( fan, Flat( 1 ), Flat( 1 ), new( .2f, .8f, 40, 4, .25f, 1.2f, .1f, 20, -.1f, 4, .0001f, .333f, 3 ) );
		return (new( scoring, queue, new( 0, 10 ), new( Flat( 0 ), correction ), new( angle, 0 ), builder, new( -1000, 0, 100, 0, 1 ) ), scoring, queue);
	}
}
