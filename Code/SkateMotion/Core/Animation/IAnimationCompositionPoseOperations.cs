namespace SkateMotion.Core.Animation;

/// <summary>Pose preparation, composition and attribute sampling for a two-child wrapper.</summary>
internal interface IAnimationCompositionPoseOperations
{
	bool Prepare( int child, bool enabled );
	void Compose( bool reverse, bool retainFirstTranslationW );
	void Sample( int child );
}
