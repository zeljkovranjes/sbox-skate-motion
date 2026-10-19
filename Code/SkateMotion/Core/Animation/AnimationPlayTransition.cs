namespace SkateMotion.Core.Animation;

// A zero kind leaves the authored transition intact when supplied as an override.
internal readonly record struct AnimationPlayTransition(
	int Kind, float Duration, bool Under, AnimationTransitionMode Synchronization, uint ChannelFlags );
