namespace SkateMotion.Core.Animation;

/// <summary>End and optional linked bone indices, with the supplied chain reach limit.</summary>
internal readonly record struct EffectorBoneBinding( int EndBone, int ReferenceBone, float MaximumReach );
