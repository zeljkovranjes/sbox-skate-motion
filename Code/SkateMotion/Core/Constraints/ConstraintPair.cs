namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal struct ConstraintPair
{
	internal int BodyA { get; set; }
	internal int BodyB { get; set; }
	internal Vector3 OffsetA { get; set; }
	internal Vector3 OffsetB { get; set; }
	internal Vector3 LinearImpulse { get; set; }
	internal Vector3 AngularImpulse { get; set; }
	internal Matrix4x4 LinearCoefficients { get; set; }
	internal Matrix4x4 AngularCoefficients { get; set; }
	internal Matrix4x4 LinearAxes { get; set; }
	internal Matrix4x4 AngularAxes { get; set; }
	internal Matrix4x4 InverseInertiaA { get; set; }
	internal Matrix4x4 InverseInertiaB { get; set; }
}
