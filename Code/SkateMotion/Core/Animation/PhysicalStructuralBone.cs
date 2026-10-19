namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Authored body offset and joint orientation for one canonical bone.</summary>
internal readonly record struct PhysicalStructuralBone(
	Vector4 ParentToBoneRotation, Vector4 JointRotation, Vector4 BodyRotation,
	Vector3 BodyPosition, float SwingAngle, float TwistAngle );
