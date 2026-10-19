namespace SkateMotion.Core.Animation;

/// <summary>Connects base-owner clock advancement and cache publication to the physical input phase.</summary>
internal sealed class AnimationBaseOwnerInputPhase
{
	private readonly AnimationBasePoseTreeOwner _owner;
	private readonly AnimationEvaluatedAttributePublisher _attributes;
	private readonly Action _physicalInput;
	private readonly Action<float> _actorContinuation;

	internal AnimationBaseOwnerInputPhase( AnimationBasePoseTreeOwner owner,
		AnimationEvaluatedAttributePublisher attributes, Action physicalInput, Action<float> actorContinuation )
	{
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( physicalInput );
		ArgumentNullException.ThrowIfNull( actorContinuation );
		_owner = owner;
		_attributes = attributes;
		_physicalInput = physicalInput;
		_actorContinuation = actorContinuation;
	}

	/// <summary>Runs after pose preparation; the graph has already consumed the preceding frame's cache.</summary>
	internal void Update( float delta, float phase )
	{
		_owner.AdvanceFrame( delta, phase );
		_attributes.Publish( _owner.Root );
		_physicalInput();
		_actorContinuation( delta );
	}
}
