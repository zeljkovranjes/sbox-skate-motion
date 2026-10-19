namespace SkateMotion.Core.Collision;

/// <summary>Routes two trajectory queues and suppresses delivery for cancelled owners.</summary>
internal sealed class TrajectoryQueryService
{
	private readonly TrajectoryQueryBatch _first;
	private readonly TrajectoryQueryBatch _second;
	private readonly bool[] _activeOwners = new bool[ 8 ];
	internal int FirstCount => _first.Count;
	internal int SecondCount => _second.Count;

	internal TrajectoryQueryService( int firstCapacity, int secondCapacity )
	{
		_first = new( firstCapacity );
		_second = new( secondCapacity );
	}

	internal void Submit( TrajectoryQueryTarget target, TrajectoryQueryRequest request, int queue, byte flag )
	{
		ArgumentNullException.ThrowIfNull( target );
		if ( request.Filter < -1 || request.Filter > 7 )
			throw new ArgumentOutOfRangeException( nameof( request ), "Owner filter must be between -1 and 7." );
		var batch = queue == 1 ? _first : _second;
		// Capacity overflow is a caller error. Keep the target intact if the queue cannot accept it.
		if ( batch.Count == batch.Capacity ) throw new InvalidOperationException( "Trajectory queue is full." );
		request = request with { Surfaces = new( 0, 0, 0, 0, true, true ) };
		target.Reset( request, flag );
		batch.Add( target, request );
		_activeOwners[ request.Filter == -1 ? 7 : request.Filter ] = true;
	}

	internal void Cancel( int owner )
	{
		if ( owner < 0 || owner > 7 ) throw new ArgumentOutOfRangeException( nameof( owner ) );
		_activeOwners[ owner ] = false;
	}

	internal void Process( TrajectoryWorldQuery query, MeshQueryInstance[] instances )
	{
		ArgumentNullException.ThrowIfNull( query );
		ArgumentNullException.ThrowIfNull( instances );
		_first.Process( query, instances, _activeOwners );
		_second.Process( query, instances, _activeOwners );
	}
}
