namespace SkateMotion.Core.Animation;

/// <summary>An ordered scalar marker or bone contact from evaluated animation output.</summary>
internal readonly record struct PhysicalAnimationAttribute(
	AnimationAttributeKey Name, float Value, sbyte Kind = 0, byte EventFlags = 0,
	AnimationAttributeKey Bone = default );
