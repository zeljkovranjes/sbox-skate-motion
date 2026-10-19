namespace SkateMotion.Core.Animation;

/// <summary>Combines prepared transition children and queues sampling with a separately captured fade weight.</summary>
internal sealed class AnimationTransitionPoseControl
{
	private readonly AnimationTransitionPlayback _playback;
	private readonly IAnimationLayerPoseOperations _operations;
	private int _blendMode;

	internal AnimationTransitionPoseControl( AnimationTransitionPlayback playback,
		IAnimationLayerPoseOperations operations, int blendMode )
	{
		_playback = playback;
		_operations = operations;
		_blendMode = blendMode;
	}

	internal bool HasPose { get; private set; }

	internal void SetBlendMode( int blendMode ) => _blendMode = blendMode;

	internal bool Prepare( bool enabled )
	{
		var weight = _playback.Weight;
		var previousReady = _operations.Prepare( 0, enabled );
		var nextReady = _operations.Prepare( 1, enabled );
		if ( enabled && previousReady && nextReady )
		{
			if ( _blendMode < 0 )
				_operations.BlendWeighted( weight, (_blendMode & 0x40000000) != 0 );
			else
				_operations.Blend( weight );
		}
		HasPose = previousReady || nextReady;
		return HasPose;
	}

	internal void Sample()
	{
		var weight = _playback.Weight;
		if ( weight < 1f )
		{
			_operations.Sample( 0 );
			if ( weight > 0f )
			{
				_operations.Sample( 1 );
				_operations.QueueBlend( weight );
			}
		}
		else
		{
			_operations.Sample( 1 );
		}
	}
}
