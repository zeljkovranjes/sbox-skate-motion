namespace SkateMotion.Core.Input;

/// <summary>Resolved axes and button values before contextual action selection.</summary>
internal readonly record struct ControllerReading(
	float LeftX, float LeftY, float RightX, float RightY,
	float LeftTrigger, float RightTrigger, ControllerButtons Buttons )
{
	internal bool Down( ControllerButtons button ) => (Buttons & button) != 0;
}
