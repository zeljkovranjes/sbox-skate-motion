namespace SkateMotion.Core.Animation;

/// <summary>Retains condition results by index until the next invalidation.</summary>
internal sealed class ConditionResultCache
{
	private readonly int _conditionCount;
	private readonly uint[] _validity;
	private readonly uint[] _values;
	private readonly Func<int, uint, uint, bool> _evaluate;

	internal ConditionResultCache( int conditionCount, Func<int, uint, uint, bool> evaluate )
	{
		if ( conditionCount < 0 )
			throw new ArgumentOutOfRangeException( nameof( conditionCount ) );
		ArgumentNullException.ThrowIfNull( evaluate );
		_conditionCount = conditionCount;
		var words = conditionCount / 32 + (conditionCount % 32 == 0 ? 0 : 1);
		_validity = new uint[ words ];
		_values = new uint[ words ];
		_evaluate = evaluate;
	}

	internal void Invalidate()
	{
		Array.Clear( _validity );
	}

	internal bool Evaluate( int condition, uint node, uint owner )
	{
		if ( condition < 0 || condition >= _conditionCount )
			throw new ArgumentOutOfRangeException( nameof( condition ) );
		var word = condition / 32;
		var mask = 1u << (condition % 32);
		if ( (_validity[ word ] & mask) != 0 )
			return (_values[ word ] & mask) != 0;

		var result = _evaluate( condition, node, owner );
		// Read words after evaluation so nested evaluations and invalidation remain visible.
		_validity[ word ] |= mask;
		_values[ word ] = (_values[ word ] & ~mask) | (result ? mask : 0);
		return result;
	}
}
