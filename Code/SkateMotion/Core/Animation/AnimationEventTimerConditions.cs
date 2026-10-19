namespace SkateMotion.Core.Animation;

/// <summary>Connects compiled guards and reset lifecycles to the shared event clocks.</summary>
internal sealed class AnimationEventTimerConditions
{
	private readonly AnimationOwnerEventTimers _timers;

	internal AnimationEventTimerConditions( AnimationOwnerEventTimers timers ) =>
		_timers = timers ?? throw new ArgumentNullException( nameof( timers ) );

	internal Func<int, bool, bool> BindKickturn( ScalarConditionComparison comparison ) =>
		( _, _ ) => comparison.Evaluate( _timers.SinceKickturn );

	internal CompiledStateBehaviorBinding BindKickturnReset() => new(
		() => -1, _ => _timers.ResetKickturn(), _ => _timers.ResetKickturn(), () => { } );
}
