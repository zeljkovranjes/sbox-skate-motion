namespace SkateMotion.Core.Input;

internal readonly record struct ControllerActionContext
{
	internal bool RetainPreviousBumper { get; init; }
	internal bool SuppressManual { get; init; }
	internal bool BailGestureEnabled { get; init; }
	internal bool BailControlEnabled { get; init; }
	internal bool BailPushOffEnabled { get; init; }
	internal bool SuppressBail { get; init; }
	internal RidingButtonContext Buttons { get; init; }
	internal RidingAnalogContext Analog { get; init; }
	internal WalkingInputContext Walking { get; init; }
	internal QueuedInputContext Queue { get; init; }
}
