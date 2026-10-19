namespace SkateMotion.Core.Animation;

/// <summary>Receives one ordered parameter pass from an animation channel.</summary>
internal interface IAnimationAttributeOverrideConsumer
{
	/// <summary>Applies the supplied parameters and reports whether playback selection changed.</summary>
	bool UpdateOverrides( ReadOnlySpan<AnimationAttributeOverride> attributes );
}
