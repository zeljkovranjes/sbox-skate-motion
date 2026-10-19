namespace SkateMotion.Core.Animation;

/// <summary>Resolves an owning state and compares its retained elapsed time.</summary>
internal sealed class StateTimeCondition
{
	private readonly ScalarConditionComparison _comparison;
	private readonly Func<uint, uint> _getStateHandle;
	private readonly Func<uint, uint> _getParent;
	private readonly Func<uint, float> _getElapsed;

	internal uint OwnerState { get; private set; }
	internal bool OwnerIsHandle { get; set; }

	// Owner zero is absent. Other values are encoded state handles or caller-owned node IDs.
	internal StateTimeCondition( ScalarConditionComparison comparison, bool ownerIsHandle,
		Func<uint, uint> getStateHandle, Func<uint, uint> getParent, Func<uint, float> getElapsed )
	{
		ArgumentNullException.ThrowIfNull( getStateHandle );
		ArgumentNullException.ThrowIfNull( getParent );
		ArgumentNullException.ThrowIfNull( getElapsed );
		_comparison = comparison;
		OwnerIsHandle = ownerIsHandle;
		_getStateHandle = getStateHandle;
		_getParent = getParent;
		_getElapsed = getElapsed;
	}

	internal bool Evaluate( uint ownerState )
	{
		OwnerState = ownerState;
		var elapsed = _getElapsed( ResolveOwner() );
		// Reject negative time before optional absolute comparison; unordered values continue.
		return !(elapsed < 0f) && _comparison.Evaluate( elapsed );
	}

	private uint ResolveOwner()
	{
		var owner = OwnerState;
		while ( true )
		{
			if ( OwnerIsHandle || owner == 0 )
				return owner;
			var handle = _getStateHandle( owner );
			if ( handle != 0 )
				return handle;
			owner = _getParent( owner );
		}
	}
}
