namespace SkateMotion.Core.Animation;

/// <summary>Gameplay snapshot the director turns into animation parameters. Speeds are metres per second.</summary>
internal readonly record struct RiderAnimationInput
{
	public RiderAnimationInput() { }

	internal RiderActivity Activity { get; init; }
	internal bool Airborne { get; init; }

	/// <summary>
	/// The board is in the air rather than carried by a rail: grinding or sliding it has no wheel on the ground, which
	/// the board reports as airborne, but it rests on the rail.
	/// </summary>
	internal bool Flying => Airborne && string.IsNullOrEmpty( Grind );
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

	/// <summary>The wind-up is off the nose (the stick pushed forward), for a nollie.</summary>
	internal bool WindupNose { get; init; }

	/// <summary>0 none, 1 manual, 2 nose manual.</summary>
	internal int Balance { get; init; }
	internal string Grind { get; init; }
	internal bool GrindFrontside { get; init; } = true;

	/// <summary>The nose points toward the rider's heels across the rail, or null when that is not known.</summary>
	internal bool? GrindNoseToHeels { get; init; }

	/// <summary>The grind is ridden backwards (nose against the travel along the rail).</summary>
	internal bool GrindBackward { get; init; }

	/// <summary>A truck grind turned well off the rail's line.</summary>
	internal bool GrindTwisted { get; init; }

	/// <summary>On one truck, the free truck hangs below the rail.</summary>
	internal bool GrindLow { get; init; }

	/// <summary>An end slide on a ledge with the deck lying over the ledge's top (the backslash blunts).</summary>
	internal bool GrindOnLedgeTop { get; init; }
	internal string Trick { get; init; }
	internal bool TrickNollie { get; init; }
	internal bool TrickFlips { get; init; }
	internal bool HighPop { get; init; }
	internal string Grab { get; init; }

	/// <summary>The trick stick while grabbing in the air, tweaking the grab: x toward the toes for the stance, y forward.</summary>
	internal float TweakX { get; init; }
	internal float TweakY { get; init; }

	/// <summary>The trick stick in the air, grabbing or not: x toward the toes for the stance, y forward.</summary>
	internal float AirStickX { get; init; }
	internal float AirStickY { get; init; }

	/// <summary>The catch button: in the air it takes the feet off a grab, or catches a flip dark side up.</summary>
	internal bool Catch { get; init; }

	/// <summary>Feet taken off the board in the air, as push buttons name them: 1 the left push, 2 the right, 3 both.</summary>
	internal int FeetOff { get; init; }

	/// <summary>A kickflip or heelflip still held at the end of its flick, to keep turning.</summary>
	internal bool FlipHeld { get; init; }

	/// <summary>Sideways lean of the wind-up toward the toes for the stance, -1 to 1: a shove-it to come.</summary>
	internal float WindupSpin { get; init; }

	/// <summary>Foot-plant style held at the pop (boneless, fastplant, nocomply), or null.</summary>
	internal string Plant { get; init; }

	/// <summary>Late trick flicked in the air (kickflip, heelflip, shuvit), or null.</summary>
	internal string LateTrick { get; init; }

	/// <summary>The late trick was flicked by the back foot (pushed forward, then pulled back).</summary>
	internal bool LateBackFoot { get; init; }

	/// <summary>A late shove spun frontside.</summary>
	internal bool LateFrontside { get; init; }

	/// <summary>Catch modifier held on a long flip (darkcatch, underflip, quadflip), or null.</summary>
	internal string CatchStyle { get; init; }

	/// <summary>The part of a hand plant under way: "into" as the hand plants, "cycle" while it is held, "out" for a
	/// moment after it lets go; null with no hand plant.</summary>
	internal string Handplant { get; init; }

	/// <summary>The hand plant turns the rider frontside (else backside).</summary>
	internal bool HandplantFrontside { get; init; }
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

	/// <summary>How far the hippy jump's crouch has wound it up (0 a quick one, 1 a full crouch), held through its flight.</summary>
	internal float HippyCharge { get; init; }

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

	/// <summary>Getting off is the board's own runout (it ran into something, a hippy jump missed it), not a step off.</summary>
	internal bool Runout { get; init; }

	/// <summary>The ground walked on, as the walking clips name it (flat, ramp-up-shallow, stair-dn-steep, ...); null is flat.</summary>
	internal string GroundSlope { get; init; }

	/// <summary>Metres ahead to the lip of a ramp the rider could drop in from with the board in hand, or infinity.</summary>
	internal float EdgeDistance { get; init; } = float.PositiveInfinity;

	/// <summary>True while the mount control is held (waiting at the lip of a drop-in).</summary>
	internal bool MountHeld { get; init; }

	/// <summary>True for the first moment after dropping in, while the board goes over the lip.</summary>
	internal bool DroppingIn { get; init; }
}
