namespace SkateMotion.Core.Animation;

/// <summary>A constant raw pose that restores its authored transforms on each enabled preparation.</summary>
internal sealed class AnimationReferencePose : IAnimationPreparedPoseNode
{
	private readonly AnimationBoneTransform[] _reference;

	internal AnimationReferencePose( AnimationBoneTransform[] reference )
	{
		ArgumentNullException.ThrowIfNull( reference );
		_reference = new AnimationBoneTransform[ reference.Length ];
		Array.Copy( reference, _reference, reference.Length );
		Pose = new SkeletonPose( reference.Length );
	}

	/// <summary>The reusable output of the last enabled preparation.</summary>
	public SkeletonPose Pose { get; }

	/// <summary>Copies constant transforms when enabled; no playback history is consumed.</summary>
	public bool Prepare( bool enabled, bool consumeHistory, uint flags )
	{
		if ( enabled )
			Pose.SetTransforms( _reference );
		return enabled;
	}
}
