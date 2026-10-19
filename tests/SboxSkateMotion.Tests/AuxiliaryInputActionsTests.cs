using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class AuxiliaryInputActionsTests
{
	[Fact]
	public void ManualBalanceUsesRadialPressureAndVerticalSign()
	{
		var sample = Sample() with { RightX = .6f, RightY = .8f };
		var positive = ManualInputActions.Evaluate( sample, false, .5f, 2f );
		var negative = ManualInputActions.Evaluate( sample with { RightY = -.8f }, false, .5f, 2f );
		Assert.True( positive.HasBalance );
		Assert.True( positive.HasBrake );
		Assert.Equal( 1f, positive.Balance, 5 );
		Assert.Equal( -positive.Balance, negative.Balance );
		Assert.Equal( -positive.Brake, negative.Brake );
	}

	[Fact]
	public void HorizontalManualPressureCanBrakeWithoutBalance()
	{
		var result = ManualInputActions.Evaluate( Sample() with { RightX = 1f }, false, .5f, 2f );
		Assert.False( result.HasBalance );
		Assert.True( result.HasBrake );
		Assert.Equal( -1f, result.Brake );
	}

	[Fact]
	public void ManualBrakeRequiresStrictlyMoreThanTheThreshold()
	{
		var result = ManualInputActions.Evaluate( Sample() with { RightY = .5f }, false, .5f, 2f );
		Assert.True( result.HasBalance );
		Assert.False( result.HasBrake );
	}

	[Fact]
	public void SuppressedManualInputOmitsBothRequests()
	{
		Assert.Equal( default, ManualInputActions.Evaluate( Sample() with { RightY = 1f }, true, .5f, 2f ) );
	}

	[Fact]
	public void HandplantRequestsKeepBothFeetAndDismountIndependent()
	{
		var result = HandplantInputActions.Evaluate( Sample( ControllerButtons.West | ControllerButtons.South
			| ControllerButtons.East ) with { RightX = -.375f, RightY = .625f } );
		Assert.True( result.OneFootLeft );
		Assert.True( result.OneFootRight );
		Assert.True( result.Dismount );
		Assert.Equal( -.375f, result.TweakX );
		Assert.Equal( .625f, result.TweakY );
	}

	[Fact]
	public void DismountKeepsHoldAfterTheInitialEdge()
	{
		var current = Sample( ControllerButtons.East );
		Assert.Equal( new DismountInputActions( true, true ), DismountInputActions.Evaluate( default, current ) );
		Assert.Equal( new DismountInputActions( true, false ), DismountInputActions.Evaluate( current, current ) );
		Assert.Equal( default, DismountInputActions.Evaluate( current, default ) );
	}

	[Fact]
	public void GestureReleaseFromBumperDoesNotInventAnEdge()
	{
		var suppressed = Sample( ControllerButtons.LeftBumper | ControllerButtons.Up );
		Assert.Equal( default, GestureInputActions.Evaluate( default, suppressed ) );
		var released = GestureInputActions.Evaluate( suppressed, Sample( ControllerButtons.Up ) );
		Assert.Equal( ControllerButtons.None, released.Started );
		Assert.Equal( ControllerButtons.Up, released.Held );
	}

	[Fact]
	public void AllGestureDirectionsCanStartTogether()
	{
		var directions = ControllerButtons.Up | ControllerButtons.Down | ControllerButtons.Left | ControllerButtons.Right;
		var result = GestureInputActions.Evaluate( default, Sample( directions ) );
		Assert.Equal( directions, result.Started );
		Assert.Equal( directions, result.Held );
		Assert.Equal( default, GestureInputActions.Evaluate( default, Sample( directions | ControllerButtons.RightBumper ) ) );
	}

	[Theory]
	[InlineData( false, false )]
	[InlineData( false, true )]
	[InlineData( true, false )]
	[InlineData( true, true )]
	public void BailAxesUseIndependentModeGates( bool gesture, bool control )
	{
		var current = Sample() with { LeftX = -.25f, LeftY = .5f, RightX = .75f, RightY = -1f };
		var result = BailInputActions.Evaluate( default, current, gesture, control, false, false );
		Assert.Equal( gesture ? .75f : 0f, result.GestureX );
		Assert.Equal( gesture ? -1f : 0f, result.GestureY );
		Assert.Equal( control ? -.25f : 0f, result.ControlX );
		Assert.Equal( control ? .5f : 0f, result.ControlY );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	[InlineData( 3 )]
	public void EachBailChordControlCanSupplyTheLastEdge( int last )
	{
		var complete = Sample( ControllerButtons.LeftStick | ControllerButtons.RightStick ) with { LeftTrigger = 1f, RightTrigger = 1f };
		var previous = last switch
		{
			0 => complete with { LeftTrigger = .99999994f },
			1 => complete with { RightTrigger = .99999994f },
			2 => complete with { Buttons = ControllerButtons.RightStick },
			_ => complete with { Buttons = ControllerButtons.LeftStick }
		};
		Assert.True( BailInputActions.Evaluate( previous, complete, false, false, false, false ).Request );
		Assert.False( BailInputActions.Evaluate( complete, complete, false, false, false, false ).Request );
		Assert.False( BailInputActions.Evaluate( previous, complete, false, false, false, true ).Request );
		Assert.False( BailInputActions.Evaluate( default, previous, false, false, false, false ).Request );
	}

	[Fact]
	public void PushOffRequiresItsModeAndRightTriggerEdge()
	{
		var complete = Sample() with { RightTrigger = 1f };
		Assert.True( BailInputActions.Evaluate( default, complete, false, false, true, false ).PushOff );
		Assert.False( BailInputActions.Evaluate( complete, complete, false, false, true, false ).PushOff );
		Assert.False( BailInputActions.Evaluate( default, complete, false, false, false, false ).PushOff );
	}

	[Fact]
	public void RecoveryAcceptsEitherPushButtonEdgeWithoutAChord()
	{
		var west = Sample( ControllerButtons.West );
		var south = Sample( ControllerButtons.South );
		Assert.True( BailInputActions.Evaluate( default, west, false, false, false, true ).Recover );
		Assert.True( BailInputActions.Evaluate( default, south, false, false, false, true ).Recover );
		Assert.False( BailInputActions.Evaluate( west, west, false, false, false, false ).Recover );
	}

	private static ControllerReading Sample( ControllerButtons buttons = ControllerButtons.None ) => new( 0f, 0f, 0f, 0f, 0f, 0f, buttons );
}
