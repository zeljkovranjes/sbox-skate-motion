namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

internal readonly record struct JointGeometry(
	int BodyA, int BodyB, Vector3 LocalAnchorA, Vector3 LocalAnchorB,
	Quaternion LinearFrameB, Vector3 LinearExtent, Vector3 MaximumLinearSpeed );
