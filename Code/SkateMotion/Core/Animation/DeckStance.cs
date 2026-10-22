namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// How a foot planted on the deck keeps to its place there (metres, in the shown deck's frame: X along it, Y across it,
/// Z up): when the clip's place for it has gone far enough for the foot to step over to it, and how high a foot held by
/// a landing stands.
/// </summary>
internal static class DeckStance
{
	/// <summary>How far (metres) the clip's place may go along the deck from a planted foot before the foot steps to it.</summary>
	internal const float SlackAlong = 0.127f;

	/// <summary>
	/// How far (metres) it may go across the deck. Left as far out across as along, a foot stood with its heel over the
	/// deck's edge (its ankle 4.8 in from the middle after a varial kickflip; riding, 0.9 to 1.9 in).
	/// </summary>
	internal const float SlackAcross = 0.0635f;

	/// <summary>
	/// How fast (metres a second) a foot held by a landing rises with the clip's. With the clip's feet still off its own
	/// board after a varial kickflip, its pull's height jumped 3 in in three frames and back, and followed, the planted feet
	/// hopped on the deck.
	/// </summary>
	internal const float LandingRise = 0.3048f;

	/// <summary>
	/// Whether a planted foot is far enough from the clip's place (<paramref name="offset"/>, from the foot to the place)
	/// to step to it: past <see cref="SlackAlong"/> along the deck or <see cref="SlackAcross"/> across it, or as far
	/// between the two. Its height is left out, being the clip's own: a foot planted as it came down onto the deck was taken
	/// for one far from its place by its height over the deck alone.
	/// </summary>
	internal static bool OutOfStance( Vector3 offset )
	{
		var along = offset.X / SlackAlong;
		var across = offset.Y / SlackAcross;
		return along * along + across * across > 1f;
	}

	/// <summary>
	/// How high a foot held by a landing stands this frame, given the clip's height for it (<paramref name="wanted"/>) and
	/// where it stood last frame (<paramref name="held"/>): down at once with the clip's, up no quicker than
	/// <see cref="LandingRise"/>.
	/// </summary>
	internal static float HeldHeight( float wanted, float held, float delta ) =>
		MathF.Min( wanted, held + LandingRise * MathF.Max( delta, 0f ) );
}
