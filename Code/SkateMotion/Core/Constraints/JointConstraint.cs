namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal struct JointConstraint
{
	internal ConstraintPair Pair { get; set; }
	internal Vector3 PositiveLinearBias { get; set; }
	internal Vector3 NegativeLinearBias { get; set; }
	internal Vector3 PositiveAngularBias { get; set; }
	internal Vector3 NegativeAngularBias { get; set; }
}
