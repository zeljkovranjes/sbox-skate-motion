namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Target identity and optional fresh contact available after the current adjustment.</summary>
internal readonly record struct FootReachInput( int TargetId, Vector3? Contact );
