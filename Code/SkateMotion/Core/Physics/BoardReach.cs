namespace SkateMotion.Core.Physics;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// Calling the board back to the hand: how far away it may be, how long it takes to come, and the arc it flies on. A
/// board in sight up to <see cref="Farthest"/> metres away comes to the rider's reaching hand; close by it is quick, from
/// the farthest it takes about a second.
/// </summary>
internal static class BoardReach
{
	/// <summary>Farthest (metres) a board in sight is called back from.</summary>
	internal const float Farthest = 30f;

	// Flight time: Quickest seconds close by, growing with the square root of the distance to Slowest at the farthest.
	private const float Quickest = 0.13f;
	private const float Slowest = 1f;

	// The flight rises above the straight way by this share of its length at the middle, and never more than Highest
	// metres, so a board called from along the ground does not plough through it.
	private const float Rise = 0.15f;
	private const float Highest = 1.5f;

	// A board called from farther than Farthest (a rider with a longer recall range) comes at least this fast (m/s).
	private const float RecallSpeed = 70f;

	/// <summary>
	/// Seconds a board <paramref name="distance"/> metres away takes to reach the hand when called back: as
	/// <see cref="FlightSeconds"/> within <see cref="Farthest"/>, and from farther at <see cref="RecallSpeed"/>.
	/// </summary>
	internal static float RecallSeconds( float distance ) =>
		float.IsFinite( distance ) ? MathF.Max( FlightSeconds( distance ), distance / RecallSpeed ) : FlightSeconds( distance );

	/// <summary>Seconds a board <paramref name="distance"/> metres away takes to reach the hand.</summary>
	internal static float FlightSeconds( float distance )
	{
		if ( !float.IsFinite( distance ) || distance <= 0f )
			return Quickest;
		var seconds = Quickest + (Slowest - Quickest) * MathF.Sqrt( MathF.Min( distance, Farthest ) / Farthest );
		return Math.Clamp( seconds, Quickest, Slowest );
	}

	/// <summary>
	/// Where the flying board is <paramref name="progress"/> (0 to 1) of the way from <paramref name="from"/> to the hand
	/// at <paramref name="to"/>: eased in and out along the way, lifted over it in an arc that rises and settles gently, and
	/// at the hand at the end, arriving there at the hand's own pace (it neither strikes the hand nor drops into it).
	/// </summary>
	internal static Vector3 Along( Vector3 from, Vector3 to, float progress )
	{
		var t = Math.Clamp( progress, 0f, 1f );
		var eased = t * t * (3f - 2f * t);
		var arc = 4f * t * (1f - t);
		var lift = MathF.Min( Rise * Vector3.Distance( from, to ), Highest ) * arc * arc;
		return Vector3.Lerp( from, to, eased ) + Vector3.UnitZ * lift;
	}
}
