namespace SkateMotion;

/// <summary>Physical feet requested for pushing, independent of stance.</summary>
[Flags]
public enum SkatePushFeet
{
	/// <summary>Use the stance-selected foot when a legacy push amount is supplied.</summary>
	None = 0,
	/// <summary>Push with the left foot.</summary>
	Left = 1,
	/// <summary>Push with the right foot.</summary>
	Right = 2
}
