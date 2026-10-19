namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;

internal struct DriveConstraint
{
	internal ConstraintPair Pair { get; set; }
	internal Vector3 LinearBias { get; set; }
	internal Vector3 AngularBias { get; set; }
	internal Vector3 LinearLimit { get; set; }
	internal Vector3 AngularLimit { get; set; }
	internal float LinearGain { get; set; }
	internal float AngularGain { get; set; }
}
