namespace SkateMotion.Core.Animation;

/// <summary>
/// Whether a rider on foot is in the air for its animation. A walking body that has left the ground only counts as in
/// the air once it rises off it or is clear of it by more than a step, so stepping down off the deck or a kerb plays
/// as a step, while a jump, a fall or stepping off the board above the ground plays the air states. Metres, z up.
/// </summary>
internal static class BipedFooting
{
	/// <summary>Height above the ground (m) beyond which a body that is not standing is in the air.</summary>
	internal const float AirGap = 0.3f;

	/// <summary>Upward speed (m/s) beyond which a body that is not standing is in the air at once.</summary>
	internal const float RisingSpeed = 1f;

	/// <summary>
	/// True for a body off the ground rising at <paramref name="risingSpeed"/> with the ground
	/// <paramref name="groundGap"/> below it (infinite with none in reach).
	/// </summary>
	internal static bool Airborne( float risingSpeed, float groundGap ) => risingSpeed > RisingSpeed || groundGap > AirGap;
}
