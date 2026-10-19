namespace SkateMotion.Core.Animation;

/// <summary>A prepared playback tree whose registered nodes share one lifetime.</summary>
internal interface IAnimationPlaybackTree
{
	IAnimationPlaybackNode Root { get; }
}
