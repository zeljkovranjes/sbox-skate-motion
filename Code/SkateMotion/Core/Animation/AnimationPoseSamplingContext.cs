namespace SkateMotion.Core.Animation;

/// <summary>Shared immediate sampling policy and suppression state for clip leaves.</summary>
internal sealed class AnimationPoseSamplingContext
{
	internal AnimationPoseSamplingContext( uint interpolationPolicy, bool trajectoryEnabled )
	{
		InterpolationPolicy = interpolationPolicy;
		TrajectoryEnabled = trajectoryEnabled;
	}

	internal uint InterpolationPolicy { get; set; }
	internal bool Suppressed { get; set; }
	internal bool TrajectoryEnabled { get; }

	internal bool Interpolate( uint flags ) => ( flags == 2 ? InterpolationPolicy : flags ) == 1;
}
