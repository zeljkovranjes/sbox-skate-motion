namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Local diagonal inverse inertia and inverse mass used when a body frame changes.</summary>
internal readonly record struct PhysicalBodyMass( Vector3 InverseInertia, float InverseMass );
