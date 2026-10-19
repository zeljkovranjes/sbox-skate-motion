namespace SkateMotion.Core.Animation;

/// <summary>Prepares and samples the child selected by the sequence clock.</summary>
internal sealed class AnimationSequencePoseControl
{
	private readonly AnimationSequencePlayback _playback;
	private readonly IAnimationPoseOperations _operations;

	internal AnimationSequencePoseControl( AnimationSequencePlayback playback, IAnimationPoseOperations operations )
	{
		_playback = playback;
		_operations = operations;
	}

	internal bool Prepare( bool enabled ) => _operations.Prepare( _playback.ActiveNext ? 1 : 0, enabled );

	internal void Sample() => _operations.Sample( _playback.ActiveNext ? 1 : 0 );
}
