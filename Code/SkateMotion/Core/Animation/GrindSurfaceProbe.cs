namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>A directed collision probe in the contact solver's Y-up metre frame.</summary>
internal readonly record struct GrindSurfaceProbe( Vector3 Start, Vector3 End, float Radius );
