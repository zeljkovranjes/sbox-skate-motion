namespace SkateMotion.Core.Input;

/// <summary>Keeps held dismount input separate from its activation edge.</summary>
internal readonly record struct DismountInputActions( bool Held, bool Started )
{
	internal static DismountInputActions Evaluate( ControllerReading previous, ControllerReading current )
	{
		var held = current.Down( ControllerButtons.East );
		return new( held, held && !previous.Down( ControllerButtons.East ) );
	}
}
