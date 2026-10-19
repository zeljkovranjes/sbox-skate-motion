namespace SkateMotion.Core.Constraints;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

internal struct ContactConstraint
{
	internal int BodyA { get; set; }
	internal int BodyB { get; set; }
	internal bool ReportContact { get; set; }
	internal uint Materials { get; set; }
	internal float InverseMassA { get; set; }
	internal float InverseMassB { get; set; }
	internal Vector3 OffsetA { get; set; }
	internal Vector3 OffsetB { get; set; }
	internal Vector3 CoefficientX { get; set; }
	internal Vector3 CoefficientY { get; set; }
	internal Vector3 CoefficientZ { get; set; }
	internal Vector3 Normal { get; set; }
	internal Vector3 TangentA { get; set; }
	internal Vector3 TangentB { get; set; }
	internal Vector3 NormalAngularA { get; set; }
	internal Vector3 NormalAngularB { get; set; }
	internal Vector3 TangentAAngularA { get; set; }
	internal Vector3 TangentAAngularB { get; set; }
	internal Vector3 TangentBAngularA { get; set; }
	internal Vector3 TangentBAngularB { get; set; }
	internal Vector4 Bias { get; set; }
	internal Vector4 Impulse { get; set; }
	internal float StaticFriction { get; set; }
	internal float SlidingFriction { get; set; }
}
