namespace SkateMotion.Core.Animation;

/// <summary>
/// Plays a clip made for one jump's flight (a jump or a step off on foot, from take-off to touchdown) over the flight
/// the rider actually has: what is left of the clip is stretched or squeezed to end as the rider lands, so a long fall
/// shows the clip's flight slowed down and then its touchdown, instead of the clip playing out early and starting over
/// in the air.
/// </summary>
internal static class AirTimeMatch
{
	/// <summary>Graph action of the states whose clip is played over the air time.</summary>
	internal const string Action = "match-air-time";

	/// <summary>Slowest and fastest the clip plays while matched, as a share of its authored rate.</summary>
	internal const float SlowestRate = 0.05f;
	internal const float FastestRate = 2f;

	// Share of what is left of the clip one frame may play: the clip never reaches its end in the air and starts over.
	private const float EndMargin = 0.9f;

	/// <summary>
	/// Playback rate that brings the clip, <paramref name="remaining"/> seconds from its end at the authored rate, to its
	/// end in <paramref name="timeToLand"/> seconds (zero with no ground below in reach, when it plays at the slowest
	/// rate), never past its end within a frame of <paramref name="step"/> seconds.
	/// </summary>
	internal static float Rate( float remaining, float timeToLand, float step )
	{
		if ( !(remaining > 0f) )
			return 0f;
		var rate = timeToLand > 0f ? remaining / timeToLand : SlowestRate;
		rate = Math.Clamp( rate, SlowestRate, FastestRate );
		return step > 0f ? MathF.Min( rate, EndMargin * remaining / step ) : rate;
	}
}
