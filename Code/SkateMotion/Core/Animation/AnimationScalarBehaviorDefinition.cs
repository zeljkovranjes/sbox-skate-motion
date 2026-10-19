namespace SkateMotion.Core.Animation;

/// <summary>Explicit scalar assignments for each enabled phase of a stateless graph behavior.</summary>
internal readonly record struct AnimationScalarBehaviorDefinition( AnimationAttributeKey Name,
	float? EnterValue, float? UpdateValue, float? ExitValue, bool PublishAnimation );
