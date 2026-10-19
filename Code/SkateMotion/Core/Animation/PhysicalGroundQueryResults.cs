namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

/// <summary>Bindings to retained support/sample batches and the registered-edge query provider.</summary>
internal sealed class PhysicalGroundQueryResults
{
	internal Action CompleteSupportQueries { get; }
	internal Action CompleteSampleQueries { get; }
	internal Func<uint, ReadOnlyMemory<uint>> GetSupport { get; }
	internal Func<uint, ReadOnlyMemory<uint>> GetSample { get; }
	internal Func<Vector4, Vector4, uint, int, ReadOnlyMemory<uint>> GetEdges { get; }

	internal PhysicalGroundQueryResults( Action completeSupportQueries, Action completeSampleQueries,
		Func<uint, ReadOnlyMemory<uint>> getSupport, Func<uint, ReadOnlyMemory<uint>> getSample,
		Func<Vector4, Vector4, uint, int, ReadOnlyMemory<uint>> getEdges )
	{
		CompleteSupportQueries = completeSupportQueries ?? throw new ArgumentNullException( nameof( completeSupportQueries ) );
		CompleteSampleQueries = completeSampleQueries ?? throw new ArgumentNullException( nameof( completeSampleQueries ) );
		GetSupport = getSupport ?? throw new ArgumentNullException( nameof( getSupport ) );
		GetSample = getSample ?? throw new ArgumentNullException( nameof( getSample ) );
		GetEdges = getEdges ?? throw new ArgumentNullException( nameof( getEdges ) );
	}
}

