using SkateMotion.Core.Animation;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

internal sealed partial class PhysicalSolverWorld
{
	internal int SolveGeneratedIntegrateAndCollect( Span<ContactConstraint> contacts, Span<JointConstraint> joints,
		Span<DriveConstraint> drives, Span<PhysicalWorldContact> output, int passes, float step, Vector3 gravity )
	{
		return SolveIntegrateAndCollectCore( contacts, joints, drives, output, passes, step, gravity, true );
	}
}
