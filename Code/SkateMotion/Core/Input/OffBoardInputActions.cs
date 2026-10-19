namespace SkateMotion.Core.Input;

/// <summary>Requests for walking, board handling, looking and airborne body control.</summary>
internal readonly record struct OffBoardInputActions(
	bool Jump, bool DropBoard, bool ThrowBoard, bool RetrieveBoard, bool AirBodyTweak,
	float LookAtX, float LookAtY, float AirBodyTweakX, float AirBodyTweakY )
{
	internal static OffBoardInputActions Evaluate( ControllerReading previous, ControllerReading current )
	{
		var drop = previous.LeftTrigger != 1f && current.LeftTrigger == 1f;
		var throwBoard = previous.RightTrigger != 1f && current.RightTrigger == 1f;
		return new(
			!previous.Down( ControllerButtons.West ) && current.Down( ControllerButtons.West ),
			drop, throwBoard, drop || throwBoard,
			current.Down( ControllerButtons.RightStick | ControllerButtons.RightBumper ),
			current.RightX, current.RightY, current.RightX, current.RightY );
	}
}
