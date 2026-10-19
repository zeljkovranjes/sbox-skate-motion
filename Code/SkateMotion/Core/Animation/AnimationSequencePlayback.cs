namespace SkateMotion.Core.Animation;

/// <summary>Waits for one child to finish, then forwards its unused update time to the successor.</summary>
internal sealed class AnimationSequencePlayback : IAnimationPlaybackNode
{
	private IAnimationPlaybackNode _previous;
	private IAnimationPlaybackNode _next;

	internal bool ActiveNext { get; private set; }

	internal AnimationSequencePlayback( IAnimationPlaybackNode previous, IAnimationPlaybackNode next )
	{
		_previous = previous;
		_next = next;
	}

	internal void Reset( IAnimationPlaybackNode previous, IAnimationPlaybackNode next )
	{
		_previous = previous;
		_next = next;
		ActiveNext = false;
		// Initialization observes both child clocks, even though it retains neither value.
		_ = _previous.Duration;
		_ = _next.Elapsed;
	}

	/// <summary>The queued successor's duration, including while the previous child still plays.</summary>
	public float Duration => _next.Duration;

	/// <summary>The queued successor's clock, including while the previous child still plays.</summary>
	public float Elapsed => _next.Elapsed;

	/// <summary>Advances the selected child and preserves caller output fields not produced by this update.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
	{
		if ( ActiveNext )
		{
			_next.Advance( delta, phase, ref completion );
			return;
		}
		var previousCompletion = AnimationPlaybackCompletion.Empty;
		_previous.Advance( delta, phase, ref previousCompletion );
		if ( previousCompletion.Finished )
		{
			ActiveNext = true;
			_next.Advance( previousCompletion.UnusedDelta, phase, ref completion );
			return;
		}
		completion = completion with
		{
			Finished = false,
			Remaining = previousCompletion.Remaining + _next.Duration
		};
	}

	/// <summary>Positions the successor's clock.</summary>
	public void Seek( float time ) => _next.Seek( time );
}
