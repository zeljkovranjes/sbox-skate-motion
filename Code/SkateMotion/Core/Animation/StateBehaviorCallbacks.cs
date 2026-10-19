namespace SkateMotion.Core.Animation;

// Creation transfers one owned reference; -1 represents an absent instance.
// Entry and exit use a copied owner context with the supplied instance selected.
internal readonly record struct StateBehaviorCallbacks(
	Func<int, int> Create,
	Action<int> Retain,
	Action<int> Release,
	Action<int, int> Enter,
	Action<int, int> Exit,
	Action<int> Transition );
