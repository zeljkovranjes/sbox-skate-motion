namespace SkateMotion.Core.Animation;

/// <summary>Per-update strength and blend controls for the physical pose.</summary>
internal readonly record struct PhysicalPoseSettings( float Blend, bool Enabled, float FirstStrength, float SecondStrength );
