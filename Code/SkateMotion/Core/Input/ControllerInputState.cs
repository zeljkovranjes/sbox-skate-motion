namespace SkateMotion.Core.Input;

/// <summary>Retains consecutive controller samples and independently timed input events.</summary>
internal sealed class ControllerInputState
{
	internal ControllerReading Previous { get; set; }
	internal ControllerReading Current { get; set; }
	internal float LeftTriggerHeld { get; set; }
	internal float RightTriggerHeld { get; set; }
	internal float SinceLeftStickPressed { get; set; }
	internal float SinceLeftStickReleased { get; set; }
	internal float SinceRightStickPressed { get; set; }
	internal float SinceRightStickReleased { get; set; }
	internal float SouthHeld { get; set; }
	internal float EastHeld { get; set; }
	internal float WestHeld { get; set; }
	internal float NorthHeld { get; set; }
	internal float LeftStickActive { get; set; }
	internal float RightStickActive { get; set; }

	internal bool Pressed( ControllerButtons button ) => Current.Down( button ) && !Previous.Down( button );

	internal bool Released( ControllerButtons button ) => Previous.Down( button ) && !Current.Down( button );
}
