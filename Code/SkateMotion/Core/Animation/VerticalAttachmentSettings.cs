namespace SkateMotion.Core.Animation;

/// <summary>Authored limits and response parameters for the vertical attachment controller.</summary>
internal readonly record struct VerticalAttachmentSettings(
	float MinimumHeight, float MaximumSpeed,
	float TransitionDamping, float TransitionSpring, float PrimaryStopDelay,
	float PrimaryDamping, float PrimarySpring, float TrackingFollowDelay,
	float TrackingDamping, float TrackingSpring, float TrackingStep, float RestHeight,
	float ImpulseDuration, float ImpulseSpeedScale, float ImpulseReleaseUpdates, float ImpulseHeight );
