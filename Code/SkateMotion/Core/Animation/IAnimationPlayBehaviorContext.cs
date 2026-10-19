namespace SkateMotion.Core.Animation;

/// <summary>Binds entry side effects and queries to the existing actor and base tree owner.</summary>
internal interface IAnimationPlayBehaviorContext
{
	void MarkEntered( int instance );
	void ExecuteEntryAction( int action, int instance );
	void RefreshCharacterSelection();
	AnimationPlayTransition ReadTransitionOverride();
	void ConsumeTransitionOverride();
	bool IsAlternateStance();
	bool IsMirrored();
	bool UseOffBoardVariant();
	void SetActorMode( byte mode );
	bool TryReplace( AnimationBasePoseRequest request );
}
