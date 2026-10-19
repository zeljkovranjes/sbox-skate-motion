namespace SkateMotion.Core.Animation;

/// <summary>Actual status of the separate interaction animation channel, in seconds.</summary>
internal readonly record struct BoardHandlingPlayback( bool Exists, float Remaining, float Elapsed );
