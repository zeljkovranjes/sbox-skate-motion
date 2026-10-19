namespace SkateMotion.Core.Animation;

/// <summary>Gameplay snapshot the director turns into animation parameters. Speeds are metres per second.</summary>
internal readonly record struct RiderAnimationInput
{
	public RiderAnimationInput() { }

	internal RiderActivity Activity { get; init; }
	internal bool Airborne { get; init; }
	internal float Speed { get; init; }
	internal float Steer { get; init; }
	internal bool Pushing { get; init; }
	internal bool PushLeft { get; init; }
	internal bool PushRight { get; init; }
	internal bool NewPush { get; init; }
	internal float Brake { get; init; }
	internal bool Sliding { get; init; }
	internal bool Pumping { get; init; }
	internal float Windup { get; init; }

	/// <summary>0 none, 1 manual, 2 nose manual.</summary>
	internal int Balance { get; init; }
	internal string Grind { get; init; }
	internal bool GrindFrontside { get; init; } = true;
	internal string Trick { get; init; }
	internal bool TrickNollie { get; init; }
	internal bool TrickFlips { get; init; }
	internal bool HighPop { get; init; }
	internal string Grab { get; init; }

	/// <summary>Foot-plant style held at the pop (boneless, fastplant, nocomply), or null.</summary>
	internal string Plant { get; init; }

	/// <summary>Late trick flicked in the air (kickflip, heelflip, shuvit), or null.</summary>
	internal string LateTrick { get; init; }

	/// <summary>Catch modifier held on a long flip (darkcatch, underflip, quadflip), or null.</summary>
	internal string CatchStyle { get; init; }

	/// <summary>Hand plant held in the air, or null.</summary>
	internal string Handplant { get; init; }
	internal bool OneFoot { get; init; }

	/// <summary>Partial nose (positive) or tail (negative) board adjustment in the air.</summary>
	internal float Adjust { get; init; }
	internal float TimeSincePop { get; init; } = 10f;
	internal float TimeSinceLanding { get; init; } = 10f;
	internal float LandingSpeed { get; init; }

	/// <summary>How the last landing went: "spin" after turning in the air, "sketchy" when it came down off its line, or null.</summary>
	internal string LandingKind { get; init; }

	/// <summary>Side of a powerslide under way (-1 left, 1 right, 0 none), and whether a revert is spinning the board.</summary>
	internal int SlideSide { get; init; }
	internal bool Reverting { get; init; }

	/// <summary>Predicted seconds until the board comes down in the air (zero when unknown or on the ground).</summary>
	internal float TimeToLand { get; init; }

	/// <summary>How far down (0 to 1) the handling has the rider crouched.</summary>
	internal float Crouch { get; init; }

	/// <summary>True while the board rides along a wall.</summary>
	internal bool WallRiding { get; init; }

	/// <summary>True while a body flip turns rider and board over.</summary>
	internal bool BodyFlipping { get; init; }

	/// <summary>True while riding switch: the other foot forward, so the board's tail leads.</summary>
	internal bool Switch { get; init; }

	/// <summary>True while the hippy jump control is held on the board (its crouch).</summary>
	internal bool HippyCrouch { get; init; }

	/// <summary>True while the rider is off the board in a hippy jump, with its height above the deck (m), its speed
	/// toward it (positive rising), whether the board is still under it, and the seconds until it is back down.</summary>
	internal bool OffBoard { get; init; }
	internal float OffBoardHeight { get; init; }
	internal float OffBoardSpeed { get; init; }
	internal bool OverBoard { get; init; }
	internal float TimeToLandOnBoard { get; init; }
	internal float VerticalSpeed { get; init; }

	/// <summary>Deck up axis height: 1 flat, 0 on its side (wall), -1 upside down.</summary>
	internal float DeckUp { get; init; } = 1f;

	/// <summary>Height of the deck's left axis: near +/-1 when the board stands on its side.</summary>
	internal float DeckRoll { get; init; }

	/// <summary>Deck nose-down pitch in radians.</summary>
	internal float DeckPitch { get; init; }
	internal float MoveSpeed { get; init; }

	/// <summary>Magnitude of walking input, independent of board steering, from zero to one.</summary>
	internal float MoveAmount { get; init; }

	/// <summary>Whether the board has reached the hand during an interaction.</summary>
	internal bool HoldingBoard { get; init; }
	internal bool Goofy { get; init; }
	internal string Style { get; init; }

	/// <summary>Seconds the rider has been still.</summary>
	internal float Stationary { get; init; }
	internal bool Skitching { get; init; }

	/// <summary>Moving-object action on foot (push, pull, strafe), or null.</summary>
	internal string ObjectMotion { get; init; }
	internal string ObjectHeight { get; init; }
	internal bool HippyJump { get; init; }
	internal bool Gesturing { get; init; }
}
