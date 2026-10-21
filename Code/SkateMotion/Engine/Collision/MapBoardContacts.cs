namespace SkateMotion.Engine.Collision;

using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Collision;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using NumericsVector = System.Numerics.Vector3;

/// <summary>Binds static map triangles to the board's existing owner and retains other native contacts.</summary>
internal sealed class MapBoardContacts
{
	/// <summary>Share of the grind friction applied this step (it builds up over a grind).</summary>
	internal float GrindFrictionScale { get; set; } = 1f;

	private const float TruckDiameterScale = .5375f;
	private const float TruckLengthScale = 1.35f;
	private const float DefaultWheelWidth = .095f;

	// The map is gathered in a box this many metres each way around the board, and gathered again once the board is
	// this far from where it last was: a level's collision is never copied whole. Each gathering welds the edges of every
	// triangle in the box, so the box is kept small: the contacts a step takes (NearReach about the board's bodies, which
	// reach under half a metre from its middle) stay inside it until it is gathered again, with 0.7 m to spare.
	private const float WindowReach = 4f;
	private const float WindowRecentre = 2f;

	// Triangles further than this many metres from every board body are not offered for contacts in a step. The board's
	// bodies are under half a metre from their centres to their edges and move under 10 cm a step even at 20 m/s.
	private const float NearReach = 1f;

	private readonly Rigidbody _deck;
	private readonly SkateSettings _settings;
	private readonly MapCollisionSource _source;
	private readonly NumericsVector[] _front = new NumericsVector[ ArticulatedBoard.DynamicCount ];
	private readonly MapContactStream _stream;
	private readonly BoardCollisionInputs _inputs;
	private readonly uint[] _staticState = new uint[ 44 ];
	private readonly CollisionMapContactContext _policy;
	private readonly BoardContactBodyBinding[] _bindings = new BoardContactBodyBinding[ 8 ];
	private readonly Func<PhysicsShape, bool> _represented;
	private bool _refresh = true;
	private bool _windowed;
	private NumericsVector _windowCentre;
	private bool _slide;
	private readonly Func<PhysicsShape, bool> _railShape = shape => shape.Collider.IsValid()
		&& shape.Collider.GameObject.Tags.Has( SkateTags.Grind );

	internal int ContactCount => _stream.Count;
	internal int TriangleCount => _stream.TriangleCount;

	/// <param name="shapes">The board's read shapes, shared with its wheel sweeps.</param>
	internal MapBoardContacts( Rigidbody deck, SkateSettings settings, float radius, MapShapeCache shapes,
		CollisionMapContactContext policy, uint duplicateSquaredTolerance, bool deferredReduction )
	{
		_deck = deck;
		_settings = settings;
		_policy = policy;
		_source = new( new MeshWorldQuery( 1e-8f, -.00001f,
			2.938735877055719e-39f, 1.52587890625e-5f, 1e-6f ), shapes );
		var truckRadius = (TruckDiameterScale * .5f) * radius;
		var truckHalfLength = (TruckLengthScale * .5f) * DefaultWheelWidth;
		// The deck's own size, as the board's collider has it: a deck shorter here than there met the floor first through
		// the collider, and the overlap was then lifted out of the floor by position alone.
		_inputs = BoardCollisionInputs.CreateRoundParts( new NumericsVector( settings.DeckCollisionLength, settings.DeckWidth, settings.DeckThickness ),
			truckRadius, truckHalfLength, radius );
		_stream = new( _source, Bind, duplicateSquaredTolerance, deferredReduction );
		_represented = _stream.ContainsShape;
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; ++i )
			_bindings[ i ] = new( (uint)i + 1, i );
		_bindings[ 7 ] = new( 8, ArticulatedBoard.StaticMapBodyIndex );
	}

	internal void Refresh()
	{
		_refresh = true;
		_stream.Reset();
	}

	internal SkateContactState[] CaptureContacts( ArticulatedContacts native )
	{
		var remaining = native.WithoutGeneratedShapes( _represented );
		var result = new SkateContactState[ _stream.Count + remaining.Length ];
		for ( var i = 0; i < _stream.Count; ++i )
		{
			var record = _stream.Records.Slice( i * GeneratedContactRecord.WordCount, GeneratedContactRecord.WordCount );
			var part = checked((int)record[ 3 ] - 1);
			var contact = GeneratedContactRecord.Decode( record, part, ArticulatedBoard.StaticMapBodyIndex ).Geometry;
			result[ i ] = new( (SkateBodyPart)part, SkateUnits.ToEngine( contact.PointA ),
				SkateUnits.ToEngine( contact.PointB ), SkateUnits.ToEngine( contact.Normal ), contact.StaticFriction );
		}
		for ( var i = 0; i < remaining.Length; ++i )
		{
			var contact = remaining[ i ];
			result[ _stream.Count + i ] = new( (SkateBodyPart)contact.BodyA, SkateUnits.ToEngine( contact.PointA ),
				SkateUnits.ToEngine( contact.PointB ), SkateUnits.ToEngine( contact.Normal ), contact.StaticFriction );
		}
		return result;
	}

	/// <param name="railSlide">The deck slides across a rail: the board does not collide with rails, the rail contact
	/// holding it on top (see SkateBody).</param>
	internal void Step( ArticulatedBoard board, ArticulatedContacts native,
		bool slide, float steering, float step, bool railSlide = false )
	{
		var (nearMinimum, nearMaximum, centre) = Near( board );
		if ( _refresh || !_windowed || NumericsVector.Distance( centre, _windowCentre ) > WindowRecentre )
		{
			// The host exposes shape friction but no readable per-face material table.
			var reach = new Vector3( WindowReach * SkateUnits.InchesPerMetre );
			var middle = SkateUnits.Inches( centre );
			_source.Refresh( _deck.Scene.PhysicsWorld, static (_, _) => 0, new BBox( middle - reach, middle + reach ) );
			_windowCentre = centre;
			_windowed = true;
			_refresh = false;
		}
		_slide = slide;
		CollisionBodyCache.Write( board.StaticMapBody, 1, _staticState );
		// Profile friction is supplied by the map side of the max/max/min material combination.
		_inputs.Update( board.Bodies, 0, 0, 0f, 0f, 0f );
		// Faces any board body was behind before this step stay out of it (a thin ramp's back, or its underside).
		for ( var i = 0; i < _front.Length; ++i )
			_front[ i ] = board.Bodies[ i ].Position;
		_source.FrontReferences = _front;
		_stream.Generate( _inputs.Bodies, nearMinimum, nearMaximum, railSlide ? _railShape : null );
		board.StepGenerated( _stream.Records, _bindings,
			native.WithoutGeneratedShapes( _represented ), steering, 0f, step );
	}

	// The box around every board body that a step's contacts can come from, and the bodies' middle (world metres).
	private static (NumericsVector Minimum, NumericsVector Maximum, NumericsVector Centre) Near( ArticulatedBoard board )
	{
		var low = new NumericsVector( float.MaxValue );
		var high = new NumericsVector( -float.MaxValue );
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; ++i )
		{
			low = NumericsVector.Min( low, board.Bodies[ i ].Position );
			high = NumericsVector.Max( high, board.Bodies[ i ].Position );
		}
		var reach = new NumericsVector( NearReach );
		return (low - reach, high + reach, (low + high) * .5f);
	}

	private CollisionMapContactContext? Bind( PhysicsShape shape )
	{
		// Stationary scene colliders can be keyframed. Their motion, not the enum,
		// determines whether the zero-mass world binding represents them.
		if ( !shape.IsValid() || !shape.Body.IsValid() || shape.Body.MotionEnabled
			|| shape.Body.Velocity != Vector3.Zero || shape.Body.AngularVelocity != Vector3.Zero
			|| shape.SurfaceVelocity != Vector3.Zero )
			return null;
		for ( var owner = shape.Collider?.GameObject; owner.IsValid(); owner = owner.Parent )
			if ( owner == _deck.GameObject || owner.Tags.Has( SkateTags.Rider ) )
				return null;
		// A negative shape value inherits the surface; an explicit zero remains frictionless.
		var surfaceFriction = shape.Friction;
		if ( surfaceFriction < 0f )
			surfaceFriction = shape.Surface?.Friction ?? 1f;
		var friction = _settings.Grip * Math.Clamp( surfaceFriction, 0f, 4f );
		if ( _slide ) friction *= .35f;
		if ( shape.Collider.IsValid() && shape.Collider.GameObject.Tags.Has( SkateTags.Grind ) )
			friction = _settings.GrindFriction * GrindFrictionScale;
		return _policy with
		{
			BodyState = _staticState,
			BodyIdentity = 8,
			Category = 0,
			StaticFriction = BitConverter.SingleToUInt32Bits( friction ),
			SlidingFriction = BitConverter.SingleToUInt32Bits( friction * .8f ),
			Restitution = 0
		};
	}
}
