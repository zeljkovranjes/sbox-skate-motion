namespace SkateMotion;

/// <summary>Normalized controls; steer and flick axes range from -1 to 1, push and brake from 0 to 1.
/// Hold Pump through a transition to extend the legs and gain speed. Gesture only changes the rider's animation; Lean
/// (positive toward the rider's toes) while grabbing in the air flips rider and board over in a front or back flip.</summary>
public readonly record struct SkateInput(
	float Steer = 0f, float Push = 0f, float Brake = 0f,
	float FlickX = 0f, float FlickY = 0f, float Lean = 0f,
	bool Slide = false, bool Goofy = false, bool Pump = false, SkateAirGrab Grab = SkateAirGrab.None,
	bool Gesture = false )
{
	/// <summary>Explicit physical push feet; None preserves stance-based legacy input.</summary>
	public SkatePushFeet PushFeet { get; init; }

	/// <summary>Whether the explicit foot request is inside its new-push window.</summary>
	public bool NewPush { get; init; }

	/// <summary>Hold to crouch for a hippy jump; letting go jumps the rider off the board while it rolls on beneath.</summary>
	public bool HippyJump { get; init; }

	/// <summary>The catch button. In the air with a grab it takes the feet off the board (a christ air, superman or
	/// no-foot air); during a flip it catches the board dark side up. It changes the rider's animation only.</summary>
	public bool Catch { get; init; }

	/// <summary>Feet taken off the board in the air, named as the push buttons are (left push, right push): one-foot
	/// airs. It changes the rider's animation only.</summary>
	public SkatePushFeet FeetOff { get; init; }

	/// <summary>The hand plant button. Off the lip of a steep ramp it plants a hand on the lip: the rider stalls upside
	/// down over it, the board held up, while it is held, and drops back in when it is let go.</summary>
	public bool HandPlant { get; init; }
}
