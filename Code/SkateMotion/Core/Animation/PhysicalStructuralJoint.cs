namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>One retained parent-child structural joint and its local attachment frames.</summary>
internal readonly record struct PhysicalStructuralJoint(
	int ParentBone, int ChildBone, PhysicalJointFrame Frame, Vector4 LinearRotation,
	PhysicalStructuralJointLimits Limits );
