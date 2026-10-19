namespace SkateMotion.Core.Animation;

/// <summary>Orientation gates and velocity conditioning parameters for a new query cycle.</summary>
internal readonly record struct GrindQueryMotionSettings(
	float Blend, float MinimumUpY, float MinimumDirectionY, float TiltGain,
	float OrientationOffset, float MinimumOrientation, float MaximumOrientation,
	float NormalVelocityScale, float MinimumLength );
