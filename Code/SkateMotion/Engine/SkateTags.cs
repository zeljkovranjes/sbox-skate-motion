namespace SkateMotion.Engine;

internal static class SkateTags
{
	/// <summary>Marks a rider's own bodies so its board never solves them as a moving prop.</summary>
	internal const string Rider = "skate_rider";

	/// <summary>Surfaces boards grind on; contacts with them use grind friction instead of wheel grip.</summary>
	internal const string Grind = "grind";
}
