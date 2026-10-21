namespace SkateMotion;

/// <summary>Which side of a GrindRail polyline the top of its ledge lies on, looking along the line from each point to
/// the next; None for a rail or a lip with no top beside it. A box GrindRail finds this for itself.</summary>
public enum GrindLedgeTop
{
	/// <summary>No top beside the line: a rail.</summary>
	None,
	/// <summary>The ledge's top lies to the left of the line.</summary>
	Left,
	/// <summary>The ledge's top lies to the right of the line.</summary>
	Right
}
