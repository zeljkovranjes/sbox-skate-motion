namespace SkateMotion.Core.Collision;

/// <summary>Supplied edge limits and boundary-normal policy for triangle contacts.</summary>
internal readonly record struct TriangleContactFilterContext(
	uint MaximumEdgeCosine, uint NormalTolerance, bool PreserveBoundaryNormal );
