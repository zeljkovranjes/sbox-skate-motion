namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>A predicted edge contact; the list producer owns EdgeIndex.</summary>
internal readonly record struct GrindEdgeSample(
	Vector3 Point, Vector3 TrajectoryPoint, Vector3 Direction, Vector3 Tangent,
	float Separation, float Time, float Angle, int SampleIndex, int EdgeIndex, bool IsValid );
