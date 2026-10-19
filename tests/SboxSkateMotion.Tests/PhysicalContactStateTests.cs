using System.Numerics;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalContactStateTests
{
	[Fact]
	public void CountdownSuppressesThenRestoresOnlyTheContactFlag()
	{
		var modes = Modes();
		modes.PrimaryPending = true;
		var body = modes.Bodies[ 1 ];
		body.CollisionCounter = 2;
		body.ShapeFlags = 0xababababu;
		body.ShapeMode = 17;
		body.ContactPropertySlot = 2;
		var mass = body.Body.Mass;
		modes.AdvanceCollisionCounters();
		Assert.Equal( 1u, body.CollisionCounter );
		Assert.Equal( 0xabababaau, body.ShapeFlags );
		Assert.True( modes.PrimaryPending );
		modes.AdvanceCollisionCounters();
		Assert.Equal( 0u, body.CollisionCounter );
		Assert.Equal( 0xababababu, body.ShapeFlags );
		Assert.False( modes.PrimaryPending );
		Assert.Equal( 17u, body.ShapeMode );
		Assert.Equal( 2, body.ContactPropertySlot );
		Assert.Equal( mass, body.Body.Mass );
	}

	[Fact]
	public void PendingGatePreventsCounterAndFlagChanges()
	{
		var modes = Modes();
		modes.Bodies[ 2 ].CollisionCounter = 2;
		modes.Bodies[ 2 ].ShapeFlags = 7;
		modes.AdvanceCollisionCounters();
		Assert.Equal( 2u, modes.Bodies[ 2 ].CollisionCounter );
		Assert.Equal( 7u, modes.Bodies[ 2 ].ShapeFlags );
	}

	[Theory]
	[InlineData( 0u )]
	[InlineData( 0x80000000u )]
	[InlineData( 0xffffffffu )]
	public void NonpositiveSignedCountsAreUntouchedAndDoNotRemainPending( uint count )
	{
		var modes = Modes();
		modes.PrimaryPending = true;
		modes.Bodies[ 3 ].CollisionCounter = count;
		modes.Bodies[ 3 ].ShapeFlags = 6;
		modes.AdvanceCollisionCounters();
		Assert.Equal( count, modes.Bodies[ 3 ].CollisionCounter );
		Assert.Equal( 6u, modes.Bodies[ 3 ].ShapeFlags );
		Assert.False( modes.PrimaryPending );
	}

	[Fact]
	public void CountdownIncludesRootButExcludesTheTwoFeedbackBodies()
	{
		var modes = Modes();
		modes.PrimaryPending = true;
		foreach ( var body in modes.Bodies )
			body.CollisionCounter = 1;
		modes.AdvanceCollisionCounters();
		Assert.Equal( 0u, modes.Bodies[ 0 ].CollisionCounter );
		Assert.Equal( 1u, modes.Bodies[ 0 ].ShapeFlags );
		Assert.Equal( 1u, modes.Bodies[ 24 ].CollisionCounter );
		Assert.Equal( 1u, modes.Bodies[ 25 ].CollisionCounter );
		Assert.Equal( 0u, modes.Bodies[ 24 ].ShapeFlags );
		Assert.False( modes.PrimaryPending );
	}

	[Theory]
	[InlineData( 201u, true )]
	[InlineData( 200u, false )]
	[InlineData( 202u, false )]
	public void SelectedStateUsesTheRetainedCanonicalVelocity( uint state, bool alternate )
	{
		var input = Input( Actor() with { State = state } );
		Assert.Equal( alternate ? new Vector3( -7f, -8f, -9f ) : new Vector3( 7f, 8f, 9f ), input.Velocity );
		Assert.Equal( new Vector3( .25f, -.5f, .75f ), input.PreviousVelocity );
	}

	[Theory]
	[InlineData( 601u, true )]
	[InlineData( 602u, true )]
	[InlineData( 600u, false )]
	public void PositionSelectionPreservesItsExactStateRange( uint state, bool alternate )
	{
		var actor = Actor() with { State = state };
		Assert.Equal( alternate ? actor.AlternatePosition : actor.Position, Input( actor ).Position );
	}

	[Theory]
	[InlineData( 32, false )]
	[InlineData( 33, true )]
	[InlineData( 34, false )]
	public void SpecialFrameUsesOnlyItsAuthoredMotionBit( int bit, bool special )
	{
		var actor = Actor() with { ParentState = 500, MotionBits = 1ul << bit };
		var input = Input( actor );
		Assert.True( input.SpecialState );
		Assert.Equal( special ? actor.SpecialPosition : actor.Position, input.Position );
		Assert.Equal( special ? actor.SpecialNormal : Vector3.UnitY, input.Up );
	}

	[Theory]
	[InlineData( 501u )]
	[InlineData( 503u )]
	public void AlternateReferenceStatesSuppressTheSpecialFrame( uint state )
	{
		var actor = Actor() with { State = state, ParentState = 500, MotionBits = 1ul << 33 };
		var input = Input( actor );
		Assert.True( input.AlternateReference );
		Assert.False( input.SpecialState );
		Assert.Equal( actor.Position, input.Position );
	}

	[Fact]
	public void ActorBitsRemainIndependentOfModeAndTimerFlags()
	{
		var modes = Modes();
		modes.ApplySecondary( new( false, false, false, 0f, 0f, 0f, 0f ) );
		var actor = Actor() with { ActionBits = (1ul << 47) | (1ul << 60), ParentState = 600 };
		var input = Input( actor, modes );
		Assert.True( input.ActorGate );
		Assert.True( input.AlternateObjectScale );
		Assert.True( input.SecondaryMode );
		Assert.True( input.Mode600 );
		Assert.True( input.ResetTimer );
		var neighbors = Input( actor with { ActionBits = (1ul << 46) | (1ul << 48) | (1ul << 59) | (1ul << 61) } );
		Assert.False( neighbors.ActorGate );
		Assert.False( neighbors.AlternateObjectScale );
	}

	private static PhysicalContactActorState Actor() => new(
		0, 0, 0, 0, new( 1f, 2f, 3f ), new( -1f, -2f, -3f ), new( 4f, 5f, 6f ), new( .6f, .8f, 0f ) );

	private static PhysicalContactFeedbackInput Input( PhysicalContactActorState actor, PhysicalRigBodyModes? modes = null )
	{
		var readback = new PhysicalPoseResetState( 26 ) { WeightedPhysicalVelocity = new( 99f ) };
		readback.Deltas[ 22 ] = new( 55f );
		readback.Deltas[ 23 ] = new( -7f, -8f, -9f );
		var feedback = new PhysicalPoseFeedbackState { Velocity = new( .25f, -.5f, .75f ) };
		return PhysicalContactInputBuilder.Build( actor, readback, feedback, modes ?? Modes(),
			new( 7f, 8f, 9f ), Vector3.UnitY, 1f / 60f, true );
	}

	private static PhysicalRigBodyModes Modes()
	{
		var mass = new PhysicalBodyMass( Vector3.One, 1f );
		var bodies = Enumerable.Range( 0, 26 ).Select( _ => new PhysicalRigModeBody(
			new PhysicalBodyState { Mass = mass, Pose = LimbRigidTransform.Identity }, mass, mass ) ).ToArray();
		return new( bodies, new Vector3[ 3 ], false );
	}
}
