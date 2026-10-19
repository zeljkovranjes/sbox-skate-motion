using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using Xunit;

namespace SboxSkateMotion.Tests;

public class GrindQueryInputPreparationTests
{
	private static GrindQueryMotionSettings Settings => new( 1, .2f, .8f, .08f, .25f, -1, 1, .25f, 1e-6f );
	private static GrindQueryRequestInput Input => new( new( 3, 0, 1, 0 ), Vector4.Zero, 1, false, 0, 1, 0,
		Vector4.UnitY, Vector4.UnitY, Vector4.UnitY, Matrix4x4.Identity, Vector4.Zero, Vector4.UnitX,
		Vector4.Zero, false, Vector4.Zero, Vector4.Zero, false, default, Vector4.Zero, new( 0, -9, 0, 0 ), 1, -1 );

	private static GrindQueryInputPreparation Create()
	{
		var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
			[ -1f / 5040f, 1f / 120f, -1f / 6f ], [ 0, 0, 0, 0, 0, 0, 0, MathF.PI / 2 ],
			2 / MathF.PI, MathF.PI / 2, 0, MathF.PI );
		var curve = new GrindEdgeSelectionCurve( [ 0, 1, 2, 3, 4, 5, 6, 7 ], [ 1, 1, 1, 1, 1, 1, 1, 1 ] );
		var fan = new GrindQueryFan( math, curve, new( 1e-6f, .1f, MathF.PI / 180, 2, 4, MathF.PI * 2, -100 ) );
		var builder = new GrindQueryRequestBuilder( fan, curve, curve,
			new( .2f, .8f, 40, 4, .25f, 1.2f, .1f, 20, -.1f, 4, .0001f, .333f, 3 ) );
		return new( new( Settings ), builder );
	}

	[Fact]
	public void PendingQueryPreservesInputAndSelectionEvenAfterCompletion()
	{
		var preparation = Create();
		var state = new GrindCandidateSelectionState();
		var service = new TrajectoryQueryService( 7, 7 );
		Assert.True( preparation.TryBegin( Input, 1, 0, state, service ) );
		var request = state.Request;
		state.UpdateCount = 9;
		state.SelectedIndex = 3;
		state.Active = true;
		state.Queries.Targets[ 0 ].Deliver( TrajectoryContact.Empty );
		Assert.False( preparation.TryBegin( Input with { Velocity = new( 100 ) }, -1, 200, state, service ) );
		Assert.Equal( request, state.Request );
		Assert.Equal( 9, state.UpdateCount );
		Assert.Equal( 3, state.SelectedIndex );
		Assert.True( state.Active );
		Assert.Equal( 1, service.FirstCount );
	}

	[Fact]
	public void NewInputResetsProductionAxesAndAcceptanceButRetainsCaptureHistory()
	{
		var state = new GrindCandidateSelectionState
		{
			UpdateCount = 9, SelectedIndex = 6, Active = true, Capture = true,
			Repeated = true, Early = true, HasSelection = true, SelectedKind = 31
		};
		state.Production = new( new() { Normal = Vector3.UnitX, EdgeUp = Vector3.UnitZ,
			Side = Vector3.UnitX, HighSide = Vector3.UnitZ, IsActive = true, IsLedge = true }, true, true );
		Create().TryBegin( Input, 1, 0, state, new( 7, 7 ) );
		Assert.Equal( 0, state.UpdateCount );
		Assert.Equal( -1, state.SelectedIndex );
		Assert.False( state.Active );
		Assert.False( state.Production.Accepted );
		Assert.False( state.Production.Candidate.IsActive );
		Assert.Equal( Vector3.UnitY, state.Production.Candidate.EdgeUp );
		Assert.Equal( Vector3.UnitY, state.Production.Candidate.Side );
		Assert.Equal( Vector3.UnitY, state.Production.Candidate.HighSide );
		Assert.Equal( Vector3.UnitX, state.Production.Candidate.Normal );
		Assert.True( state.Production.Candidate.IsLedge );
		Assert.True( state.Production.FoundEdges );
		Assert.True( state.Capture && state.Repeated && state.Early && state.HasSelection );
		Assert.Equal( 31, state.SelectedKind );
	}

	[Fact]
	public void NewCycleStoresConditionedVelocityAndTracksSubmittedCandidate()
	{
		var state = new GrindCandidateSelectionState();
		Create().TryBegin( Input, 1, 0, state, new( 7, 7 ) );
		Assert.True( state.HasInput );
		Assert.False( state.MotionSpecial );
		Assert.Equal( new Vector4( .75f, 0, 1, 0 ), state.Request.Velocity );
		Assert.Equal( state.Request.Velocity, state.Candidates[ 0 ].Trajectory.Velocity );
		Assert.Equal( new Vector4( -1 ), state.Candidates[ 0 ].Time );
		Assert.False( state.Queries.Targets[ 0 ].Completed );
	}

	[Fact]
	public void ClearedReadinessAllowsReplacingTheStoredInput()
	{
		var preparation = Create();
		var state = new GrindCandidateSelectionState();
		var service = new TrajectoryQueryService( 7, 7 );
		preparation.TryBegin( Input, 1, 0, state, service );
		state.Queries.Ready = false;
		var next = Input with { Velocity = new( 0, 4, 0, 0 ), Point = new( 1, 2, 3, 4 ) };
		Assert.True( preparation.TryBegin( next, 1, 0, state, service ) );
		Assert.True( state.MotionSpecial );
		Assert.Equal( next.Point, state.Request.Point );
		Assert.Equal( 2, service.FirstCount );
	}

	[Fact]
	public void UpThresholdIncludesEqualityAndPreservesEveryVelocityLane()
	{
		var velocity = new Vector4( 1, 3, 2, 4 );
		var actual = new GrindQueryMotionPreparation( Settings ).Prepare( velocity, new( 1, .2f, 0, 0 ), 1, false, 0, out var special );
		Assert.Equal( velocity, actual );
		Assert.False( special );
	}

	[Theory]
	[InlineData( true, 0 )]
	[InlineData( false, 200 )]
	public void MovementGatesBypassProjectionDamping( bool secondary, int movement )
	{
		var actual = new GrindQueryMotionPreparation( Settings ).Prepare( Input.Velocity, Vector4.UnitX, 1, secondary, movement, out var special );
		Assert.Equal( Input.Velocity, actual );
		Assert.False( special );
	}

	[Fact]
	public void SpecialDirectionPrecedesBothMovementBypassGates()
	{
		var velocity = new Vector4( 0, 4, 0, 0 );
		var actual = new GrindQueryMotionPreparation( Settings ).Prepare( velocity, Vector4.UnitX, 1, true, 200, out var special );
		Assert.True( special );
		Assert.True( actual.X < 0 );
		Assert.InRange( Vector3.Distance( new( actual.X, actual.Y, actual.Z ), Vector3.Zero ), 3.99999f, 4.00001f );
	}

	[Fact]
	public void OrientationClampUsesOffsetBeforeItsBounds()
	{
		var motion = new GrindQueryMotionPreparation( Settings );
		var velocity = new Vector4( 0, 4, 0, 0 );
		var atLow = motion.Prepare( velocity, Vector4.UnitX, -.75f, false, 0, out _ );
		var below = motion.Prepare( velocity, Vector4.UnitX, -4, false, 0, out _ );
		var atHigh = motion.Prepare( velocity, Vector4.UnitX, 1.25f, false, 0, out _ );
		var above = motion.Prepare( velocity, Vector4.UnitX, 4, false, 0, out _ );
		Assert.Equal( atLow, below );
		Assert.Equal( atHigh, above );
		Assert.True( atLow.X > 0 && atHigh.X < 0 );
	}

	[Fact]
	public void StationaryMotionStaysFiniteWithoutEnteringSpecialMode()
	{
		var actual = new GrindQueryMotionPreparation( Settings ).Prepare( Vector4.Zero, Vector4.UnitX, 1, false, 0, out var special );
		Assert.Equal( Vector4.Zero, actual );
		Assert.False( special );
	}
}
