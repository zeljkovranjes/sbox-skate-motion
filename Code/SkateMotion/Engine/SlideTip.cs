using System.Collections.Generic;
using Sandbox;
using SkateMotion.Core.Grinding;

namespace SkateMotion.Engine;

/// <summary>
/// How a nose or tail slide's board is shown: pitched up off the rail toward its middle, the end on the rail lower than
/// the rest, as the slides' clips hold it. The physical board slides flat across the rail; the shown one turns about the
/// rail's edge it rests on (or its own end, where that comes first), so its end stays on the rail and the rest lifts
/// clear of it.
/// </summary>
internal static class SlideTip
{
	/// <summary>Pitch of a nose or tail slide (degrees): the original's tip slide angle.</summary>
	internal const float Pitch = 20f;

	/// <summary>Pitch of a blunt whose middle lies over a ledge's top (degrees): the original's backslash angle.</summary>
	internal const float BackslashPitch = 42f;

	/// <summary>
	/// The point (world inches) the shown deck turns about and the axis (along the rail) that lifts its middle when turned
	/// by a positive angle; null when no rail lies under the deck's <paramref name="end"/> (+1 the nose, -1 the tail).
	/// </summary>
	/// <param name="deck">The deck as shown, before any pitch.</param>
	/// <param name="bounds">The deck model's box in the deck's frame (inches).</param>
	internal static (Vector3 Pivot, Vector3 Axis)? Find( Transform deck, BBox bounds, IReadOnlyList<RailSolid> rails, int end )
	{
		(Vector3 Pivot, Vector3 Axis)? best = null;
		var closest = float.MaxValue;
		var forward = deck.Rotation.Forward;
		foreach ( var rail in rails )
		{
			var along = SkateUnits.ToEngine( System.Numerics.Vector3.Transform( System.Numerics.Vector3.UnitX, rail.Rotation ) );
			var up = SkateUnits.ToEngine( rail.Up );
			var across = up.Cross( along ).Normal;
			var top = SkateUnits.Inches( rail.Centre ) + up * (rail.Half.Z * SkateUnits.InchesPerMetre);
			var halfWidth = rail.Half.Y * SkateUnits.InchesPerMetre;
			// Where the deck's line crosses the rail's middle line, along the deck from its centre.
			var slope = forward.Dot( across );
			if ( MathF.Abs( slope ) < 0.3f )
				continue;
			var crossing = -(deck.Position - top).Dot( across ) / slope;
			if ( crossing * end <= 0f )
				continue;
			// Beyond the rail's end the rail is not under the deck.
			var point = deck.Position + forward * crossing;
			if ( MathF.Abs( (point - top).Dot( along ) ) > rail.Half.X * SkateUnits.InchesPerMetre )
				continue;
			var height = MathF.Abs( (point - top).Dot( up ) );
			if ( height > closest )
				continue;
			// The edge of the rail on the end's side, or the deck's own end where that comes first.
			var edge = crossing + end * halfWidth / MathF.Abs( slope );
			var tip = end > 0 ? bounds.Maxs.x : bounds.Mins.x;
			var at = end > 0 ? MathF.Min( edge, tip ) : MathF.Max( edge, tip );
			var pivot = deck.PointToWorld( new Vector3( at, 0f, bounds.Mins.z ) );
			// Turned about the rail's direction, the middle (the deck's centre) must rise.
			var axis = along;
			if ( axis.Cross( deck.Position - pivot ).Dot( up ) < 0f )
				axis = -axis;
			closest = height;
			best = (pivot, axis);
		}
		return best;
	}

	/// <summary>A pose turned by <paramref name="turn"/> about <paramref name="pivot"/>.</summary>
	internal static Transform About( Transform pose, Vector3 pivot, Rotation turn ) =>
		new( pivot + turn * (pose.Position - pivot), turn * pose.Rotation, pose.Scale );
}
