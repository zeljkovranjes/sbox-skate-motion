using SkateMotion.Core.Grinding;

namespace SkateMotion.Core;

using Vector3 = System.Numerics.Vector3;

internal readonly record struct PhysicsStep(
	Vector3 Force, Vector3 Torque, Vector3 Impulse, Vector3 AngularImpulse,
	int WheelCount, MotionMode Mode, TrickKind Trick, bool Landed, float ImpactSpeed )
{
	/// <summary>Rail engagement this step; None while rolling, airborne or after a pop.</summary>
	internal GrindKind Grind { get; init; }

	/// <summary>The trick this step was popped off the nose: its nollie version.</summary>
	internal bool TrickNose { get; init; }

	/// <summary>How the board sits on the rail this step, while grinding.</summary>
	internal GrindShape GrindShape { get; init; }

	/// <summary>The grind as named to the rider and the game: <see cref="Grind"/> decided as it locked on and held through
	/// the contacts' wobble, changing at once only when the rider asks (see <see cref="GrindHold"/>).</summary>
	internal GrindKind NamedGrind { get; init; }

	/// <summary>The grind's shape with the named grind's flags (see <see cref="NamedGrind"/>).</summary>
	internal GrindShape NamedGrindShape { get; init; }

	/// <summary>Fastest approach of the deck into a surface this step, in metres per second.</summary>
	internal float CollisionSpeed { get; init; }

	/// <summary>The rider ran into something side-on while upright and should step off and run out, not bail.</summary>
	internal bool Runout { get; init; }

	/// <summary>True while the rider's hand is planted on a ramp's lip.</summary>
	internal bool HandPlanted { get; init; }

	/// <summary>Side of a powerslide under way: -1 left, 1 right, 0 none.</summary>
	internal int PowerSlide { get; init; }

	/// <summary>True while a revert spins the board half way round.</summary>
	internal bool Reverting { get; init; }

	/// <summary>Body flip under way: 1 front, -1 back, 0 none.</summary>
	internal int BodyFlip { get; init; }

	/// <summary>True while the board rides along a wall.</summary>
	internal bool WallRiding { get; init; }

	/// <summary>How far down (0 to 1) the rider is crouched by the handling.</summary>
	internal float Crouch { get; init; }
}
