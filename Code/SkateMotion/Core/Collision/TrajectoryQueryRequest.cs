namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Quadratic motion and the collision filters used by its ordered segment queries.</summary>
internal readonly record struct TrajectoryQueryRequest(
	Vector3 Position, Vector3 Velocity, Vector3 Acceleration,
	float Duration, float Radius, float FirstStep, float LaterStep,
	int Filter, uint Exclusion, MeshQueryFilter Surfaces )
{
	internal float PositionAuxiliary { get; init; }
	internal float VelocityAuxiliary { get; init; }
	internal float AccelerationAuxiliary { get; init; }
}
