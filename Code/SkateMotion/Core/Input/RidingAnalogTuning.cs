namespace SkateMotion.Core.Input;

/// <summary>Caller-supplied angular response limits and scales.</summary>
internal readonly record struct RidingAnalogTuning(
	float TurnFullAngle, float TurnZeroAngle, float TurnScale, float FullStickThreshold,
	float HardTurnMinAngle, float HardTurnMaxAngle, float HardTurnScale, float HardTurnMinimum,
	float HardTurnCrouchScale, float CrouchAngle, float SlideOuterAngle, float SlideOverlapAngle,
	float SlideScale, float Pi );
