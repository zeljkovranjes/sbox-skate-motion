namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>A common anchor expressed in the child and attached body's local frames.</summary>
internal readonly record struct PhysicalJointFrame(
	Vector4 ChildRotation, Vector3 ChildPosition, Vector4 BodyRotation, Vector3 BodyPosition );
