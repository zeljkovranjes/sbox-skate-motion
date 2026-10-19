namespace SkateMotion.Core.Animation;

/// <summary>A retained status and the status raised during the current reach update.</summary>
internal readonly record struct FootReachFlags( bool Persistent, bool Frame );
