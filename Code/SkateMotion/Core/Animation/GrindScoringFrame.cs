namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Direction, sample-index velocity and time-vector position retained during scoring.</summary>
internal readonly record struct GrindScoringFrame( Vector4 Direction, Vector4 Velocity, Vector4 Position );
