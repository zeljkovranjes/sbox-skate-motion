using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Retains one joint's current geometry and limits independently of its world membership.</summary>
internal sealed class PhysicalStructuralJointRegistration( PhysicalStructuralJoint joint )
{
	internal PhysicalStructuralJoint Joint { get; set; } = joint;
	internal PhysicalSolverWorld Owner { get; set; }
}
