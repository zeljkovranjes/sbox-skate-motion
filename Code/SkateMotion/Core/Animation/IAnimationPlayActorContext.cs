namespace SkateMotion.Core.Animation;

/// <summary>Supplies the actor-owned selectors, transition override and mode used on playback entry.</summary>
internal interface IAnimationPlayActorContext
{
	void RefreshCharacterSelection();
	AnimationPlayTransition ReadTransitionOverride();
	void ConsumeTransitionOverride();
	bool IsAlternateStance();
	bool IsMirrored();
	bool UseOffBoardVariant();
	void SetActorMode( byte mode );
}
