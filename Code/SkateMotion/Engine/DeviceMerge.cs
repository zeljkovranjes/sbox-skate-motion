using Sandbox;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>
/// Engine side of <see cref="DeviceInputMerge"/>: the keyboard's share of the movement input, and board input from both
/// devices merged into one.
/// </summary>
internal static class DeviceMerge
{
	// A movement key held reads a full unit on its axis; a share this big counts as held.
	private const float KeyHeld = 0.5f;

	/// <summary>
	/// The movement keys' input alone: the engine adds the first controller's left stick into the movement input, so
	/// its share (from the raw reading) is taken back out.
	/// </summary>
	internal static Vector3 KeyboardMove( ControllerReading raw ) =>
		(Input.AnalogMove - new Vector3( raw.LeftY, -raw.LeftX, 0f )).WithZ( 0f );

	/// <summary>True while a steering key is held.</summary>
	internal static bool SteeringKey( Vector3 move ) => MathF.Abs( move.y ) > KeyHeld;

	/// <summary>True while any movement key is held.</summary>
	internal static bool MovementKey( Vector3 move ) => move.Length > KeyHeld;

	/// <summary>Board input from the keyboard and mouse and from the controller, merged control by control.</summary>
	internal static SkateInput Board( SkateInput keys, bool steeringKey, SkateInput pad, DeviceActivity active )
	{
		var merged = DeviceInputMerge.Board( Controls( keys ), steeringKey, Controls( pad ), active );
		return new SkateInput( merged.Steer, merged.Push, merged.Brake, merged.FlickX, merged.FlickY, merged.Lean,
			merged.Slide, keys.Goofy, merged.Pump, (SkateAirGrab)merged.Grab, merged.Gesture )
		{
			HippyJump = merged.HippyJump,
			Catch = merged.Catch,
			FeetOff = (SkatePushFeet)merged.FeetOff,
			PushFeet = (SkatePushFeet)merged.PushFeet,
			NewPush = merged.NewPush,
			HandPlant = merged.HandPlant
		};
	}

	private static BoardControls Controls( SkateInput input ) => new( input.Steer, input.Push, input.Brake, input.FlickX,
		input.FlickY, input.Lean, input.Slide, input.Pump, (int)input.Grab, input.Gesture, input.HippyJump, input.Catch,
		(int)input.FeetOff, (int)input.PushFeet, input.NewPush ) { HandPlant = input.HandPlant };
}
