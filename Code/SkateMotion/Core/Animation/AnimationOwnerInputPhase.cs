namespace SkateMotion.Core.Animation;

/// <summary>Advances the effective base and overlay tree before publishing physical input attributes.</summary>
internal sealed class AnimationOwnerInputPhase
{
	private readonly AnimationBasePoseTreeOwner _baseOwner;
	private readonly AnimationOverlayOwner _overlay;
	private readonly AnimationEvaluatedAttributePublisher _attributes;
	private readonly Action _physicalInput;
	private readonly Action<float> _actorContinuation;

	internal AnimationOwnerInputPhase( AnimationBasePoseTreeOwner baseOwner, AnimationOverlayOwner overlay,
		AnimationEvaluatedAttributePublisher attributes, Action physicalInput, Action<float> actorContinuation )
	{
		ArgumentNullException.ThrowIfNull( baseOwner );
		ArgumentNullException.ThrowIfNull( overlay );
		ArgumentNullException.ThrowIfNull( attributes );
		ArgumentNullException.ThrowIfNull( physicalInput );
		ArgumentNullException.ThrowIfNull( actorContinuation );
		_baseOwner = baseOwner;
		_overlay = overlay;
		_attributes = attributes;
		_physicalInput = physicalInput;
		_actorContinuation = actorContinuation;
	}

	/// <summary>Runs after pose preparation, retaining completion and the cache for the next graph update.</summary>
	internal void Update( float delta, float phase )
	{
		RequirePublishedBase();
		_overlay.PromoteCompleted();
		_overlay.RemoveFinished();
		_baseOwner.CleanupCompleted();
		RequirePublishedBase();
		_baseOwner.AdvancePreparedFrame( delta, phase, _overlay.Root );
		_attributes.Publish( _overlay.Root );
		_physicalInput();
		_actorContinuation( delta );
	}

	private void RequirePublishedBase()
	{
		if ( !ReferenceEquals( _baseOwner.Root, _overlay.BaseRoot ) )
			throw new InvalidOperationException( "The base owner must publish root changes to this overlay owner." );
	}
}
