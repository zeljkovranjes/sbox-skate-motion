using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;

namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

// Rail contact model. Capture sizes are estimates.
internal static class RailContacts
{
	/// <summary>Highest axle or deck clearance above a rail that still locks on, in metres.</summary>
	internal const float CaptureHeight = 0.05f;

	/// <summary>Deepest overlap still treated as resting on the rail rather than passing below it.</summary>
	internal const float CaptureDepth = 0.03f;

	/// <summary>Half the gap between a truck's inner wheel faces on the default board; the rail stays inside it. A board
	/// measured from its model has its own (SkateSettings.InnerWheelGap).</summary>
	internal const float GrooveHalfWidth = 0.06f;

	/// <summary>Least room (metres) left either side of a rail too wide, or a truck turned too far, for its wheels to
	/// straddle it: the hanger is then held to the rail's line.</summary>
	private const float LeastPlay = 0.002f;

	/// <summary>Hanger radius around the axle line that touches the rail, on the default board (a measured board has its
	/// own, SkateSettings.HangerRadius).</summary>
	internal const float AxleRadius = 0.008f;

	// The board's measured geometry: half its inner wheel gap, its hanger radius, half its deck's length and thickness.
	private static float Groove( ArticulatedBoard board ) => board.Settings.InnerWheelGap * 0.5f;
	private static float Hanger( ArticulatedBoard board ) => board.Settings.HangerRadius + RailSkin;

	/// <summary>Gap (metres) the hanger is held off a rail beyond its own radius: the solver lets a held contact sink about
	/// this far, which put a drawn truck that deep into the rail and kept the shown board lifted (and bobbing) all grind.</summary>
	internal const float RailSkin = 0.002f;

	/// <summary>Share of the deck's forward along the rail (the cosine of its turn off the rail's line) that divides the
	/// trucks' grinds from the deck's slides: a deck turned further across the rail slides on it, nearer along rides its
	/// trucks.</summary>
	internal const float SlideAlong = 0.6f;

	/// <summary>Appends rail contacts for both trucks and the deck; returns the resulting grind.</summary>
	internal static GrindLock Build( ArticulatedBoard board, ReadOnlySpan<RailSegment> rails, int surface,
		float friction, Span<CollisionContact> output, ref int count )
	{
		if ( rails.IsEmpty )
			return default;
		var deck = board.Deck;
		var nose = TryTruck( board, 1, rails, surface, friction, output, ref count, out var noseRail );
		var tail = TryTruck( board, 2, rails, surface, friction, output, ref count, out var tailRail );
		if ( nose || tail )
		{
			var rail = nose ? noseRail : tailRail;
			var kind = nose && tail ? GrindKind.FiftyFifty : nose ? GrindKind.Nosegrind : GrindKind.FiveO;
			return new( kind, rail.Direction, RailUp( deck.Up, rail.Direction ), rail.Closest( deck.Position, out _ ) ) { Top = rail.Top };
		}
		if ( !TryDeck( deck, board.Settings, rails, surface, friction, output, ref count, out var slideRail, out var along ) )
			return default;
		return new( SlideKind( board, along ), slideRail.Direction, RailUp( deck.Up, slideRail.Direction ),
			slideRail.Closest( deck.Position, out _ ) ) { Top = slideRail.Top };
	}

	// A deck crossing the rail between its trucks boardslides; past the nose or tail truck it slides on that end.
	private static GrindKind SlideKind( ArticulatedBoard board, float along )
	{
		var deck = board.Deck;
		var nose = Vector3.Dot( board.Body( 1 ).Position - deck.Position, deck.Forward );
		var tail = Vector3.Dot( board.Body( 2 ).Position - deck.Position, deck.Forward );
		if ( along > nose )
			return GrindKind.Noseslide;
		return along < tail ? GrindKind.Tailslide : GrindKind.Boardslide;
	}

	private static bool TryTruck( ArticulatedBoard board, int truck, ReadOnlySpan<RailSegment> rails, int surface,
		float friction, Span<CollisionContact> output, ref int count, out RailSegment captured )
	{
		captured = default;
		if ( count + 3 > output.Length )
			return false;
		var axle = board.Body( truck ).Position;
		var deck = board.Deck;
		var bestDistance = float.MaxValue;
		var bestPoint = Vector3.Zero;
		var bestNormal = Vector3.Zero;
		var bestSide = Vector3.Zero;
		var groove = Groove( board );
		var bestPlay = groove;
		foreach ( var rail in rails )
		{
			if ( !rail.IsValid )
				continue;
			var point = rail.Closest( axle, out var along );
			if ( along <= 0f || along >= rail.Length )
				continue;
			var direction = rail.Direction;
			// A deck turned across the rail slides on it; its trucks lie along the rail and do not grind.
			if ( MathF.Abs( Vector3.Dot( deck.Forward, direction ) ) < SlideAlong )
				continue;
			// A ledge's edge holds the hanger straight up off it, however the deck rolls: held along the deck's rolled up
			// instead (right on a rail, where the deck rides level), a board rolled over a ledge's top slid down its hanger
			// toward the low side, pressed its top-side wheel onto the top and hopped on the edge.
			Vector3 normal;
			if ( rail.Top.LengthSquared() > 0.25f ? !TryRailNormal( Vector3.UnitZ, direction, out normal )
				: !TryHangerNormal( deck, direction, out normal ) )
				continue;
			var offset = axle - point;
			var height = Vector3.Dot( offset, normal );
			var side = Vector3.Normalize( Vector3.Cross( direction, normal ) );
			var lateral = Vector3.Dot( offset, side );
			if ( height < -CaptureDepth || height > CaptureHeight || MathF.Abs( lateral ) > groove )
				continue;
			var distance = MathF.Abs( height ) + MathF.Abs( lateral );
			if ( distance >= bestDistance )
				continue;
			bestDistance = distance;
			bestPoint = point;
			bestNormal = normal;
			bestSide = side;
			bestPlay = Play( deck, side, rail.HalfWidth, groove );
			captured = rail;
		}
		if ( bestDistance == float.MaxValue )
			return false;
		var railDirection = captured.Direction;
		output[ count++ ] = Contact( truck, surface, axle, bestPoint + bestNormal * Hanger( board ), bestNormal,
			railDirection, friction );
		// The wheels on either side of the hanger keep the whole rail, both its sides, between them.
		output[ count++ ] = Contact( truck, surface, axle, bestPoint - bestSide * bestPlay, bestSide,
			railDirection, 0f );
		output[ count++ ] = Contact( truck, surface, axle, bestPoint + bestSide * bestPlay, -bestSide,
			railDirection, 0f );
		ClearWheels( board, truck, surface, bestPoint, captured.HalfWidth, railDirection, output, ref count );
		return true;
	}

	/// <summary>Distance (metres) within which a grinding truck's wheel near the rail's side is kept off it.</summary>
	private const float WheelReach = 0.01f;

	/// <summary>Gap (metres) a grinding truck's wheels keep from the rail's sides, so they never touch it: a wheel rubbing
	/// the rail's side drags at it and tips the board over the grinding truck.</summary>
	internal const float WheelGap = 0.006f;

	// The truck's wheels stay off the rail's sides: each keeps its whole round clear of the side it hangs beside, however
	// the truck is turned or rolled over the rail, so a wheel never rides up the rail's edge (and lifts the truck off it).
	// A truck turned so far that its wheels cannot straddle the rail rides on it instead. The wheels are held off level
	// across the rail: held off square to a rolled hanger, the low wheel would be pressed down and roll the deck further.
	private static void ClearWheels( ArticulatedBoard board, int truck, int surface, Vector3 point,
		float halfWidth, Vector3 direction, Span<CollisionContact> output, ref int count )
	{
		var across = Vector3.Cross( RailUp( board.Deck.Up, direction ), direction );
		if ( across.LengthSquared() < 0.0001f )
			return;
		var side = Vector3.Normalize( across );
		var axle = board.Body( truck ).Position;
		var clear = halfWidth + board.WheelRadius + WheelGap;
		var span = MathF.Abs( Vector3.Dot( board.Body( truck * 2 + 1 ).Position - board.Body( truck * 2 + 2 ).Position, side ) );
		if ( span < 2f * clear )
			return;
		for ( var wheel = truck * 2 + 1; wheel <= truck * 2 + 2 && count < output.Length; wheel++ )
		{
			var centre = board.Body( wheel ).Position;
			var outward = MathF.Sign( Vector3.Dot( centre - axle, side ) );
			if ( outward == 0f || outward * Vector3.Dot( centre - point, side ) > clear + WheelReach )
				continue;
			// Held through the truck at the wheel's centre: a contact on the wheel itself would read as the wheel rolling on
			// the rail's side, a support that tips the board over.
			output[ count++ ] = Contact( truck, surface, centre, point + side * (outward * clear), side * outward, direction, 0f );
		}
	}

	/// <summary>
	/// How far (metres, across the rail) a grinding truck's axle may sit off the rail's line with the whole rail still
	/// between its wheels: the inner wheel faces, along the hanger, reach across the rail by the groove times how square
	/// the hanger lies to it, less the rail's own half width. A truck turned off the rail's line (a crook) has less room.
	/// </summary>
	internal static float Play( BoardBody deck, Vector3 side, float halfWidth ) => Play( deck, side, halfWidth, GrooveHalfWidth );

	/// <summary>As <see cref="Play(BoardBody, Vector3, float)"/>, for wheels whose inner faces are 2 <paramref name="groove"/>
	/// apart.</summary>
	internal static float Play( BoardBody deck, Vector3 side, float halfWidth, float groove )
	{
		var hanger = Vector3.Cross( deck.Forward, deck.Up );
		var square = hanger.LengthSquared() > 0.0001f ? MathF.Abs( Vector3.Dot( Vector3.Normalize( hanger ), side ) ) : 1f;
		return MathF.Max( groove * square - halfWidth, LeastPlay );
	}

	private static bool TryDeck( BoardBody deck, SkateSettings geometry, ReadOnlySpan<RailSegment> rails, int surface,
		float friction, Span<CollisionContact> output, ref int count, out RailSegment captured, out float along )
	{
		captured = default;
		along = 0f;
		if ( count >= output.Length )
			return false;
		var bottom = -deck.Up * (geometry.DeckThickness * 0.5f);
		var nose = deck.Position + deck.Forward * (geometry.DeckLength * 0.5f) + bottom;
		var tail = deck.Position - deck.Forward * (geometry.DeckLength * 0.5f) + bottom;
		foreach ( var rail in rails )
		{
			if ( !rail.IsValid )
				continue;
			var direction = rail.Direction;
			// Slides need the deck across the rail; a board along it rides on its trucks.
			if ( MathF.Abs( Vector3.Dot( deck.Forward, direction ) ) > SlideAlong )
				continue;
			if ( !TryRailNormal( deck.Up, direction, out var normal ) )
				continue;
			if ( !ClosestBetween( tail, nose, rail.Start, rail.End, out var onDeck, out var onRail ) )
				continue;
			var height = Vector3.Dot( onDeck - onRail, normal );
			if ( height < -CaptureDepth || height > CaptureHeight )
				continue;
			captured = rail;
			along = Vector3.Dot( onDeck - deck.Position, deck.Forward );
			output[ count++ ] = Contact( 0, surface, onDeck, onRail, normal, direction, friction );
			return true;
		}
		return false;
	}

	// Only a board riding upright over the rail can rest on it.
	// A hanger crossing the rail touches it along their common perpendicular, so tipping the deck toward its nose or tail
	// does not tilt the contact, even on a truck turned off the rail's line; a hanger nearly along the rail falls back
	// to the deck's up.
	private static bool TryHangerNormal( BoardBody deck, Vector3 direction, out Vector3 normal )
	{
		var hanger = Vector3.Cross( deck.Forward, deck.Up );
		normal = Vector3.Cross( hanger, direction );
		if ( normal.LengthSquared() < 0.25f )
			return TryRailNormal( deck.Up, direction, out normal );
		normal = Vector3.Normalize( normal );
		if ( Vector3.Dot( normal, deck.Up ) < 0f )
			normal = -normal;
		return normal.Z >= 0.3f;
	}

	private static bool TryRailNormal( Vector3 up, Vector3 direction, out Vector3 normal )
	{
		normal = up - direction * Vector3.Dot( up, direction );
		if ( normal.LengthSquared() < 0.25f )
			return false;
		normal = Vector3.Normalize( normal );
		return normal.Z >= 0.3f;
	}

	private static CollisionContact Contact( int body, int surface, Vector3 point, Vector3 railPoint,
		Vector3 normal, Vector3 direction, float friction )
	{
		var tangent = Vector3.Normalize( direction - normal * Vector3.Dot( direction, normal ) );
		return new CollisionContact( body, surface, point, railPoint, normal, tangent,
			Vector3.Cross( normal, tangent ), friction, friction, 0f ).WithProjectedSurfacePoint();
	}

	private static Vector3 RailUp( Vector3 up, Vector3 direction )
	{
		var world = Vector3.UnitZ - direction * direction.Z;
		return world.LengthSquared() > 0.0001f ? Vector3.Normalize( world ) : up;
	}

	// Closest points between two segments; false when either lies at an end of the deck or
	// rail, because the board is then leaving it.
	private static bool ClosestBetween( Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1,
		out Vector3 onA, out Vector3 onB )
	{
		var u = a1 - a0;
		var v = b1 - b0;
		var w = a0 - b0;
		var a = Vector3.Dot( u, u );
		var b = Vector3.Dot( u, v );
		var c = Vector3.Dot( v, v );
		var d = Vector3.Dot( u, w );
		var e = Vector3.Dot( v, w );
		var denominator = a * c - b * b;
		onA = onB = default;
		if ( denominator < 0.000001f )
			return false;
		var s = (b * e - c * d) / denominator;
		var t = (a * e - b * d) / denominator;
		if ( s <= 0f || s >= 1f || t <= 0f || t >= 1f )
			return false;
		onA = a0 + u * s;
		onB = b0 + v * t;
		return true;
	}
}
