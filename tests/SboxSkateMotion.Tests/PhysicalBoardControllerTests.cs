using SkateMotion.Core.Physics;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalBoardControllerTests
{
	[Theory]
	[InlineData( 0x2000UL )]
	[InlineData( 0x3000UL )]
	public void ReleaseMarkerOverridesContactHoldGates( ulong action )
	{
		var result = PhysicalBoardController.Evaluate( Input( 1 ) with
		{
			ActionFlags = action, RigFlags = 1UL << 51, TimingFlags = 1UL << 60
		} );
		Assert.Equal( new PhysicalBoardDecision( 2, ChangeState: true ), result );
	}

	[Fact]
	public void ContactReleaseUsesTimingBitRatherThanTheWholeNibble()
	{
		var input = Input( 1 ) with { BoardContact = true, TimingFlags = 2UL << 60 };
		Assert.Equal( 2, PhysicalBoardController.Evaluate( input ).NextState );
		input = input with { TimingFlags = 3UL << 60 };
		Assert.Equal( new PhysicalBoardDecision( 1, UpdateHeld: true ), PhysicalBoardController.Evaluate( input ) );
	}

	[Fact]
	public void RetrievalRequestKeepsGroundQueryAndTakesPriorityOverGroundTransition()
	{
		var input = Input( 2 ) with { ActionFlags = 0x800, GroundCheck = true, BodyMode = 6 };
		Assert.Equal( new PhysicalBoardDecision( 4, ChangeState: true, QueryGround: true ),
			PhysicalBoardController.Evaluate( input ) );
	}

	[Fact]
	public void RetrievalCompletionNeedsProgressAndAnAllowedJoint()
	{
		var input = Input( 4 ) with { RetrievalProgress = 0.999f };
		Assert.Equal( new PhysicalBoardDecision( 4 ), PhysicalBoardController.Evaluate( input ) );
		input = input with { RetrievalProgress = 1f, SelectedJointContact = true };
		Assert.Equal( new PhysicalBoardDecision( 2, ChangeState: true ), PhysicalBoardController.Evaluate( input ) );
		input = input with { RigFlags = 1UL << 51 };
		Assert.Equal( new PhysicalBoardDecision( 1, ChangeState: true, ClearVelocity: true ),
			PhysicalBoardController.Evaluate( input ) );
	}

	[Fact]
	public void PhysicalFeedbackUsesOwnershipStateAndContactSide()
	{
		Assert.True( Input( 1 ).Holding );
		Assert.False( Input( 4 ).Holding );
		Assert.True( Input( 4 ).Retrieving );
		Assert.Equal( 7, Input( 4 ).ContactJointIndex );
		Assert.Equal( 3, (Input( 4 ) with { ActionFlags = 4 }).ContactJointIndex );
		Assert.Equal( new PhysicalBoardDecision( 5, UpdateContactSpring: true, ContactSpringStrength: 0.95f ),
			PhysicalBoardController.Evaluate( Input( 5 ) ) );
		Assert.Equal( new PhysicalBoardDecision( -1 ), PhysicalBoardController.Evaluate( Input( -1 ) ) );
	}

	private static PhysicalBoardInput Input( int state ) => new( state, 0, 0, 0, 0f, false, 0, false, false );
}
