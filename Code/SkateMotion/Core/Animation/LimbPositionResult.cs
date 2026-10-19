namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Position solve output: status zero is bent, one is fully extended, and two skips pose application.</summary>
internal readonly record struct LimbPositionResult( int Status, Vector3 Knee, Vector3 Target );
