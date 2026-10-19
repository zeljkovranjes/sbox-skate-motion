namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>World motion and surface reference with an optional local side request.</summary>
internal readonly record struct PhysicalHandSourceInput(
	Vector3 Origin, Vector3 Velocity, Vector3 Normal, int RequestedSide );
