namespace SkateMotion.Core.Animation;

/// <summary>Dispatch counters at completion or a combine-boundary early exit.</summary>
internal readonly record struct AnimationPoseCommandResult( uint Depth, int Values, int WeightCount, int WordIndex );
