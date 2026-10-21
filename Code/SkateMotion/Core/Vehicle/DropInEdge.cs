namespace SkateMotion.Core.Vehicle;

/// <summary>
/// Finds the lip of a ramp the rider can drop in from: walking ahead of the rider, the first place the ground falls away
/// by more than <see cref="Drop"/> below its feet, with sloping ground (a ramp, not a sheer drop or a step down to flat
/// ground) below it within <see cref="SlopeReach"/>.
/// </summary>
internal static class DropInEdge
{
	/// <summary>Farthest (metres) from a lip that the rider drops in from: it steps up to the lip first.</summary>
	internal const float Reach = 1.5f;

	/// <summary>Least fall (metres) below the feet that counts as the ground falling away at a lip.</summary>
	internal const float Drop = 0.2f;

	/// <summary>How far past the lip (metres) the ramp's slope must begin.</summary>
	internal const float SlopeReach = 0.6f;

	// Ground whose up axis is within this of straight up is flat; a ramp below the lip is steeper. Sloping ground counts as
	// past the lip once it is this far (metres) below the feet.
	private const float FlatUp = 0.97f;
	private const float Settled = 0.02f;

	/// <summary>
	/// The distance (metres) ahead to the lip, from ground samples taken every <paramref name="spacing"/> metres along the
	/// way the rider faces (the first a spacing ahead): each the ground's height relative to the feet (metres) and how
	/// upright it is there (the up component of its normal; NaN where no ground was found). Null with no lip.
	/// </summary>
	internal static float? Find( ReadOnlySpan<float> heights, ReadOnlySpan<float> ups, float spacing )
	{
		if ( heights.Length != ups.Length || !(spacing > 0f) )
			return null;
		for ( var i = 0; i < heights.Length; i++ )
		{
			// None found is a sheer drop, no ramp.
			if ( !float.IsFinite( heights[ i ] ) )
				return null;
			// Ground at the feet's level (or a step up) carries on toward the lip; past the lip it falls away, or slopes
			// down from right there.
			var sloping = float.IsFinite( ups[ i ] ) && ups[ i ] < FlatUp && heights[ i ] < -Settled;
			if ( heights[ i ] > -Drop && !sloping )
				continue;
			// The ground has fallen away: a ramp starts here or just past, sloping on down.
			var beyond = (int)MathF.Ceiling( SlopeReach / spacing );
			for ( var j = i; j < heights.Length && j <= i + beyond; j++ )
			{
				if ( float.IsFinite( ups[ j ] ) && ups[ j ] < FlatUp && (j + 1 >= heights.Length || heights[ j + 1 ] <= heights[ j ]) )
					return i * spacing + spacing * 0.5f;
			}
			return null;
		}
		return null;
	}
}
