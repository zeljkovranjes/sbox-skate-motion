namespace SkateMotion.Core.Input;

/// <summary>Mode-gated body axes, recovery, push-off and deliberate bail requests.</summary>
internal readonly record struct BailInputActions(
	float GestureX, float GestureY, float ControlX, float ControlY, bool Recover, bool PushOff, bool Request )
{
	internal static BailInputActions Evaluate( ControllerReading previous, ControllerReading current,
		bool gestureEnabled, bool controlEnabled, bool pushOffEnabled, bool requestSuppressed )
	{
		var pressed = current.Buttons & ~previous.Buttons;
		var leftTriggerPressed = previous.LeftTrigger != 1f && current.LeftTrigger == 1f;
		var rightTriggerPressed = previous.RightTrigger != 1f && current.RightTrigger == 1f;
		const ControllerButtons sticks = ControllerButtons.LeftStick | ControllerButtons.RightStick;
		var chord = current.LeftTrigger == 1f && current.RightTrigger == 1f
			&& (current.Buttons & sticks) == sticks;
		var chordEdge = leftTriggerPressed || rightTriggerPressed || (pressed & sticks) != 0;
		return new(
			gestureEnabled ? current.RightX : 0f, gestureEnabled ? current.RightY : 0f,
			controlEnabled ? current.LeftX : 0f, controlEnabled ? current.LeftY : 0f,
			(pressed & (ControllerButtons.South | ControllerButtons.West)) != 0,
			pushOffEnabled && rightTriggerPressed, !requestSuppressed && chord && chordEdge );
	}
}
