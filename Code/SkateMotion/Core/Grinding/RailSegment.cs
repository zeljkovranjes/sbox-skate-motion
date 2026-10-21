namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>A straight grindable edge in metres.</summary>
internal readonly record struct RailSegment( Vector3 Start, Vector3 End )
{
	/// <summary>
	/// Half the rail's width (metres) about its line, which runs along the middle of its top: a truck grinding it keeps
	/// the whole rail between its wheels. Zero for an edge or a line with no width of its own.
	/// </summary>
	internal float HalfWidth { get; init; }

	/// <summary>
	/// Horizontal direction (unit) across the edge toward the top of the ledge it belongs to, or zero for an edge with no
	/// top beside it (a rail, or a lip with nothing behind it). An end slide with the deck lying over that top is on the
	/// ledge's top (the backslash blunts).
	/// </summary>
	internal Vector3 Top { get; init; }

	internal Vector3 Direction => Vector3.Normalize( End - Start );
	internal float Length => Vector3.Distance( Start, End );

	internal bool IsValid => float.IsFinite( Start.X + Start.Y + Start.Z + End.X + End.Y + End.Z )
		&& Vector3.DistanceSquared( Start, End ) > 0.0001f;

	/// <summary>Closest point on the segment; along is the unclamped distance from the start.</summary>
	internal Vector3 Closest( Vector3 point, out float along )
	{
		along = Vector3.Dot( point - Start, Direction );
		return Start + Direction * Math.Clamp( along, 0f, Length );
	}
}
