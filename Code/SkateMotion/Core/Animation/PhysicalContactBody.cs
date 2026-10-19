namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Contact-side motion and mass supplied by the physical world.</summary>
internal readonly record struct PhysicalContactBody( Vector3 Velocity, float InverseMass, bool Fixed );
