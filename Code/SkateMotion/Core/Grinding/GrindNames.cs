namespace SkateMotion.Core.Grinding;

/// <summary>
/// Names a grind the way a state graph tests it ("bs-smith", "bf-fs-5-o", "fs-noseblunt"). The name follows from
/// the board's contact with the rail and a handful of facts about its pose.
/// <list type="bullet">
/// <item>The side: frontside when the rail was on the rider's toe side as the board came onto it.</item>
/// <item>Which end is on the rail.</item>
/// <item>Whether the nose points toward the rider's heels across the rail.</item>
/// <item>Whether the grind is ridden backwards (nose against the travel): the <c>bf-</c> grinds.</item>
/// <item>Whether the board is twisted off the rail's line.</item>
/// <item>Whether the free truck hangs below the rail.</item>
/// </list>
/// </summary>
internal static class GrindNames
{
	/// <summary>Graph name for a grind, or an empty string when <paramref name="kind"/> is not a grind.</summary>
	/// <param name="kind">fiftyfifty, fivezero, nosegrind, boardslide, noseslide or tailslide.</param>
	/// <param name="frontside">The rail was on the rider's toe side as the board came onto it.</param>
	/// <param name="noseToHeels">The nose points toward the rider's heels across the rail, or null when not yet known
	/// (a board lying along the rail).</param>
	/// <param name="backward">The nose points against the direction of travel along the rail.</param>
	/// <param name="twisted">A truck grind with the board turned well off the rail's line.</param>
	/// <param name="low">A truck grind with the free truck hanging below the rail.</param>
	/// <param name="dark">The board is upside down (dark side up).</param>
	/// <param name="backslash">An end slide on a ledge with the deck over its higher side.</param>
	internal static string Graph( string kind, bool frontside, bool? noseToHeels, bool backward, bool twisted, bool low,
		bool dark = false, bool backslash = false )
	{
		var side = frontside ? "fs" : "bs";
		var other = frontside ? "bs" : "fs";
		// Measured across the rail from the approach: for a frontside approach the heels face the side the board came
		// from, so a nose toward the heels lies on the near side; backside, on the far side.
		var noseFar = noseToHeels is { } heels ? frontside != heels : frontside;
		var turned = noseToHeels ?? !frontside;
		var fakie = backward ? "bf-" : "";
		switch ( kind )
		{
			case "fiftyfifty":
				return $"{fakie}{side}-50-50";
			case "fivezero":
				return $"{fakie}{side}-{TailTruck( twisted, low, noseFar )}";
			case "nosegrind":
				return $"{fakie}{side}-{NoseTruck( twisted, low, !noseFar )}";
			case "boardslide":
				if ( dark )
					return $"{(turned ? "fs" : "bs")}-darkslide";
				return noseToHeels is null || noseFar ? $"{side}-board" : $"{side}-lip";
			case "noseslide":
				if ( dark )
					return $"{(turned ? "fs" : "bs")}-darkslide";
				if ( backslash )
					return $"{(turned ? "fs" : "bs")}-noseblunt";
				return noseToHeels is null || noseFar ? $"{side}-noseslide" : $"{side}-noseblunt";
			case "tailslide":
				if ( dark )
					return $"{(turned ? "bs" : "fs")}-darkslide";
				if ( backslash )
					return $"{(turned ? "bs" : "fs")}-blunt";
				return noseToHeels is null || !noseFar ? $"{side}-tailslide" : $"{side}-blunt";
			default:
				return "";
		}
	}

	// On the back truck: the nose up (a 5-0, or a salad turned over the far side) or hanging down (a smith on the
	// near side, a feeble over the far side).
	private static string TailTruck( bool twisted, bool low, bool noseFar )
	{
		if ( low )
			return noseFar ? "feeble" : "smith";
		return twisted && noseFar ? "salad" : "5-o";
	}

	// On the front truck: the tail up (a nosegrind, or a crook/overcrook when turned) or hanging down (a willy on the
	// near side, an overwilly over the far side).
	private static string NoseTruck( bool twisted, bool low, bool tailFar )
	{
		if ( low )
			return tailFar ? "overwilly" : "willy";
		if ( !twisted )
			return "nosegrind";
		return tailFar ? "overcrook" : "crook";
	}
}
