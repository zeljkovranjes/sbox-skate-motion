using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.BoardModel;

using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// Finds a skateboard model's deck, trucks and wheels from its geometry. The deck is what crosses the board's middle;
/// the wheels are the four round shapes at its lowest corners; the trucks are what lies between them and the deck,
/// their hanger the piece reaching across to the wheels and their baseplate any piece fixed under the deck. Each
/// connected piece of mesh goes to one part where it can; a piece holding several parts (a model welded into one) is
/// split triangle by triangle by where each lies. Bones and attachments, where the model has them, say which bone moves
/// which part and where the wheels' centres are.
/// </summary>
internal sealed class BoardPartDetector
{
	// The fewest triangles a deck, two trucks and four wheels can be drawn with.
	private const int FewestTriangles = 24;

	// Share of the height under the deck that counts as the ground the wheels' treads stand on.
	private const float TreadShare = 0.12f;

	// Share of the deck's width around its centre line whose cross-section gives the deck's thickness.
	private const float CentreLine = 0.1f;

	// Between a truck's axle and the deck, the share of the way up at which the hanger meets the baseplate.
	private const float PivotShare = 0.6f;

	// Share of a piece's triangles on a wheel's tyre that makes it hold that wheel as well as whatever else it holds.
	private const float TyreShare = 0.08f;

	// Where along its centre line the deck is measured when something hangs under its middle: shares of the half length
	// from the middle, either side.
	private static readonly float[] ClearOfMiddle = { 0.15f, 0.25f, 0.35f };

	// The share of the deck's thickness a hanger or wheel may sit inside the deck or a baseplate as the deck leans: where
	// they meet (a kingpin in its baseplate, a bushing against it) is hidden there, and nothing shows through.
	private const float HiddenShare = 0.2f;

	// Cells across the deck's width of the grid its underside is measured on, for how far it can lean.
	private const int UndersideCells = 50;
	private static readonly float[] Sides = { -1f, 1f };

	private readonly BoardMesh _mesh;
	private readonly Vector3[] _points;
	private readonly int[] _triangles;
	private readonly MeshPieces _pieces;
	private readonly int[] _vertexPiece;
	private readonly WheelFit[] _wheels = new WheelFit[ 4 ];
	private readonly int[] _wheelPieces = { -1, -1, -1, -1 };
	private List<int>[] _pieceVertices;
	private readonly Quaternion _rotation;
	private Vector3 _min;
	private Vector3 _max;
	private float _centreX;
	private float _centreY;
	private float _width;
	private float _deckTop;
	private float _deckBottom;
	private float _ground;

	private BoardPartDetector( BoardMesh mesh )
	{
		_mesh = mesh;
		_triangles = mesh.Triangles;
		_rotation = BoardFrame.Find( mesh.Positions );
		_points = new Vector3[ mesh.Positions.Length ];
		_min = new Vector3( float.MaxValue );
		_max = new Vector3( float.MinValue );
		for ( var i = 0; i < _points.Length; i++ )
		{
			_points[ i ] = Vector3.Transform( mesh.Positions[ i ], _rotation );
			_min = Vector3.Min( _min, _points[ i ] );
			_max = Vector3.Max( _max, _points[ i ] );
		}
		_pieces = new MeshPieces( _points, _triangles, (_max.X - _min.X) * 1e-4f );
		_vertexPiece = new int[ _points.Length ];
		Array.Fill( _vertexPiece, -1 );
		for ( var t = 0; t < _pieces.OfTriangle.Length; t++ )
		{
			for ( var corner = 0; corner < 3; corner++ )
				_vertexPiece[ _triangles[ t * 3 + corner ] ] = _pieces.OfTriangle[ t ];
		}
	}

	/// <summary>The model's parts and measurements, or null with what kept them from being found.</summary>
	internal static BoardLayout Detect( BoardMesh mesh, out string problem )
	{
		if ( mesh.TriangleCount < FewestTriangles )
		{
			problem = "has too few triangles for a deck, two trucks and four wheels";
			return null;
		}
		var detector = new BoardPartDetector( mesh );
		if ( !detector.MeasureDeck( out problem ) || !detector.FindWheels( out problem ) )
			return null;
		return detector.Classify();
	}

	private float Gap => _deckBottom - _ground;

	private float Thickness => _deckTop - _deckBottom;

	private float Radius => (_wheels[ 0 ].Radius + _wheels[ 1 ].Radius + _wheels[ 2 ].Radius + _wheels[ 3 ].Radius) * 0.25f;

	// How far along the board from its axle a truck's parts reach.
	private float TruckReach => Radius + 0.5f * Gap;

	// Only the deck crosses the board's middle: its cross-section there gives the deck's width and thickness.
	private bool MeasureDeck( out string problem )
	{
		_centreX = (_min.X + _max.X) * 0.5f;
		_ground = _min.Z;
		var length = _max.X - _min.X;
		var section = MeshSection.Across( _points, _triangles, _centreX );
		if ( section.Count < 2 )
			section = MeshSection.Across( _points, _triangles, _centreX + length * 0.03f );
		if ( section.Count < 2 )
		{
			problem = "has no deck across its middle";
			return false;
		}
		var minY = float.MaxValue;
		var maxY = float.MinValue;
		foreach ( var point in section )
		{
			minY = MathF.Min( minY, point.X );
			maxY = MathF.Max( maxY, point.X );
		}
		_width = maxY - minY;
		_centreY = (minY + maxY) * 0.5f;
		// The deck's top and underside at its very middle, where nothing else is: a deck drawn with few, long triangles
		// may cross the section only at its edges.
		var heights = MeshSection.Down( _points, _triangles, _centreX, _centreY );
		_deckTop = heights.Count > 0 ? heights.Max() : float.MinValue;
		_deckBottom = heights.Count > 0 ? heights.Min() : float.MaxValue;
		foreach ( var point in section )
		{
			if ( heights.Count >= 2 || MathF.Abs( point.X - _centreY ) > _width * CentreLine )
				continue;
			_deckTop = MathF.Max( _deckTop, point.Y );
			_deckBottom = MathF.Min( _deckBottom, point.Y );
		}
		if ( _deckTop < _deckBottom )
			(_deckTop, _deckBottom) = (MaxZ( section ), MinZ( section ));
		// Something hung under the deck's middle (a case, a tank) leaves no room there: the deck is then measured along
		// its centre line toward the axles, where the most room is left under it.
		if ( Gap < _width * 0.1f )
			MeasureClearOfMiddle( length );
		if ( Gap < _width * 0.1f )
		{
			problem = "has nothing standing under its deck";
			return false;
		}
		problem = null;
		return true;
	}

	// The deck's top and underside where the most room is left under it along its centre line, either side of its
	// middle (shares of the half length from the middle, all short of the axles).
	private void MeasureClearOfMiddle( float length )
	{
		foreach ( var share in ClearOfMiddle )
		{
			foreach ( var side in Sides )
			{
				var heights = MeshSection.Down( _points, _triangles, _centreX + side * share * length * 0.5f, _centreY );
				if ( heights.Count < 2 || heights.Min() <= _deckBottom )
					continue;
				_deckTop = heights.Max();
				_deckBottom = heights.Min();
			}
		}
	}

	// The wheels' treads are the lowest points of the four corners; the piece (or cluster) holding each is its wheel.
	private bool FindWheels( out string problem )
	{
		var tread = _ground + Gap * TreadShare;
		var corners = new List<int>[ 4 ];
		for ( var i = 0; i < 4; i++ )
			corners[ i ] = new List<int>();
		for ( var v = 0; v < _points.Length; v++ )
		{
			if ( _vertexPiece[ v ] < 0 || _points[ v ].Z > tread )
				continue;
			corners[ Corner( _points[ v ] ) ].Add( v );
		}
		for ( var i = 0; i < 4; i++ )
		{
			if ( corners[ i ].Count == 0 )
			{
				problem = $"has no wheel at its {CornerName( i )}";
				return false;
			}
			var piece = MostCommonPiece( corners[ i ] );
			if ( WheelPiece( piece, i ) is { } fit )
			{
				_wheels[ i ] = fit;
				_wheelPieces[ i ] = piece;
			}
			else
			{
				_wheels[ i ] = WheelCluster( corners[ i ], i );
			}
			if ( !(_wheels[ i ].Radius > 0f) || _wheels[ i ].Radius * 2f > Gap * 1.25f )
			{
				problem = $"has no round wheel at its {CornerName( i )}";
				return false;
			}
		}
		problem = null;
		return true;
	}

	// Corners in the physics order: front left, front right, rear left, rear right.
	private int Corner( Vector3 point ) => (point.X >= _centreX ? 0 : 2) + (point.Y >= _centreY ? 0 : 1);

	private static string CornerName( int corner ) => corner switch
	{
		0 => "front left",
		1 => "front right",
		2 => "rear left",
		_ => "rear right"
	};

	private int MostCommonPiece( List<int> vertices )
	{
		var counts = new Dictionary<int, int>();
		var best = -1;
		foreach ( var vertex in vertices )
		{
			var piece = _vertexPiece[ vertex ];
			counts[ piece ] = counts.GetValueOrDefault( piece ) + 1;
			if ( best < 0 || counts[ piece ] > counts[ best ] )
				best = piece;
		}
		return best;
	}

	// A piece of its own, round seen along its axle, fitting under the deck in one corner: a wheel.
	private WheelFit? WheelPiece( int piece, int corner )
	{
		var min = _pieces.Mins[ piece ];
		var max = _pieces.Maxs[ piece ];
		var size = max - min;
		var round = MathF.Abs( size.X - size.Z ) <= 0.25f * MathF.Max( size.X, size.Z );
		var fits = size.Z <= Gap * 1.1f && size.Y <= _width * 0.35f;
		var inCorner = Corner( min ) == corner && Corner( max ) == corner;
		if ( !round || !fits || !inCorner )
			return null;
		var centre = (min + max) * 0.5f;
		return new WheelFit( centre, (size.X + size.Z) * 0.25f, min.Y, max.Y, true );
	}

	// A wheel sharing its piece with more of the board: its tread's points give where it stands and how wide it is,
	// and the points above the tread within that width its radius.
	private WheelFit WheelCluster( List<int> tread, int corner )
	{
		var minX = float.MaxValue;
		var maxX = float.MinValue;
		var minY = float.MaxValue;
		var maxY = float.MinValue;
		foreach ( var vertex in tread )
		{
			var point = _points[ vertex ];
			minX = MathF.Min( minX, point.X );
			maxX = MathF.Max( maxX, point.X );
			minY = MathF.Min( minY, point.Y );
			maxY = MathF.Max( maxY, point.Y );
		}
		var x = (minX + maxX) * 0.5f;
		var alongX = 0f;
		var top = _ground;
		for ( var v = 0; v < _points.Length; v++ )
		{
			var point = _points[ v ];
			if ( _vertexPiece[ v ] < 0 || point.Y < minY || point.Y > maxY || point.Z > _deckBottom - Gap * 0.05f
				|| MathF.Abs( point.X - x ) > Gap * 0.6f || Corner( point ) != corner )
				continue;
			alongX = MathF.Max( alongX, MathF.Abs( point.X - x ) );
			top = MathF.Max( top, point.Z );
		}
		var radius = (alongX + (top - _ground) * 0.5f) * 0.5f;
		// A tread's points stop short of the wheel's rounded edges.
		var edge = radius * 0.15f;
		return new WheelFit( new Vector3( x, (minY + maxY) * 0.5f, _ground + radius ), radius, minY - edge, maxY + edge, false );
	}

	private BoardLayout Classify()
	{
		var count = _pieces.Count;
		var pieceParts = new BoardPart?[ count ];
		var truckOf = new int[ count ];
		Array.Fill( truckOf, -1 );
		var mixed = false;
		var tyre = TyreTriangles();
		for ( var i = 0; i < 4; i++ )
		{
			if ( _wheelPieces[ i ] >= 0 )
				pieceParts[ _wheelPieces[ i ] ] = BoardLayout.Wheel( i );
		}
		// Bearings, cores and the like inside a wheel turn with it.
		for ( var i = 0; i < 4; i++ )
		{
			if ( _wheelPieces[ i ] < 0 )
				continue;
			for ( var piece = 0; piece < count; piece++ )
			{
				if ( pieceParts[ piece ] is null && InsideWheel( piece, _wheels[ i ] ) )
					pieceParts[ piece ] = BoardLayout.Wheel( i );
			}
		}
		for ( var piece = 0; piece < count; piece++ )
		{
			if ( pieceParts[ piece ] is not null )
				continue;
			if ( Mixed( piece, tyre[ piece ] ) )
			{
				mixed = true;
				continue;
			}
			// Whatever sits mostly at or above the deck's underside (grip, graphics, bolt heads) is the deck's.
			var centre = _pieces.Centre( piece );
			var truck = NearestTruck( centre.X );
			if ( centre.Z >= _deckBottom || MathF.Abs( centre.X - AxleCentre( truck ).X ) > TruckReach )
				pieceParts[ piece ] = BoardPart.Deck;
			else
				truckOf[ piece ] = truck;
		}
		var baseplates = new bool[ 2 ];
		for ( var truck = 0; truck < 2; truck++ )
			baseplates[ truck ] = SplitTruck( truck, truckOf, pieceParts );
		var parts = new BoardPart[ _pieces.OfTriangle.Length ];
		for ( var t = 0; t < parts.Length; t++ )
			parts[ t ] = pieceParts[ _pieces.OfTriangle[ t ] ] ?? PartAt( Centroid( t ) );
		var pieces = !mixed && _wheels[ 0 ].Piece && _wheels[ 1 ].Piece && _wheels[ 2 ].Piece && _wheels[ 3 ].Piece;
		return Layout( parts, baseplates, pieces ? BoardPartSource.Pieces : BoardPartSource.Clusters );
	}

	// A piece within a wheel's outline seen along its axle, and mostly within its width.
	private bool InsideWheel( int piece, WheelFit wheel )
	{
		var min = _pieces.Mins[ piece ];
		var max = _pieces.Maxs[ piece ];
		var overlap = MathF.Min( max.Y, wheel.MaxY ) - MathF.Max( min.Y, wheel.MinY );
		if ( overlap < 0.5f * (max.Y - min.Y) || max.X < wheel.Centre.X - wheel.Radius * 1.1f || min.X > wheel.Centre.X + wheel.Radius * 1.1f )
			return false;
		_pieceVertices ??= PieceVertices();
		foreach ( var vertex in _pieceVertices[ piece ] )
		{
			if ( !wheel.Within( _points[ vertex ] ) )
				return false;
		}
		return true;
	}

	private List<int>[] PieceVertices()
	{
		var lists = new List<int>[ _pieces.Count ];
		for ( var piece = 0; piece < lists.Length; piece++ )
			lists[ piece ] = new List<int>();
		for ( var vertex = 0; vertex < _vertexPiece.Length; vertex++ )
		{
			if ( _vertexPiece[ vertex ] >= 0 )
				lists[ _vertexPiece[ vertex ] ].Add( vertex );
		}
		return lists;
	}

	// How many of each piece's triangles lie on a wheel's tyre.
	private int[] TyreTriangles()
	{
		var counts = new int[ _pieces.Count ];
		for ( var t = 0; t < _pieces.OfTriangle.Length; t++ )
		{
			var centroid = Centroid( t );
			foreach ( var wheel in _wheels )
			{
				if ( wheel.Tyre( centroid ) && centroid.Y >= wheel.MinY + wheel.Width * 0.1f && centroid.Y <= wheel.MaxY - wheel.Width * 0.1f )
				{
					counts[ _pieces.OfTriangle[ t ] ]++;
					break;
				}
			}
		}
		return counts;
	}

	// A piece holding several parts: the deck with what hangs well below it, a wheel's tyre with more besides, or both
	// trucks at once.
	private bool Mixed( int piece, int tyreTriangles )
	{
		var min = _pieces.Mins[ piece ];
		var max = _pieces.Maxs[ piece ];
		if ( min.Z < _deckBottom - Gap * 0.35f && max.Z > _deckBottom + Thickness * 0.5f )
			return true;
		if ( tyreTriangles >= Math.Max( 8, _pieces.Triangles[ piece ] * TyreShare ) )
			return true;
		return min.Z < _deckBottom - Gap * 0.35f && min.X < AxleCentre( 1 ).X + TruckReach * 0.5f
			&& max.X > AxleCentre( 0 ).X - TruckReach * 0.5f;
	}

	// A truck's pieces: the hanger reaches across to the wheels; a piece reaching up to the deck is its baseplate; any
	// other (kingpin, bushings, nuts) goes with whichever of the two it is nearer, or with the hanger where that shares a
	// piece with the wheels. True when there is a baseplate.
	private bool SplitTruck( int truck, int[] truckOf, BoardPart?[] pieceParts )
	{
		var hanger = -1;
		for ( var piece = 0; piece < truckOf.Length; piece++ )
		{
			if ( truckOf[ piece ] == truck && (hanger < 0 || _pieces.Size( piece ).Y > _pieces.Size( hanger ).Y) )
				hanger = piece;
		}
		var track = _wheels[ truck * 2 ].Centre.Y - _wheels[ truck * 2 + 1 ].Centre.Y;
		if ( hanger >= 0 && _pieces.Size( hanger ).Y < track * 0.4f )
			hanger = -1;
		var baseplates = new List<int>();
		for ( var piece = 0; piece < truckOf.Length; piece++ )
		{
			if ( truckOf[ piece ] != truck )
				continue;
			var plate = piece != hanger && _pieces.Maxs[ piece ].Z >= _deckBottom - Gap * 0.15f;
			pieceParts[ piece ] = plate ? BoardLayout.Baseplate( truck ) : BoardLayout.Truck( truck );
			if ( plate )
				baseplates.Add( piece );
		}
		if ( baseplates.Count == 0 || hanger < 0 )
			return baseplates.Count > 0;
		for ( var piece = 0; piece < truckOf.Length; piece++ )
		{
			if ( truckOf[ piece ] != truck || piece == hanger || baseplates.Contains( piece ) )
				continue;
			var toPlate = float.MaxValue;
			foreach ( var plate in baseplates )
				toPlate = MathF.Min( toPlate, BoxGap( piece, plate ) );
			if ( toPlate < BoxGap( piece, hanger ) )
				pieceParts[ piece ] = BoardLayout.Baseplate( truck );
		}
		return true;
	}

	// The part a triangle of a piece holding several parts belongs to, from where it lies.
	private BoardPart PartAt( Vector3 point )
	{
		for ( var i = 0; i < 4; i++ )
		{
			if ( _wheels[ i ].Holds( point ) )
				return _wheels[ i ].Tyre( point ) ? BoardLayout.Wheel( i ) : BoardLayout.Truck( i / 2 );
		}
		var truck = NearestTruck( point.X );
		var axle = AxleCentre( truck );
		if ( point.Z >= _deckBottom - Thickness * 0.15f || MathF.Abs( point.X - axle.X ) > TruckReach )
			return BoardPart.Deck;
		return point.Z < Pivot( truck ) ? BoardLayout.Truck( truck ) : BoardLayout.Baseplate( truck );
	}

	private Vector3 AxleCentre( int truck ) => (_wheels[ truck * 2 ].Centre + _wheels[ truck * 2 + 1 ].Centre) * 0.5f;

	private int NearestTruck( float x ) => MathF.Abs( x - AxleCentre( 0 ).X ) <= MathF.Abs( x - AxleCentre( 1 ).X ) ? 0 : 1;

	// Height at which a truck's hanger meets its baseplate.
	private float Pivot( int truck )
	{
		var axle = AxleCentre( truck ).Z;
		return axle + (_deckBottom - axle) * PivotShare;
	}

	private Vector3 Centroid( int triangle ) => (_points[ _triangles[ triangle * 3 ] ] + _points[ _triangles[ triangle * 3 + 1 ] ]
		+ _points[ _triangles[ triangle * 3 + 2 ] ]) / 3f;

	// Distance between two pieces' boxes (0 where they overlap).
	private float BoxGap( int a, int b )
	{
		var apart = Vector3.Max( Vector3.Zero, Vector3.Max( _pieces.Mins[ a ] - _pieces.Maxs[ b ], _pieces.Mins[ b ] - _pieces.Maxs[ a ] ) );
		return apart.Length();
	}

	private BoardLayout Layout( BoardPart[] parts, bool[] baseplates, BoardPartSource source )
	{
		var middle = new Vector3( _centreX, _centreY, (_deckTop + _deckBottom) * 0.5f );
		var centres = new Vector3[ 4 ];
		for ( var i = 0; i < 4; i++ )
			centres[ i ] = _wheels[ i ].Centre - middle;
		var pivot = (Pivot( 0 ) + Pivot( 1 )) * 0.5f - middle.Z;
		ApplyAttachments( centres, ref pivot, middle );
		var bones = MapBones( centres, middle );
		var allWheels = bones[ (int)BoardPart.FrontLeftWheel ] >= 0 && bones[ (int)BoardPart.FrontRightWheel ] >= 0
			&& bones[ (int)BoardPart.RearLeftWheel ] >= 0 && bones[ (int)BoardPart.RearRightWheel ] >= 0;
		var width = 0f;
		foreach ( var wheel in _wheels )
			width += wheel.Width * 0.25f;
		var gap = MathF.Min( _wheels[ 0 ].MinY - _wheels[ 1 ].MaxY, _wheels[ 2 ].MinY - _wheels[ 3 ].MaxY );
		return new BoardLayout
		{
			Rotation = _rotation,
			Origin = Vector3.Transform( middle, Quaternion.Conjugate( _rotation ) ),
			Parts = parts,
			WheelCentres = centres,
			AxleCentres = new[] { (centres[ 0 ] + centres[ 1 ]) * 0.5f, (centres[ 2 ] + centres[ 3 ]) * 0.5f },
			WheelRadius = Radius,
			WheelWidth = width,
			PivotHeight = pivot,
			LeanLimit = LeanLimit( parts ),
			InnerWheelGap = gap,
			HangerRadius = HangerRadius( parts, gap ),
			Length = _max.X - _min.X,
			Width = _width,
			Thickness = Thickness,
			RideHeight = middle.Z - _ground,
			Baseplates = baseplates,
			PartBones = bones,
			Source = allWheels ? BoardPartSource.Bones : source
		};
	}

	// How far (radians) the deck can lean on its trucks before its underside and the baseplates on it come down through a
	// hanger or a wheel, which stay level with the ground as it leans. A deck dipping by a small angle drops by about that
	// angle times how far a point lies from its long middle, so each hanger or wheel point under the deck allows its gap
	// (and a little more, hidden in the deck: HiddenShare) over that distance. The deck's underside is measured on a grid,
	// at each cell's middle, every surface over it kept; a point is held to the nearest above it (or just into it, as far
	// as is hidden).
	private float LeanLimit( BoardPart[] parts )
	{
		var cell = _width / UndersideCells;
		if ( !(cell > 0f) )
			return float.PositiveInfinity;
		var columns = (int)((_max.X - _min.X) / cell) + 1;
		var rows = (int)((_max.Y - _min.Y) / cell) + 1;
		var over = new List<float>[ columns * rows ];
		for ( var t = 0; t < parts.Length; t++ )
		{
			if ( !Leans( parts[ t ] ) )
				continue;
			var a = _points[ _triangles[ t * 3 ] ];
			var b = _points[ _triangles[ t * 3 + 1 ] ];
			var c = _points[ _triangles[ t * 3 + 2 ] ];
			var fromX = Math.Max( 0, (int)MathF.Ceiling( (MathF.Min( a.X, MathF.Min( b.X, c.X ) ) - _min.X) / cell - 0.5f ) );
			var toX = Math.Min( columns - 1, (int)MathF.Floor( (MathF.Max( a.X, MathF.Max( b.X, c.X ) ) - _min.X) / cell - 0.5f ) );
			var fromY = Math.Max( 0, (int)MathF.Ceiling( (MathF.Min( a.Y, MathF.Min( b.Y, c.Y ) ) - _min.Y) / cell - 0.5f ) );
			var toY = Math.Min( rows - 1, (int)MathF.Floor( (MathF.Max( a.Y, MathF.Max( b.Y, c.Y ) ) - _min.Y) / cell - 0.5f ) );
			for ( var x = fromX; x <= toX; x++ )
			{
				for ( var y = fromY; y <= toY; y++ )
				{
					var middle = new Vector2( _min.X + (x + 0.5f) * cell, _min.Y + (y + 0.5f) * cell );
					if ( HeightAt( a, b, c, middle ) is { } height )
						(over[ x * rows + y ] ??= new List<float>()).Add( height );
				}
			}
		}
		var hidden = Thickness * HiddenShare;
		var limit = float.PositiveInfinity;
		for ( var t = 0; t < parts.Length; t++ )
		{
			if ( Leans( parts[ t ] ) )
				continue;
			for ( var corner = 0; corner < 3; corner++ )
			{
				var point = _points[ _triangles[ t * 3 + corner ] ];
				var away = MathF.Abs( point.Y - _centreY );
				var x = (int)((point.X - _min.X) / cell);
				var y = (int)((point.Y - _min.Y) / cell);
				if ( away < cell || x < 0 || x >= columns || y < 0 || y >= rows || over[ x * rows + y ] is not { } heights )
					continue;
				var nearest = float.PositiveInfinity;
				foreach ( var height in heights )
				{
					if ( height >= point.Z - hidden && height < nearest )
						nearest = height;
				}
				if ( float.IsFinite( nearest ) )
					limit = MathF.Min( limit, MathF.Max( 0f, nearest - point.Z + hidden ) / away );
			}
		}
		return limit;
	}

	// The parts that lean with the deck: the deck and the baseplates fixed under it.
	private static bool Leans( BoardPart part ) => part is BoardPart.Deck or BoardPart.FrontBaseplate or BoardPart.RearBaseplate;

	// The height of a triangle over a point seen from above, or null when the point lies outside it.
	private static float? HeightAt( Vector3 a, Vector3 b, Vector3 c, Vector2 point )
	{
		var d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
		if ( MathF.Abs( d ) < 1e-12f )
			return null;
		var u = ((b.Y - c.Y) * (point.X - c.X) + (c.X - b.X) * (point.Y - c.Y)) / d;
		var v = ((c.Y - a.Y) * (point.X - c.X) + (a.X - c.X) * (point.Y - c.Y)) / d;
		var w = 1f - u - v;
		return u < 0f || v < 0f || w < 0f ? null : u * a.Z + v * b.Z + w * c.Z;
	}

	// How far the trucks reach below their axles across the middle of the gap between their wheels, where a rail ground on
	// a truck lies: the lowest point of any truck triangle over that stretch (clipped to it, so a long face spanning the
	// gap counts by its part inside), the deeper truck's; or a usual 8 mm where no truck was found there.
	private float HangerRadius( BoardPart[] parts, float gap )
	{
		var radius = 0f;
		for ( var truck = 0; truck < 2; truck++ )
		{
			var axle = AxleCentre( truck );
			var lowest = float.MaxValue;
			for ( var t = 0; t < parts.Length; t++ )
			{
				if ( parts[ t ] == BoardLayout.Truck( truck ) )
					lowest = MathF.Min( lowest, LowestWithin( t, axle, Radius, gap * 0.4f ) );
			}
			if ( lowest < axle.Z )
				radius = MathF.Max( radius, axle.Z - lowest );
		}
		return radius > 0f && radius < Radius ? radius : 0.008f;
	}

	private readonly Vector3[] _clipFrom = new Vector3[ 8 ];
	private readonly Vector3[] _clipTo = new Vector3[ 8 ];

	// The lowest point of a triangle within along of the axle lengthwise and across of it sideways, or MaxValue.
	private float LowestWithin( int triangle, Vector3 axle, float along, float across )
	{
		for ( var corner = 0; corner < 3; corner++ )
			_clipFrom[ corner ] = _points[ _triangles[ triangle * 3 + corner ] ];
		var count = Clip( _clipFrom, 3, _clipTo, 0, axle.X - along, true );
		count = Clip( _clipTo, count, _clipFrom, 0, axle.X + along, false );
		count = Clip( _clipFrom, count, _clipTo, 1, axle.Y - across, true );
		count = Clip( _clipTo, count, _clipFrom, 1, axle.Y + across, false );
		var lowest = float.MaxValue;
		for ( var i = 0; i < count; i++ )
			lowest = MathF.Min( lowest, _clipFrom[ i ].Z );
		return lowest;
	}

	// One step of clipping a convex polygon to a half-space along X (axis 0) or Y (axis 1): the side at or above the
	// bound when above is true, else at or below it. Returns the clipped polygon's corner count.
	private static int Clip( Vector3[] input, int count, Vector3[] output, int axis, float bound, bool above )
	{
		var written = 0;
		for ( var i = 0; i < count; i++ )
		{
			var a = input[ i ];
			var b = input[ (i + 1) % count ];
			var da = ((axis == 0 ? a.X : a.Y) - bound) * (above ? 1f : -1f);
			var db = ((axis == 0 ? b.X : b.Y) - bound) * (above ? 1f : -1f);
			if ( da >= 0f && written < output.Length )
				output[ written++ ] = a;
			if ( (da >= 0f) != (db >= 0f) && written < output.Length )
				output[ written++ ] = Vector3.Lerp( a, b, da / (da - db) );
		}
		return written;
	}

	// Attachments named for a wheel or truck mark where it was authored: a wheel's centre close to the one found, or the
	// height a truck turns about.
	private void ApplyAttachments( Vector3[] centres, ref float pivot, Vector3 middle )
	{
		foreach ( var hint in _mesh.Hints )
		{
			if ( hint.Bone >= 0 )
				continue;
			var point = Vector3.Transform( hint.Position, _rotation ) - middle;
			var name = hint.Name?.ToLowerInvariant() ?? "";
			if ( IsWheel( name ) )
			{
				var wheel = Nearest( centres, point );
				if ( Vector3.Distance( centres[ wheel ], point ) <= Radius * 0.5f )
					centres[ wheel ] = point;
			}
			else if ( IsTruck( name ) && MathF.Abs( point.Z - pivot ) <= Gap * 0.5f )
			{
				pivot = point.Z;
			}
		}
	}

	// Which bone moves each part: bones named for a wheel or a truck go to the nearest one, and so does any other bone
	// right at a wheel's centre; the deck takes the bone named for it, or the first.
	private int[] MapBones( Vector3[] centres, Vector3 middle )
	{
		var bones = BoardLayout.NoBones();
		var axles = new[] { (centres[ 0 ] + centres[ 1 ]) * 0.5f, (centres[ 2 ] + centres[ 3 ]) * 0.5f };
		var reach = MathF.Min( MathF.Abs( centres[ 0 ].Y - centres[ 1 ].Y ), MathF.Abs( centres[ 0 ].X - centres[ 2 ].X ) ) * 0.5f;
		var first = -1;
		var distances = new float[ bones.Length ];
		Array.Fill( distances, float.MaxValue );
		foreach ( var hint in _mesh.Hints )
		{
			if ( hint.Bone < 0 )
				continue;
			if ( first < 0 || hint.Bone < first )
				first = hint.Bone;
			var point = Vector3.Transform( hint.Position, _rotation ) - middle;
			var name = hint.Name?.ToLowerInvariant() ?? "";
			var wheel = Nearest( centres, point );
			var truck = Nearest( axles, point );
			if ( IsWheel( name ) || !IsTruck( name ) && Vector3.Distance( centres[ wheel ], point ) <= Radius * 0.3f )
				Claim( bones, distances, BoardLayout.Wheel( wheel ), hint.Bone, Vector3.Distance( centres[ wheel ], point ), reach );
			else if ( IsTruck( name ) )
				Claim( bones, distances, BoardLayout.Truck( truck ), hint.Bone, Vector3.Distance( axles[ truck ], point ), reach );
			else if ( name.Contains( "deck" ) || name.Contains( "board" ) || name.Contains( "root" ) )
				Claim( bones, distances, BoardPart.Deck, hint.Bone, name.Contains( "root" ) ? 1f : 0f, float.MaxValue );
		}
		if ( bones[ (int)BoardPart.Deck ] < 0 )
			bones[ (int)BoardPart.Deck ] = first;
		return bones;
	}

	private static void Claim( int[] bones, float[] distances, BoardPart part, int bone, float distance, float reach )
	{
		if ( distance > reach || distance >= distances[ (int)part ] )
			return;
		bones[ (int)part ] = bone;
		distances[ (int)part ] = distance;
	}

	private static bool IsWheel( string name ) => name.Contains( "wheel" ) || name.Contains( "whl" );

	private static bool IsTruck( string name ) => !IsWheel( name ) && (name.Contains( "truck" ) || name.Contains( "hanger" ) || name.Contains( "axle" ));

	private static int Nearest( Vector3[] places, Vector3 point )
	{
		var best = 0;
		for ( var i = 1; i < places.Length; i++ )
		{
			if ( Vector3.DistanceSquared( places[ i ], point ) < Vector3.DistanceSquared( places[ best ], point ) )
				best = i;
		}
		return best;
	}

	private static float MaxZ( List<Vector2> section )
	{
		var top = float.MinValue;
		foreach ( var point in section )
			top = MathF.Max( top, point.Y );
		return top;
	}

	private static float MinZ( List<Vector2> section )
	{
		var bottom = float.MaxValue;
		foreach ( var point in section )
			bottom = MathF.Min( bottom, point.Y );
		return bottom;
	}
}
