namespace SkateMotion.Core.Animation;

/// <summary>Tree construction and envelope settings for a queued or blended overlay replacement.</summary>
internal readonly record struct AnimationChannelReplacement(
	int Definition,
	float Rate,
	bool UsePosture,
	bool Transition,
	float BlendDuration,
	float RecoveryDuration,
	float ExitDuration,
	uint Flags,
	bool AllowRecovery );
