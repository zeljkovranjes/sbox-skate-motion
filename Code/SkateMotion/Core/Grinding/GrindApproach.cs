namespace SkateMotion.Core.Grinding;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// The way the deck's right faced as the board came onto a rail, which names the grind's sides (see
/// <see cref="GrindNames"/>): flat and square across the nearest rail, taken while the deck still lies along the rail's
/// line. A board turned onto the rail (a quarter turn in the air into a boardslide) keeps the right it had before the
/// turn, so the side of the rail its nose ends up on follows the way it turned, whether it locks on a little short of
/// square or a little past it. Turned back along the line (a half turn), it takes the right it has then.
/// </summary>
internal sealed class GrindApproach
{
	/// <summary>Turn off the rail's line, in radians, up to which the deck counts as still coming along it.</summary>
	internal const float AlongAngle = MathF.PI / 4f;

	/// <summary>The deck's right as the board came onto the rail (flat, unit), or zero before any step.</summary>
	internal Vector3 Right { get; private set; }

	/// <summary>Follows the deck off the rail, each step.</summary>
	/// <param name="forward">The deck's forward.</param>
	/// <param name="right">The deck's right.</param>
	/// <param name="rail">The nearest rail's direction, or null with none near.</param>
	internal void Track( Vector3 forward, Vector3 right, Vector3? rail )
	{
		var deckRight = Flat( right );
		if ( deckRight == Vector3.Zero )
			return;
		var along = rail is { } line ? Flat( line ) : Vector3.Zero;
		var heading = Flat( forward );
		if ( along == Vector3.Zero || heading == Vector3.Zero )
		{
			Right = deckRight;
			return;
		}
		var across = new Vector3( -along.Y, along.X, 0f );
		if ( MathF.Abs( Vector3.Dot( heading, along ) ) < MathF.Cos( AlongAngle ) )
		{
			// Turned across the rail: the right it came along the line with stands. Never along this rail's line (coming
			// straight at it, or from another rail), the deck's own right.
			if ( MathF.Abs( Vector3.Dot( Right, across ) ) < MathF.Cos( AlongAngle ) )
				Right = deckRight;
			return;
		}
		Right = Vector3.Dot( across, deckRight ) >= 0f ? across : -across;
	}

	/// <summary>How far across the rail the nose must reach from the deck's middle (metres) to say which side it is on.</summary>
	internal const float NoseClear = 0.02f;

	/// <summary>How far an offset (such as from the deck to the rail) lies to the approach's right, in its own units.</summary>
	internal float Across( Vector3 offset ) => Vector3.Dot( offset, Right );

	/// <summary>
	/// Which side of the rail the deck's nose lies on, across the approach: true on its right, false on its left, null while
	/// the deck lies too near the rail's line to tell. Taken from the way the nose points from the deck's middle: on a
	/// noseslide the nose's tip sits about on the rail, where its own offset cannot tell a noseslide from a noseblunt.
	/// </summary>
	internal bool? NoseSide( Vector3 forward )
	{
		var across = Across( forward ) * GrindShape.NoseReach;
		return MathF.Abs( across ) > NoseClear ? across > 0f : null;
	}

	internal void Reset() => Right = Vector3.Zero;

	private static Vector3 Flat( Vector3 v )
	{
		var flat = new Vector3( v.X, v.Y, 0f );
		var length = flat.LengthSquared();
		return float.IsFinite( length ) && length >= 1e-8f ? Vector3.Normalize( flat ) : Vector3.Zero;
	}
}
