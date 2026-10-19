namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>Board pose and motion fields used to populate a new contact-query input.</summary>
internal readonly record struct GrindQueryBoardState(
	Matrix4x4 WorldFrame, Matrix4x4 LocalFrame,
	Vector4 Velocity, Vector4 SecondaryVelocity, Vector4 Reference, Vector4 Point,
	float LeadTime, ulong MotionFlags );
