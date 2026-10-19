namespace SkateMotion;

/// <summary>Physical wheel-balance state, reported only after the opposite axle lifts clear.</summary>
public enum SkateBalanceMode
{
	/// <summary>No supported manual balance.</summary>
	None,
	/// <summary>Supported by the rear axle with the front wheels raised.</summary>
	Manual,
	/// <summary>Supported by the front axle with the rear wheels raised.</summary>
	NoseManual
}
