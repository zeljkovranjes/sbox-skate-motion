namespace SkateMotion.Core.Animation;

/// <summary>Completed physical and hand state used to choose a gesture channel.</summary>
internal readonly record struct AnimationGestureModeInput(
	uint PhysicalMode, bool PhysicalGate, bool HoldingBoard, uint FirstHand, uint SecondHand )
{
	internal int Resolve()
	{
		if ( unchecked( PhysicalMode - 6u ) < 2u && !PhysicalGate )
			return HoldingBoard ? 2 : 0;
		if ( SecondHand == 0 )
			return FirstHand == 0 ? 0 : 2;
		return FirstHand == 0 ? 1 : -1;
	}
}
