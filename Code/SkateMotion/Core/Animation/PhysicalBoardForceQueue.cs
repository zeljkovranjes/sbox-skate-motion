namespace SkateMotion.Core.Animation;

/// <summary>The board's bounded force queue; consuming it does not reset its lifetime.</summary>
internal sealed class PhysicalBoardForceQueue
{
	private readonly PhysicalBoardForce[] _entries = new PhysicalBoardForce[ 21 ];
	internal int Count { get; private set; }
	internal ReadOnlySpan<PhysicalBoardForce> Entries => _entries.AsSpan( 0, Count );

	internal bool TryAppend( PhysicalBoardForce force )
	{
		if ( Count > 20 )
			return false;
		_entries[ Count++ ] = force;
		return true;
	}

	internal void Reset() => Count = 0;
}
