namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Authored cylinder frame and canonical sphere dimensions for a compound body.</summary>
internal readonly record struct PhysicalCompoundBodyDefinition(
	Vector3 Angles, Vector3 Position, Vector3 Dimensions,
	float SizeScale, float CylinderHeight, float CylinderRadius, float MassScale );
