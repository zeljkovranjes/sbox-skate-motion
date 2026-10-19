namespace SkateMotion.Core.Animation;

/// <summary>An encoded stream name retaining all six identity words.</summary>
internal readonly record struct AnimationPoseStreamKey( uint A, uint B, uint C, uint D, uint E, uint F );
