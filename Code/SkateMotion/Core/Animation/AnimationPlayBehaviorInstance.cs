namespace SkateMotion.Core.Animation;

/// <summary>Retains the entry flag that suppresses one scheduled action update.</summary>
internal sealed class AnimationPlayBehaviorInstance
{
	internal bool SuppressNextUpdate { get; private set; } = true;

	internal void Enter() => SuppressNextUpdate = true;

	internal void Update( ReadOnlySpan<AnimationEntryAction> actions, IAnimationEntryActionContext context )
	{
		if ( !SuppressNextUpdate )
			AnimationEntryActionProcessor.Execute( actions, false, context );
		SuppressNextUpdate = false;
	}

	internal void Exit()
	{
		// Exit preserves this flag; retained reference ownership belongs to the graph lifecycle.
	}
}
