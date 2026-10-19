namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Trajectory, four-lane validity time and signed sample index for candidate bias.</summary>
internal readonly record struct GrindCandidateBiasInput(
	GrindTrajectoryState Trajectory, Vector4 Time, int SampleIndex );
