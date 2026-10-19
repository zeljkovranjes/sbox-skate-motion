namespace SkateMotion.Core.Animation;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

internal readonly record struct PhysicalLocomotionOwnerInput(
	Vector3 Position, Matrix4x4 ContactFrame, uint ContactIdentity, uint ContactFlags,
	bool TargetMode, bool FlipAcceleration, bool ReplaceTarget, Matrix4x4 TargetFrame );
