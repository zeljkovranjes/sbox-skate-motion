namespace SkateMotion.Engine.Collision;

using SkateMotion.Core.Animation;

/// <summary>Connects board probe production and later result publication to one map geometry owner.</summary>
internal sealed class MapBoardProbeBatch : IPhysicalBoardProbeSink, IPhysicalBoardProbeResults
{
	private readonly MapCollisionSource _map;
	private readonly PhysicalBoardMeshProbeBatch _batch;
	internal int Count => _batch.Count;
	internal int State => _batch.State;

	internal MapBoardProbeBatch( MapCollisionSource map, int capacity = 6 )
	{
		ArgumentNullException.ThrowIfNull( map );
		_map = map;
		_batch = new( capacity );
	}

	public void Begin() => _batch.Begin();
	public void Add( PhysicalBoardProbe probe ) => _batch.TryAdd( probe );
	public void End() => _map.ProcessBoardProbes( _batch );
	public void Complete() => _batch.Complete();
	public bool TryGet( int index, out PhysicalBoardProbeHit hit ) => _batch.TryGet( index, out hit );
}
