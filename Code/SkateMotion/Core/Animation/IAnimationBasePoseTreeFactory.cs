namespace SkateMotion.Core.Animation;

/// <summary>Rebuilds one retained tree storage slot and publishes changes to the base pose root.</summary>
internal interface IAnimationBasePoseTreeFactory
{
	IAnimationPlaybackTree Create( int storageSlot, int definition );
	void Publish( IAnimationPosePlaybackNode root );
}
