namespace SkateMotion.Core.Animation;

// Creation transfers an instance reference. The binding retains its definition arguments;
// entry and exit receive the selected instance, while transition actions have no instance.
internal readonly record struct CompiledStateBehaviorBinding(
	Func<int> Create,
	Action<int> Enter,
	Action<int> Exit,
	Action Transition );
