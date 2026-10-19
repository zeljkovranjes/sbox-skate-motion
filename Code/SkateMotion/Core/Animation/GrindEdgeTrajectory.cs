namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Quadratic world motion and contact radius in Y-up metres and seconds.</summary>
internal readonly record struct GrindEdgeTrajectory( Vector3 Position, Vector3 Velocity, Vector3 Acceleration, float Radius );
