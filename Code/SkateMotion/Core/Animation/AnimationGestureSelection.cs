namespace SkateMotion.Core.Animation;

/// <summary>Four configured gesture identities, retained in the producer's field order.</summary>
internal readonly record struct AnimationGestureSelection( int First, int Second, int Third, int Fourth )
{
	internal int Get( int gesture ) => gesture switch
	{
		0 => Third,
		1 => Fourth,
		2 => First,
		3 => Second,
		_ => 0x25
	};
}
