namespace SkateMotion.Core.Animation;

/// <summary>Receives an ordered query batch; implementations own execution and later result delivery.</summary>
internal interface IPhysicalBoardProbeSink
{
	void Begin();
	void Add( PhysicalBoardProbe probe );
	void End();
}
