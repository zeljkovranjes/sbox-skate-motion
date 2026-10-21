namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A board's size for fitting the clips' feet and hands onto it (metres): wheelbase, length nose to tail, width and deck
/// thickness. <see cref="Foot"/> and <see cref="Hand"/> move a point given against the clips' board (in its frame: +X
/// along it, +Y left, +Z up, origin in the deck's middle) onto the same place on this one. Along the board the clips'
/// trucks land on these trucks and their ends on these ends, linearly between; across it their edges land on these
/// edges; up and down a point keeps its height over the deck's top or under its underside. A rider's stance stretches
/// only so far, so feet follow the trucks within <see cref="LeastStance"/> to <see cref="MostStance"/> of the clips'
/// spacing: on a longboard the rider stands between its trucks.
/// </summary>
internal readonly record struct BoardFit( float Wheelbase, float Length, float Width, float Thickness )
{
	/// <summary>The board the clips were made on, when their graph does not say.</summary>
	internal static BoardFit Default => new( 0.486f, 0.902f, 0.24f, 0.015f );

	/// <summary>Least and most share of the clips' stance a rider's feet stand at.</summary>
	internal const float LeastStance = 0.8f;
	internal const float MostStance = 1.25f;

	/// <summary>Points further than this (metres) off the clips' deck are not on the board and are left where they are.</summary>
	internal const float Reach = 0.08f;

	/// <summary>The clips' board scaled with the rider, as the clips' positions are.</summary>
	internal BoardFit Scaled( float scale ) => new( Wheelbase * scale, Length * scale, Width * scale, Thickness * scale );

	/// <summary>
	/// A foot's point on <paramref name="clips"/> (already scaled with the rider) on this board. <paramref name="edge"/>,
	/// when given, is the deck's half width at a place along it (the fitted place) and side of it (the sign of the clips'
	/// point across): where the deck is narrower there than the stance (a tapered nose or tail), its edges there are the
	/// edges a foot is fitted across to, so it stays on the deck.
	/// </summary>
	internal Vector3 Foot( Vector3 point, BoardFit clips, Func<float, float, float?> edge = null )
	{
		var halfBase = Math.Clamp( Wheelbase, clips.Wheelbase * LeastStance, clips.Wheelbase * MostStance ) * 0.5f;
		var halfWidth = Math.Clamp( Width, clips.Width * LeastStance, clips.Width * MostStance ) * 0.5f;
		var foot = Fit( point, clips, halfBase, MathF.Max( Length * 0.5f, halfBase ), halfWidth );
		if ( edge?.Invoke( foot.X, point.Y ) is { } here && here < halfWidth )
			foot.Y = Stretch( point.Y, clips.Width * 0.5f, clips.Width * 0.5f, here, here );
		return foot;
	}

	/// <summary>
	/// A foot's point beside the deck of <paramref name="clips"/> (a foot down on the ground, pushing) beside this deck: as
	/// far past this deck's edge as it was past the clips', easing in over <paramref name="reach"/> past the edge from
	/// where <see cref="Foot"/> puts it. On a wider deck the pushing foot steps further out, rather than under the deck;
	/// beside a deck narrower there than at its widest (<paramref name="edge"/>, as for <see cref="Foot"/>), past its edge
	/// there.
	/// </summary>
	internal Vector3 Beside( Vector3 point, BoardFit clips, float reach, Func<float, float, float?> edge = null )
	{
		var foot = Foot( point, clips, edge );
		var past = MathF.Abs( point.Y ) - clips.Width * 0.5f;
		if ( past <= 0f || !(reach > 0f) )
			return foot;
		var share = Math.Clamp( past / reach, 0f, 1f );
		share *= share * (3f - 2f * share);
		var deckEdge = Width * 0.5f;
		if ( edge?.Invoke( foot.X, point.Y ) is { } here && here < deckEdge )
			deckEdge = here;
		var beside = MathF.CopySign( deckEdge + past, point.Y );
		return foot with { Y = foot.Y + (beside - foot.Y) * share };
	}

	/// <summary>A hand's point on <paramref name="clips"/> (already scaled with the rider) on this board.</summary>
	internal Vector3 Hand( Vector3 point, BoardFit clips ) => Fit( point, clips, Wheelbase * 0.5f, Length * 0.5f, Width * 0.5f );

	/// <summary>How far (metres) a point is off the clips' deck: zero on or inside it.</summary>
	internal static float OffDeck( Vector3 point, BoardFit clips )
	{
		var outside = new Vector3( MathF.Max( 0f, MathF.Abs( point.X ) - clips.Length * 0.5f ),
			MathF.Max( 0f, MathF.Abs( point.Y ) - clips.Width * 0.5f ), MathF.Max( 0f, MathF.Abs( point.Z ) - clips.Thickness * 0.5f ) );
		return outside.Length();
	}

	private Vector3 Fit( Vector3 point, BoardFit clips, float halfBase, float halfLength, float halfWidth ) => new(
		Stretch( point.X, clips.Wheelbase * 0.5f, clips.Length * 0.5f, halfBase, halfLength ),
		Stretch( point.Y, clips.Width * 0.5f, clips.Width * 0.5f, halfWidth, halfWidth ),
		Stretch( point.Z, clips.Thickness * 0.5f, clips.Thickness * 0.5f, Thickness * 0.5f, Thickness * 0.5f ) );

	// One axis, symmetric about the middle: the inner station (a truck, or an edge) maps onto the inner station, the outer
	// (an end) onto the outer, linearly between; past the outer a point keeps its distance beyond it.
	private static float Stretch( float value, float inner, float outer, float toInner, float toOuter )
	{
		var size = MathF.Abs( value );
		float fitted;
		if ( size <= inner )
			fitted = inner > 1e-6f ? size * toInner / inner : size;
		else if ( size <= outer )
			fitted = toInner + (size - inner) * (toOuter - toInner) / MathF.Max( outer - inner, 1e-6f );
		else
			fitted = toOuter + (size - outer);
		return MathF.CopySign( fitted, value );
	}
}
