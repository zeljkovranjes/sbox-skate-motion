namespace SkateMotion.Core.Animation;

/// <summary>Updates shared event clocks from retained physical classification and the graph clock.</summary>
internal sealed class AnimationEventTimerBehaviorBinding
{
	private readonly AnimationOwnerEventTimers _timers;
	private readonly AnimationPhysicalMovementState _movement;
	private readonly Func<float> _graphDelta;

	internal AnimationEventTimerBehaviorBinding( AnimationOwnerEventTimers timers,
		AnimationPhysicalMovementState movement, Func<float> graphDelta )
	{
		_timers = timers ?? throw new ArgumentNullException( nameof( timers ) );
		_movement = movement ?? throw new ArgumentNullException( nameof( movement ) );
		_graphDelta = graphDelta ?? throw new ArgumentNullException( nameof( graphDelta ) );
	}

	internal CompiledStateBehaviorBinding Bind() => new( () => -1, _ => { }, _ => { }, () => { } );

	internal void UpdateTeleport()
	{
		if ( _movement.Current == 5 )
			_timers.ResetTeleport();
		else
			_timers.AdvanceTeleport( _graphDelta() );
	}

	internal void UpdateKickturn() => _timers.AdvanceKickturn( _graphDelta() );
}
