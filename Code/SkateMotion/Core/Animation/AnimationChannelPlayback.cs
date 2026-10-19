namespace SkateMotion.Core.Animation;

/// <summary>Advances the base clock and envelope while retaining separate overlay completion.</summary>
internal sealed class AnimationChannelPlayback : IAnimationPlaybackNode
{
	private readonly IAnimationPlaybackNode _base;
	private readonly IAnimationPlaybackNode _overlay;
	private readonly AnimationChannelBlend _blend;

	internal AnimationChannelPlayback( IAnimationPlaybackNode baseNode,
		IAnimationPlaybackNode overlay, AnimationChannelBlend blend )
	{
		_base = baseNode;
		_overlay = overlay;
		_blend = blend;
	}

	/// <summary>The underlying base duration, independent of the overlay.</summary>
	public float Duration => _base.Duration;

	/// <summary>The underlying base clock, independent of the overlay.</summary>
	public float Elapsed => _base.Elapsed;

	/// <summary>Preserves the base completion output and uses a separate result for overlay completion.</summary>
	public void Advance( float delta, float phase, ref AnimationPlaybackCompletion completion )
	{
		_base.Advance( delta, phase, ref completion );
		if ( !_blend.Update( delta, _overlay ) )
			return;

		var overlayCompletion = AnimationPlaybackCompletion.Empty;
		_overlay.Advance( delta, phase, ref overlayCompletion );
		_blend.CompleteOverlayUpdate( overlayCompletion.Finished );
	}

	/// <summary>Seeks the underlying base without resetting the overlay or envelope.</summary>
	public void Seek( float time ) => _base.Seek( time );
}
