namespace SkateMotion.Core.Constraints;

// Strength is stiffness for Spring, correction speed for SpeedLimited, and unused for Position.
internal readonly record struct DriveSettings(
	DriveMode Mode, float Strength, float Damping, float MaximumAcceleration );
