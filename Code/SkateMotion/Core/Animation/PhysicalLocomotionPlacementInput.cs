namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Physical job points used by placement, separate from the contact velocity reference.</summary>
internal readonly record struct PhysicalLocomotionPlacementInput( Vector3 DesiredPosition, Vector3 SecondaryPosition );
