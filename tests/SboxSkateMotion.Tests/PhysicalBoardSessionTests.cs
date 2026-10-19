using System;
using System.Collections.Generic;
using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardSessionTests
{
	[Fact]
	public void RetrievalAdvancesOnEntryAndTransfersToTheHandOnTheFollowingTick()
	{
		var fixture = new Fixture();
		fixture.Enter( 2 );
		fixture.Tick( 0x804 );
		Assert.Equal( 4, fixture.Session.State );
		Assert.Equal( .5f, fixture.Session.Motion.Progress );
		Assert.Equal( new[] { "joints-off", "clear-direction", "target", "retrieval-feedback" }, fixture.Events );
		fixture.Tick( 4 );
		Assert.Equal( 1f, fixture.Session.Motion.Progress );
		Assert.Equal( 4, fixture.Session.State );
		fixture.Tick( 4 );
		Assert.Equal( 1, fixture.Session.State );
		Assert.Equal( 0, fixture.Session.Hands.SelectedHand );
		Assert.Equal( new[] { "velocity", "held-bodies", "joints-on" }, fixture.Events );
		fixture.Tick();
		Assert.Equal( new[] { "contact-feedback" }, fixture.Events );
		Assert.True( fixture.Feedback.Enabled );
		Assert.Equal( .8f, fixture.Feedback.AlignmentThreshold );
	}

	[Fact]
	public void OrdinaryReleaseDoesNotMoveTheBoardOrClearItsVelocity()
	{
		var fixture = new Fixture();
		fixture.Enter( 1 );
		fixture.Tick( 0x2000 );
		Assert.Equal( 2, fixture.Session.State );
		Assert.Equal( 2, fixture.Session.Hands.SelectedHand );
		Assert.False( fixture.Session.TargetDrive.Enabled );
		Assert.Equal( new[] { "released-bodies", "joints-on", "clear-direction" }, fixture.Events );
		fixture.Tick();
		Assert.Empty( fixture.Events );
	}

	[Fact]
	public void PlacementRetainsItsSourceForRetrievalEvenWhenTheObservedBoardMoved()
	{
		var fixture = new Fixture();
		fixture.Poses = fixture.Poses with { ProcessedBoard = At( 40f ) };
		fixture.Enter( 2 );
		fixture.Tick();
		Assert.Equal( 3, fixture.Session.State );
		Assert.Equal( At( 40f ), fixture.Session.Motion.Source );
		Assert.Equal( new[] { "joints-off", "clear-direction", "target", "relocate", "velocity" }, fixture.Events );
		fixture.Poses = fixture.Poses with { ProcessedBoard = At( -100f ) };
		fixture.Tick( 0x800 );
		Assert.Equal( 4, fixture.Session.State );
		Assert.Equal( 31f, fixture.Session.Motion.Source.Translation.X, 4 );
		Assert.Equal( new[] { "joints-off", "clear-direction", "relocate", "target", "retrieval-feedback" }, fixture.Events );
	}

	[Fact]
	public void ContactCancelsRetrievalAndClearsVelocityWithoutCompletingTheHandTransfer()
	{
		var fixture = new Fixture();
		fixture.Enter( 4 );
		fixture.Tick( jointContact: true );
		Assert.Equal( 2, fixture.Session.State );
		Assert.Equal( 2, fixture.Session.Hands.SelectedHand );
		Assert.Equal( new[] { "released-bodies", "joints-on", "clear-direction", "velocity" }, fixture.Events );
	}

	[Fact]
	public void ThrowRunsExactlyEightUpdatesIncludingItsReleaseTick()
	{
		var fixture = new Fixture();
		fixture.Enter( 1 );
		fixture.Tick( 0x3000 );
		Assert.Equal( 7, fixture.Session.ThrowUpdatesRemaining );
		Assert.Equal( new[] { "released-bodies", "joints-on", "clear-direction", "throw", "throw-update-7" }, fixture.Events );
		for ( var remaining = 6; remaining >= 0; --remaining )
		{
			fixture.Tick();
			Assert.Equal( new[] { "throw-update-" + remaining }, fixture.Events );
		}
		fixture.Tick();
		Assert.Empty( fixture.Events );
	}

	[Fact]
	public void DisabledTicksPreserveMotionAndThrowCounters()
	{
		var fixture = new Fixture();
		fixture.Enter( 2, 0x1000 );
		fixture.Session.Update( false, default, default );
		Assert.Equal( 8, fixture.Session.ThrowUpdatesRemaining );
		Assert.Equal( 2, fixture.Session.State );
		Assert.Empty( fixture.Events );
	}

	[Fact]
	public void ReenteringTheSameStateResetsTheThrowCounterWithoutBodyOperations()
	{
		var fixture = new Fixture();
		fixture.Enter( 2, 0x1000 );
		fixture.Session.ChangeState( 2, default, fixture.Poses );
		Assert.Equal( 0, fixture.Session.ThrowUpdatesRemaining );
		Assert.Empty( fixture.Events );
	}

	[Fact]
	public void SpeedFollowingWritesContactDirectionsWithoutAdvancingMotion()
	{
		var fixture = new Fixture();
		fixture.Enter( 5 );
		fixture.Tick();
		Assert.Equal( new[] { "contact-feedback" }, fixture.Events );
		Assert.False( fixture.Feedback.Enabled );
		Assert.Equal( .95f, fixture.Feedback.AlignmentThreshold );
		Assert.Equal( fixture.Poses.ContactDirection, fixture.Feedback.FirstDirection );
		Assert.Equal( fixture.Feedback.FirstDirection, fixture.Feedback.SecondDirection );
		Assert.Equal( 0f, fixture.Session.Motion.Elapsed );
	}

	private static LimbRigidTransform At( float x ) => LimbRigidTransform.Identity with { Translation = new Vector3( x, 0f, 0f ) };

	private sealed class Fixture
	{
		internal List<string> Events { get; } = new();
		internal PhysicalBoardSession Session { get; }
		internal PhysicalBoardPoses Poses { get; set; } = new( At( 0f ), At( 0f ), At( 1f ), At( 2f ), At( -2f ),
			Vector3.UnitX, -Vector3.UnitX, Vector3.Zero, Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY );
		internal BoardContactFeedback Feedback { get; private set; }

		internal Fixture()
		{
			var x = new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f };
			var duration = new BoardCorrectionCurve( x, new[] { .1f, .1f, .1f, .1f, .1f, .1f, .1f, .1f } );
			var weight = new BoardCorrectionCurve( x, new[] { 0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } );
			var math = new LimbSolverMath( new[] { -1f / 720f, 1f / 24f, -.5f },
				new[] { -1f / 5040f, 1f / 120f, -1f / 6f }, new float[ 8 ], 2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
			var blender = new LimbPoseBlender( math, new LimbArcTangent( new float[ 8 ], MathF.PI / 2f, MathF.PI ), .00001f, .1f );
			var actuators = new PhysicalBoardActuators(
				() => Events.Add( "held-bodies" ), () => Events.Add( "released-bodies" ),
				enabled => Events.Add( enabled ? "joints-on" : "joints-off" ), () => Events.Add( "clear-direction" ),
				() => 4UL, feedback => { Feedback = feedback; Events.Add( "contact-feedback" ); },
				() => Events.Add( "velocity" ), frame => Events.Add( "target" ),
				position => Events.Add( "relocate" ), position => Events.Add( "retrieval-feedback" ),
				() => Events.Add( "throw" ), remaining => Events.Add( "throw-update-" + remaining ) );
			Session = new( new( duration, .001f, 30f, 5f, .1f ), new( weight, blender, .05f ),
				new( 100f, .05f ), 30f, new BoardTargetDrive(), new BoardHeldConstraints(), actuators );
		}

		internal void Enter( int state, ulong action = 0 )
		{
			Session.ChangeState( state, Input( action ), Poses );
			Events.Clear();
		}

		internal void Tick( ulong action = 0, bool jointContact = false )
		{
			Events.Clear();
			Session.Update( true, Input( action ) with { SelectedJointContact = jointContact }, Poses );
		}

		private static PhysicalBoardInput Input( ulong action ) => new( 99, action, 0, 0, 99f, false, 0, false, false );
	}
}
