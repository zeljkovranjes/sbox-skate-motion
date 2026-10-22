using Sandbox;
using SkateMotion.Core.Animation;

namespace SkateMotion.Engine;

using NumericsVector = System.Numerics.Vector3;
using NumericsRotation = System.Numerics.Quaternion;

/// <summary>
/// Fits the clips' board onto the board being ridden for the rider's animation (see <see cref="BoardFit"/>): a place
/// the clips give against their board (a foot, a hand, the grip on a held board) moves onto the same place on the real
/// board, sized from its measurements. Places are in the rider model's units and the clips' board's frame, as the
/// retargeter gives them; the clips' board is scaled with the rider, as their positions are.
/// </summary>
internal sealed class RiderBoardFit
{
	private readonly BoardFit _clips;
	private BoardFit _scaledClips;
	private BoardFit _ridden;
	private float _reach;
	private float _span;

	// How far (clip metres) a wrist is from what its fingers close on: a hand holding the deck has its wrist about this
	// far off it.
	private const float HandSpan = 0.1f;

	// The shown deck's outline: at stations evenly along its box, how far its surface reaches to its left and to its right
	// (inches, the shown deck's frame; negative where its middle has no surface), read again when the deck's box changes.
	private const int OutlineStations = 64;
	private const int EdgeSteps = 14;
	private BBox? _outlineBox;
	private float[] _outlineLeft;
	private float[] _outlineRight;
	private readonly Func<float, float, float?> _edge;
	private readonly Func<float, float, float?> _switchedEdge;

	internal RiderBoardFit( ClipBoardDefinition board )
	{
		var fallback = BoardFit.Default;
		_clips = new BoardFit( Pick( board?.Wheelbase, fallback.Wheelbase ), Pick( board?.Length, fallback.Length ),
			Pick( board?.Width, fallback.Width ), Pick( board?.Thickness, fallback.Thickness ) );
		_scaledClips = _ridden = _clips;
		// The shown stance turns the clips' board round on the deck (switch: their nose over the deck's tail).
		_edge = ( along, across ) => HalfWidth( along, across >= 0f );
		_switchedEdge = ( along, across ) => HalfWidth( -along, across < 0f );
	}

	/// <summary>Takes up this frame's board and the rider's scale (model units per clip metre).</summary>
	internal void Update( IBoardDisplay board, float scale )
	{
		_scaledClips = _clips.Scaled( scale );
		_reach = BoardFit.Reach * scale;
		_span = HandSpan * scale;
		_ridden = board?.Dimensions is { Wheelbase: > 0f, Length: > 0f, Width: > 0f } size
			? new BoardFit( size.Wheelbase, size.Length, size.Width, MathF.Max( size.Thickness, 0.001f ) ).Scaled( SkateUnits.InchesPerMetre )
			: _scaledClips;
		var box = board?.DeckBounds;
		if ( !Equals( box, _outlineBox ) )
			Outline( board, box );
	}

	/// <summary>
	/// Where a foot the clips put at <paramref name="point"/> on their board stands on the real one, within the deck's
	/// edges where it stands (<paramref name="switched"/>: the clips' board is shown turned round on the deck).
	/// </summary>
	internal NumericsVector Foot( NumericsVector point, bool switched = false ) =>
		_ridden.Foot( point, _scaledClips, Edge( switched ) );

	/// <summary>Where a foot the clips put at <paramref name="point"/> beside their board (down on the ground) is beside the
	/// real one: as far past its edge (see <see cref="BoardFit.Beside"/>).</summary>
	internal NumericsVector Beside( NumericsVector point, bool switched = false ) =>
		_ridden.Beside( point, _scaledClips, _reach, Edge( switched ) );

	/// <summary>Where a hand the clips put at <paramref name="point"/> on their board holds the real one.</summary>
	internal NumericsVector Hand( NumericsVector point ) => _ridden.Hand( point, _scaledClips );

	/// <summary>
	/// How fully a foot the clips put at <paramref name="point"/> stands on their board: 1 within its outline with the ball
	/// no higher over its top than <paramref name="ballHeight"/> (the ball's own height over the sole), easing to 0 within
	/// <see cref="BoardFit.Reach"/> further off or up.
	/// </summary>
	internal float Standing( NumericsVector point, float ballHeight )
	{
		var off = new NumericsVector( MathF.Max( 0f, MathF.Abs( point.X ) - _scaledClips.Length * 0.5f ),
			MathF.Max( 0f, MathF.Abs( point.Y ) - _scaledClips.Width * 0.5f ),
			MathF.Max( 0f, point.Z - _scaledClips.Thickness * 0.5f - ballHeight ) ).Length();
		var share = Math.Clamp( 1f - off / MathF.Max( _reach, 1e-4f ), 0f, 1f );
		return share * share * (3f - 2f * share);
	}

	/// <summary>How high the clips' deck top is over its middle (model units).</summary>
	internal float ClipsTop => _scaledClips.Thickness * 0.5f;

	/// <summary>How near a point is to the clips' deck: 1 on it, easing to 0 at <see cref="BoardFit.Reach"/> off it.</summary>
	internal float Nearness( NumericsVector point )
	{
		var share = Math.Clamp( 1f - BoardFit.OffDeck( point, _scaledClips ) / MathF.Max( _reach, 1e-4f ), 0f, 1f );
		return share * share * (3f - 2f * share);
	}

	/// <summary>
	/// How fully a hand the clips put with its wrist at <paramref name="wrist"/> holds their board: 1 within a palm's span
	/// of the deck, easing to 0 within <see cref="BoardFit.Reach"/> further off. The wrist is a palm's length from what
	/// the fingers close on: measured from the wrist as a foot is (see <see cref="Nearness"/>), a hand gripping the deck's
	/// edge was 3.4 in off it and never counted as near.
	/// </summary>
	internal float Holding( NumericsVector wrist ) => Holding( wrist, _span );

	/// <summary>
	/// How fully a point of a hand the clips put at <paramref name="point"/> (a fingertip) is on their board: 1 within
	/// <paramref name="span"/> (model units) of the deck, easing to 0 within <see cref="BoardFit.Reach"/> further off.
	/// </summary>
	internal float Holding( NumericsVector point, float span )
	{
		var off = MathF.Max( 0f, BoardFit.OffDeck( point, _scaledClips ) - span );
		var share = Math.Clamp( 1f - off / MathF.Max( _reach, 1e-4f ), 0f, 1f );
		return share * share * (3f - 2f * share);
	}

	/// <summary>
	/// The shown deck's half width (inches, its frame) at a place along it, toward its left or its right; null past its
	/// ends or where its middle has no surface.
	/// </summary>
	internal float? HalfWidthAt( float along, bool left ) => HalfWidth( along, left );

	/// <summary>How far (model units) a fingertip may be from the clips' deck and still count as on it.</summary>
	internal float TipSpan => _span * TipShare;

	// A fingertip on the deck is about this share of a palm's span off its middle (the deck's half thickness, the finger's).
	private const float TipShare = 0.3f;

	/// <summary>
	/// A board the clips put in a hand (its place and turn in the model's frame) moved so the hand grips the same place on
	/// the real board as on theirs: a longer board is held further from its middle, a wider one further out to its edge.
	/// </summary>
	internal (NumericsVector Position, NumericsRotation Rotation) Held( (NumericsVector Position, NumericsRotation Rotation) board,
		NumericsVector hand )
	{
		var grip = NumericsVector.Transform( hand - board.Position, NumericsRotation.Conjugate( board.Rotation ) );
		return (hand - NumericsVector.Transform( Hand( grip ), board.Rotation ), board.Rotation);
	}

	private Func<float, float, float?> Edge( bool switched ) => _outlineLeft is null ? null : switched ? _switchedEdge : _edge;

	// The deck's half width at a place along its box, to one side, between the outline's stations; null past its ends
	// or where its middle has no surface.
	private float? HalfWidth( float x, bool left )
	{
		var widths = left ? _outlineLeft : _outlineRight;
		if ( widths is null || _outlineBox is not { } box || !(box.Size.x > 0f) )
			return null;
		var at = (x - box.Mins.x) / box.Size.x * OutlineStations;
		if ( !(at >= 0f && at <= OutlineStations) )
			return null;
		var station = Math.Min( (int)at, OutlineStations - 1 );
		var (from, to) = (widths[ station ], widths[ station + 1 ]);
		if ( from < 0f || to < 0f )
			return null;
		return from + (to - from) * (at - station);
	}

	// Reads the deck's outline from its surface: at each station, out from the middle line to where the surface ends.
	private void Outline( IBoardDisplay board, BBox? bounds )
	{
		_outlineBox = bounds;
		_outlineLeft = _outlineRight = null;
		if ( board is null || bounds is not { } box || !(box.Size.x > 0f) )
			return;
		var left = new float[ OutlineStations + 1 ];
		var right = new float[ OutlineStations + 1 ];
		for ( var i = 0; i <= OutlineStations; i++ )
		{
			var x = box.Mins.x + box.Size.x * i / OutlineStations;
			var inside = board.DeckSurface( x, 0f ) is not null;
			left[ i ] = inside ? Reach( board, x, box.Maxs.y ) : -1f;
			right[ i ] = inside ? Reach( board, x, box.Mins.y ) : -1f;
		}
		_outlineLeft = left;
		_outlineRight = right;
	}

	// How far across (inches, positive) the deck's surface reaches from its middle line toward a side's limit at x.
	private static float Reach( IBoardDisplay board, float x, float limit )
	{
		if ( board.DeckSurface( x, limit ) is not null )
			return MathF.Abs( limit );
		var (inside, outside) = (0f, limit);
		for ( var i = 0; i < EdgeSteps; i++ )
		{
			var middle = (inside + outside) * 0.5f;
			if ( board.DeckSurface( x, middle ) is null )
				outside = middle;
			else
				inside = middle;
		}
		return MathF.Abs( inside );
	}

	private static float Pick( float? value, float fallback ) => value is > 0f ? value.Value : fallback;
}
