namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Motion and orientation retained before preparing a hand surface request.</summary>
internal readonly record struct PhysicalHandRequestInput(
	Vector3 Origin, Vector3 Velocity, Vector3 Normal, Vector3 Side, Vector3 Tangent,
	int RequestedSide );
