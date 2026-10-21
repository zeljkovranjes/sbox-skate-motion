namespace SkateMotion.Core.Input;

/// <summary>A grab chosen from the hands that reach and where the trick stick points as they do.</summary>
internal enum GrabPick
{
	None,
	/// <summary>Back hand on the toe edge (a frontside grab).</summary>
	Indy,
	/// <summary>Front hand on the heel edge (a backside grab).</summary>
	Melon,
	/// <summary>Front hand on the toe edge.</summary>
	Mute,
	/// <summary>Back hand on the heel edge.</summary>
	Stale,
	/// <summary>Front hand on the nose.</summary>
	Nose,
	/// <summary>Back hand on the nose.</summary>
	NoseCrail,
	/// <summary>Back hand on the tail.</summary>
	Tail,
	/// <summary>Front hand reaching back to the tail.</summary>
	Seatbelt,
	/// <summary>Both hands.</summary>
	Double,
	/// <summary>Both hands on the nose, the board pulled up the body.</summary>
	Rocket
}
