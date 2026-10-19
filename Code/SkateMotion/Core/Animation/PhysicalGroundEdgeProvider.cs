namespace SkateMotion.Core.Animation;

/// <summary>Appends a registered provider's edges after static traversal, preserving the shared output count and ordering.</summary>
internal delegate void PhysicalGroundEdgeProvider( GrindEdgeBounds bounds, uint worldSelection,
	int instanceFilter, Span<uint> output, ref int count );
