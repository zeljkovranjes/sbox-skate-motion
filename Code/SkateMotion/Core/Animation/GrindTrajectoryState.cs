namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Four-lane trajectory storage whose acceleration and auxiliary vector survive preparation.</summary>
internal readonly record struct GrindTrajectoryState(
	Vector4 Position, Vector4 Velocity, Vector4 Acceleration, Vector4 Auxiliary );
