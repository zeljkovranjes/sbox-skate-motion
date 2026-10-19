namespace SkateMotion.Core.Animation;

// Motion and action IDs belong to the caller's explicit binding tables, not object addresses.
internal readonly record struct AnimationPlayBehaviorDefinition(
	AnimationPlayTransition Transition,
	int DefaultMotion, int AlternateStanceMotion, int MirroredMotion, int OffBoardMotion,
	float Rate, uint ControlFlags, int EntryAction );
