namespace SkateMotion.Core.Animation;

/// <summary>Pose preparation and immediate or deferred combination supplied by an animation backend.</summary>
internal interface IAnimationPoseOperations
{
	bool Prepare( int child, bool enabled );
	void Blend( float weight );
	void Sample( int child );
	void QueueBlend( float weight );
}
