namespace SkateMotion.Core.Animation;

/// <summary>A completed surface query that can be retrieved once before another completion.</summary>
internal sealed class GrindSurfaceQueryResult
{
	private GrindSurfaceClassification _classification;
	private bool _ready;

	internal void Complete( GrindSurfaceClassification classification )
	{
		_classification = classification;
		_ready = true;
	}

	internal bool TryConsume( out GrindSurfaceClassification classification )
	{
		classification = _ready ? _classification : default;
		var ready = _ready;
		_ready = false;
		return ready;
	}
}
