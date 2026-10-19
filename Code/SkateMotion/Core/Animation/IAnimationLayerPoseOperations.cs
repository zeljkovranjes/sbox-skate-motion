namespace SkateMotion.Core.Animation;

/// <summary>Pose operations with per-bone masks supplied by either prepared input.</summary>
internal interface IAnimationLayerPoseOperations : IAnimationPoseOperations
{
	void BlendWeighted( float weight, bool maskFromFirst );
}
