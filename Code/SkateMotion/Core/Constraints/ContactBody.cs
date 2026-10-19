namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

// LinearMotion/AngularMotion are acceleration channels; historical names remain for fixture compatibility.
internal readonly record struct ContactBody(
	Vector3 Center, float InverseMass, Matrix4x4 InverseInertia,
	Vector3 LinearMotion, Vector3 AngularMotion, bool Responds )
{
	internal int MotionFlags { get; init; }

	internal Vector3 AngularResponse( Vector3 torque ) => Responds
		? Vector3.TransformNormal( torque, InverseInertia ) : Vector3.Zero;

	internal Vector3 PointMotion( Vector3 offset ) => Responds
		? LinearMotion + Vector3.Cross( AngularMotion, offset ) : Vector3.Zero;

	internal float EffectiveInverseMass => Responds ? InverseMass : 0f;
}
