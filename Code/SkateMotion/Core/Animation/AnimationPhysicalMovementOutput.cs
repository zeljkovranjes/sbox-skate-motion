namespace SkateMotion.Core.Animation;

/// <summary>Published movement state and selected physical contact metadata.</summary>
internal readonly record struct AnimationPhysicalMovementOutput(
	int Current, int Previous, float RetainedValue,
	bool ContactActive, AnimationMovementContactData Contact );
