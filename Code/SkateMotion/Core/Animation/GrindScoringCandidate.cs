namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Prediction data and mutable outputs for one candidate in a scoring pass.</summary>
internal record struct GrindScoringCandidate(
	Vector4 Point, Vector4 Direction, Vector4 Axis, Vector4 Time,
	GrindTrajectoryState Trajectory, int SampleIndex, int Kind, float Radius,
	float CorrectionAuxiliary )
{
	internal Matrix4x4 ContactFrame { get; init; }
	internal uint Material { get; init; }
	internal uint Identity { get; init; }
	internal float FirstStep { get; init; }
	internal float LaterStep { get; init; }
	internal byte RequestFlag { get; init; }
}
