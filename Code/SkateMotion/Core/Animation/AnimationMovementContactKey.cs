namespace SkateMotion.Core.Animation;

/// <summary>Preserves the five opaque words of a physical movement contact identifier.</summary>
internal readonly record struct AnimationMovementContactKey(
	uint First, uint Second, uint Third, uint Fourth, uint Fifth );
