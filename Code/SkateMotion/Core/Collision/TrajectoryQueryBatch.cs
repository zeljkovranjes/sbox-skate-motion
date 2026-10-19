namespace SkateMotion.Core.Collision;

/// <summary>Stores ordered requests and their independently retained output targets.</summary>
internal sealed class TrajectoryQueryBatch
{
	private readonly TrajectoryQueryRequest[] _requests;
	private readonly TrajectoryQueryTarget[] _targets;
	internal int Count { get; private set; }
	internal int Capacity => _requests.Length;

	internal TrajectoryQueryBatch( int capacity )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( capacity );
		_requests = new TrajectoryQueryRequest[ capacity ];
		_targets = new TrajectoryQueryTarget[ capacity ];
	}

	internal void Add( TrajectoryQueryTarget target, TrajectoryQueryRequest request )
	{
		_requests[ Count ] = request;
		_targets[ Count++ ] = target;
	}

	internal void Process( TrajectoryWorldQuery query, MeshQueryInstance[] instances, bool[] activeOwners )
	{
		for ( var i = 0; i < Count; i++ )
		{
			var request = _requests[ i ];
			var contact = query.Evaluate( request, instances );
			var owner = request.Filter == -1 ? 7 : request.Filter;
			if ( activeOwners[ owner ] ) _targets[ i ].Deliver( contact );
			_targets[ i ] = null;
		}
		Count = 0;
	}
}
