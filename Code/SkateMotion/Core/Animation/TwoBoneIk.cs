using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// Moves the end of a two-bone chain (thigh, shin, ankle) to a target in model space, keeping the chain's bend
/// plane and the end bone's orientation. Every descendant bone follows the bone it hangs from.
/// </summary>
internal sealed class TwoBoneIk
{
	private readonly int _upper;
	private readonly int _lower;
	private readonly int _end;
	private readonly int[] _upperOnly;
	private readonly int[] _lowerOnly;
	private readonly int[] _endTree;

	/// <summary>The first bone (hip or shoulder).</summary>
	internal int Upper => _upper;

	/// <summary>The middle bone (knee or elbow).</summary>
	internal int Lower => _lower;

	internal TwoBoneIk( int[] parents, int upper, int lower, int end )
	{
		_upper = upper;
		_lower = lower;
		_end = end;
		var upperTree = new List<int>();
		var lowerTree = new List<int>();
		var endTree = new List<int>();
		for ( var bone = 0; bone < parents.Length; bone++ )
		{
			if ( bone == lower || bone == end )
				continue;
			if ( Under( parents, bone, end ) ) endTree.Add( bone );
			else if ( Under( parents, bone, lower ) ) lowerTree.Add( bone );
			else if ( Under( parents, bone, upper ) ) upperTree.Add( bone );
		}
		_upperOnly = upperTree.ToArray();
		_lowerOnly = lowerTree.ToArray();
		_endTree = endTree.ToArray();
	}

	/// <param name="positions">Model-space positions, updated in place.</param>
	/// <param name="rotations">Model-space rotations, updated in place.</param>
	/// <param name="target">Where the end bone should go.</param>
	/// <param name="hint">Direction the knee should point (e.g. the toes' direction); keeps a nearly straight
	/// leg from flipping its knee to the other side. Zero keeps the current bend.</param>
	/// <param name="carryEnd">The end bone (a foot) turns with the lower bone, keeping its angle to it, instead of keeping
	/// its orientation: a foot moved out of something it has no surface to stand on keeps its ankle's angle.</param>
	internal void Solve( Span<Vector3> positions, Span<Quaternion> rotations, Vector3 target, Vector3 hint = default,
		bool carryEnd = false ) => Solve( positions, rotations, target, hint, carryEnd ? 1f : 0f );

	/// <summary>As above, the end bone (the foot) turning with the lower bone by <paramref name="carryEnd"/> (0 to 1):
	/// switched between turning and not in one frame (a foot on a rail turning with the shin, off it not), the ankle
	/// opened or closed by the shin's whole swing at once.</summary>
	internal void Solve( Span<Vector3> positions, Span<Quaternion> rotations, Vector3 target, Vector3 hint, float carryEnd )
	{
		var hip = positions[ _upper ];
		var knee = positions[ _lower ];
		var ankle = positions[ _end ];
		var thigh = Vector3.Distance( hip, knee );
		var shin = Vector3.Distance( knee, ankle );
		var reach = target - hip;
		var reachLength = reach.Length();
		if ( !float.IsFinite( thigh + shin + reachLength ) || reachLength < 0.0001f || thigh < 0.001f || shin < 0.001f )
			return;
		var distance = Math.Clamp( reachLength, Math.Abs( thigh - shin ) + 0.001f, thigh + shin - 0.001f );
		var direction = reach / reachLength;
		// The knee bends toward its offset from the hip-ankle line; when the leg is nearly straight that offset
		// is noise and the hint decides, and a knee pointing against the hint is turned back to it.
		var line = ankle - hip;
		line = line.LengthSquared() > 1e-8f ? Vector3.Normalize( line ) : direction;
		var pole = (knee - hip) - line * Vector3.Dot( knee - hip, line );
		if ( hint != Vector3.Zero )
		{
			var wanted = hint - direction * Vector3.Dot( hint, direction );
			if ( wanted.LengthSquared() > 1e-8f )
				pole = BlendHint( pole, wanted, thigh );
		}
		var bend = Vector3.Cross( pole, direction );
		if ( bend.LengthSquared() < 0.000001f )
			bend = Vector3.Cross( knee - hip, ankle - hip );
		if ( bend.LengthSquared() < 0.000001f )
			bend = Vector3.Cross( direction, MathF.Abs( direction.Z ) < 0.9f ? Vector3.UnitZ : Vector3.UnitY );
		// The bend axis must be perpendicular to the new reach; rotating the reach about it by minus the hip angle
		// puts the knee on the side it was bent toward.
		var axis = Vector3.Normalize( bend - direction * Vector3.Dot( bend, direction ) );
		var cosine = Math.Clamp( (thigh * thigh + distance * distance - shin * shin) / (2f * thigh * distance), -1f, 1f );
		var newKnee = hip + Vector3.Transform( direction, Quaternion.CreateFromAxisAngle( axis, -MathF.Acos( cosine ) ) ) * thigh;
		var newAnkle = hip + direction * distance;

		var upperTurn = Between( knee - hip, newKnee - hip );
		var lowerTurn = Between( Vector3.Transform( ankle - knee, upperTurn ), newAnkle - newKnee ) * upperTurn;
		Rotate( positions, rotations, _upper, hip, hip, upperTurn );
		foreach ( var bone in _upperOnly )
			Rotate( positions, rotations, bone, hip, hip, upperTurn );
		Rotate( positions, rotations, _lower, knee, newKnee, lowerTurn );
		foreach ( var bone in _lowerOnly )
			Rotate( positions, rotations, bone, knee, newKnee, lowerTurn );
		if ( carryEnd > 0f )
		{
			var endTurn = carryEnd >= 1f ? lowerTurn : Quaternion.Slerp( Quaternion.Identity, lowerTurn, carryEnd );
			Rotate( positions, rotations, _end, ankle, newAnkle, endTurn );
			foreach ( var bone in _endTree )
				Rotate( positions, rotations, bone, ankle, newAnkle, endTurn );
			return;
		}
		var shift = newAnkle - ankle;
		positions[ _end ] = newAnkle;
		foreach ( var bone in _endTree )
			positions[ bone ] += shift;
	}

	/// <summary>Turns the end bone (a hand) and everything hanging from it about the end's own place.</summary>
	internal void TurnEnd( Span<Vector3> positions, Span<Quaternion> rotations, Quaternion turn )
	{
		var pivot = positions[ _end ];
		rotations[ _end ] = Quaternion.Normalize( turn * rotations[ _end ] );
		foreach ( var bone in _endTree )
			Rotate( positions, rotations, bone, pivot, pivot, turn );
	}

	/// <summary>
	/// Keeps the middle bone (the elbow) within <paramref name="most"/> radians of <paramref name="before"/>, a direction it
	/// pointed from the line between the first bone and the end a moment ago, turning it back round that line if it has
	/// swung further: the end and everything hanging from it stay as they are. Returns the direction the middle bone
	/// points from that line now, or zero when the chain is too straight to say.
	/// </summary>
	internal Vector3 LimitSwing( Span<Vector3> positions, Span<Quaternion> rotations, Vector3 before, float most )
	{
		var hip = positions[ _upper ];
		var line = positions[ _end ] - hip;
		if ( line.LengthSquared() < 1e-8f )
			return Vector3.Zero;
		line = Vector3.Normalize( line );
		var now = Across( positions[ _lower ] - hip, line );
		var was = Across( before, line );
		if ( now == Vector3.Zero || was == Vector3.Zero )
			return now;
		var angle = MathF.Atan2( Vector3.Dot( Vector3.Cross( was, now ), line ), Vector3.Dot( was, now ) );
		if ( MathF.Abs( angle ) <= most )
			return now;
		var back = Quaternion.CreateFromAxisAngle( line, MathF.Sign( angle ) * most - angle );
		Rotate( positions, rotations, _upper, hip, hip, back );
		foreach ( var bone in _upperOnly )
			Rotate( positions, rotations, bone, hip, hip, back );
		Rotate( positions, rotations, _lower, hip, hip, back );
		foreach ( var bone in _lowerOnly )
			Rotate( positions, rotations, bone, hip, hip, back );
		return Across( positions[ _lower ] - hip, line );
	}

	// The unit direction of a vector across a line (zero when it lies along it).
	private static Vector3 Across( Vector3 value, Vector3 line )
	{
		var across = value - line * Vector3.Dot( value, line );
		return across.LengthSquared() > 1e-8f ? Vector3.Normalize( across ) : Vector3.Zero;
	}

	/// <summary>
	/// Opens the joint at the end (an ankle) to at least <paramref name="least"/> radians between the lower bone and
	/// <paramref name="tip"/> (the ball of the foot, a bone under the end), by <paramref name="weight"/> of what it lacks:
	/// the end bone and everything under it turn about the end, away from the lower bone. A foot moved down onto a deck
	/// lower than the clip's, its shin swung forward over it, would otherwise fold its ankle shut; it tips onto its toes
	/// instead. Returns the turn given (radians).
	/// </summary>
	internal float OpenEnd( Span<Vector3> positions, Span<Quaternion> rotations, int tip, float least, float weight = 1f )
	{
		var end = positions[ _end ];
		var lower = positions[ _lower ] - end;
		var toward = positions[ tip ] - end;
		if ( !(lower.LengthSquared() > 1e-8f) || !(toward.LengthSquared() > 1e-8f) )
			return 0f;
		var angle = MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Normalize( lower ), Vector3.Normalize( toward ) ), -1f, 1f ) );
		var missing = (least - angle) * Math.Clamp( weight, 0f, 1f );
		var axis = Vector3.Cross( lower, toward );
		if ( !(missing > 0f) || !(axis.LengthSquared() > 1e-12f) )
			return 0f;
		var turn = Quaternion.CreateFromAxisAngle( Vector3.Normalize( axis ), missing );
		Rotate( positions, rotations, _end, end, end, turn );
		foreach ( var bone in _endTree )
			Rotate( positions, rotations, bone, end, end, turn );
		return missing;
	}

	/// <summary>The angle (radians) at the end joint between the lower bone and <paramref name="tip"/>.</summary>
	internal float EndAngle( ReadOnlySpan<Vector3> positions, int tip )
	{
		var lower = positions[ _lower ] - positions[ _end ];
		var toward = positions[ tip ] - positions[ _end ];
		if ( !(lower.LengthSquared() > 1e-8f) || !(toward.LengthSquared() > 1e-8f) )
			return MathF.PI;
		return MathF.Acos( Math.Clamp( Vector3.Dot( Vector3.Normalize( lower ), Vector3.Normalize( toward ) ), -1f, 1f ) );
	}

	// A hard switch at a straightness threshold moves the knee to a different plane in one frame.
	// Gradually trust the animated bend as it becomes well defined; use the hint when it is ambiguous.
	private static Vector3 BlendHint( Vector3 pole, Vector3 hint, float thigh )
	{
		var length = pole.Length();
		var wanted = Vector3.Normalize( hint );
		if ( length < 0.0001f )
			return wanted;
		var current = pole / length;
		var confidence = Math.Clamp( length / (0.15f * thigh), 0f, 1f );
		confidence *= confidence * (3f - 2f * confidence);
		var weight = MathF.Max( 1f - confidence, -Vector3.Dot( current, wanted ) );
		return Vector3.Lerp( current, wanted, weight );
	}

	private static void Rotate( Span<Vector3> positions, Span<Quaternion> rotations, int bone, Vector3 pivot,
		Vector3 newPivot, Quaternion turn )
	{
		positions[ bone ] = newPivot + Vector3.Transform( positions[ bone ] - pivot, turn );
		rotations[ bone ] = Quaternion.Normalize( turn * rotations[ bone ] );
	}

	private static Quaternion Between( Vector3 from, Vector3 to )
	{
		from = Vector3.Normalize( from );
		to = Vector3.Normalize( to );
		var dot = Vector3.Dot( from, to );
		if ( dot > 0.99999f )
			return Quaternion.Identity;
		var axis = Vector3.Cross( from, to );
		if ( axis.LengthSquared() < 0.000001f )
			axis = Math.Abs( from.X ) < 0.9f ? Vector3.Cross( from, Vector3.UnitX ) : Vector3.Cross( from, Vector3.UnitY );
		return Quaternion.CreateFromAxisAngle( Vector3.Normalize( axis ), MathF.Acos( Math.Clamp( dot, -1f, 1f ) ) );
	}

	private static bool Under( int[] parents, int bone, int ancestor )
	{
		for ( bone = parents[ bone ]; bone >= 0; bone = parents[ bone ] )
		{
			if ( bone == ancestor )
				return true;
		}
		return false;
	}
}
