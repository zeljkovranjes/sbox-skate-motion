namespace SkateMotion.Core.Input;

/// <summary>Directional gesture edges and holds after bumper suppression.</summary>
internal readonly record struct GestureInputActions( ControllerButtons Started, ControllerButtons Held )
{
	internal static GestureInputActions Evaluate( ControllerReading previous, ControllerReading current )
	{
		if ( current.Down( ControllerButtons.LeftBumper | ControllerButtons.RightBumper ) )
			return default;
		const ControllerButtons directions = ControllerButtons.Up | ControllerButtons.Down
			| ControllerButtons.Left | ControllerButtons.Right;
		var held = current.Buttons & directions;
		return new( held & ~previous.Buttons, held );
	}
}
