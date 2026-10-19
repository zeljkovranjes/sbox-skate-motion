namespace SkateMotion.Core.Animation;

/// <summary>Advances or synchronizes child clocks while retaining a separate transition fade clock.</summary>
internal sealed class AnimationTransitionPlayback : IAnimationPlaybackNode
{
	private IAnimationPlaybackNode _previous;
	private IAnimationPlaybackNode _next;
	private float _blendDuration;
	private AnimationTransitionMode _mode;

	internal float BlendElapsed { get; private set; }
	internal bool CanReleasePrevious => _blendDuration < BlendElapsed;

	internal AnimationTransitionPlayback( IAnimationPlaybackNode previous, IAnimationPlaybackNode next,
		float blendDuration, AnimationTransitionMode mode )
	{
		_previous = previous;
		_next = next;
		_blendDuration = blendDuration;
		_mode = mode;
	}

	internal void Reset( IAnimationPlaybackNode previous, IAnimationPlaybackNode next,
		float blendDuration, AnimationTransitionMode mode )
	{
		_previous = previous;
		_next = next;
		_blendDuration = blendDuration;
		_mode = mode;
		BlendElapsed = 0f;
	}

	/// <summary>The successor's duration.</summary>
	public float Duration => _next.Duration;

	/// <summary>The successor's elapsed time, independent of the transition fade clock.</summary>
	public float Elapsed => _next.Elapsed;

	internal float Weight
	{
		get
		{
			var ratio = BlendElapsed / _blendDuration;
			var lower = -ratio >= 0f ? 0f : ratio;
			var weight = 1f - lower >= 0f ? lower : 1f;
			if ( _blendDuration >= 0.05f && weight <= 1f && weight >= 0f )
			{
				var square = weight * weight;
				var cubic = weight * (square * -2f);
				weight = MathF.FusedMultiplyAdd( square, 3f, cubic );
			}
			return weight;
		}
	}

	/// <summary>Advances children in order or synchronizes the successor after the previous child advances.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
	{
		if ( _mode != AnimationTransitionMode.NextOnly )
		{
			var previousCompletion = AnimationPlaybackCompletion.Empty;
			_previous.Advance( delta, phase, ref previousCompletion );
			if ( _mode == AnimationTransitionMode.MatchTime )
			{
				var previousTime = _previous.Elapsed;
				var nextDuration = _next.Duration;
				_next.Seek( previousTime - nextDuration >= 0f ? nextDuration : previousTime );
				BlendElapsed += delta;
				return;
			}
			if ( _mode == AnimationTransitionMode.MatchPhase && _previous.Duration > 0f )
			{
				// Read the duration again after elapsed, preserving the child query order.
				var previousPhase = _previous.Elapsed / _previous.Duration;
				_next.Seek( previousPhase * _next.Duration );
				BlendElapsed += delta;
				return;
			}
		}
		_next.Advance( delta, phase, ref completion );
		BlendElapsed += delta;
	}

	/// <summary>Positions the successor's clock without changing the transition fade clock.</summary>
	public void Seek( float time ) => _next.Seek( time );

	internal void RestartBlend() => BlendElapsed = 0f;
}
