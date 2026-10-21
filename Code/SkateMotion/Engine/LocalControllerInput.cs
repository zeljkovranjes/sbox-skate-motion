using Sandbox;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

internal static class LocalControllerInput
{
	/// <summary>True while a connected controller is the last device used: for prompts and glyphs only. The rider reads
	/// both devices every frame and merges them (DeviceInputMerge), since a drifting stick or a resting trigger keeps
	/// the engine's flag on a controller while keys are held.</summary>
	internal static bool InUse => Input.UsingController && Input.ControllerCount > 0;

	/// <summary>
	/// The first controller's sticks, triggers and buttons as it reports them, read in its own input scope so whatever
	/// device the engine counts as last; nothing without a controller.
	/// </summary>
	internal static ControllerReading Read()
	{
		if ( Input.Suppressed || Input.ControllerCount == 0 )
		{
			return default;
		}

		using var scope = Input.PlayerScope( Input.PlayerIndex.Controller1 );

		var buttons = ControllerButtons.None;
		Add( ref buttons, ControllerButtons.LeftStick, "SkatePadLeftStick" );
		Add( ref buttons, ControllerButtons.RightStick, "SkatePadRightStick" );
		Add( ref buttons, ControllerButtons.LeftBumper, "SkatePadLeftBumper" );
		Add( ref buttons, ControllerButtons.RightBumper, "SkatePadRightBumper" );
		Add( ref buttons, ControllerButtons.Up, "SkatePadUp" );
		Add( ref buttons, ControllerButtons.Down, "SkatePadDown" );
		Add( ref buttons, ControllerButtons.Left, "SkatePadLeft" );
		Add( ref buttons, ControllerButtons.Right, "SkatePadRight" );
		Add( ref buttons, ControllerButtons.West, "SkatePadWest" );
		Add( ref buttons, ControllerButtons.North, "SkatePadNorth" );
		Add( ref buttons, ControllerButtons.South, "SkatePadSouth" );
		Add( ref buttons, ControllerButtons.East, "SkatePadEast" );

		// Domain readings use right-positive X and up-positive Y.
		return new(
			Input.GetAnalog( InputAnalog.LeftStickX ),
			-Input.GetAnalog( InputAnalog.LeftStickY ),
			Input.GetAnalog( InputAnalog.RightStickX ),
			-Input.GetAnalog( InputAnalog.RightStickY ),
			Input.GetAnalog( InputAnalog.LeftTrigger ),
			Input.GetAnalog( InputAnalog.RightTrigger ), buttons );
	}

	private static void Add( ref ControllerButtons buttons, ControllerButtons button, string action )
	{
		if ( Input.Down( action ) )
		{
			buttons |= button;
		}
	}
}
