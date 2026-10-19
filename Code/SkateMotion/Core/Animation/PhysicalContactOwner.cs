namespace SkateMotion.Core.Animation;

/// <summary>Retains the bounded solved-contact list for one physical owner.</summary>
internal sealed class PhysicalContactOwner
{
	internal const int Capacity = 16;
	private readonly PhysicalCollectedContact[] _records = new PhysicalCollectedContact[ Capacity ];
	private readonly PhysicalRiderContact[] _riderContacts = new PhysicalRiderContact[ Capacity ];

	internal object ActorIdentity { get; set; }
	internal int Kind { get; set; }
	internal int? Identifier { get; set; }
	internal int Count { get; private set; }
	internal ReadOnlySpan<PhysicalRiderContact> RiderContacts => _riderContacts.AsSpan( 0, Count );

	internal PhysicalCollectedContact Contact( int index )
	{
		if ( (uint)index >= (uint)Count )
			throw new ArgumentOutOfRangeException( nameof( index ) );
		return _records[ index ];
	}

	internal void Clear() => Count = 0;

	internal void Append( PhysicalCollectedContact contact )
	{
		if ( Count == Capacity )
			return;
		_records[ Count ] = contact;
		_riderContacts[ Count ] = contact.RiderContact;
		++Count;
	}
}
