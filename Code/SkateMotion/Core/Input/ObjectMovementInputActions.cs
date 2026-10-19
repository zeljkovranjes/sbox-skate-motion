namespace SkateMotion.Core.Input;

/// <summary>Independent object translation and bounded rotation requests.</summary>
internal readonly record struct ObjectMovementInputActions( float Sideways, float Forward, float Rotation );
