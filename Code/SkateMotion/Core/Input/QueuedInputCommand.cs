namespace SkateMotion.Core.Input;

internal readonly record struct QueuedInputCommand( int Kind, InputCommandKey Key, float Speed );
