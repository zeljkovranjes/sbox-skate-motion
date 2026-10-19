namespace SkateMotion.Core.Animation;

internal readonly record struct StateSelectionRecord(
	int Parent, int Guard, int FirstChild, int ChildCount,
	int FirstTransition, int TransitionCount, bool Active,
	StateSelectionMode Mode, int RetainedAncestor = -1 );
