using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Explicit channel and motion identities supplied by initialized behavior data.</summary>
internal readonly record struct AnimationFakieHeadDefinition( InputCommandKey Channel, InputCommandKey Motion );
