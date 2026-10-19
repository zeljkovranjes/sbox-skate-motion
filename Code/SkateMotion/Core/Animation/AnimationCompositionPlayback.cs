namespace SkateMotion.Core.Animation;

/// <summary>Uses one child's playback while forwarding seek and rate to both in storage order.</summary>
internal sealed class AnimationCompositionPlayback : IAnimationRatePlaybackNode
{
	private readonly IAnimationRatePlaybackNode _first;
	private readonly IAnimationRatePlaybackNode _second;
	private readonly IAnimationRatePlaybackNode _active;

	internal AnimationCompositionPlayback( IAnimationRatePlaybackNode first,
		IAnimationRatePlaybackNode second, bool firstActive )
	{
		_first = first;
		_second = second;
		_active = firstActive ? first : second;
	}

	/// <summary>The active child's duration.</summary>
	public float Duration => _active.Duration;
	/// <summary>The active child's elapsed time.</summary>
	public float Elapsed => _active.Elapsed;

	/// <summary>Advances only the active child, preserving its completion output.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion ) =>
		_active.Advance( delta, phase, ref completion );

	/// <summary>Seeks first then second, regardless of the active child.</summary>
	public void Seek( float time )
	{
		_first.Seek( time );
		_second.Seek( time );
	}

	/// <summary>Sets first then second's rate, regardless of the active child.</summary>
	public void SetRate( float rate )
	{
		_first.SetRate( rate );
		_second.SetRate( rate );
	}
}
