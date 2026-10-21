using SkateMotion.Core.Vehicle;

namespace SkateMotion.Core.Grinding;

using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

/// <summary>
/// How the rider holds the board on a rail. The trick stick tips the deck toward its nose or tail: on both trucks
/// that rocks it onto one, and on one truck it drops the free end below the rail. Steering turns a one-truck grind off
/// the rail's line (crooks, salads, smiths, feebles). With the controls centred the board is held level along the
/// rail on its trucks, or across it in a slide, as before.
/// </summary>
internal static class GrindControl
{
	/// <summary>Largest tip of the deck toward its nose or tail, in radians, at the edge of the stick's range.</summary>
	internal const float MaximumPitch = 0.35f;

	/// <summary>Largest turn of a one-truck grind off the rail's line, in radians, at full steering.</summary>
	internal const float MaximumTwist = 0.58f;

	/// <summary>Stick deflection that gives the full tip; farther is the wind-up of a pop out of the grind.</summary>
	internal const float PitchRange = 0.65f;

	/// <summary>Reach from the deck's centre to the part of its end that rests on the rail in an end slide: midway between
	/// the truck's wheels and the deck's end, so a rail a few inches wide clears the wheels and stays under the deck with
	/// as much to spare toward its end (a deck resting its last centimetre on the rail is pushed off it as it lands). This
	/// is the default board's; <see cref="Reach"/> gives a board's own.</summary>
	internal const float TipReach = 0.344f;

	/// <summary>
	/// How far (radians) a board grinding a ledge's edge on its trucks rolls to rest the wheels over the ledge's top on it:
	/// the hanger held on the edge (its radius and <see cref="RailContacts.RailSkin"/> above it), those wheels' bottoms the
	/// same skin above the top, from the board's wheel size, hanger and track. Rolled just onto the top, the balance's
	/// sway pressed the wheel into it every few steps on one truck and it hopped there; just clear of it, a 5-0 holds the
	/// ledge as it holds a rail.
	/// </summary>
	internal static float LedgeRoll( ArticulatedBoard board ) => MathF.Asin( Math.Clamp(
		(board.WheelRadius - board.Settings.HangerRadius - RailContacts.RailSkin + LedgeWheelGap)
		/ MathF.Max( board.Settings.Track * 0.5f, 0.01f ), 0f, 0.5f ) );

	/// <summary>Gap (metres) the wheels over a ledge's top are held above it: as far as the hanger is held off the edge.</summary>
	internal const float LedgeWheelGap = RailContacts.RailSkin;

	/// <summary>A board's <see cref="TipReach"/>, from its wheelbase, wheel size and deck length.</summary>
	internal static float Reach( ArticulatedBoard board ) =>
		(board.Settings.Wheelbase * 0.5f + board.WheelRadius + board.Settings.DeckLength * 0.5f) * 0.5f;

	/// <summary>Deck-up direction to balance toward: the rail's up leaned by steering, tipped by the trick stick. On a
	/// ledge's edge the trucks' wheels over its top rest on it, the board rolled by <paramref name="ledgeRoll"/> (radians,
	/// see <see cref="LedgeRoll"/>) with that side up: balanced level instead, the deck pressed those wheels into the top
	/// and bounced off it every few steps.</summary>
	internal static Vector3 Balance( GrindLock grind, Vector3 deckForward, ControlInput input, float ledgeRoll = 0f )
	{
		// On one truck steering turns the board off the rail's line instead of leaning the rider over it: a lean there
		// rolls the deck, and the rail's push on a rolled truck slides it off the rail's line.
		var oneTruck = grind.Kind is GrindKind.FiveO or GrindKind.Nosegrind;
		var normal = GrindBalance.Target( grind, oneTruck ? input with { Steer = 0f } : input );
		if ( grind.Kind is not (GrindKind.FiftyFifty or GrindKind.FiveO or GrindKind.Nosegrind) )
			return normal;
		if ( ledgeRoll > 0f && grind.Top.LengthSquared() > 0.25f )
		{
			var top = grind.Top - normal * Vector3.Dot( grind.Top, normal );
			if ( top.LengthSquared() > 0.0001f )
				normal = Vector3.Normalize( normal * MathF.Cos( ledgeRoll ) - Vector3.Normalize( top ) * MathF.Sin( ledgeRoll ) );
		}
		// Stick up leans onto the nose (the nose goes down), stick down onto the tail, as in a manual. A positive turn
		// about the deck's right raises its nose.
		var pitch = -Math.Clamp( input.FlickY / PitchRange, -1f, 1f ) * MaximumPitch;
		if ( pitch == 0f )
			return normal;
		var right = Vector3.Cross( deckForward, grind.Up );
		if ( right.LengthSquared() < 0.0001f )
			return normal;
		var tip = Quaternion.CreateFromAxisAngle( Vector3.Normalize( right ), pitch );
		return Vector3.Normalize( Vector3.Transform( normal, tip ) );
	}

	/// <summary>Line the deck is turned to: along the rail on its trucks, turned off it by steering on one truck, and
	/// across it in a slide.</summary>
	internal static Vector3 Line( GrindLock grind, ControlInput input )
	{
		switch ( grind.Kind )
		{
			case GrindKind.Boardslide or GrindKind.Noseslide or GrindKind.Tailslide:
				return Vector3.Cross( grind.Up, grind.Direction );
			case GrindKind.FiveO or GrindKind.Nosegrind when input.Steer != 0f:
				var twist = Quaternion.CreateFromAxisAngle( grind.Up, input.Steer * MaximumTwist );
				return Vector3.Transform( grind.Direction, twist );
			default:
				return grind.Direction;
		}
	}

	/// <summary>Stick deflection or steering past which the rider's weight is over one end of the board.</summary>
	internal const float LeanThreshold = 0.15f;

	/// <summary>
	/// The point the rider's weight is over, when it is not the deck's centre: the resting end in an end slide; the
	/// grinding truck on one truck while the rider holds it there (tipping or turning the board); and on both trucks,
	/// the truck the trick stick leans toward. Otherwise null: let go, a one-truck grind settles back onto both trucks,
	/// or tips off a rail's end.
	/// </summary>
	internal static Vector3? Weight( ArticulatedBoard board, GrindKind kind, ControlInput input )
	{
		var leaning = MathF.Abs( input.FlickY ) > LeanThreshold;
		switch ( kind )
		{
			case GrindKind.Noseslide or GrindKind.Tailslide:
				return Pin( board, kind );
			case GrindKind.FiveO or GrindKind.Nosegrind when leaning || MathF.Abs( input.Steer ) > LeanThreshold:
				return Pin( board, kind );
			case GrindKind.FiftyFifty when leaning:
				return board.Body( input.FlickY > 0f ? 1 : 2 ).Position;
			default:
				return null;
		}
	}

	/// <summary>The point of the board held over the rail's line: the grinding truck on one truck, the resting end in
	/// an end slide, and the deck's centre otherwise.</summary>
	internal static Vector3 Pin( ArticulatedBoard board, GrindKind kind )
	{
		var deck = board.Deck;
		return kind switch
		{
			GrindKind.FiveO => board.Body( 2 ).Position,
			GrindKind.Nosegrind => board.Body( 1 ).Position,
			GrindKind.Noseslide => deck.Position + deck.Forward * Reach( board ),
			GrindKind.Tailslide => deck.Position - deck.Forward * Reach( board ),
			_ => deck.Position
		};
	}
}
