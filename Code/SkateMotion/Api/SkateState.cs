namespace SkateMotion;

/// <summary>Read-only fixed-step telemetry in metres, seconds and radians.</summary>
public readonly record struct SkateState(
	float Speed, int GroundedWheels, bool IsAirborne, bool IsBailed, float LandingSpeed )
{
	/// <summary>Current supported manual, determined from wheel contacts and board tilt.</summary>
	public SkateBalanceMode Balance { get; init; }

	/// <summary>Current grind, determined from which parts of the board rest on a grind rail.</summary>
	public SkateGrind Grind { get; init; }

	/// <summary>Side of a powerslide under way: -1 left, 1 right, 0 none.</summary>
	public int PowerSlide { get; init; }

	/// <summary>True while a revert spins the board half way round.</summary>
	public bool Reverting { get; init; }

	/// <summary>True while the board rolls backwards (tail first): riding fakie.</summary>
	public bool Fakie { get; init; }

	/// <summary>Body flip under way: 1 front, -1 back, 0 none.</summary>
	public int BodyFlip { get; init; }

	/// <summary>True while the board rides along a wall.</summary>
	public bool WallRiding { get; init; }

	/// <summary>True while the rider's hand is planted on a ramp's lip (see <see cref="SkateInput.HandPlant"/>).</summary>
	public bool HandPlanting { get; init; }

	/// <summary>True when the rail being ground was on the deck's right as the board came onto it.</summary>
	public bool GrindRailOnRight { get; init; }

	/// <summary>True when the deck's nose lies across the rail on the side the deck's right faced as the board came
	/// onto it. With <see cref="GrindRailOnRight"/> and the rider's stance this tells smiths from feebles, crooks from
	/// overcrooks and boardslides from lipslides.</summary>
	public bool GrindNoseRight { get; init; }

	/// <summary>True while the grind is ridden backwards: the nose points against the board's travel along the rail.</summary>
	public bool GrindBackward { get; init; }

	/// <summary>True while a grind is turned well off the rail's line (crooks, salads).</summary>
	public bool GrindTwisted { get; init; }

	/// <summary>True while, on one truck, the free truck hangs below the rail (smiths, feebles, willies).</summary>
	public bool GrindLow { get; init; }

	/// <summary>True in an end slide on a ledge with the deck lying over the ledge's top: a bluntslide or nosebluntslide on
	/// top of the ledge (see <see cref="Components.GrindRail.LedgeTop"/>).</summary>
	public bool GrindOnLedgeTop { get; init; }

	// The grind as the rider's animation names it: decided as it locked on and held through the rail contacts' wobble
	// (Grind and its flags report the contacts step by step).
	internal SkateGrind NamedGrind { get; init; }
	internal bool NamedBackward { get; init; }
	internal bool NamedTwisted { get; init; }
	internal bool NamedLow { get; init; }
	internal bool NamedOnLedgeTop { get; init; }

	/// <summary>How far down (0 standing, 1 fully crouched) the handling has the rider: pumping, or low on steep ground.</summary>
	public float Crouch { get; init; }
}
