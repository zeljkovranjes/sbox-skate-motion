namespace SkateMotion.Core.Animation;

/// <summary>Creates a rated and wrapped tree, then commits or releases its storage in owner order.</summary>
internal interface IAnimationChannelTreeFactory
{
	IAnimationPlaybackTree Create( AnimationChannelReplacement request );
	void Commit( IAnimationPlaybackTree tree );
	void Release( IAnimationPlaybackTree tree );
}
