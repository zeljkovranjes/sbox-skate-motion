namespace SkateMotion.Core.Collision;

/// <summary>Distinguishes a receding query from an approaching query without an accepted root.</summary>
internal enum SweepRootStatus
{
	NotApproaching = -1,
	Miss = 0,
	Hit = 1
}
