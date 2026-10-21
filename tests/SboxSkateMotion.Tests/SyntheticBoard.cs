using System.Numerics;
using SkateMotion.Core.BoardModel;

namespace SboxSkateMotion.Tests;

/// <summary>
/// A skateboard model built from boxes and cylinders for part detection: a deck, two trucks (hanger, kingpin and
/// baseplate each) and four wheels, every part its own piece of mesh or all of them welded into one. Its origin is on
/// the ground under the deck's middle, +X along the deck and +Z up, in metres; each part's triangles are recorded.
/// </summary>
internal sealed class SyntheticBoard
{
	internal const float Wheelbase = 0.5f;
	internal const float Track = 0.2f;
	internal const float Radius = 0.03f;
	internal const float WheelWidth = 0.035f;
	internal const float RideHeight = 0.1f;
	internal const float DeckLength = 0.8f;
	internal const float DeckWidth = 0.21f;
	internal const float DeckThickness = 0.012f;

	private const int Segments = 16;

	private readonly List<Vector3> _points = new();
	private readonly List<int> _triangles = new();
	private readonly Dictionary<string, List<int>> _pieces = new();

	private SyntheticBoard( bool welded, bool wheelsOnTrucks )
	{
		var deckBottom = RideHeight - DeckThickness * 0.5f;
		Box( "deck", new Vector3( 0f, 0f, RideHeight ), new Vector3( DeckLength, DeckWidth, DeckThickness ) );
		for ( var truck = 0; truck < 2; truck++ )
		{
			var x = (truck == 0 ? 0.5f : -0.5f) * Wheelbase;
			var end = truck == 0 ? "front" : "rear";
			Box( $"hanger {end}", new Vector3( x, 0f, Radius + 0.005f ), new Vector3( 0.025f, Track * 0.9f, 0.03f ) );
			Box( $"kingpin {end}", new Vector3( x - 0.01f, 0f, (Radius + 0.02f + deckBottom - 0.008f) * 0.5f ),
				new Vector3( 0.012f, 0.012f, deckBottom - 0.008f - Radius - 0.02f ) );
			Box( $"baseplate {end}", new Vector3( x - 0.01f, 0f, deckBottom - 0.004f ), new Vector3( 0.07f, 0.06f, 0.008f ) );
			for ( var side = 0; side < 2; side++ )
				Wheel( $"wheel {truck * 2 + side}", new Vector3( x, (side == 0 ? 0.5f : -0.5f) * Track, Radius ) );
		}
		if ( welded )
			Weld();
		else if ( wheelsOnTrucks )
			WeldWheelsToTrucks();
	}

	/// <summary>Every part its own piece of mesh, all welded into one piece, or each truck's wheels welded to its hanger
	/// (a model with a mesh for the deck and one for each truck with its wheels).</summary>
	internal static SyntheticBoard Build( bool welded = false, bool wheelsOnTrucks = false ) => new( welded, wheelsOnTrucks );

	/// <summary>The board as a mesh, turned into another frame (the board's own frame turned by <paramref name="turn"/>)
	/// and scaled.</summary>
	internal BoardMesh Mesh( Quaternion? turn = null, float scale = 1f, BoardHint[] hints = null )
	{
		var rotation = turn ?? Quaternion.Identity;
		var points = _points.Select( point => Vector3.Transform( point * scale, rotation ) ).ToArray();
		return new BoardMesh( points, _triangles.ToArray(), hints );
	}

	/// <summary>The triangles of a named piece ("deck", "hanger front", "wheel 0" and so on).</summary>
	internal IReadOnlyList<int> Triangles( string piece ) => _pieces[ piece ];

	internal int TriangleCount => _triangles.Count / 3;

	/// <summary>A wheel's centre in the board's frame (origin in the deck's middle).</summary>
	internal static Vector3 WheelCentre( int wheel ) => new( (wheel < 2 ? 0.5f : -0.5f) * Wheelbase,
		(wheel % 2 == 0 ? 0.5f : -0.5f) * Track, Radius - RideHeight );

	private void Box( string name, Vector3 centre, Vector3 size )
	{
		var first = _points.Count;
		for ( var i = 0; i < 8; i++ )
			_points.Add( centre + size * 0.5f * new Vector3( (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f ) );
		int[] faces = { 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5 };
		for ( var i = 0; i < faces.Length; i += 3 )
			Triangle( name, first + faces[ i ], first + faces[ i + 1 ], first + faces[ i + 2 ] );
	}

	private void Wheel( string name, Vector3 centre )
	{
		var first = _points.Count;
		for ( var i = 0; i < Segments; i++ )
		{
			var angle = i * MathF.Tau / Segments;
			var ring = new Vector3( MathF.Cos( angle ) * Radius, 0f, MathF.Sin( angle ) * Radius );
			_points.Add( centre + ring + Vector3.UnitY * (WheelWidth * 0.5f) );
			_points.Add( centre + ring - Vector3.UnitY * (WheelWidth * 0.5f) );
		}
		var outer = _points.Count;
		_points.Add( centre + Vector3.UnitY * (WheelWidth * 0.5f) );
		_points.Add( centre - Vector3.UnitY * (WheelWidth * 0.5f) );
		for ( var i = 0; i < Segments; i++ )
		{
			var a = first + i * 2;
			var b = first + (i + 1) % Segments * 2;
			Triangle( name, a, b, b + 1 );
			Triangle( name, a, b + 1, a + 1 );
			Triangle( name, outer, b, a );
			Triangle( name, outer + 1, a + 1, b + 1 );
		}
	}

	// Thin triangles joining each part to the deck's end on its side: the whole board becomes one connected piece, as a
	// model welded into one mesh reads, with nothing crossing the deck's middle.
	private void Weld()
	{
		foreach ( var piece in _pieces.Keys.Where( key => key != "deck" ).ToArray() )
		{
			var a = _triangles[ _pieces[ piece ][ 0 ] * 3 ];
			var b = _triangles[ _pieces[ piece ][ 0 ] * 3 + 1 ];
			// The deck's first eight points are its corners, odd ones at its front end.
			Triangle( "weld", _points[ a ].X > 0f ? 1 : 0, a, b );
		}
	}

	private void WeldWheelsToTrucks()
	{
		for ( var wheel = 0; wheel < 4; wheel++ )
		{
			var hanger = _pieces[ wheel < 2 ? "hanger front" : "hanger rear" ];
			var tyre = _pieces[ $"wheel {wheel}" ];
			Triangle( "weld", _triangles[ hanger[ 0 ] * 3 ], _triangles[ tyre[ 0 ] * 3 ], _triangles[ tyre[ 0 ] * 3 + 1 ] );
		}
	}

	private void Triangle( string piece, int a, int b, int c )
	{
		if ( !_pieces.TryGetValue( piece, out var list ) )
			_pieces[ piece ] = list = new List<int>();
		list.Add( _triangles.Count / 3 );
		_triangles.Add( a );
		_triangles.Add( b );
		_triangles.Add( c );
	}
}
