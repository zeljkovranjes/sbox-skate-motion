namespace SkateMotion.Core.Animation;

/// <summary>A signed normalized-time key and its discrete pose selection.</summary>
internal readonly record struct AnimationPoseStreamKeyframe( short Time, ushort Value );
