namespace SkateMotion.Core.Input;

/// <summary>Caller-supplied hold windows and automatic-push direction limits.</summary>
internal readonly record struct RidingButtonTuning(
	float PushWindow, float MountWindow, float AutoPushAngle, float AutoPushMagnitude, float Pi );
