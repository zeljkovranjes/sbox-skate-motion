using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Input;

/// <summary>Updates resolved input without clamping axes or merging distinct buttons.</summary>
internal static class ControllerInputProcessor
{
	internal static void Update( ControllerInputState state, ControllerReading reading,
		float delta, float activityThreshold, bool retainPreviousBumper )
	{
		// Only the populated portion of the flag word transfers to the previous sample.
		var previousFlags = ((uint)state.Current.Buttons & 0xffff0000u)
			| ((uint)state.Previous.Buttons & 0x0000ffffu);
		state.Previous = state.Current with { Buttons = (ControllerButtons)previousFlags };
		var flags = ((uint)reading.Buttons & 0xfff00000u) | ((uint)state.Current.Buttons & 0x000fffffu);
		var bumpers = ControllerButtons.LeftBumper | ControllerButtons.RightBumper;
		if ( ((ControllerButtons)flags & bumpers) == bumpers )
		{
			var previousBumper = state.Previous.Buttons & bumpers;
			var selected = retainPreviousBumper && (previousBumper == ControllerButtons.LeftBumper
				|| previousBumper == ControllerButtons.RightBumper) ? previousBumper : ControllerButtons.LeftBumper;
			flags = (flags & ~(uint)bumpers) | (uint)selected;
		}
		state.Current = reading with { Buttons = (ControllerButtons)flags };
		UpdateTimers( state, delta, activityThreshold );
	}

	private static void UpdateTimers( ControllerInputState state, float delta, float threshold )
	{
		var current = state.Current;
		state.LeftTriggerHeld = current.LeftTrigger == 1f ? state.LeftTriggerHeld + delta : 0f;
		state.RightTriggerHeld = current.RightTrigger == 1f ? state.RightTriggerHeld + delta : 0f;
		state.SinceLeftStickPressed = state.Pressed( ControllerButtons.LeftStick ) ? 0f : state.SinceLeftStickPressed + delta;
		state.SinceLeftStickReleased = state.Released( ControllerButtons.LeftStick ) ? 0f : state.SinceLeftStickReleased + delta;
		state.SinceRightStickPressed = state.Pressed( ControllerButtons.RightStick ) ? 0f : state.SinceRightStickPressed + delta;
		state.SinceRightStickReleased = state.Released( ControllerButtons.RightStick ) ? 0f : state.SinceRightStickReleased + delta;
		state.SouthHeld = current.Down( ControllerButtons.South ) ? state.SouthHeld + delta : 0f;
		state.EastHeld = current.Down( ControllerButtons.East ) ? state.EastHeld + delta : 0f;
		state.WestHeld = current.Down( ControllerButtons.West ) ? state.WestHeld + delta : 0f;
		state.NorthHeld = current.Down( ControllerButtons.North ) ? state.NorthHeld + delta : 0f;
		state.LeftStickActive = Magnitude( current.LeftX, current.LeftY ) > threshold ? state.LeftStickActive + delta : 0f;
		state.RightStickActive = Magnitude( current.RightX, current.RightY ) > threshold ? state.RightStickActive + delta : 0f;
	}

	private static float Magnitude( float x, float y )
	{
		var squaredX = x * x;
		return LimbSolverMath.SquareRoot( LimbSolverMath.MultiplyAdd( y, y, squaredX ) );
	}
}
