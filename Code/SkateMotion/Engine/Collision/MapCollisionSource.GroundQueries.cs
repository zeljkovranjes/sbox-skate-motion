namespace SkateMotion.Engine.Collision;

using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;

internal sealed partial class MapCollisionSource
{
	/// <summary>Refreshes the selected map source and issues ordinary ground queries in query-space metres.</summary>
	internal void ProcessGroundQueries( PhysicalGroundMeshQueryBatch batch, Span<uint> owner,
		ReadOnlySpan<uint> frame, TrajectoryWorldQuery trajectory )
	{
		ArgumentNullException.ThrowIfNull( batch );
		UpdateTransforms();
		batch.Issue( owner, frame, _query, trajectory, _instances );
	}
}
