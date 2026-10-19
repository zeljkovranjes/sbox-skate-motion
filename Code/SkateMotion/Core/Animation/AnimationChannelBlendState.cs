namespace SkateMotion.Core.Animation;

/// <summary>Retained weights, fade clocks and playback flags for one animation layer.</summary>
internal readonly record struct AnimationChannelBlendState(
	float Weight,
	float TargetWeight,
	float BlendInElapsed,
	float BlendInDuration,
	float BlendOutElapsed,
	float BlendOutDuration,
	float RecoveryElapsed,
	float RecoveryDuration,
	uint Flags );
