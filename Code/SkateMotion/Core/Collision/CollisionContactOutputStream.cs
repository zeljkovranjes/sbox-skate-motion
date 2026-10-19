namespace SkateMotion.Core.Collision;

internal sealed class CollisionContactOutputStream : ICollisionContactOutput
{
	private readonly uint[] _records;
	private int _reservedCount;

	internal CollisionContactOutputStream( uint[] records, int reservedCount = 0 )
	{
		ArgumentNullException.ThrowIfNull( records );
		if ( records.Length % 64 != 0 || reservedCount < 0 )
			throw new ArgumentException( "Output storage requires complete contact records and a nonnegative reservation count." );
		_records = records;
		_reservedCount = reservedCount;
	}

	internal uint ReservedCount => unchecked( (uint)System.Threading.Interlocked.CompareExchange( ref _reservedCount, 0, 0 ) );

	/// <summary>Reserves output space, clamps an overflowing reservation and copies its accepted prefix.</summary>
	public void Write( ReadOnlySpan<uint> records )
	{
		if ( records.Length % 64 != 0 )
			throw new ArgumentException( "Output requires complete contact records.", nameof( records ) );
		var count = records.Length / 64;
		if ( count == 0 )
			return;
		var end = System.Threading.Interlocked.Add( ref _reservedCount, count );
		var start = unchecked( end - count );
		var capacity = _records.Length / 64;
		var remaining = unchecked( capacity - start );
		var available = remaining < 0 ? 0 : remaining;
		if ( count > available )
			System.Threading.Interlocked.Exchange( ref _reservedCount, capacity );
		var accepted = Math.Min( count, available );
		if ( accepted != 0 )
			records[ ..(accepted * 64) ].CopyTo( _records.AsSpan( start * 64, accepted * 64 ) );
	}
}
