using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Copies one present input request to a named scalar output during behavior updates.</summary>
internal readonly record struct AnimationIntentDefinition(
	InputCommandKey Request,
	AnimationAttributeKey Target,
	bool PublishAnimation );
