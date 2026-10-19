using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class OffBoardInputActionsTests
{
	[Fact]
	public void DropAndRetrieveStartAtFullLeftTriggerPressure()
	{
		var partial = Sample() with { LeftTrigger = .99999994f };
		Assert.False( OffBoardInputActions.Evaluate( default, partial ).DropBoard );
		var full = partial with { LeftTrigger = 1f };
		var pressed = OffBoardInputActions.Evaluate( partial, full );
		Assert.True( pressed.DropBoard );
		Assert.True( pressed.RetrieveBoard );
		Assert.False( pressed.ThrowBoard );
		Assert.False( OffBoardInputActions.Evaluate( full, full ).DropBoard );
		Assert.False( OffBoardInputActions.Evaluate( full, partial ).RetrieveBoard );
	}

	[Fact]
	public void ThrowAndRetrieveStartAtFullRightTriggerPressure()
	{
		var full = Sample() with { RightTrigger = 1f };
		var pressed = OffBoardInputActions.Evaluate( default, full );
		Assert.True( pressed.ThrowBoard );
		Assert.True( pressed.RetrieveBoard );
		Assert.False( pressed.DropBoard );
		Assert.False( OffBoardInputActions.Evaluate( full, full ).ThrowBoard );
		Assert.False( OffBoardInputActions.Evaluate( full, default ).RetrieveBoard );
	}

	[Fact]
	public void BothTriggerEdgesKeepBothRequests()
	{
		var result = OffBoardInputActions.Evaluate( default, Sample() with { LeftTrigger = 1f, RightTrigger = 1f } );
		Assert.True( result.DropBoard );
		Assert.True( result.ThrowBoard );
		Assert.True( result.RetrieveBoard );
	}

	[Fact]
	public void JumpUsesOnlyTheWestButtonPressEdge()
	{
		var held = Sample( ControllerButtons.West );
		Assert.True( OffBoardInputActions.Evaluate( default, held ).Jump );
		Assert.False( OffBoardInputActions.Evaluate( held, held ).Jump );
		Assert.False( OffBoardInputActions.Evaluate( held, default ).Jump );
		Assert.False( OffBoardInputActions.Evaluate( default, Sample( ControllerButtons.South ) ).Jump );
	}

	[Theory]
	[InlineData( 0x40000000u, true )]
	[InlineData( 0x10000000u, true )]
	[InlineData( 0x50000000u, true )]
	[InlineData( 0x20000000u, false )]
	[InlineData( 0x80000000u, false )]
	public void BodyTweakUsesRightStickClickOrRightBumper( uint buttons, bool expected )
	{
		var held = Sample( (ControllerButtons)buttons );
		Assert.Equal( expected, OffBoardInputActions.Evaluate( held, held ).AirBodyTweak );
	}

	[Fact]
	public void LookingAndBodyAxesPreserveSignedAnalogValuesWithoutActivation()
	{
		var result = OffBoardInputActions.Evaluate( default, Sample() with { RightX = -.25f, RightY = .75f } );
		Assert.False( result.AirBodyTweak );
		Assert.Equal( -.25f, result.LookAtX );
		Assert.Equal( .75f, result.LookAtY );
		Assert.Equal( -.25f, result.AirBodyTweakX );
		Assert.Equal( .75f, result.AirBodyTweakY );
	}

	private static ControllerReading Sample( ControllerButtons buttons = ControllerButtons.None ) => new( 0f, 0f, 0f, 0f, 0f, 0f, buttons );
}
