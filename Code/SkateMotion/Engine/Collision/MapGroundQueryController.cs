namespace SkateMotion.Engine.Collision;

using SkateMotion.Core.Animation;
using SkateMotion.Core.Collision;
using NumericsVector3 = System.Numerics.Vector3;

/// <summary>Connects a completed walking owner to its selected retained map-query services.</summary>
internal sealed class MapGroundQueryController
{
	private readonly uint[] _owner;
	private readonly PhysicalGroundQueryContext _context;
	private readonly MapCollisionSource _map;
	private readonly TrajectoryWorldQuery _trajectory;
	private readonly PhysicalGroundMeshQueryBatch _batch;

	internal MapGroundQueryController( uint[] owner, PhysicalGroundQueryContext context,
		MapCollisionSource primaryWorld, MapCollisionSource alternateWorld,
		TrajectoryWorldQuery trajectory, PhysicalGroundRegisteredEdges registeredEdges )
	{
		ArgumentNullException.ThrowIfNull( owner );
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( trajectory );
		ArgumentNullException.ThrowIfNull( registeredEdges );
		_owner = owner;
		_context = context;
		_map = context.AlternateWorld ? alternateWorld : primaryWorld;
		ArgumentNullException.ThrowIfNull( _map );
		_trajectory = trajectory;
		_batch = new( registeredEdges.Collect );
		context.BindOwner( owner );
	}

	/// <summary>Runs after solved-frame adoption; contact feedback remains the preceding published record.</summary>
	internal void Issue( PhysicalLocomotionOwnerState physical, NumericsVector3 orientationAxis,
		PhysicalLocomotionFrameState adopted, ReadOnlySpan<uint> retainedQuery )
	{
		_context.Capture( physical, orientationAxis, adopted, retainedQuery );
		_map.ProcessGroundQueries( _batch, _owner, _context.Frame, _trajectory );
	}

	/// <summary>Consumes completed results at the later physical query-owner phase.</summary>
	internal int CompleteAndSelect( LimbSolverMath angles ) => PhysicalGroundQuerySamples.Update( _owner, _batch.Results, angles );
}
