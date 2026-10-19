namespace SkateMotion.Core.Input;

/// <summary>Handplant axes and independent one-foot or dismount requests.</summary>
internal readonly record struct HandplantInputActions(
	float TweakX, float TweakY, bool Dismount, bool OneFootLeft, bool OneFootRight )
{
	internal static HandplantInputActions Evaluate( ControllerReading current ) => new(
		current.RightX, current.RightY, current.Down( ControllerButtons.East ),
		current.Down( ControllerButtons.West ), current.Down( ControllerButtons.South ) );
}
