using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

internal readonly record struct AnimationActionValueEntry( InputCommandKey Key, float Value, int Next );
