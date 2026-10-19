using Sandbox;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

internal static class LocalControllerInput
{
	internal static ControllerReading Read()
	{
		if ( !Input.UsingController || Input.Suppressed || Input.ControllerCount == 0 )
		{
			return default;
		}

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
