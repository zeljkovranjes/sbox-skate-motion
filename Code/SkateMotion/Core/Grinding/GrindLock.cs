namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>Rail engagement for one substep: which grind and the rail frame it acts in.</summary>
/// <param name="Point">Point on the rail beside the deck's centre (metres).</param>
internal readonly record struct GrindLock( GrindKind Kind, Vector3 Direction, Vector3 Up, Vector3 Point = default )
{
	internal bool Active => Kind != GrindKind.None;

	/// <summary>Direction across the rail toward the top of its ledge, or zero (see <see cref="RailSegment.Top"/>).</summary>
	internal Vector3 Top { get; init; }
}
