namespace SkateMotion.Core.Animation;

/// <summary>Pose operations requested by a compact attribute command stream.</summary>
internal interface IAnimationPoseCommandOperations
{
	void Sample( int value );
	void Blend( float weight );
	void BlendMany( ReadOnlySpan<float> weights );
}
