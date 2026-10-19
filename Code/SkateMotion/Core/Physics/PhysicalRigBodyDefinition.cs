namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Authored geometry and mass coefficients for one canonical body.</summary>
internal readonly record struct PhysicalRigBodyDefinition(
	int Shape, Vector3 Dimensions, float SizeScale,
	float PrimaryMassCoefficient, float AlternateMassCoefficient, bool IsRoot );
