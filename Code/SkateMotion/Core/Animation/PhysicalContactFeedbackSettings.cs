namespace SkateMotion.Core.Animation;

/// <summary>Authored contact duration, mass gate, height gate and object-speed scales.</summary>
internal readonly record struct PhysicalContactFeedbackSettings(
	float Duration, float LightMassLimit, float MinimumHeight, float MaximumAlignment,
	float ObjectSpeedScale, float AlternateObjectSpeedScale );
