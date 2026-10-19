using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

internal sealed class InputRequestCondition
{
	private readonly InputCommandKey _key;
	private readonly ScalarConditionComparison _comparison;

	internal uint OwnerState { get; private set; }

	internal InputRequestCondition( InputCommandKey key, ScalarConditionComparison comparison )
	{
		_key = key;
		_comparison = comparison;
	}

	internal bool Evaluate( InputRequestBuffer requests, uint ownerState )
	{
		ArgumentNullException.ThrowIfNull( requests );
		OwnerState = ownerState;

		for ( var i = 0; i < requests.Count; i++ )
		{
			var request = requests[ i ];
			if ( request.Key == _key )
			{
				// Presence is independent of value when comparison is disabled.
				return _comparison.Mode == ConditionComparisonMode.None
					|| _comparison.Evaluate( request.Value );
			}
		}

		return false;
	}
}
