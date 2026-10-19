namespace SkateMotion.Core.Physics;

/// <summary>Retained construction settings shared by the canonical bodies.</summary>
internal readonly record struct PhysicalRigBodyTuning(
	float RootRadius, float RootHalfLength, float MassScale, float AlternateMassScale,
	float CapsuleRadiusScale, float CapsuleLengthScale,
	int InertiaMode, float InertiaScale, float AxisTolerance );
