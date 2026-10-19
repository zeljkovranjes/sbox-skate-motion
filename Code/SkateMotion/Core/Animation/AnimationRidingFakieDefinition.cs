namespace SkateMotion.Core.Animation;

internal readonly record struct AnimationRidingFakieDefinition(
	float HighSpeedThreshold,
	float LowSpeedThreshold,
	float ConfirmationTimeThreshold,
	float TeleportTimeThreshold );
