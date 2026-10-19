using SkateMotion.Core.Input;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ControllerInputProcessorTests
{
	[Theory]
	[InlineData( 0u, false, 0x20000000u )]
	[InlineData( 0u, true, 0x20000000u )]
	[InlineData( 0x10000000u, false, 0x20000000u )]
	[InlineData( 0x10000000u, true, 0x10000000u )]
	[InlineData( 0x20000000u, false, 0x20000000u )]
	[InlineData( 0x20000000u, true, 0x20000000u )]
	[InlineData( 0x30000000u, false, 0x20000000u )]
	[InlineData( 0x30000000u, true, 0x20000000u )]
	public void BumperConflictUsesContextAndPriorExclusiveChoice( uint previous, bool retain, uint expected )
	{
		var state = new ControllerInputState { Current = Sample( (ControllerButtons)previous ) };
		ControllerInputProcessor.Update( state, Sample( ControllerButtons.LeftBumper | ControllerButtons.RightBumper ), .125f, .1f, retain );
		Assert.Equal( expected, (uint)state.Current.Buttons );
	}

	[Fact]
	public void StickClickTimersMeasureTimeSinceEachEdge()
	{
		var state = new ControllerInputState();
		var held = Sample( ControllerButtons.LeftStick | ControllerButtons.RightStick );
		ControllerInputProcessor.Update( state, held, .125f, .1f, false );
		Assert.Equal( 0f, state.SinceLeftStickPressed );
		Assert.Equal( 0f, state.SinceRightStickPressed );
		Assert.Equal( .125f, state.SinceLeftStickReleased );
		ControllerInputProcessor.Update( state, held, .125f, .1f, false );
		Assert.Equal( .125f, state.SinceLeftStickPressed );
		Assert.Equal( .25f, state.SinceRightStickReleased );
		ControllerInputProcessor.Update( state, default, .125f, .1f, false );
		Assert.Equal( .25f, state.SinceLeftStickPressed );
		Assert.Equal( .25f, state.SinceRightStickPressed );
		Assert.Equal( 0f, state.SinceLeftStickReleased );
		Assert.Equal( 0f, state.SinceRightStickReleased );
	}

	[Fact]
	public void TriggerPressureIsRetainedButOnlyFullPressureAccumulatesHoldTime()
	{
		var state = new ControllerInputState { LeftTriggerHeld = 1f, RightTriggerHeld = 2f };
		ControllerInputProcessor.Update( state, Sample() with { LeftTrigger = .99999994f, RightTrigger = 1f }, .125f, .1f, false );
		Assert.Equal( .99999994f, state.Current.LeftTrigger );
		Assert.Equal( 0f, state.LeftTriggerHeld );
		Assert.Equal( 2.125f, state.RightTriggerHeld );
		ControllerInputProcessor.Update( state, Sample() with { LeftTrigger = 1f, RightTrigger = .5f }, .125f, .1f, false );
		Assert.Equal( .125f, state.LeftTriggerHeld );
		Assert.Equal( 0f, state.RightTriggerHeld );
	}

	[Fact]
	public void FaceButtonsKeepIndependentHeldDurations()
	{
		var state = new ControllerInputState();
		ControllerInputProcessor.Update( state, Sample( ControllerButtons.West ), .125f, .1f, false );
		ControllerInputProcessor.Update( state, Sample( ControllerButtons.South | ControllerButtons.West ), .125f, .1f, false );
		Assert.Equal( .25f, state.WestHeld );
		Assert.Equal( .125f, state.SouthHeld );
		Assert.True( state.Pressed( ControllerButtons.South ) );
		Assert.False( state.Pressed( ControllerButtons.West ) );
		ControllerInputProcessor.Update( state, Sample( ControllerButtons.East | ControllerButtons.North ), .125f, .1f, false );
		Assert.Equal( 0f, state.WestHeld );
		Assert.Equal( 0f, state.SouthHeld );
		Assert.Equal( .125f, state.EastHeld );
		Assert.Equal( .125f, state.NorthHeld );
	}

	[Fact]
	public void ActivityThresholdIsStrictAndDoesNotApplyAnAxisDeadzone()
	{
		var state = new ControllerInputState();
		var reading = Sample() with { LeftX = .5f, RightY = -.500001f };
		ControllerInputProcessor.Update( state, reading, .125f, .5f, false );
		Assert.Equal( 0f, state.LeftStickActive );
		Assert.Equal( .125f, state.RightStickActive );
		Assert.Equal( .5f, state.Current.LeftX );
		Assert.Equal( -.500001f, state.Current.RightY );
		ControllerInputProcessor.Update( state, reading with { LeftY = .5f, RightY = 0f }, .125f, .5f, false );
		Assert.Equal( .125f, state.LeftStickActive );
		Assert.Equal( 0f, state.RightStickActive );
	}

	[Fact]
	public void ReservedFlagsSurviveInputSamplingAndPartialPreviousCopy()
	{
		var state = new ControllerInputState
		{
			Previous = Sample( (ControllerButtons)0x13579bdf ),
			Current = Sample( (ControllerButtons)0x200a5c3f )
		};
		ControllerInputProcessor.Update( state, Sample( ControllerButtons.South ), 0f, .1f, false );
		Assert.Equal( 0x200a9bdfu, (uint)state.Previous.Buttons );
		Assert.Equal( 0x002a5c3fu, (uint)state.Current.Buttons );
	}

	private static ControllerReading Sample( ControllerButtons buttons = ControllerButtons.None ) => new( 0f, 0f, 0f, 0f, 0f, 0f, buttons );
}
