namespace SkateMotion.Core.Input;

internal readonly record struct ControllerActionTuning(
	float ActivityThreshold,
	float ManualBrakeThreshold,
	float ManualBrakeScale,
	RidingButtonTuning Buttons,
	RidingAnalogTuning Analog );
