using SkateMotion.Core.Vehicle;

namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>How the board sits on the rail during a grind, as far as naming the grind needs it.</summary>
/// <param name="NoseOffset">Horizontal offset from the rail's line to the deck's nose, across the rail (metres).</param>
/// <param name="Backward">The nose points against the direction the board travels along the rail.</param>
/// <param name="Twisted">The deck is turned more than <see cref="TwistAngle"/> off the rail's line.</param>
/// <param name="Low">On one truck, the free truck hangs more than <see cref="LowDrop"/> below the rail.</param>
internal readonly record struct GrindShape( Vector3 NoseOffset, bool Backward, bool Twisted, bool Low )
{
	/// <summary>An end slide on a ledge with the deck lying over the ledge's top (the backslash blunts).</summary>
	internal bool OnLedgeTop { get; init; }

	/// <summary>Turn off the rail's line, in radians, past which a truck grind counts as twisted (crooks, salads).</summary>
	internal const float TwistAngle = 0.2f;

	/// <summary>Drop of the free truck below the rail, in metres, past which it hangs low (smiths, feebles, willies).</summary>
	internal const float LowDrop = 0.02f;

	/// <summary>Reach from the deck's centre to its nose, in metres.</summary>
	internal const float NoseReach = 0.4f;

	// Below this speed along the rail the direction of travel is the deck's own.
	private const float StillAlong = 0.05f;

	internal static GrindShape Measure( ArticulatedBoard board, GrindLock grind )
	{
		if ( !grind.Active )
			return default;
		var deck = board.Deck;
		var across = Vector3.Cross( grind.Up, grind.Direction );
		if ( across.LengthSquared() < 0.0001f )
			return default;
		across = Vector3.Normalize( across );
		var nose = deck.Position + deck.Forward * NoseReach;
		var noseOffset = across * Vector3.Dot( nose - grind.Point, across );
		var along = Vector3.Dot( deck.Velocity, grind.Direction );
		var travel = MathF.Abs( along ) < StillAlong ? Vector3.Dot( deck.Forward, grind.Direction ) : along;
		var backward = Vector3.Dot( deck.Forward, grind.Direction ) * travel < 0f;
		var heading = deck.Forward - grind.Up * Vector3.Dot( deck.Forward, grind.Up );
		var twisted = heading.LengthSquared() > 0.0001f
			&& MathF.Abs( Vector3.Dot( Vector3.Normalize( heading ), grind.Direction ) ) < MathF.Cos( TwistAngle );
		// On the end of the deck along a ledge's edge, with the rest of the deck over the ledge's top.
		var onTop = grind.Kind is GrindKind.Noseslide or GrindKind.Tailslide && grind.Top.LengthSquared() > 0.25f
			&& Vector3.Dot( deck.Position - grind.Point, grind.Top ) > 0f;
		return new( noseOffset, backward, twisted, HangsLow( board, grind ) ) { OnLedgeTop = onTop };
	}

	// Riding one truck, the other one hanging below the rail's top.
	private static bool HangsLow( ArticulatedBoard board, GrindLock grind )
	{
		if ( grind.Kind is not (GrindKind.FiveO or GrindKind.Nosegrind) )
			return false;
		var free = board.Body( grind.Kind == GrindKind.FiveO ? 1 : 2 ).Position;
		var onLine = grind.Point + grind.Direction * Vector3.Dot( free - grind.Point, grind.Direction );
		return Vector3.Dot( free - onLine, grind.Up ) < -LowDrop;
	}
}
