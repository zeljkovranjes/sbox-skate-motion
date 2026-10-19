namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Cached contact and independent requested/smoothed height for one foot.</summary>
internal readonly record struct FootReachState( int ContactState, float Requested, float Smoothed, Vector3 CachedContact );
