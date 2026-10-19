namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>Volume and diagonal inertia per unit mass in a shape's local mass frame.</summary>
internal readonly record struct PhysicalShapeMassProperties( Vector3 UnitInertia, float Volume );
