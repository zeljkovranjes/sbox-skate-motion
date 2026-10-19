namespace SkateMotion.Core.Animation;

/// <summary>Directional child preparation and sampling, retaining distinct immediate and deferred weights.</summary>
internal sealed class AnimationDirectionalPoseControl
{
	private readonly IAnimationPoseOperations _operations;

	internal AnimationDirectionalPoseControl( IAnimationPoseOperations operations ) => _operations = operations;

	internal float Threshold { get; private set; }
	internal bool HasPose { get; private set; }

	internal bool Prepare( int first, int second, float weight, float threshold,
		bool firstIsEndpoint, bool secondIsEndpoint, bool enabled )
	{
		Threshold = threshold;
		var upper = 1f - threshold;
		var lower = threshold - weight >= 0f ? threshold : weight;
		var clamped = upper - lower >= 0f ? lower : upper;
		var divisor = MathF.FusedMultiplyAdd( threshold, -2f, 1f );
		var phase = ( clamped - threshold ) / divisor;
		var square = phase * phase;
		if ( phase > .5f )
		{
			if ( secondIsEndpoint )
			{
				var cubic = MathF.FusedMultiplyAdd( phase, square * -4f, 1f );
				var quadratic = MathF.FusedMultiplyAdd( square, 8f, phase * -4f );
				weight = quadratic + cubic;
			}
		}
		else if ( firstIsEndpoint )
		{
			weight = MathF.FusedMultiplyAdd( phase, square * -4f, square * 4f );
		}

		var firstReady = _operations.Prepare( first, enabled && weight < 1f );
		var secondReady = _operations.Prepare( second, enabled && weight > 0f );
		if ( firstReady && secondReady )
			_operations.Blend( weight );
		HasPose = firstReady || secondReady;
		return HasPose;
	}

	internal void Sample( int first, int second, float weight, bool firstIsEndpoint, bool secondIsEndpoint )
	{
		var lower = .3f - weight >= 0f ? .3f : weight;
		var clamped = .7f - lower >= 0f ? lower : .7f;
		var phase = ( clamped - .3f ) * 2.500000238418579f;
		var square = phase * phase;
		var selectionWeight = weight;
		if ( phase > .5f )
		{
			if ( secondIsEndpoint )
			{
				var cubic = MathF.FusedMultiplyAdd( square, phase * -4f, 1f );
				var quadratic = MathF.FusedMultiplyAdd( square, 8f, phase * -4f );
				selectionWeight = quadratic + cubic;
			}
		}
		else if ( firstIsEndpoint )
		{
			selectionWeight = MathF.FusedMultiplyAdd( square, phase * -4f, square * 4f );
		}

		if ( selectionWeight < 1f )
		{
			_operations.Sample( first );
			if ( weight > 0f )
			{
				_operations.Sample( second );
				_operations.QueueBlend( phase );
			}
		}
		else
		{
			_operations.Sample( second );
		}
	}
}
