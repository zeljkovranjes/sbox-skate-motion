namespace SkateMotion.Core.Animation;

/// <summary>Selects immediate replacement, concurrent transition or completion-based sequencing.</summary>
internal enum AnimationBaseTransition
{
	Play,
	Blend,
	Sequence,
	ChannelBlend
}
