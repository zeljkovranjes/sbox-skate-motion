namespace SkateMotion.Core;

/// <summary>How a trick turns the board: flip about its length and spin about its normal.</summary>
internal static class TrickMotion
{
	/// <summary>-1 flips outward (kickflip family), 1 inward (heelflip family), 0 no flip.</summary>
	internal static float Flip( TrickKind trick ) => trick switch
	{
		TrickKind.Kickflip or TrickKind.VarialKickflip or TrickKind.Hardflip or TrickKind.Treflip => -1f,
		TrickKind.Heelflip or TrickKind.VarialHeelflip or TrickKind.InwardHeelflip or TrickKind.Laserflip => 1f,
		_ => 0f
	};

	/// <summary>Half turns of board spin as a multiple of the spin rate: 0, 1 (180) or 2 (360).</summary>
	internal static float Spin( TrickKind trick ) => trick switch
	{
		TrickKind.Shuvit or TrickKind.FrontsideShuvit or TrickKind.VarialKickflip or TrickKind.VarialHeelflip
			or TrickKind.Hardflip or TrickKind.InwardHeelflip => 1f,
		TrickKind.Shuvit360 or TrickKind.FrontsideShuvit360 or TrickKind.Treflip or TrickKind.Laserflip => 2f,
		_ => 0f
	};
}
