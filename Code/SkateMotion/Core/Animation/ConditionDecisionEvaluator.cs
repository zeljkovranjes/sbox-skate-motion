namespace SkateMotion.Core.Animation;

/// <summary>Evaluates ordered condition branches for entry and staying guards.</summary>
internal sealed class ConditionDecisionEvaluator
{
	internal const int False = -1;
	internal const int True = -2;

	private readonly ConditionDecision[] _decisions;
	private readonly int[] _entryRoots;
	private readonly int[] _stayingRoots;
	private readonly Func<int, uint, uint, bool> _evaluate;

	internal ConditionDecisionEvaluator( ConditionDecision[] decisions, int[] entryRoots,
		int[] stayingRoots, Func<int, uint, uint, bool> evaluate )
	{
		ArgumentNullException.ThrowIfNull( decisions );
		ArgumentNullException.ThrowIfNull( entryRoots );
		ArgumentNullException.ThrowIfNull( stayingRoots );
		ArgumentNullException.ThrowIfNull( evaluate );
		_decisions = new ConditionDecision[ decisions.Length ];
		_entryRoots = new int[ entryRoots.Length ];
		_stayingRoots = new int[ stayingRoots.Length ];
		Array.Copy( decisions, _decisions, decisions.Length );
		Array.Copy( entryRoots, _entryRoots, entryRoots.Length );
		Array.Copy( stayingRoots, _stayingRoots, stayingRoots.Length );
		_evaluate = evaluate;
		for ( var index = 0; index < _decisions.Length; index++ )
		{
			var decision = _decisions[ index ];
			if ( decision.Condition < 0 )
				throw new ArgumentException( "Condition indices must be nonnegative.", nameof( decisions ) );
			// Earlier-node references make every path finite without a per-call visited set.
			ValidateTarget( decision.WhenFalse, index, nameof( decisions ) );
			ValidateTarget( decision.WhenTrue, index, nameof( decisions ) );
		}
		foreach ( var root in _entryRoots )
			ValidateTarget( root, _decisions.Length, nameof( entryRoots ) );
		foreach ( var root in _stayingRoots )
			ValidateTarget( root, _decisions.Length, nameof( stayingRoots ) );
	}

	internal bool Evaluate( int guard, uint node, uint owner, bool staying )
	{
		var roots = staying ? _stayingRoots : _entryRoots;
		if ( guard < 0 || guard >= roots.Length )
			throw new ArgumentOutOfRangeException( nameof( guard ) );
		var current = roots[ guard ];
		while ( current >= 0 )
		{
			var decision = _decisions[ current ];
			// Equal branches still evaluate: callbacks can alter the shared condition cache.
			current = _evaluate( decision.Condition, node, owner )
				? decision.WhenTrue : decision.WhenFalse;
		}
		return current == True;
	}

	private static void ValidateTarget( int target, int limit, string parameter )
	{
		if ( target < True || target >= limit )
			throw new ArgumentException( "Targets must be terminals or earlier decisions.", parameter );
	}
}
