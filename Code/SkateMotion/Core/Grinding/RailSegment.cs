namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>A straight grindable edge in metres.</summary>
internal readonly record struct RailSegment( Vector3 Start, Vector3 End )
{
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
