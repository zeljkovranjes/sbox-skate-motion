namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Collision;

/// <summary>Retains board query slots and evaluates them through the recovered mesh worker.</summary>
internal sealed class PhysicalBoardMeshProbeBatch : IPhysicalBoardProbeResults
{
	private readonly MeshQueryRequest[] _requests;
	private readonly PhysicalBoardProbeHit[] _hits;
	private readonly bool[] _valid;
	internal int Count { get; private set; }
	internal int State { get; private set; }

	internal PhysicalBoardMeshProbeBatch( int capacity )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( capacity );
		_requests = new MeshQueryRequest[ capacity ];
		_hits = new PhysicalBoardProbeHit[ capacity ];
		_valid = new bool[ capacity ];
	}

	internal void Begin()
	{
		State = 1;
		Count = 0;
	}

	internal bool TryAdd( PhysicalBoardProbe probe )
	{
		if ( Count == _requests.Length )
			return false;
		if ( probe.Auxiliary != 0 )
			throw new ArgumentException( "An external surface-mask payload requires an explicit decoder.", nameof( probe ) );
		var surfaces = new MeshQueryFilter( 0, 0, 0, 0, (probe.Filter & 1U) != 0, (probe.Filter & 2U) != 0 );
		// The retained probe names reflect call registers: actor selects instances; flags exclude instance bits.
		_requests[ Count ] = new( probe.Start, probe.End, probe.Radius, probe.ExcludedActor, probe.Flags, surfaces );
		_valid[ Count ] = false;
		_hits[ Count ] = default;
		Count++;
		return true;
	}

	internal void Execute( MeshWorldQuery query, MeshQueryInstance[] instances )
	{
		ArgumentNullException.ThrowIfNull( query );
		ArgumentNullException.ThrowIfNull( instances );
		State = 2;
		for ( var index = 0; index < Count; index++ )
		{
			var hit = query.Evaluate( _requests[ index ], instances );
			_valid[ index ] = hit.HitCount != 0;
			_hits[ index ] = new( hit.Position, hit.Normal, hit.Fraction, hit.Material );
		}
	}

	public void Complete()
	{
		// Execute completes synchronously; publication still clears the batch state in the later owner phase.
		State = 0;
	}

	public bool TryGet( int index, out PhysicalBoardProbeHit hit )
	{
		if ( (uint)index >= (uint)Count )
			throw new ArgumentOutOfRangeException( nameof( index ) );
		hit = _hits[ index ];
		return _valid[ index ];
	}
}
