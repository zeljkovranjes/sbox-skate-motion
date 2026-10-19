namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retained placement reference and the explicit secondary correction target.</summary>
internal readonly record struct PhysicalLocomotionPlacementState( Vector3 Reference, Vector3 SecondaryTarget );
