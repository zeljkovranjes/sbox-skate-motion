namespace SkateMotion.Core.Input;

/// <summary>
/// One device's board controls for a frame, as merged by <see cref="DeviceInputMerge"/>: the same controls the board
/// takes, with the grab and the foot sets as plain numbers (0 for none).
/// </summary>
internal readonly record struct BoardControls( float Steer, float Push, float Brake, float FlickX, float FlickY,
	float Lean, bool Slide, bool Pump, int Grab, bool Gesture, bool HippyJump, bool Catch, int FeetOff, int PushFeet,
	bool NewPush )
{
	/// <summary>The hand plant button.</summary>
	internal bool HandPlant { get; init; }
}
