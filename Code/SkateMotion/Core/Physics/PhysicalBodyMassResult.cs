using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Constructed body mass, optional local correction and retained shape measurements.</summary>
internal readonly record struct PhysicalBodyMassResult(
	PhysicalBodyMass Mass, LimbRigidTransform? LocalOffset, float MaximumInertia,
	float Volume, bool HasShapeProperties );
