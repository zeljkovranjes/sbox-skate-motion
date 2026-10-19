namespace SkateMotion.Core.Animation;

/// <summary>Prepares a base and weighted overlay, observing channel changes made by child callbacks.</summary>
internal sealed class AnimationChannelPoseControl
{
	private readonly AnimationChannelBlend _blend;
	private readonly IAnimationLayerPoseOperations _operations;

	internal AnimationChannelPoseControl( AnimationChannelBlend blend, IAnimationLayerPoseOperations operations )
	{
		_blend = blend;
		_operations = operations;
	}

	internal bool Prepare( bool enabled )
	{
		var baseReady = _operations.Prepare( 0, enabled );
		var overlayReady = _operations.Prepare( 1, enabled && _blend.State.Weight > 0f );
		// Preparation may update the envelope, so selection and combination observe it separately.
		var weight = _blend.State.Weight;
		if ( enabled && weight > 0f && baseReady && overlayReady )
			_operations.BlendWeighted( weight, false );
		return baseReady || overlayReady;
	}

	internal void Sample()
	{
		_operations.Sample( 0 );
		if ( _blend.State.Weight > 0f )
		{
			_operations.Sample( 1 );
			_operations.QueueBlend( _blend.State.Weight );
		}
	}
}
