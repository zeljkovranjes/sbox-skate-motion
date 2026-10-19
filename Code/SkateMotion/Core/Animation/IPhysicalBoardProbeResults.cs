namespace SkateMotion.Core.Animation;

/// <summary>Completes the submitted query batch before exposing its ordered results.</summary>
internal interface IPhysicalBoardProbeResults
{
	void Complete();
	bool TryGet( int index, out PhysicalBoardProbeHit hit );
}
