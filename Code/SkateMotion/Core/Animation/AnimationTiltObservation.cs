namespace SkateMotion.Core.Animation;

/// <summary>Retained physical output read by the animation tilt behavior.</summary>
internal readonly record struct AnimationTiltObservation( float LateralTilt, float SpinSpeed, int MovementCategory );
