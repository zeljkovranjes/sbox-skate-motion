namespace SkateMotion.Core.Animation;

/// <summary>Builds a rated, wrapped overlay in an explicit storage slot and releases its nodes in order.</summary>
internal interface IAnimationOverlayTreeFactory
{
	IAnimationPlaybackTree Create( int storage, AnimationChannelReplacement request );
	void Release( int storage, IAnimationPlaybackTree tree );
}
