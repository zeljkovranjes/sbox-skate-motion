namespace SkateMotion.Core.Animation;

/// <summary>Required resource properties read by the board's queued force consumer.</summary>
internal interface IPhysicalBoardForceProperties
{
	float ReadApplicationHeight();
	void ReadPostForceProperty();
}
