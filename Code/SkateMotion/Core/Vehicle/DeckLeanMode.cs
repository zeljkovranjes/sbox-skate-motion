namespace SkateMotion.Core.Vehicle;

/// <summary>What the board is doing, as far as the deck's lean into a turn is concerned.</summary>
internal enum DeckLeanMode
{
	/// <summary>Rolling on its wheels: steering leans the deck.</summary>
	Carving,

	/// <summary>Balancing a manual: steering leans the deck less.</summary>
	Manual,

	/// <summary>On its wheels or a rail but not carving (a powerslide, a revert, a grind, no rider): the deck settles level.</summary>
	Level,

	/// <summary>Off the ground: the trucks let go of their lean slowly.</summary>
	Airborne
}
