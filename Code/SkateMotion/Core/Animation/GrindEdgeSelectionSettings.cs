namespace SkateMotion.Core.Animation;

/// <summary>Initial comparison state for each scan of the pending contact samples.</summary>
internal readonly record struct GrindEdgeSelectionSettings( float InitialSeparation, float InitialHeight, float InitialAngle );
