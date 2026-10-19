namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct WheelForce( Vector3 Point, Vector3 Force, float Load );
