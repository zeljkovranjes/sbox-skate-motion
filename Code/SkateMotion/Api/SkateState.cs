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

	/// <summary>How far down (0 standing, 1 fully crouched) the handling has the rider: pumping, or low on steep ground.</summary>
	public float Crouch { get; init; }
}
