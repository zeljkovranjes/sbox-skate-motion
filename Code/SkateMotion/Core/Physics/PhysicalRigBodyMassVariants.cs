using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Primary and alternate owner masses with the final body descriptor.</summary>
internal readonly record struct PhysicalRigBodyMassVariants(
	PhysicalBodyMass Primary, PhysicalBodyMass Alternate, PhysicalBodyMassResult Descriptor );
