namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Observed contact and retained physical frame positions for gait adjustment.</summary>
internal readonly record struct PhysicalLocomotionCadenceContactInput( Vector3 FramePosition, Vector3 ContactPoint );
