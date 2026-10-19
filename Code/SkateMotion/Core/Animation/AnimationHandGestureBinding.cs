namespace SkateMotion.Core.Animation;

/// <summary>Shares hand occupancy and ordered gesture-channel stops with the character gesture owner.</summary>
internal sealed class AnimationHandGestureBinding
{
	private readonly AnimationGestureActorState _state;
	private readonly AnimationOverlayOwner _overlays;
	private readonly AnimationCharacterGestureDefinition _definition;

	internal AnimationHandGestureBinding( AnimationGestureActorState state,
		AnimationOverlayOwner overlays, AnimationCharacterGestureDefinition definition )
	{
		_state = state ?? throw new ArgumentNullException( nameof( state ) );
		_overlays = overlays;
		_definition = definition ?? throw new ArgumentNullException( nameof( definition ) );
	}

	internal CompiledStateBehaviorBinding BindHandBusy( int side ) => new(
		() => -1, _ => _state.AdjustHand( side, 1 ), _ => _state.AdjustHand( side, -1 ), () => { } );

	internal CompiledStateBehaviorBinding BindEndGesture( int side ) => new(
		() => -1, _ => EndGesture( side ), _ => { }, () => { } );

	private void EndGesture( int side )
	{
		if ( _overlays is null || (side != 0 && side != 1) )
			return;
		_overlays.Stop( _definition.Channel( side == 0 ? 2 : 1 ) );
		_overlays.Stop( _definition.Channel( 0 ) );
	}
}
