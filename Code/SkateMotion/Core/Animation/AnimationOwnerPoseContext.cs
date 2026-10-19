namespace SkateMotion.Core.Animation;

/// <summary>Owner-supplied sampling policy shared by retained base and overlay motion trees.</summary>
internal sealed class AnimationOwnerPoseContext
{
	internal AnimationOwnerPoseContext( AnimationPoseSamplingContext sampling, uint samplingFlags,
		bool consumeHistory, float poseThreshold )
	{
		ArgumentNullException.ThrowIfNull( sampling );
		Sampling = sampling;
		SamplingFlags = samplingFlags;
		ConsumeHistory = consumeHistory;
		PoseThreshold = poseThreshold;
	}

	internal AnimationPoseSamplingContext Sampling { get; }
	internal uint SamplingFlags { get; }
	internal bool ConsumeHistory { get; }
	internal float PoseThreshold { get; set; }
}
