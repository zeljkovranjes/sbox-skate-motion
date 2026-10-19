namespace SkateMotion.Core.Animation;

/// <summary>Retains actor selection, posture flags and the next playback transition.</summary>
internal sealed class AnimationPlayActorState
{
	internal AnimationPlayActorState( AnimationAttributeKey characterSelection, int alternateStanceValue,
		ulong flags, AnimationPlayTransition transitionOverride )
	{
		CharacterSelection = characterSelection;
		AlternateStanceValue = alternateStanceValue;
		Flags = flags;
		TransitionOverride = transitionOverride;
	}

	internal AnimationAttributeKey CharacterSelection { get; set; }
	internal int AlternateStanceValue { get; set; }
	internal ulong Flags { get; set; }
	internal AnimationPlayTransition TransitionOverride { get; private set; }

	internal void SetTransitionOverride( AnimationPlayTransition transition ) => TransitionOverride = transition;

	internal void ConsumeTransitionOverride()
	{
		// Only the high channel flag bit is meaningful; unspecified padding is canonicalized.
		TransitionOverride = new( 0, .2f, false, AnimationTransitionMode.Both, 0 );
	}
}
