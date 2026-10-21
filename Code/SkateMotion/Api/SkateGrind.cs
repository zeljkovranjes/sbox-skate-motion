namespace SkateMotion;

/// <summary>Rail contact reported while the board is supported by a grind rail.</summary>
public enum SkateGrind
{
	/// <summary>Not grinding.</summary>
	None,
	/// <summary>Both trucks on the rail.</summary>
	FiftyFifty,
	/// <summary>Only the tail truck on the rail.</summary>
	FiveO,
	/// <summary>Only the nose truck on the rail.</summary>
	Nosegrind,
	/// <summary>Deck across the rail between the trucks.</summary>
	Boardslide,
	/// <summary>The deck's end past the nose truck on the rail: noseslides and nosebluntslides.</summary>
	Noseslide,
	/// <summary>The deck's end past the tail truck on the rail: tailslides and bluntslides.</summary>
	Tailslide
}
