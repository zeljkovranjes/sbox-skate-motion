using System.Numerics;
using SkateMotion;
using SkateMotion.Core.Networking;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class NetBoardTests
{
	[Fact]
	public void EveryPartOfABoardsStateReachesTheOtherPlayers()
	{
		var state = new SkateState( 6.5f, 3, true, false, 2.25f )
		{
			Balance = SkateBalanceMode.NoseManual,
			Grind = SkateGrind.Noseslide,
			PowerSlide = -1,
			Reverting = true,
			Fakie = true,
			BodyFlip = 1,
			WallRiding = true,
			HandPlanting = true,
			GrindRailOnRight = true,
			GrindNoseRight = true,
			GrindBackward = true,
			GrindTwisted = true,
			GrindLow = true,
			GrindOnLedgeTop = true,
			NamedGrind = SkateGrind.Boardslide,
			NamedBackward = true,
			NamedTwisted = true,
			NamedLow = true,
			NamedOnLedgeTop = true,
			Crouch = 0.4f
		};

		Assert.Equal( state, Shared( state ) );
	}

	[Fact]
	public void EachYesOrNoOfABoardsStateIsItsOwn()
	{
		Assert.Equal( new SkateState( 0f, 0, false, true, 0f ), Shared( new SkateState( 0f, 0, false, true, 0f ) ) );
		Assert.Equal( new SkateState() with { Fakie = true }, Shared( new SkateState() with { Fakie = true } ) );
		Assert.Equal( new SkateState() with { GrindLow = true }, Shared( new SkateState() with { GrindLow = true } ) );
		Assert.Equal( new SkateState() with { NamedOnLedgeTop = true }, Shared( new SkateState() with { NamedOnLedgeTop = true } ) );
		Assert.Equal( new SkateState(), Shared( new SkateState() ) );
	}

	[Fact]
	public void EveryControlTheRiderGivesReachesTheOtherPlayers()
	{
		var input = new SkateInput( -0.5f, 1f, 0.25f, 0.3f, -0.9f, 0.6f, true, true, true, SkateAirGrab.Indy, true )
		{
			PushFeet = SkatePushFeet.Right,
			NewPush = true,
			HippyJump = true,
			Catch = true,
			FeetOff = SkatePushFeet.Left,
			HandPlant = true
		};

		Assert.Equal( input, Shared( input ) );
		Assert.Equal( new SkateInput( Goofy: true ), Shared( new SkateInput( Goofy: true ) ) );
		Assert.Equal( new SkateInput() with { Catch = true }, Shared( new SkateInput() with { Catch = true } ) );
		Assert.Equal( new SkateInput(), Shared( new SkateInput() ) );
	}

	[Fact]
	public void AnotherPlayersHippyJumpIsShownAsItsOwnerHasIt()
	{
		var jump = new HippyJump();

		jump.Show( true, false, Vector3.Zero, 0f, 0.6f );
		Assert.True( jump.Crouching );
		Assert.False( jump.Flying );
		Assert.Equal( 0.6f, jump.Charge );

		var offset = new Vector3( 0.1f, -0.05f, 0.8f );
		jump.Show( false, true, offset, 1.5f, 0.6f );
		Assert.False( jump.Crouching );
		Assert.True( jump.Flying );
		Assert.Equal( offset, jump.Offset );
		Assert.Equal( 1.5f, jump.VerticalSpeed );
		Assert.True( jump.OverBoard );
		Assert.True( jump.TimeToLand > 0f );

		jump.Show( false, false, Vector3.Zero, 0f, 0f );
		Assert.False( jump.Flying );
		Assert.Equal( 0f, jump.TimeToLand );
	}

	private static SkateState Shared( SkateState state ) => NetBoard.State( state.Speed, state.GroundedWheels, state.LandingSpeed,
		state.Crouch, state.PowerSlide, state.BodyFlip, state.Balance, state.Grind, state.NamedGrind, NetBoard.StateFlags( state ) );

	private static SkateInput Shared( SkateInput input ) => NetBoard.Input( input.Steer, input.Push, input.Brake, input.FlickX,
		input.FlickY, input.Lean, input.Grab, input.PushFeet, input.FeetOff, NetBoard.InputFlags( input ) );
}
