namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Current motion, reference frame and retained trajectory used to construct queries.</summary>
internal readonly record struct GrindQueryRequestInput(
	Vector4 Velocity, Vector4 SecondaryVelocity, ushort Count, bool SecondarySpeed,
	float Spread, float SpreadScale, float LeadTime,
	Vector4 Reference, Vector4 Point, Vector4 Up, Matrix4x4 LocalFrame,
	Vector4 ExternalReference, Vector4 ExternalUp, Vector4 ExternalVelocity,
	bool Override, Vector4 OverridePosition, Vector4 OverrideReference,
	bool AllowReuse, GrindTrajectoryState RetainedTrajectory, Vector4 RetainedAxis,
	Vector4 Acceleration, int Queue, int Owner );
