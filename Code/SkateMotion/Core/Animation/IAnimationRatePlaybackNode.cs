namespace SkateMotion.Core.Animation;

/// <summary>A playback clock whose authored frame rate can be scaled without changing phase.</summary>
internal interface IAnimationRatePlaybackNode : IAnimationPlaybackNode
{
	void SetRate( float rate );
}
