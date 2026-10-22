namespace SkateMotion.Core.Animation;

/// <summary>
/// How a shown pose eased over from where it was shown to where it is (StepSmoother.EaseFrom) closes the gap: the share of
/// the offset still shown after some of the ease has passed, falling along a smoothstep, so the shown pose leaves and
/// meets the real one at rest (its turn rising from nothing and settling to nothing).
/// </summary>
internal static class ShownEase
{
	/// <summary>
	/// Seconds the shown board takes to turn into its drop after a hand plant lets go: physics turns the board nose down
	/// into the ramp in one step, which showed as a turn of 100 degrees and more in a frame, the rider riding it with it.
	/// </summary>
	internal const float PlantLetGo = 0.25f;

	/// <summary>The share of the offset still shown <paramref name="elapsed"/> seconds into an ease of
	/// <paramref name="length"/> seconds: one at its start, nothing at its end or with no ease.</summary>
	internal static float Left( float elapsed, float length )
	{
		if ( !(length > 0f) || elapsed >= length )
			return 0f;
		var amount = Math.Clamp( elapsed / length, 0f, 1f );
		return 1f - amount * amount * (3f - 2f * amount);
	}
}
