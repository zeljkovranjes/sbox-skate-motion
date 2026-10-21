namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// How a board leaves the hand when no animation shows the release: the moment it lets go, and a throw's toss. With
/// clips the board instead leaves at the clip's own release, moving as the clip moves it.
/// </summary>
internal static class BoardThrow
{
	/// <summary>Seconds into a drop or throw that the hand lets go (the clips let go a little earlier).</summary>
	internal const float ReleaseSeconds = 0.45f;

	/// <summary>A toss (m/s): the board goes on ahead and a little up, landing about a metre in front.</summary>
	internal const float TossForward = 1.4f;

	internal const float TossUp = 0.4f;

	/// <summary>
	/// The board's velocity (m/s) as a toss sends it: <see cref="TossForward"/> along the rider's level facing and
	/// <see cref="TossUp"/> up, on top of the rider's own velocity.
	/// </summary>
	internal static Vector3 Toss( Vector3 facing, Vector3 riderVelocity )
	{
		var level = new Vector3( facing.X, facing.Y, 0f );
		level = level.LengthSquared() > 1e-8f ? Vector3.Normalize( level ) : Vector3.UnitX;
		return riderVelocity + level * TossForward + Vector3.UnitZ * TossUp;
	}
}
