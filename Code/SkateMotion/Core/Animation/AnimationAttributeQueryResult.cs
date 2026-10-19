namespace SkateMotion.Core.Animation;

/// <summary>A sampled attribute with its full key, interval, status and retained value lanes.</summary>
internal readonly record struct AnimationAttributeQueryResult(
	AnimationAttributePayload Payload, AnimationAttributeKey Name,
	float Start, float End, byte Flags, sbyte Kind );
