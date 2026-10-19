namespace SkateMotion.Core.Animation;

/// <summary>Indexed selection, lifecycle and dispatch tables sharing one state identity space.</summary>
// TransitionActions uses the same explicit binding IDs as Behaviors. An importer
// with a separate action namespace must append ownerless definitions and remap it.
internal sealed record CompiledStateGraphDefinition(
	StateSelectionRecord[] States,
	int[] TransitionIds,
	TransitionSelectionRecord[] Transitions,
	int[][] TransitionActions,
	StateBehaviorRange[] StateBehaviors,
	BehaviorLifecycleDefinition[] Behaviors,
	int[][] Operations );
