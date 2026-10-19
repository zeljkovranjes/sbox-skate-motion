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

	/// <summary>Half the gap between a truck's inner wheel faces; the rail stays inside it.</summary>
	internal const float GrooveHalfWidth = 0.06f;

	/// <summary>Hanger radius around the axle line that touches the rail.</summary>
	internal const float AxleRadius = 0.008f;

	private const float DeckHalfLength = 0.4f;
	private const float DeckHalfThickness = 0.0125f;

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
			return new( kind, rail.Direction, RailUp( deck.Up, rail.Direction ), rail.Closest( deck.Position, out _ ) );
		}
		return TryDeck( deck, rails, surface, friction, output, ref count, out var slideRail )
			? new( GrindKind.Boardslide, slideRail.Direction, RailUp( deck.Up, slideRail.Direction ), slideRail.Closest( deck.Position, out _ ) )
			: default;
	}

	private static bool TryTruck( ArticulatedBoard board, int truck, ReadOnlySpan<RailSegment> rails, int surface,
		float friction, Span<CollisionContact> output, ref int count, out RailSegment captured )
	{
		captured = default;
		if ( count + 3 > output.Length )
			return false;
		var axle = board.Body( truck ).Position;
		var up = board.Deck.Up;
		var bestDistance = float.MaxValue;
		var bestPoint = Vector3.Zero;
		var bestNormal = Vector3.Zero;
		var bestSide = Vector3.Zero;
		foreach ( var rail in rails )
		{
			if ( !rail.IsValid )
				continue;
			var point = rail.Closest( axle, out var along );
			if ( along <= 0f || along >= rail.Length )
				continue;
			var direction = rail.Direction;
			if ( !TryRailNormal( up, direction, out var normal ) )
				continue;
			var offset = axle - point;
			var height = Vector3.Dot( offset, normal );
			var side = Vector3.Normalize( Vector3.Cross( direction, normal ) );
			var lateral = Vector3.Dot( offset, side );
			if ( height < -CaptureDepth || height > CaptureHeight || MathF.Abs( lateral ) > GrooveHalfWidth )
				continue;
			var distance = MathF.Abs( height ) + MathF.Abs( lateral );
			if ( distance >= bestDistance )
				continue;
			bestDistance = distance;
			bestPoint = point;
			bestNormal = normal;
			bestSide = side;
			captured = rail;
		}
		if ( bestDistance == float.MaxValue )
			return false;
		var railDirection = captured.Direction;
		output[ count++ ] = Contact( truck, surface, axle, bestPoint + bestNormal * AxleRadius, bestNormal,
			railDirection, friction );
		// The wheels on either side of the hanger keep the rail between them.
		output[ count++ ] = Contact( truck, surface, axle, bestPoint - bestSide * GrooveHalfWidth, bestSide,
			railDirection, 0f );
		output[ count++ ] = Contact( truck, surface, axle, bestPoint + bestSide * GrooveHalfWidth, -bestSide,
			railDirection, 0f );
		return true;
	}

	private static bool TryDeck( BoardBody deck, ReadOnlySpan<RailSegment> rails, int surface,
		float friction, Span<CollisionContact> output, ref int count, out RailSegment captured )
	{
		captured = default;
		if ( count >= output.Length )
			return false;
		var bottom = -deck.Up * DeckHalfThickness;
		var nose = deck.Position + deck.Forward * DeckHalfLength + bottom;
		var tail = deck.Position - deck.Forward * DeckHalfLength + bottom;
		foreach ( var rail in rails )
		{
			if ( !rail.IsValid )
				continue;
			var direction = rail.Direction;
			// Slides need the deck across the rail; a board along it rides on its trucks.
			if ( MathF.Abs( Vector3.Dot( deck.Forward, direction ) ) > 0.6f )
				continue;
			if ( !TryRailNormal( deck.Up, direction, out var normal ) )
				continue;
			if ( !ClosestBetween( tail, nose, rail.Start, rail.End, out var onDeck, out var onRail ) )
				continue;
			var height = Vector3.Dot( onDeck - onRail, normal );
			if ( height < -CaptureDepth || height > CaptureHeight )
				continue;
			captured = rail;
			output[ count++ ] = Contact( 0, surface, onDeck, onRail, normal, direction, friction );
			return true;
		}
		return false;
	}

	// Only a board riding upright over the rail can rest on it.
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
