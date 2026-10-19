namespace SkateMotion.Engine.Collision;

using SkateMotion.Core.Animation;

internal sealed partial class MapCollisionSource
{
	/// <summary>Executes queued board probes in original query-space metres against the current map snapshot.</summary>
	internal void ProcessBoardProbes( PhysicalBoardMeshProbeBatch batch )
	{
		ArgumentNullException.ThrowIfNull( batch );
		UpdateTransforms();
		batch.Execute( _query, _instances );
	}
}
