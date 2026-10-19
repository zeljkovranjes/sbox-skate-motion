namespace SkateMotion.Core.Input;

internal readonly record struct QueuedInputActions( bool HasCommand, bool IsTrick, InputCommandKey Key, float Speed );
