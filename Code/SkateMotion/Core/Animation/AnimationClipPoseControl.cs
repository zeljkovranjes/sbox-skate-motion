namespace SkateMotion.Core.Animation;

/// <summary>Prepares authored clip time ranges and consumes playback history in source order.</summary>
internal sealed class AnimationClipPoseControl
{
	private readonly AnimationClipClock _clock;
	private readonly IAnimationClipPoseBackend _backend;

	internal AnimationClipPoseControl( AnimationClipClock clock, IAnimationClipPoseBackend backend )
	{
		_clock = clock;
		_backend = backend;
	}

	internal bool HasPose { get; private set; }

	internal bool Prepare( bool enabled, bool consumeHistory, uint flags )
	{
		var maximum = _clock.AuthoredDuration;
		var rate = _clock.EffectiveRate;
		var previous = ClampTime( rate * _clock.PreviousPosition, maximum );
		var current = ClampTime( rate * _clock.Elapsed, maximum );
		var start = previous;
		if ( _clock.WrapCount == 0 )
			start = previous - current >= 0f ? current : previous;

		if ( enabled && !_backend.Suppressed )
		{
			var clip = _backend.ResolveClip();
			_backend.Sample( clip, start, current, _clock.WrapCount, flags );
		}
		if ( consumeHistory )
			_clock.ConsumePoseHistory();
		if ( !enabled )
		{
			HasPose = false;
			return false;
		}

		// These are separate backend observations in the preparation contract.
		HasPose = !_backend.Suppressed;
		return !_backend.Suppressed;
	}

	private static float ClampTime( float time, float maximum )
	{
		var lower = -time >= 0f ? 0f : time;
		return maximum - lower >= 0f ? lower : maximum;
	}
}
